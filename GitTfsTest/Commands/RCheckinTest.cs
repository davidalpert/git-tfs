using System.Collections.Generic;
using System.IO;
using Rhino.Mocks;
using Sep.Git.Tfs.Commands;
using Sep.Git.Tfs.Core;
using StructureMap.AutoMocking;
using Xunit;

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

        protected void WireUpMockRemote()
        {
            mocks.Get<Globals>().Repository = mocks.Get<IGitRepository>();
            var remote = mocks.Get<IGitTfsRemote>();
            mocks.Get<IGitRepository>().Stub(x => x.GetLastParentTfsCommits(null)).IgnoreArguments()
                .Return(new[] { new TfsChangesetInfo { Remote = remote } });
        }

    }

    public class RCheckinTest : AbstractGitTfsCommandTest<Rcheckin>
    {
        CheckinOptions CheckinOptions = new CheckinOptions();

        public RCheckinTest()
        {
            // use a real CheckinOptions so that we can inspect it after the fact.
            mocks.Inject(CheckinOptions);
        }

        [Fact]
        public void ParsingOptions_Associate_WorkItem()
        {
            IEnumerable<string> args = "-w12345:associate".Split(' ');

            CommandUnderTest.GetAllOptions(mocks.Container).Parse(args);

            Assert.Equal(1, CheckinOptions.WorkItemsToAssociate.Count);
            Assert.Equal(0, CheckinOptions.WorkItemsToResolve.Count);
            Assert.Equal("12345", CheckinOptions.WorkItemsToAssociate[0]);
        }

        [Fact]
        public void ParsingOptions_Resolve_WorkItem()
        {
            IEnumerable<string> args = "-w12345:resolve".Split(' ');

            CommandUnderTest.GetAllOptions(mocks.Container).Parse(args);

            Assert.Equal(0, CheckinOptions.WorkItemsToAssociate.Count);
            Assert.Equal(1, CheckinOptions.WorkItemsToResolve.Count);
            Assert.Equal("12345", CheckinOptions.WorkItemsToResolve[0]);
        }

        [Fact(Skip = "not yet working")]
        public void x()
        {
            IEnumerable<string> args = "-w12345:associate".Split(' ');

            CommandUnderTest.GetAllOptions(mocks.Container).Parse(args);

            base.WireUpMockRemote();

            CommandUnderTest.Run();
        }
    }
}