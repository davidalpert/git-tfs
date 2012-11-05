using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Mocks;
using Sep.Git.Tfs.Commands;
using Sep.Git.Tfs.Core;
using Xunit;

namespace Sep.Git.Tfs.Test.Commands
{
    public class RCheckinTest : AbstractGitTfsCommandTest<Rcheckin>
    {
        CheckinOptions CheckinOptions = new CheckinOptions();

        public RCheckinTest()
        {
            // use a real CheckinOptions so that we can inspect it after the fact.
            mocks.Inject(CheckinOptions);
        }

        [Fact]
        public void SanityCheck_ParsingOptions_Associate_WorkItem()
        {
            UseCommandArgs("-w12345:associate");

            Assert.Equal(1, CheckinOptions.WorkItemsToAssociate.Count);
            Assert.Equal(0, CheckinOptions.WorkItemsToResolve.Count);
            Assert.Equal("12345", CheckinOptions.WorkItemsToAssociate[0]);
        }

        [Fact]
        public void SanityCheck_ParsingOptions_Resolve_WorkItem()
        {
            UseCommandArgs("-w12345:resolve");

            Assert.Equal(0, CheckinOptions.WorkItemsToAssociate.Count);
            Assert.Equal(1, CheckinOptions.WorkItemsToResolve.Count);
            Assert.Equal("12345", CheckinOptions.WorkItemsToResolve[0]);
        }

        [Fact]
        public void Cannot_run_with_unstaged_local_changes()
        {
            WireUpMockRemote();
            RepoHasUnstagedLocalChanges();

            var ex = Assert.Throws<GitTfsException>(() =>
            {
                CommandUnderTest.Run();
            });

            Assert.Equal(Rcheckin.Errors.LOCAL_CHANGES, ex.Message);
            Assert.Equal(Rcheckin.Recommendations.TRY_STASH, ex.RecommendedSolutions.First());
        }

        [Fact]
        public void Cannot_run_with_new_upstream_TFS_changesets()
        {
            WireUpMockRemote();
            RepoHasNoUnstagedLocalChanges();
            RepoHasUpstreamTFSChangesets();

            var ex = Assert.Throws<GitTfsException>(() =>
            {
                CommandUnderTest.Run();
            });

            Assert.Equal(Rcheckin.Errors.NEW_UPSTREAM_CHANGES, ex.Message);
            Assert.Equal(Rcheckin.Recommendations.TRY_REBASE, ex.RecommendedSolutions.First());
        }

        [Fact]
        public void Cannot_run_when_tfsLatest_is_not_a_parent_of_Head()
        {
            WireUpMockRemote();
            RepoHasNoUnstagedLocalChanges();
            RepoHasNoUpstreamTFSChangesets();
            TfsLatestIsNotAParentOfHead();

            var ex = Assert.Throws<GitTfsException>(() =>
            {
                CommandUnderTest.Run();
            });

            Assert.Equal(Rcheckin.Errors.LATEST_TFS_COMMIT_MUST_BE_A_PARENT_OF_HEAD, ex.Message);
            Assert.Equal(Rcheckin.Recommendations.TRY_REBASE, ex.RecommendedSolutions.First());
        }

        /// <summary>
        /// Verifies the <see cref="base.RepoHasCommitsToCheckIn"/> helper.
        /// </summary>
        [Fact]
        public void SanityCheck_RepoHasCommitsToCheckIn_behaves_as_expected()
        {
            // given a latest hash and an empty revList
            var tfsLatest = "someChangesetHash";
            string[] revList = null;

            // when I use this setup method
            RepoHasCommitsToCheckIn(
                @"hash3 hash2 commit message for hash3",
                @"hash2 hash1 commit message for hash2",
                @"hash1 hash0 commit message for hash1"
                );

            // and simulate the call inside Rcheckin
            var repo = mocks.Get<IGitRepository>();
            repo.CommandOutputPipe(tr => revList = tr.ReadToEnd().Split('\n').Where(s => !String.IsNullOrWhiteSpace(s)).ToArray(),
                    "rev-list", "--parents", "--ancestry-path", "--first-parent", "--reverse", tfsLatest + "..HEAD");

            // revList should be populated as expected
            Assert.Equal(3, revList.Count());
            Assert.Equal("hash1 hash0\r", revList[0]);
            Assert.Equal("hash2 hash1\r", revList[1]);
            Assert.Equal("hash3 hash2", revList[2]);

            // commit messages should be set up as expected
            Assert.Equal("commit message for hash1", repo.GetCommitMessage("hash1", "hash0"));
            Assert.Equal("commit message for hash2", repo.GetCommitMessage("hash2", "hash1"));
            Assert.Equal("commit message for hash3", repo.GetCommitMessage("hash3", "hash2"));

            // and rev-list .. last-parent should set up as expected
            Func<string, string> getRevListLastParentOf = latest => repo.CommandOneline("rev-list", "--parents", "--ancestry-path", "--first-parent", "--reverse", latest + "..HEAD");

            tfsLatest = mocks.Get<IGitTfsRemote>().MaxCommitHash;
            Assert.Equal("hash1 hash0", getRevListLastParentOf(tfsLatest)); 
            Assert.Equal("hash2 hash1", getRevListLastParentOf("hash1"));
            Assert.Equal("hash3 hash2", getRevListLastParentOf("hash2"));
        }

