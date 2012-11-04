using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rhino.Mocks;
using Sep.Git.Tfs.Core;
using StructureMap.AutoMocking;

namespace Sep.Git.Tfs.Test.Commands
{
    public abstract class AbstractGitTfsCommandTest<T>
        where T : class, GitTfsCommand
    {
        protected StringWriter writer;

        protected RhinoAutoMocker<T> mocks;

        protected T CommandUnderTest { get { return mocks.ClassUnderTest; } }

        public AbstractGitTfsCommandTest()
        {
            writer = new StringWriter();

            mocks = new RhinoAutoMocker<T>();
            mocks.Inject<TextWriter>(writer);
            mocks.Get<Globals>().Repository = mocks.Get<IGitRepository>();
        }

        protected TfsChangesetInfo ChangesetForRemote(string remoteId)
        {
            var mockRemote = mocks.AddAdditionalMockFor<IGitTfsRemote>();
            mockRemote.Stub(x => x.Id).Return(remoteId);
            return new TfsChangesetInfo() {Remote = mockRemote};
        }

        protected TfsChangesetInfo parentChangeset;

        protected void WireUpMockRemote()
        {
            mocks.Get<Globals>().Repository = mocks.Get<IGitRepository>();

            var remote = mocks.Get<IGitTfsRemote>();
            remote.Stub(r => r.Repository).Return(mocks.Get<IGitRepository>());

            parentChangeset = new TfsChangesetInfo {Remote = remote};

            mocks.Get<IGitRepository>()
                .Stub(x => x.GetLastParentTfsCommits(null)).IgnoreArguments()
                .Return(new[] {parentChangeset});
        }

        protected void RepoHasUnstagedLocalChanges()
        {
            mocks.Get<IGitRepository>().Stub(r => r.WorkingCopyHasUnstagedOrUncommitedChanges).Return(true);
        }

        protected void RepoHasNoUnstagedLocalChanges()
        {
            mocks.Get<IGitRepository>().Stub(r => r.WorkingCopyHasUnstagedOrUncommitedChanges).Return(false);
        }

        protected void RepoHasNoUpstreamTFSChangesets()
        {
            mocks.Get<IGitTfsRemote>().Stub(r => r.MaxChangesetId).Return(parentChangeset.ChangesetId);
        }

        protected void RepoHasUpstreamTFSChangesets()
        {
            mocks.Get<IGitTfsRemote>().Stub(r => r.MaxChangesetId).Return(parentChangeset.ChangesetId + 1);
        }

        protected void TfsLatestIsAParentOfHead()
        {
            mocks.Get<IGitTfsRemote>().Stub(r => r.MaxCommitHash).Return("maxCommitHash");
            mocks.Get<IGitRepository>()
                .Stub(r => r.CommandOneline("rev-list", "maxCommitHash", "^HEAD"))
                .Return(string.Empty);
        }

        protected void TfsLatestIsNotAParentOfHead()
        {
            mocks.Get<IGitTfsRemote>().Stub(r => r.MaxCommitHash).Return("maxCommitHash");
            mocks.Get<IGitRepository>()
                .Stub(r => r.CommandOneline("rev-list", "maxCommitHash", "^HEAD"))
                .Return(
                    @"commit1
commit2
commit3");
        }

        protected void RepoHasCommitsToCheckIn(int n)
        {
            var commits = Enumerable.Range(1, n).Reverse()
                .Select(i => string.Format(@"hash{0} hash{1}, ""commit message for hash{0}""", i, i-1))
                .ToArray();

            RepoHasCommitsToCheckIn(commits);
        }

        protected void RepoHasCommitsToCheckIn(params string[] commitSummaries)
        {
            var commits = commitSummaries.Select(s => new CommitSummary(s));

            var sourceString =
                String.Join(Environment.NewLine,
                            commits.Reverse().Select(c => String.Format("{0} {1}", c.Hash, c.ParentHash)));

            var repo = mocks.Get<IGitRepository>();

            repo.Stub(r => r.CommandOutputPipe(null, null)).IgnoreArguments()
                .Callback<Action<TextReader>, string[]>((action, args) =>
                    {
                        using (StringReader fakeReader = new StringReader(sourceString))
                        {
                            action(fakeReader);
                        }

                        return true;
                    });

            var lastCommitHash = mocks.Get<IGitTfsRemote>().MaxCommitHash;
            foreach (var c in commits.Reverse())
            {
                repo.Stub(r => r.GetCommitMessage(Arg<string>.Is.Equal(c.Hash), Arg<string>.Is.Anything))
                    .Return(c.CommitMessage);

                repo.Stub(
                    r =>
                    r.CommandOneline("rev-list", "--parents", "--ancestry-path", "--first-parent", "--reverse",
                                     lastCommitHash + "..HEAD"))
                    .Return(String.Format("{0} {1}", c.Hash, c.ParentHash));

                lastCommitHash = c.Hash;
            }
        }

        /// <summary>
        /// Helper class to simplify parsing inputs to the 
        /// <see cref="RepoHasCommitsToCheckIn"/> helper.
        /// </summary>
        private class CommitSummary
        {
            public CommitSummary(string summaryLine)
            {
                string[] parts = summaryLine.Split(' ');
                Hash = parts.First();
                ParentHash = parts.Skip(1).First();
                CommitMessage = String.Join(" ", parts.Skip(2));
            }

            public string Hash { get; set; }
            public string ParentHash { get; set; }
            public string CommitMessage { get; set; }
        }

        protected void UseCommandArgs(string input)
        {
            IEnumerable<string> args = input.Split(' ');

            CommandUnderTest.GetAllOptions(mocks.Container).Parse(args);
        }
    }
}