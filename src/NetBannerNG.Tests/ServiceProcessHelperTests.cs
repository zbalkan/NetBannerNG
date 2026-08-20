using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Watchdog;

namespace NetBannerNG.Tests
{
    [TestClass]
    public sealed class ServiceProcessHelperTests
    {
        [TestMethod]
        [DataRow("NetBannerNG.exe --pipe=netbannerng-pipe-s3", "netbannerng-pipe-s3", true)]
        [DataRow("NetBannerNG.exe --PIPE=netbannerng-pipe-s3", "netbannerng-pipe-s3", true)]
        [DataRow("NetBannerNG.exe --pipe=netbannerng-pipe-s4", "netbannerng-pipe-s3", false)]
        [DataRow("NetBannerNG.exe", "netbannerng-pipe-s3", false)]
        [DataRow(null, "netbannerng-pipe-s3", false)]
        [DataRow("NetBannerNG.exe --pipe=netbannerng-pipe-s3", "", false)]
        public void HasExpectedPipeArgument_ValidatesExpectedPipe(string commandLine, string expectedPipeName, bool expected)
        {
            var actual = ProcessHelper.HasExpectedPipeArgument(commandLine, expectedPipeName);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        [DataRow(1, 1u, true)]
        [DataRow(2, 1u, false)]
        [DataRow(-1, 1u, false)]
        public void IsExpectedChildSession_RequiresRecordedLaunchSession(int processSessionId, uint launchedSessionId, bool expected)
        {
            var actual = ProcessHelper.IsExpectedChildSession(processSessionId, launchedSessionId);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        [DataRow(0, false)]
        [DataRow(1, true)]
        [DataRow(4, true)]
        [DataRow(5, false)]
        [DataRow(6, false)]
        public void ShouldRetryLaunchTracking_RetriesOnlyBeforeTheBoundedFinalAttempt(int completedAttempt, bool expected)
        {
            var actual = ProcessHelper.ShouldRetryLaunchTracking(completedAttempt);

            Assert.AreEqual(expected, actual);
        }
    }
}