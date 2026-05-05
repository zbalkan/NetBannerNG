using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Common.NamedPipes;

namespace NetBannerNG.Tests
{
    [TestClass]
    public sealed class PipeMessageValidatorTests
    {
        [TestMethod]
        public void IsValidInboundClientMessage_ReturnsFalse_ForNullMessage() => Assert.IsFalse(PipeMessageValidator.IsValidInboundClientMessage(null!));

        [TestMethod]
        public void IsValidInboundClientMessage_ReturnsFalse_ForWrongAction()
        {
            var message = Build(ActionType.IsAdmin, "x");
            Assert.IsFalse(PipeMessageValidator.IsValidInboundClientMessage(message));
        }

        [TestMethod]
        public void IsValidInboundClientMessage_ReturnsFalse_ForInvalidChecksum()
        {
            var message = new PipeMessage { Action = ActionType.SendLog, Text = "abc" };
            message.Checksum = new byte[PipeMessageChecksum.ChecksumLengthBytes];
            Assert.IsFalse(PipeMessageValidator.IsValidInboundClientMessage(message));
        }

        [TestMethod]
        public void IsValidInboundClientMessage_ReturnsFalse_ForTooLongText()
        {
            var message = Build(ActionType.SendLog, new string('a', PipeMessageValidator.MaxLogTextLength + 1));
            Assert.IsFalse(PipeMessageValidator.IsValidInboundClientMessage(message));
        }

        [TestMethod]
        public void IsValidInboundClientMessage_ReturnsTrue_ForValidLogMessage()
        {
            var message = Build(ActionType.SendLog, "valid log");
            Assert.IsTrue(PipeMessageValidator.IsValidInboundClientMessage(message));
        }

        private static PipeMessage Build(ActionType action, string text)
        {
            var message = new PipeMessage { Action = action, Text = text };
            message.Checksum = PipeMessageChecksum.Compute(message);
            return message;
        }
    }
}
