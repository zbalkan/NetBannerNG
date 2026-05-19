using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NetBannerNG.Services;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG
{
    internal interface IMonitorSurfaceCatalog
    {
        List<IMonitorSurfaceSet> Reconcile(IEnumerable<Monitor> monitors, bool clean);

        bool TryGet(string groupId, out IMonitorSurfaceSet? group);

        int Count { get; }

        List<IMonitorSurfaceSet> Snapshot(bool clear = false);
    }

    internal sealed class MonitorSurfaceCatalog : IMonitorSurfaceCatalog
    {
        internal delegate IMonitorSurfaceSet SurfaceSetFactory(Monitor monitor, bool clean);

        private static class EventIds
        {
            internal const int GroupRemoveFailure = 4104;
            internal const int GroupUpdateFailure = 4105;
            internal const int GroupAddFailure = 4106;
        }

        private readonly IMonitorIdentity _monitorIdentity;
        private readonly SurfaceSetFactory _surfaceSetFactory;
        private readonly Dictionary<string, IMonitorSurfaceSet> _surfaces = new(StringComparer.Ordinal);
        private readonly object _sync = new();

        internal MonitorSurfaceCatalog(IMonitorIdentity monitorIdentity)
            : this(monitorIdentity, (monitor, clean) => new MonitorSurfaceSet(monitor, clean, monitorIdentity))
        {
        }

        internal MonitorSurfaceCatalog(IMonitorIdentity monitorIdentity, SurfaceSetFactory surfaceSetFactory)
        {
            _monitorIdentity = monitorIdentity;
            _surfaceSetFactory = surfaceSetFactory;
        }

        public List<IMonitorSurfaceSet> Reconcile(IEnumerable<Monitor> monitors, bool clean)
        {
            var nextMonitors = monitors.ToList();
            var nextIds = nextMonitors
                .Select(_monitorIdentity.BuildGroupId)
                .ToHashSet(StringComparer.Ordinal);
            var groupsToShow = new List<IMonitorSurfaceSet>();

            List<IMonitorSurfaceSet> groupsToRemove;
            lock (_sync)
            {
                groupsToRemove = _surfaces.Where(entry => !nextIds.Contains(entry.Key)).Select(entry => entry.Value).ToList();
            }

            foreach (var group in groupsToRemove)
            {
#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    group.Close();
                }
                catch (Exception ex)
                {
                    LogMonitorGroupFailure(EventIds.GroupRemoveFailure, "Remove", group.GroupId, ex);
                }
                finally
                {
                    lock (_sync)
                    {
                        _ = _surfaces.Remove(group.GroupId);
                    }
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }

            foreach (var monitor in nextMonitors)
            {
                var groupId = _monitorIdentity.BuildGroupId(monitor);
                IMonitorSurfaceSet? existingGroup;
                var shouldSyncExistingGroup = false;
                lock (_sync)
                {
                    _ = _surfaces.TryGetValue(groupId, out existingGroup);
                    shouldSyncExistingGroup = existingGroup != null && existingGroup.MatchesMonitor(monitor);
                }

                if (shouldSyncExistingGroup && existingGroup != null)
                {
#pragma warning disable CA1031 // Do not catch general exception types
                    try
                    {
                        if (!existingGroup.HasMonitorLayoutChanged(monitor))
                        {
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMonitorGroupFailure(EventIds.GroupUpdateFailure, "Update", existingGroup.GroupId, ex);
                        continue;
                    }
#pragma warning restore CA1031 // Do not catch general exception types

                    // Layout changed: fall through to the recreate block below. Patching the
                    // WPF window state of an existing surface set in place across resolution
                    // changes is fragile (the OS reports transient working areas mid-change,
                    // ABN_POSCHANGED keeps re-entering, and DIP/pixel matrices race), so we
                    // tear the group down and rebuild it from scratch.
                }

#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    var createdGroup = _surfaceSetFactory(monitor, clean);
                    IMonitorSurfaceSet? replacedGroup = null;
                    lock (_sync)
                    {
                        if (_surfaces.TryGetValue(groupId, out var existingById))
                        {
                            replacedGroup = existingById;
                        }

                        _surfaces[groupId] = createdGroup;
                    }

                    if (replacedGroup != null)
                    {
                        try
                        {
                            replacedGroup.Close();
                        }
                        catch (Exception ex)
                        {
                            LogMonitorGroupFailure(EventIds.GroupRemoveFailure, "Remove", replacedGroup.GroupId, ex);
                        }
                    }

                    groupsToShow.Add(createdGroup);
                }
                catch (Exception ex)
                {
                    LogMonitorGroupFailure(EventIds.GroupAddFailure, "Add", groupId, ex);
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }

            return groupsToShow;
        }

        public bool TryGet(string groupId, out IMonitorSurfaceSet? group)
        {
            lock (_sync)
            {
                return _surfaces.TryGetValue(groupId, out group);
            }
        }

        public int Count {
            get {
                lock (_sync)
                {
                    return _surfaces.Count;
                }
            }
        }

        public List<IMonitorSurfaceSet> Snapshot(bool clear = false)
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