        [Fact]
        public void Running_quick_with_unassociated_checkins()
        {
            WireUpMockRemote();
            RepoHasNoUnstagedLocalChanges();
            RepoHasNoUpstreamTFSChangesets();
            TfsLatestIsAParentOfHead();
            RepoHasCommitsToCheckIn(
                "hash2000 hash1000 commit message for hash2",
                "hash1000 hash0000 commit message for hash1"
                );
            //UseCommandArgs("quick"); // doesn't work - don't know why...
            CommandUnderTest.Quick = true;

            CommandUnderTest.Run();

            var output = writer.ToString().Split(new string[] {writer.NewLine}, StringSplitOptions.None);
            var i = 0;
            Assert.Equal(Rcheckin.Messages.FETCHING_CHANGES, output[i++]);
            Assert.Equal(String.Format(Rcheckin.Messages.STARTING_CHECKIN_0_1, "hash1000", "commit message for hash1"), output[i++]);
            Assert.Equal(String.Format(Rcheckin.Messages.DONE_WITH_0, "hash1000"), output[i++]);
            Assert.Equal(String.Format(Rcheckin.Messages.STARTING_CHECKIN_0_1, "hash2000", "commit message for hash2"), output[i++]);
            Assert.Equal(String.Format(Rcheckin.Messages.DONE_WITH_0, "hash2000"), output[i++]);
            Assert.Equal(Rcheckin.Messages.NO_MORE, output[i++]);
        }

        [Fact]
        public void Running_quick_with_associated_checkins()
        {
            WireUpMockRemote();
            RepoHasNoUnstagedLocalChanges();
            RepoHasNoUpstreamTFSChangesets();
            TfsLatestIsAParentOfHead();
            RepoHasCommitsToCheckIn(
                "hash2000 hash1000 commit message for hash2",
                "hash1000 hash0000 commit message for hash1"
                );

            UseCommandArgs("-w12345:associate"); 
            Assert.Equal(1, CheckinOptions.WorkItemsToAssociate.Count);
            Assert.Equal(0, CheckinOptions.WorkItemsToResolve.Count);
            Assert.Equal("12345", CheckinOptions.WorkItemsToAssociate[0]);

            CommandUnderTest.Quick = true;
            CommandUnderTest.Run();

            var output = writer.ToString().Split(new string[] {writer.NewLine}, StringSplitOptions.None);
            var i = 0;
            Assert.Equal(Rcheckin.Messages.FETCHING_CHANGES, output[i++]);
            Assert.Equal(String.Format(Rcheckin.Messages.ASSOCIATING_0, "12345"), output[i++]);
            Assert.Equal(String.Format(Rcheckin.Messages.STARTING_CHECKIN_0_1, "hash1000", "commit message for hash1"), output[i++]);
            Assert.Equal(String.Format(Rcheckin.Messages.DONE_WITH_0, "hash1000"), output[i++]);
            Assert.Equal(String.Format(Rcheckin.Messages.ASSOCIATING_0, "12345"), output[i++]);
            Assert.Equal(String.Format(Rcheckin.Messages.STARTING_CHECKIN_0_1, "hash2000", "commit message for hash2"), output[i++]);
            Assert.Equal(String.Format(Rcheckin.Messages.DONE_WITH_0, "hash2000"), output[i++]);
            Assert.Equal(Rcheckin.Messages.NO_MORE, output[i++]);
        }

        [Fact(Skip = "pending")]
        public void Running_with_unassociated_checkins()
        {
            WireUpMockRemote();
            RepoHasNoUnstagedLocalChanges();
            RepoHasNoUpstreamTFSChangesets();
            TfsLatestIsAParentOfHead();
            RepoHasCommitsToCheckIn(
                "hash2000 hash1000 commit message for hash2",
                "hash1000 hash0000 commit message for hash1"
                );

            mocks.Get<IGitTfsRemote>().Stub(r => r.FetchWithMerge(default(long), null)).IgnoreArguments()
                .WhenCalled(invocation =>
                    {
                        mocks.Get<IGitTfsRemote>().BackToRecord();
                        mocks.Get<IGitTfsRemote>().Stub(r => r.MaxCommitHash).Return("hash1000").Repeat.Once();
                    }).Repeat.Once();

            mocks.Get<IGitTfsRemote>().Stub(r => r.FetchWithMerge(default(long), null)).IgnoreArguments()
                .WhenCalled(invocation =>
                    {
                        mocks.Get<IGitTfsRemote>().BackToRecord();
                        mocks.Get<IGitTfsRemote>().Stub(r => r.MaxCommitHash).Return("hash2000").Repeat.Once();
                    }).Repeat.Once();

            CommandUnderTest.Run();

            var output = writer.ToString().Split(new string[] {writer.NewLine}, StringSplitOptions.None);
            var i = 0;
            Assert.Equal(Rcheckin.Messages.FETCHING_CHANGES, output[i++]);
            Assert.Equal(String.Format(Rcheckin.Messages.STARTING_CHECKIN_0_1, "hash1000", "commit message for hash1"), output[i++]);
            Assert.Equal(String.Format(Rcheckin.Messages.DONE_WITH_0, "hash1000"), output[i++]);
            Assert.Equal(String.Format(Rcheckin.Messages.STARTING_CHECKIN_0_1, "hash2000", "commit message for hash2"), output[i++]);
            Assert.Equal(String.Format(Rcheckin.Messages.DONE_WITH_0, "hash2000"), output[i++]);
            Assert.Equal(Rcheckin.Messages.NO_MORE, output[i++]);
        }
    }
}