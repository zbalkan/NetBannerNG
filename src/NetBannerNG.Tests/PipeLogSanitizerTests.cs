using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Common.NamedPipes;

namespace NetBannerNG.Tests
{
    [TestClass]
    public sealed class PipeLogSanitizerTests
    {
        [TestMethod]
        public void SanitizeForSingleLineLog_EscapesCrLfTabAndOtherControlCharacters()
        {
            var input = "a\r\nb\tc" + (char)0x1f;

            var output = PipeLogSanitizer.SanitizeForSingleLineLog(input);

            Assert.AreEqual("a\\r\\nb\\tc\\u001f", output);
        }

        [TestMethod]
        public void SanitizeForSingleLineLog_ReturnsEmpty_ForNullOrEmpty()
        {
            Assert.AreEqual(string.Empty, PipeLogSanitizer.SanitizeForSingleLineLog(null));
            Assert.AreEqual(string.Empty, PipeLogSanitizer.SanitizeForSingleLineLog(string.Empty));
        }
    }
}
