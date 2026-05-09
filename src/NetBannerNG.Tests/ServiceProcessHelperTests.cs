using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Service;

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
    }
}
