using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Services;

namespace NetBannerNG.Tests
{
    [TestClass]
    public class AppLifecycleServiceTests
    {
        private static readonly string[] expected = new[] { "Init", "InitiateAllSurfaces", "ApplyFullscreenSuppressionStates" };

        [TestMethod]
        public async Task InitializeRuntimeAsync_InitializesAndWiresSuppressionUpdates()
        {
            var orchestrator = new FakeOverlayOrchestrator();
            var suppression = new FakeSuppressionService();
            var monitorWatcher = new FakeMonitorWatcher();
            var sut = new AppLifecycleService(orchestrator, suppression, monitorWatcher);

            try
            {
                await sut.InitializeRuntimeAsync().ConfigureAwait(false);
                suppression.RaiseSuppression(new Dictionary<string, FullscreenSuppressionState> { ["DISPLAY1"] = new FullscreenSuppressionState(true, "VideoPlayer") });
                CollectionAssert.AreEqual(
                    expected,
                    orchestrator.Calls);
                Assert.AreEqual(1, suppression.StartCalls);
                Assert.AreEqual(1, monitorWatcher.WatchCalls);
                Assert.IsNotNull(monitorWatcher.RefreshAction);
            }
            finally
            {
                await sut.ShutdownRuntimeAsync().ConfigureAwait(false);
                sut.Dispose();
            }
        }

        [TestMethod]
        public async Task ShutdownRuntimeAsync_StopsWatchersAndClosesBorders()
        {
            var orchestrator = new FakeOverlayOrchestrator();
            var suppression = new FakeSuppressionService();
            var monitorWatcher = new FakeMonitorWatcher();
            var sut = new AppLifecycleService(orchestrator, suppression, monitorWatcher);

            await sut.InitializeRuntimeAsync().ConfigureAwait(false);
            await sut.ShutdownRuntimeAsync().ConfigureAwait(false);

            CollectionAssert.Contains(orchestrator.Calls, "BeginShutdown");
            CollectionAssert.Contains(orchestrator.Calls, "CloseAllSurfaces");
            Assert.AreEqual(1, monitorWatcher.UnwatchCalls);
            Assert.AreEqual(1, suppression.StopCalls);
            sut.Dispose();
        }

        private sealed class FakeOverlayOrchestrator : IDisplayOverlayOrchestrator
        {
            internal List<string> Calls { get; } = new();

            public void Init(bool clean) => Calls.Add("Init");

            public void InitiateAllSurfaces() => Calls.Add("InitiateAllSurfaces");

            public void Refresh() => Calls.Add("Refresh");

            public void ApplyFullscreenSuppressionStates(IReadOnlyDictionary<string, FullscreenSuppressionState> suppressionByGroup) => Calls.Add("ApplyFullscreenSuppressionStates");

            public void BeginShutdown() => Calls.Add("BeginShutdown");

            public void CloseAllSurfaces() => Calls.Add("CloseAllSurfaces");
        }

        private sealed class FakeSuppressionService : IFullscreenSuppressionService
        {
            public event Action<IReadOnlyDictionary<string, FullscreenSuppressionState>>? SuppressionUpdated;

            internal int StartCalls { get; private set; }
            internal int StopCalls { get; private set; }

            public void Start() => StartCalls++;

            public void Stop() => StopCalls++;

            internal void RaiseSuppression(IReadOnlyDictionary<string, FullscreenSuppressionState> suppressionByGroup) =>
                SuppressionUpdated?.Invoke(suppressionByGroup);
        }

        private sealed class FakeMonitorWatcher : IMonitorTopologyWatcher
        {
            internal int WatchCalls { get; private set; }
            internal int UnwatchCalls { get; private set; }
            internal Action? RefreshAction { get; private set; }

            public void Watch(Action refreshAction)
            {
                WatchCalls++;
                RefreshAction = refreshAction;
            }

            public void Unwatch() => UnwatchCalls++;
        }
    }
}