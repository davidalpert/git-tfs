using System;
using System.IO;
using System.Linq;
using NDesk.Options;
using Sep.Git.Tfs.Core;
using Sep.Git.Tfs.Util;
using StructureMap;

namespace Sep.Git.Tfs.Commands
{
    [Pluggable("rcheckin")]
    [RequiresValidGitRepository]
    public class Rcheckin : GitTfsCommand
    {
        private readonly TextWriter _stdout;
        private readonly CheckinOptions _checkinOptions;
        private readonly CommitSpecificCheckinOptionsFactory _checkinOptionsFactory;
        private readonly TfsWriter _writer;

        public bool Quick { get; set; }

        public Rcheckin(TextWriter stdout, CheckinOptions checkinOptions, TfsWriter writer)
        {
            _stdout = stdout;
            _checkinOptions = checkinOptions;
            _checkinOptionsFactory = new CommitSpecificCheckinOptionsFactory(_stdout);
            _writer = writer;
        }

        public OptionSet OptionSet
        {
            get
            {
                return new OptionSet
                {
                    { "no-rebase|quick", "omit rebases (faster)\nNote: this can lead to problems if someone checks something in while the command is running.",
                        v => Quick = v != null },
                }.Merge(_checkinOptions.OptionSet);
            }
        }

        // uses rebase and works only with HEAD
        public int Run()
        {
            return _writer.Write("HEAD", PerformRCheckin);
        }

        private int PerformRCheckin(TfsChangesetInfo parentChangeset)
        {
            var tfsRemote = parentChangeset.Remote;
            var repo = tfsRemote.Repository;

            if (repo.WorkingCopyHasUnstagedOrUncommitedChanges)
            {
                throw new GitTfsException(Errors.LOCAL_CHANGES)
                    .WithRecommendation(Recommendations.TRY_STASH);
            }

            _stdout.WriteLine(Messages.FETCHING_CHANGES);
            parentChangeset.Remote.Fetch();
            if (parentChangeset.ChangesetId != parentChangeset.Remote.MaxChangesetId)
            {
                throw new GitTfsException(Errors.NEW_UPSTREAM_CHANGES)
                    .WithRecommendation(Recommendations.TRY_REBASE);
            }

            string tfsLatest = parentChangeset.Remote.MaxCommitHash;

            var commitsReachableByTfsLatestThatAreNotReachableByHead = 
                repo.CommandOneline("rev-list", tfsLatest, "^HEAD");

            if (!string.IsNullOrWhiteSpace(commitsReachableByTfsLatestThatAreNotReachableByHead))
                throw new GitTfsException(Errors.LATEST_TFS_COMMIT_MUST_BE_A_PARENT_OF_HEAD)
                    .WithRecommendation(Recommendations.TRY_REBASE);

            if (Quick)
            {
                string[] revList = null;
                repo.CommandOutputPipe(tr => revList = tr.ReadToEnd().Split('\n').Where(s => !String.IsNullOrWhiteSpace(s)).ToArray(),
                    "rev-list", "--parents", "--ancestry-path", "--first-parent", "--reverse", tfsLatest + "..HEAD");

                string currentParent = tfsLatest;
                foreach (string commitWithParents in revList)
                {
                    string[] strs = commitWithParents.Split(' ');
                    string target = strs[0];
                    string[] gitParents = strs.AsEnumerable().Skip(1).Where(hash => hash != currentParent).ToArray();

                    string commitMessage = repo.GetCommitMessage(target, currentParent).Trim(' ', '\r', '\n');
                    var commitSpecificCheckinOptions = _checkinOptionsFactory.BuildCommitSpecificCheckinOptions(_checkinOptions, commitMessage);

                    _stdout.WriteLine(Messages.STARTING_CHECKIN_0_1, 
                                      target.Substring(0, 8), 
                                      commitSpecificCheckinOptions.CheckinComment);
                    long newChangesetId = tfsRemote.Checkin(target, currentParent, parentChangeset, commitSpecificCheckinOptions);
                    tfsRemote.FetchWithMerge(newChangesetId, gitParents);
                    if (tfsRemote.MaxChangesetId != newChangesetId)
                        throw new GitTfsException("error: New TFS changesets were found. Rcheckin was not finished.");

                    currentParent = target;
                    parentChangeset = new TfsChangesetInfo { ChangesetId = newChangesetId, GitCommit = tfsRemote.MaxCommitHash, Remote = tfsRemote };
                    _stdout.WriteLine(Messages.DONE_WITH_0, target);
                }

                _stdout.WriteLine(Messages.NO_MORE);
                return GitTfsExitCodes.OK;
            }
            else
            {
                while (true)
                {
                    // determine first descendant of tfsLatest
                    string revList = repo.CommandOneline("rev-list", "--parents", "--ancestry-path", "--first-parent", "--reverse", tfsLatest + "..HEAD");
                    if (String.IsNullOrWhiteSpace(revList))
                    {
                        _stdout.WriteLine("No more to rcheckin.");
                        return GitTfsExitCodes.OK;
                    }

                    string[] strs = revList.Split(' ');
                    string target = strs[0];
                    string[] gitParents = strs.AsEnumerable().Skip(1).Where(hash => hash != tfsLatest).ToArray();

                    string commitMessage = repo.GetCommitMessage(target, tfsLatest).Trim(' ', '\r', '\n');
                    var commitSpecificCheckinOptions = _checkinOptionsFactory.BuildCommitSpecificCheckinOptions(_checkinOptions, commitMessage);
                    _stdout.WriteLine("Starting checkin of {0} '{1}'", target.Substring(0, 8), commitSpecificCheckinOptions.CheckinComment);
                    long newChangesetId = tfsRemote.Checkin(target, parentChangeset, commitSpecificCheckinOptions);
                    tfsRemote.FetchWithMerge(newChangesetId, gitParents);
                    if (tfsRemote.MaxChangesetId != newChangesetId)
                        throw new GitTfsException("error: New TFS changesets were found. Rcheckin was not finished.");

                    tfsLatest = tfsRemote.MaxCommitHash;
                    parentChangeset = new TfsChangesetInfo { ChangesetId = newChangesetId, GitCommit = tfsLatest, Remote = tfsRemote };
                    _stdout.WriteLine("Done with {0}, rebasing tail onto new TFS-commit...", target);

                    repo.CommandNoisy("rebase", "--preserve-merges", "--onto", tfsLatest, target);
                    _stdout.WriteLine("Rebase done successfully.");
                }
            }
        }

        public static class Messages
        {
            public const string FETCHING_CHANGES =
                "Fetching changes from TFS to minimize possibility of late conflict...";

            public const string STARTING_CHECKIN_0_1 = 
                "Starting checkin of {0} '{1}'";

            public const string DONE_WITH_0 = 
                "Done with {0}.";

            public const string NO_MORE = 
                "No more to rcheckin.";
        }

        public static class Errors
        {
            public const string LOCAL_CHANGES = 
                "error: You have local changes; rebase-workflow checkin only possible with clean working directory.";

            public const string NEW_UPSTREAM_CHANGES = 
                "error: New TFS changesets were found.";

            public const string LATEST_TFS_COMMIT_MUST_BE_A_PARENT_OF_HEAD = 
                "error: latest TFS commit must be parent of the commits being checked in so that rebase-workflow checkin can happen without conflicts. ";
        }

        public static class Recommendations
        {
            public const string TRY_STASH = 
                "Try 'git stash' to stash your local changes and checkin again.";

            public const string TRY_REBASE = 
                "Try to rebase HEAD onto latest TFS checkin and repeat rcheckin or alternatively checkins";
        }
    }
}
