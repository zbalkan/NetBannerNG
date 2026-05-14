using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NetBannerNG.Services;
using NetBannerNG.Utils;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG
{
    internal interface IMonitorSurfaceCatalog
    {
        List<DisplayOverlayOrchestrator.MonitorSurfaceSet> Reconcile(IEnumerable<Monitor> monitors, bool clean);
        bool TryGet(string groupId, out DisplayOverlayOrchestrator.MonitorSurfaceSet? group);
        int Count { get; }
        List<DisplayOverlayOrchestrator.MonitorSurfaceSet> Snapshot(bool clear = false);
    }

    internal sealed class MonitorSurfaceCatalog : IMonitorSurfaceCatalog
    {
        private static class EventIds
        {
            internal const int GroupRemoveFailure = 4104;
            internal const int GroupUpdateFailure = 4105;
            internal const int GroupAddFailure = 4106;
        }

        private readonly IMonitorIdentity _monitorIdentity;
        private readonly Dictionary<string, DisplayOverlayOrchestrator.MonitorSurfaceSet> _surfaces = new(StringComparer.Ordinal);
        private readonly object _sync = new();

        internal MonitorSurfaceCatalog(IMonitorIdentity monitorIdentity)
        {
            _monitorIdentity = monitorIdentity;
        }

        public List<DisplayOverlayOrchestrator.MonitorSurfaceSet> Reconcile(IEnumerable<Monitor> monitors, bool clean)
        {
            var nextMonitors = monitors.ToList();
            var nextIds = nextMonitors
                .Select(_monitorIdentity.BuildGroupId)
                .ToHashSet(StringComparer.Ordinal);
            var groupsToShow = new List<DisplayOverlayOrchestrator.MonitorSurfaceSet>();

            List<DisplayOverlayOrchestrator.MonitorSurfaceSet> groupsToRemove;
            lock (_sync)
            {
                groupsToRemove = _surfaces.Where(entry => !nextIds.Contains(entry.Key)).Select(entry => entry.Value).ToList();
            }

            foreach (var group in groupsToRemove)
            {
                try
                {
                    group.Close();
                    lock (_sync)
                    {
                        _ = _surfaces.Remove(group.GroupId);
                    }
                }
                catch (Exception ex)
                {
                    LogMonitorGroupFailure(EventIds.GroupRemoveFailure, "Remove", group.GroupId, ex);
                }
            }

            foreach (var monitor in nextMonitors)
            {
                var groupId = DisplayOverlayOrchestrator.BuildGroupId(monitor);
                DisplayOverlayOrchestrator.MonitorSurfaceSet? existingGroup;
                var shouldSyncExistingGroup = false;
                lock (_sync)
                {
                    _ = _surfaces.TryGetValue(groupId, out existingGroup);
                    shouldSyncExistingGroup = existingGroup != null && existingGroup.MatchesMonitor(monitor);
                }

                if (shouldSyncExistingGroup && existingGroup != null)
                {
                    try
                    {
                        if (DisplayOverlayOrchestrator.HasMonitorLayoutChanged(existingGroup.Monitor, monitor))
                        {
                            existingGroup.SyncMonitor(monitor);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMonitorGroupFailure(EventIds.GroupUpdateFailure, "Update", existingGroup.GroupId, ex);
                    }

                    continue;
                }

                try
                {
                    var createdGroup = new DisplayOverlayOrchestrator.MonitorSurfaceSet(monitor, clean);
                    lock (_sync)
                    {
                        _surfaces[groupId] = createdGroup;
                    }

                    groupsToShow.Add(createdGroup);
                }
                catch (Exception ex)
                {
                    LogMonitorGroupFailure(EventIds.GroupAddFailure, "Add", groupId, ex);
                }
            }

            return groupsToShow;
        }

        public bool TryGet(string groupId, out DisplayOverlayOrchestrator.MonitorSurfaceSet? group)
        {
            lock (_sync)
            {
                return _surfaces.TryGetValue(groupId, out group);
            }
        }

        public int Count
        {
            get
            {
                lock (_sync)
                {
                    return _surfaces.Count;
                }
            }
        }

        public List<DisplayOverlayOrchestrator.MonitorSurfaceSet> Snapshot(bool clear = false)
        {
            lock (_sync)
            {
                var groups = _surfaces.Values.ToList();
                if (clear)
                {
                    _surfaces.Clear();
                }

                return groups;
            }
        }

        private static void LogMonitorGroupFailure(int eventId, string stage, string groupId, Exception ex) =>
            Debug.WriteLine($"[EVT:{eventId}][MonitorGroup][{stage}][{groupId}] failed: {ex}");
    }
}
