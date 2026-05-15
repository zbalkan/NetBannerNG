using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Services;

namespace NetBannerNG.Tests
{
    [TestClass]
    public class FullscreenSuppressionServiceTests
    {
        [TestMethod]
        public void Start_WatchesAndForwardsSuppressionUpdates()
        {
            var watcher = new FakeForegroundWindowWatcher();
            var sut = new FullscreenSuppressionService(watcher);
            IReadOnlyDictionary<string, FullscreenSuppressionState>? received = null;
            sut.SuppressionUpdated += map => received = map;

            sut.Start();
            watcher.RaiseSuppression(new Dictionary<string, FullscreenSuppressionState> { ["GROUP1"] = new FullscreenSuppressionState(true, "Game") });

            Assert.AreEqual(1, watcher.WatchCalls);
            Assert.IsNotNull(received);
            Assert.IsTrue(received!["GROUP1"].IsSuppressed);
        }

        [TestMethod]
        public void Stop_UnwatchesAndDetachesSuppressionHandler()
        {
            var watcher = new FakeForegroundWindowWatcher();
            var sut = new FullscreenSuppressionService(watcher);
            var raisedCount = 0;
            sut.SuppressionUpdated += _ => raisedCount++;

            sut.Start();
            sut.Stop();
            watcher.RaiseSuppression(new Dictionary<string, FullscreenSuppressionState> { ["GROUP1"] = new FullscreenSuppressionState(false, null) });

            Assert.AreEqual(1, watcher.UnwatchCalls);
            Assert.AreEqual(0, raisedCount);
        }

        private sealed class FakeForegroundWindowWatcher : IForegroundWindowWatcher
        {
            public event Action<IReadOnlyDictionary<string, FullscreenSuppressionState>>? FullscreenSuppressionUpdated;

            public Func<string, System.Threading.Tasks.Task>? EventLogSinkAsync { get; set; }

            internal int WatchCalls { get; private set; }
            internal int UnwatchCalls { get; private set; }

            public void Watch() => WatchCalls++;

            public void Unwatch() => UnwatchCalls++;

            internal void RaiseSuppression(IReadOnlyDictionary<string, FullscreenSuppressionState> map) =>
                FullscreenSuppressionUpdated?.Invoke(map);
        }
    }
}