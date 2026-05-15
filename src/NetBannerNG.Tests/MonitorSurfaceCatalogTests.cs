using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Borders;
using NetBannerNG.Common;
using NetBannerNG.Services;

namespace NetBannerNG.Tests
{
    [TestClass]
    public class MonitorSurfaceCatalogTests
    {
        private sealed class FakeMonitorIdentity : IMonitorIdentity
        {
            public string BuildGroupId(Monitor monitor) => monitor.Name;

            public string BuildGroupId(string monitorName, Rect bounds) => monitorName;
        }

        private sealed class FakeSurfaceSet : IMonitorSurfaceSet
        {
            public FakeSurfaceSet(string groupId, Monitor monitor)
            { GroupId = groupId; Monitor = monitor; }

            public Monitor Monitor { get; private set; }
            public string GroupId { get; }
            public int SyncCount { get; private set; }

            public IEnumerable<BorderBase> CreateLaunchEntries() => Array.Empty<BorderBase>();

            public bool MatchesMonitor(Monitor monitor) => monitor.Name == GroupId;

            public bool HasMonitorLayoutChanged(Monitor monitor) => Monitor.Bounds != monitor.Bounds;

            public void SyncMonitor(Monitor monitor)
            { Monitor = monitor; SyncCount++; }

            public void ApplyPostDockVisualState()
            { }

            public void SetTopMost(bool topMost)
            { }

            public void SetBarsVisibility(bool isVisible)
            { }

            public void Close()
            { }

            public bool TryShowWindow(BorderBase window, out Exception? error)
            { error = null; return true; }
        }

        [TestMethod]
        public void Reconcile_AddsAndSnapshots_WhenNewMonitorsProvided()
        {
            var catalog = new MonitorSurfaceCatalog(new FakeMonitorIdentity(), (monitor, _) => new FakeSurfaceSet(monitor.Name, monitor));
            var toShow = catalog.Reconcile(new[] { CreateMonitor("DISPLAY1", 0), CreateMonitor("DISPLAY2", 1920) }, clean: false);
            Assert.HasCount(2, toShow);
            CollectionAssert.AreEquivalent(new[] { "DISPLAY1", "DISPLAY2" }, toShow.ConvertAll(group => group.GroupId));
            Assert.AreEqual(2, catalog.Count);
        }

        [TestMethod]
        public void Reconcile_RemovesMissingGroups()
        {
            var catalog = new MonitorSurfaceCatalog(new FakeMonitorIdentity(), (monitor, _) => new FakeSurfaceSet(monitor.Name, monitor));
            _ = catalog.Reconcile(new[] { CreateMonitor("DISPLAY1", 0), CreateMonitor("DISPLAY2", 1920) }, clean: true);
            _ = catalog.Reconcile(new[] { CreateMonitor("DISPLAY1", 0) }, clean: true);
            Assert.AreEqual(1, catalog.Count);
            Assert.IsTrue(catalog.TryGet("DISPLAY1", out _));
            Assert.IsFalse(catalog.TryGet("DISPLAY2", out _));
        }

        [TestMethod]
        public void Snapshot_WithClear_ReturnsCurrentAndClearsCatalog()
        {
            var catalog = new MonitorSurfaceCatalog(new FakeMonitorIdentity(), (monitor, _) => new FakeSurfaceSet(monitor.Name, monitor));
            _ = catalog.Reconcile(new[] { CreateMonitor("DISPLAY1", 0), CreateMonitor("DISPLAY2", 1920) }, clean: true);
            var snapshot = catalog.Snapshot(clear: true);
            Assert.HasCount(2, snapshot);
            Assert.AreEqual(0, catalog.Count);
        }

        [TestMethod]
        public void Reconcile_SyncsExistingSet_WhenLayoutChanged()
        {
            FakeSurfaceSet? set = null;
            var catalog = new MonitorSurfaceCatalog(new FakeMonitorIdentity(), (monitor, _) => set = new FakeSurfaceSet(monitor.Name, monitor));
            _ = catalog.Reconcile(new[] { CreateMonitor("DISPLAY1", 0) }, clean: false);
            _ = catalog.Reconcile(new[] { CreateMonitor("DISPLAY1", 100) }, clean: false);
            Assert.IsNotNull(set);
            Assert.AreEqual(1, set!.SyncCount);
        }

        private static Monitor CreateMonitor(string name, double left) => new(name, new Rect(left, 0, 1920, 1080), new Rect(left, 0, 1920, 1040), true);
    }
}