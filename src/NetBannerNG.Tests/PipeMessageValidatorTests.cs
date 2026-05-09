using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Common.NamedPipes;
using System.Linq;
using System.Threading.Tasks;

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
        public void IsValidInboundClientMessage_ReturnsFalse_ForWhitespaceOnlyText()
        {
            var message = Build(ActionType.SendLog, "   ");
            Assert.IsFalse(PipeMessageValidator.IsValidInboundClientMessage(message));
        }

        [TestMethod]
        public void IsValidInboundClientMessage_ReturnsTrue_ForBoundaryLengthText()
        {
            var message = Build(ActionType.SendLog, new string('a', PipeMessageValidator.MaxLogTextLength));
            Assert.IsTrue(PipeMessageValidator.IsValidInboundClientMessage(message));
        }

        [TestMethod]
        public void IsValidInboundClientMessage_ReturnsFalse_WhenPayloadTamperedAfterChecksum()
        {
            var message = Build(ActionType.SendLog, "safe");
            message.Text = "tampered";
            Assert.IsFalse(PipeMessageValidator.IsValidInboundClientMessage(message));
        }

        [TestMethod]
        public void IsValidInboundClientMessage_ReturnsTrue_ForValidLogMessage()
        {
            var message = Build(ActionType.SendLog, "valid log");
            Assert.IsTrue(PipeMessageValidator.IsValidInboundClientMessage(message));
        }

        [TestMethod]
        public async Task IsValidInboundClientMessage_RemainsStableUnderConcurrentValidAndInvalidTraffic()
        {
            var tasks = Enumerable.Range(0, 1000).Select(i => Task.Run(() =>
            {
                var valid = Build(ActionType.SendLog, $"valid-{i}");
                var invalid = Build(ActionType.SendLog, $"invalid-{i}");
                invalid.Text = new string('x', PipeMessageValidator.MaxLogTextLength + 1);

                var validAccepted = PipeMessageValidator.IsValidInboundClientMessage(valid);
                var invalidAccepted = PipeMessageValidator.IsValidInboundClientMessage(invalid);

                return validAccepted && !invalidAccepted;
            }));

            var results = await Task.WhenAll(tasks);
            Assert.IsTrue(results.All(r => r));
        }

        private static PipeMessage Build(ActionType action, string text)
        {
            var message = new PipeMessage { Action = action, Text = text };
            message.Checksum = PipeMessageChecksum.Compute(message);
            return message;
        }
    }
}
