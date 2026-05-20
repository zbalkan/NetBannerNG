using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Common.NamedPipes;
using NetBannerNG.Watchdog;

namespace NetBannerNG.Tests
{
    /// <summary>
    /// Tests for multi-session GUI launcher behaviour (F1, F2, F4, N2, N3).
    /// These tests cover pure logic that does not require a running Windows session.
    /// </summary>
    [TestClass]
    public sealed class MultiSessionLauncherTests
    {
        // ──────────────────────────────────────────────────────────────────────────
        // F4 / pipe-argument validation (per-session pipe names)
        // ──────────────────────────────────────────────────────────────────────────

        [TestMethod]
        [DataRow(1u)]
        [DataRow(3u)]
        [DataRow(99u)]
        public void HasExpectedPipeArgument_MatchesSessionSpecificPipeName(uint sessionId)
        {
            var pipeName = PipeNaming.ForSession(sessionId);
            var commandLine = $"NetBannerNG.exe --pipe={pipeName}";

            Assert.IsTrue(ProcessHelper.HasExpectedPipeArgument(commandLine, pipeName));
        }

        [TestMethod]
        public void HasExpectedPipeArgument_DoesNotMatchPipeFromDifferentSession()
        {
            var pipeForSession3 = PipeNaming.ForSession(3);
            var pipeForSession5 = PipeNaming.ForSession(5);
            var commandLine = $"NetBannerNG.exe --pipe={pipeForSession3}";

            Assert.IsFalse(ProcessHelper.HasExpectedPipeArgument(commandLine, pipeForSession5));
        }

        // ──────────────────────────────────────────────────────────────────────────
        // IsAuthorizedClientConnection — two-argument (multi-session) overload
        // ──────────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void IsAuthorizedClientConnection_TwoArg_ReturnsTrueWhenPipeMatchesSession()
        {
            const uint sessionId = 7;
            var pipeName = PipeNaming.ForSession(sessionId);

            Assert.IsTrue(NamedPipeServer.IsAuthorizedClientConnection(sessionId, pipeName));
        }

        [TestMethod]
        public void IsAuthorizedClientConnection_TwoArg_ReturnsFalseWhenPipeIsForDifferentSession()
        {
            const uint expectedSessionId = 2;
            var wrongPipe = PipeNaming.ForSession(9);

            Assert.IsFalse(NamedPipeServer.IsAuthorizedClientConnection(expectedSessionId, wrongPipe));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void IsAuthorizedClientConnection_TwoArg_ReturnsFalseWhenPipeNameIsNullOrEmpty(string? pipeName)
        {
            Assert.IsFalse(NamedPipeServer.IsAuthorizedClientConnection(3, pipeName));
        }

        [TestMethod]
        public void IsAuthorizedClientConnection_TwoArg_IsCaseInsensitive()
        {
            const uint sessionId = 6;
            var upper = PipeNaming.ForSession(sessionId).ToUpperInvariant();

            Assert.IsTrue(NamedPipeServer.IsAuthorizedClientConnection(sessionId, upper));
        }

        // ──────────────────────────────────────────────────────────────────────────
        // N2: idempotency — three-argument overload preserved for compat
        // ──────────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void IsAuthorizedClientConnection_ThreeArg_ReturnsFalseWhenActiveSessionDiffers()
        {
            const uint expectedSession = 10;
            var pipe = PipeNaming.ForSession(expectedSession);

            // The three-argument overload still enforces the activeSessionId gate,
            // preserving behaviour for callers that pass it explicitly.
            Assert.IsFalse(NamedPipeServer.IsAuthorizedClientConnection(expectedSession, pipe, activeSessionId: 11));
        }

        [TestMethod]
        public void IsAuthorizedClientConnection_ThreeArg_ReturnsTrueWhenAllMatch()
        {
            const uint sessionId = 4;
            var pipe = PipeNaming.ForSession(sessionId);

            Assert.IsTrue(NamedPipeServer.IsAuthorizedClientConnection(sessionId, pipe, activeSessionId: sessionId));
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Backoff / watchdog state (unchanged, per-session scalars)
        // ──────────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void CalculateBackoffDelay_PerSessionBehaviourUnchanged()
        {
            // Verify that per-session back-off still grows exponentially.
            var d1 = ServiceHost.CalculateBackoffDelay(1);
            var d2 = ServiceHost.CalculateBackoffDelay(2);
            var d3 = ServiceHost.CalculateBackoffDelay(3);

            Assert.IsTrue(d1.TotalSeconds < d2.TotalSeconds);
            Assert.IsTrue(d2.TotalSeconds < d3.TotalSeconds);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Pipe naming — each session produces a unique, deterministic name
        // ──────────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void PipeNaming_ProducesUniqueNamesForDifferentSessions()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1u; i <= 20u; i++)
            {
                Assert.IsTrue(names.Add(PipeNaming.ForSession(i)), $"Duplicate pipe name for session {i}");
            }
        }

        [TestMethod]
        public void PipeNaming_EachNameEmbeditsSessionId()
        {
            for (var id = 1u; id <= 10u; id++)
            {
                var name = PipeNaming.ForSession(id);
                Assert.IsTrue(name.IndexOf(id.ToString(), StringComparison.Ordinal) >= 0,
                    $"Pipe name '{name}' does not contain session ID {id}");
            }
        }
    }
}
