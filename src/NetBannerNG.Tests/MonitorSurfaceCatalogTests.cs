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
        private static readonly string[] expected = new[] { "DISPLAY1", "DISPLAY2" };

        [TestMethod]
        public void Reconcile_AddsAndSnapshots_WhenNewMonitorsProvided()
        {
            var catalog = new MonitorSurfaceCatalog(new FakeMonitorIdentity(), (monitor, _) => new FakeSurfaceSet(monitor.Name, monitor));
            var toShow = catalog.Reconcile(new[] { CreateMonitor("DISPLAY1", 0), CreateMonitor("DISPLAY2", 1920) }, clean: false);
            Assert.HasCount(2, toShow);
            CollectionAssert.AreEquivalent(expected, toShow.ConvertAll(group => group.GroupId));
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
        public void Reconcile_RecreatesExistingSet_WhenLayoutChanged()
        {
            // Layout changes (resolution, taskbar move, monitor reposition) are recreate-only.
            // Patching an existing surface set in place is unreliable because the OS reports
            // transient working areas mid-change and WPF can't recover its DIP/pixel state.
            var instances = new System.Collections.Generic.List<FakeSurfaceSet>();
            var catalog = new MonitorSurfaceCatalog(new FakeMonitorIdentity(), (monitor, _) =>
            {
                var s = new FakeSurfaceSet(monitor.Name, monitor);
                instances.Add(s);
                return s;
            });
            _ = catalog.Reconcile(new[] { CreateMonitor("DISPLAY1", 0) }, clean: false);
            _ = catalog.Reconcile(new[] { CreateMonitor("DISPLAY1", 100) }, clean: false);
            Assert.AreEqual(2, instances.Count, "Layout change must produce a fresh surface set, not patch the previous one.");
            Assert.AreEqual(1, instances[0].CloseCount, "The previous surface set must be closed before its replacement is shown.");
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

        private static Monitor CreateMonitor(string name, double left) => new(name, new Rect(left, 0, 1920, 1080), new Rect(left, 0, 1920, 1040), true);

        private sealed class FakeMonitorIdentity : IMonitorIdentity
        {
            public string BuildGroupId(Monitor monitor) => monitor.Name;

            public string BuildGroupId(string monitorName, Rect bounds) => monitorName;
        }

        private sealed class FakeSurfaceSet : IMonitorSurfaceSet
        {
            public FakeSurfaceSet(string groupId, Monitor monitor)
            { GroupId = groupId; Monitor = monitor; }

            public string GroupId { get; }
            public Monitor Monitor { get; }
            public int CloseCount { get; private set; }

            public void ApplyPostDockVisualState()
            { }

            public void Close() => CloseCount++;

            public IEnumerable<BorderBase> CreateLaunchEntries() => Array.Empty<BorderBase>();

            public bool HasMonitorLayoutChanged(Monitor monitor) => Monitor.Bounds != monitor.Bounds;

            public bool MatchesMonitor(Monitor monitor) => monitor.Name == GroupId;
            public void SetSuppressed(bool isSuppressed)
            { }

            public bool TryShowWindow(BorderBase window, out Exception? error)
            { error = null; return true; }
        }
    }
}