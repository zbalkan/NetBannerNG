using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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

        [TestMethod]
        public void Reconcile_AddsAndSnapshots_WhenNewMonitorsProvided()
        {
            var catalog = new MonitorSurfaceCatalog(new FakeMonitorIdentity(), (monitor, _) => CreateSet(monitor.Name, monitor));

            var toShow = catalog.Reconcile(new[] { CreateMonitor("DISPLAY1", 0), CreateMonitor("DISPLAY2", 1920) }, clean: false);

            Assert.HasCount(2, toShow);
            CollectionAssert.AreEquivalent(new[] { "DISPLAY1", "DISPLAY2" }, toShow.ConvertAll(group => group.GroupId));
            Assert.AreEqual(2, catalog.Count);
        }

        [TestMethod]
        public void Reconcile_RemovesMissingGroups()
        {
            var catalog = new MonitorSurfaceCatalog(new FakeMonitorIdentity(), (monitor, _) => CreateSet(monitor.Name, monitor));
            _ = catalog.Reconcile(new[] { CreateMonitor("DISPLAY1", 0), CreateMonitor("DISPLAY2", 1920) }, clean: true);

            _ = catalog.Reconcile(new[] { CreateMonitor("DISPLAY1", 0) }, clean: true);

            Assert.AreEqual(1, catalog.Count);
            Assert.IsTrue(catalog.TryGet("DISPLAY1", out _));
            Assert.IsFalse(catalog.TryGet("DISPLAY2", out _));
        }

        [TestMethod]
        public void Snapshot_WithClear_ReturnsCurrentAndClearsCatalog()
        {
            var catalog = new MonitorSurfaceCatalog(new FakeMonitorIdentity(), (monitor, _) => CreateSet(monitor.Name, monitor));
            _ = catalog.Reconcile(new[] { CreateMonitor("DISPLAY1", 0), CreateMonitor("DISPLAY2", 1920) }, clean: true);

            var snapshot = catalog.Snapshot(clear: true);

            Assert.HasCount(2, snapshot);
            Assert.AreEqual(0, catalog.Count);
        }

        private static Monitor CreateMonitor(string name, double left) => new Monitor(
            name: name,
            bounds: new Rect(left, 0, 1920, 1080),
            workingArea: new Rect(left, 0, 1920, 1040),
            isPrimary: true);

        private static DisplayOverlayOrchestrator.MonitorSurfaceSet CreateSet(string groupId, Monitor monitor)
        {
            var set = (DisplayOverlayOrchestrator.MonitorSurfaceSet)FormatterServices.GetUninitializedObject(typeof(DisplayOverlayOrchestrator.MonitorSurfaceSet));
            SetField(set, "_monitorIdentity", groupId);
            SetField(set, "_windows", new List<NetBannerNG.Borders.BorderBase>());
            SetAutoPropertyBackingField(set, "Monitor", monitor);
            return set;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            field.SetValue(target, value);
        }

        private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
        {
            var field = target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
            field.SetValue(target, value);
        }
    }
}
