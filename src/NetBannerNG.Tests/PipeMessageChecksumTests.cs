using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Common.NamedPipes;

namespace NetBannerNG.Tests
{
    [TestClass]
    public sealed class PipeMessageChecksumTests
    {
        [TestMethod]
        public void Compute_Throws_ForNullMessage()
        {
            Assert.Throws<ArgumentNullException>(() => PipeMessageChecksum.Compute(null!));
        }

        [TestMethod]
        public void IsValid_ReturnsFalse_ForNullMessage()
        {
            Assert.IsFalse(PipeMessageChecksum.IsValid(null!));
        }

        [TestMethod]
        public void Compute_ReturnsExpectedChecksumLength()
        {
            var message = new PipeMessage { Action = ActionType.SendLog, Text = "hello" };

            var checksum = PipeMessageChecksum.Compute(message);

            Assert.AreEqual(PipeMessageChecksum.ChecksumLengthBytes, checksum.Length);
        }

        [TestMethod]
        public void Compute_ProducesStableValue_ForEquivalentMessages()
        {
            var id = Guid.NewGuid();
            var left = new PipeMessage { Id = id, Action = ActionType.SendLog, Text = "same payload" };
            var right = new PipeMessage { Id = id, Action = ActionType.SendLog, Text = "same payload" };

            var leftChecksum = PipeMessageChecksum.Compute(left);
            var rightChecksum = PipeMessageChecksum.Compute(right);

            CollectionAssert.AreEqual(leftChecksum, rightChecksum);
        }

        [TestMethod]
        public void Compute_ChangesWhenPayloadChanges()
        {
            var id = Guid.NewGuid();
            var original = new PipeMessage { Id = id, Action = ActionType.SendLog, Text = "hello" };
            var changedText = new PipeMessage { Id = id, Action = ActionType.SendLog, Text = "HELLO" };
            var changedAction = new PipeMessage { Id = id, Action = ActionType.IsAdmin, Text = "hello" };

            var originalChecksum = PipeMessageChecksum.Compute(original);

            CollectionAssert.AreNotEqual(originalChecksum, PipeMessageChecksum.Compute(changedText));
            CollectionAssert.AreNotEqual(originalChecksum, PipeMessageChecksum.Compute(changedAction));
        }

        [TestMethod]
        public void IsValid_ReturnsTrue_ForMatchingChecksum()
        {
            var message = new PipeMessage { Action = ActionType.SendLog, Text = "legit" };
            message.Checksum = PipeMessageChecksum.Compute(message);

            Assert.IsTrue(PipeMessageChecksum.IsValid(message));
        }

        [TestMethod]
        public void IsValid_ReturnsFalse_WhenChecksumIsTampered()
        {
            var message = new PipeMessage { Action = ActionType.SendLog, Text = "legit" };
            var checksum = PipeMessageChecksum.Compute(message);
            checksum[0] ^= 0xFF;
            message.Checksum = checksum;

            Assert.IsFalse(PipeMessageChecksum.IsValid(message));
        }
    }
}
