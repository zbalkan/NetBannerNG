using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NetBannerNG.Borders;
using NetBannerNG.Common.AppBar;
using NetBannerNG.Common.Native;
using NetBannerNG.Services;

namespace NetBannerNG
{
    internal static class BorderManager
    {
        private static class EventIds
        {
            internal const int GroupSyncFailure = 4101;
            internal const int GroupCloseFailure = 4102;
            internal const int GroupShowFailure = 4103;
            internal const int GroupRemoveFailure = 4104;
            internal const int GroupUpdateFailure = 4105;
            internal const int GroupAddFailure = 4106;
        }

        private static readonly Dictionary<string, MonitorBorderGroup> MonitorGroups = new(StringComparer.Ordinal);
        private static readonly object MonitorGroupsSync = new();
        private static readonly List<Monitor> PreviousMonitors = new();
        private static bool _isInitiated;
        private static bool _cleanStart;

        private sealed class BorderLaunchEntry

        {
            internal BorderBase? Window { get; set; }
            internal string? GroupId { get; set; }
            internal bool IsPrimaryMonitor { get; set; }
            internal double MonitorY { get; set; }
            internal double MonitorX { get; set; }
            internal int WindowOrder { get; set; }
        }

        private sealed class MonitorBorderGroup
        {
            private readonly bool _cleanStart;
            private readonly string _monitorIdentity;
            private readonly List<BorderBase> _windows;
            private readonly GroupHealthPolicy _healthPolicy = new(disableThreshold: 3, disableDuration: TimeSpan.FromSeconds(30));

            internal MonitorBorderGroup(Monitor monitor, bool cleanStart)
            {
                Monitor = monitor;
                _cleanStart = cleanStart;
                _monitorIdentity = BuildMonitorIdentity(monitor);
                _windows = CreateWindows().ToList();
            }

            internal Monitor Monitor { get; private set; }

            internal string GroupId => _monitorIdentity;

            internal IEnumerable<BorderLaunchEntry> CreateLaunchEntries()
            {
                for (var i = 0; i < _windows.Count; i++)
                {
                    yield return new BorderLaunchEntry
                    {
                        Window = _windows[i],
                        GroupId = GroupId,
                        IsPrimaryMonitor = Monitor.IsPrimary,
                        MonitorY = Monitor.Bounds.Y,
                        MonitorX = Monitor.Bounds.X,
                        WindowOrder = i
                    };
                }
            }

            internal bool MatchesMonitor(Monitor monitor) => BuildMonitorIdentity(monitor) == GroupId;

            internal void SyncMonitor(Monitor monitor)
            {
                if (!_healthPolicy.CanAttempt(DateTime.UtcNow))
                {
                    return;
                }

                Monitor = monitor;
                foreach (var window in _windows)
                {
                    try
                    {
                        ApplyMonitorBounds(window, monitor);
                        window.Render(true);
                        _healthPolicy.RecordSuccess();
                    }
                    catch (Exception ex)
                    {
                        MarkFailure("Sync", window.GetType().Name, ex);
                    }
                }
            }

            internal void ShowWindows()
            {
                foreach (var window in _windows)
                {
                    ShowWindowIfNeeded(window);
                }
            }

            internal void ApplyPostDockVisualState()
            {
                foreach (var window in _windows)
                {
                    window.ApplyPostDockVisualState();
                }
            }

            internal void SetTopMost(bool topMost)
            {
                foreach (var window in _windows)
                {
                    window.Topmost = topMost;
                }
            }

            internal void Close()
            {
                foreach (var window in _windows.ToList())
                {
                    try
                    {
                        window.Close();
                        _healthPolicy.RecordSuccess();
                    }
                    catch (Exception ex)
                    {
                        MarkFailure("Close", window.GetType().Name, ex);
                    }
                }
            }

            internal bool TryShowWindow(BorderBase window, out Exception? error)
            {
                error = null;
                if (!_healthPolicy.CanAttempt(DateTime.UtcNow))
                {
                    return false;
                }

                try
                {
                    ShowWindowIfNeeded(window);
                    _healthPolicy.RecordSuccess();
                    return true;
                }
                catch (Exception ex)
                {
                    MarkFailure("Show", window.GetType().Name, ex);
                    error = ex;
                    return false;
                }
            }


            private static void ShowWindowIfNeeded(BorderBase window)
            {
                if (window.IsVisible)
                {
                    return;
                }

                window.Render();
                window.Show();
            }

            private static void ApplyMonitorBounds(BorderBase window, Monitor monitor)
            {
                window.Top = monitor.Bounds.Top;
                window.Left = monitor.Bounds.Left;

                switch (window)
                {
                    case Banner or BottomBar:
                        window.Width = monitor.Bounds.Width;
                        break;

                    case LeftBar or RightBar:
                        window.Height = monitor.Bounds.Height;
                        break;
                }
            }

            private void MarkFailure(string stage, string windowType, Exception ex)
            {
                _healthPolicy.RecordFailure(DateTime.UtcNow);
                LogGroupFailure(stage, windowType, ex);
            }

            private void LogGroupFailure(string stage, string windowType, Exception ex)
            {
                var eventId = stage switch
                {
                    "Sync" => EventIds.GroupSyncFailure,
                    "Close" => EventIds.GroupCloseFailure,
                    "Show" => EventIds.GroupShowFailure,
                    _ => 4199
                };
                Debug.WriteLine($"[EVT:{eventId}][MonitorGroup][{stage}][{GroupId}][Health={_healthPolicy.State}][Failures={_healthPolicy.ConsecutiveFailures}] Window={windowType} Error={ex}");
            }

            private IEnumerable<BorderBase> CreateWindows()
            {
                yield return new Banner
                {
                    Owner = System.Windows.Application.Current.MainWindow,
                    Top = Monitor.Bounds.Top,
                    Left = Monitor.Bounds.Left,
                    Width = Monitor.Bounds.Width,
                    AppBarMessageKey = BuildMessageKey("Banner"),
                    IsDocked = !_cleanStart
                };

                if (Settings.Instance.DisableBorders)
                {
                    yield break;
                }

                yield return new BottomBar
                {
                    Owner = System.Windows.Application.Current.MainWindow,
                    Top = Monitor.Bounds.Top,
                    Left = Monitor.Bounds.Left,
                    Width = Monitor.Bounds.Width,
                    AppBarMessageKey = BuildMessageKey("Bottom"),
                    IsDocked = !_cleanStart
                };

                var initialVerticalTop = Monitor.Bounds.Top + Settings.Instance.BannerSize;
                var initialVerticalHeight = Math.Max(1, Monitor.Bounds.Height - Settings.Instance.BannerSize - Settings.Instance.BorderSize);

                yield return new LeftBar
                {
                    Owner = System.Windows.Application.Current.MainWindow,
                    Top = initialVerticalTop,
                    Left = Monitor.Bounds.Left,
                    Height = initialVerticalHeight,
                    AppBarMessageKey = BuildMessageKey("Left"),
                    IsDocked = !_cleanStart
                };

                yield return new RightBar
                {
                    Owner = System.Windows.Application.Current.MainWindow,
                    Top = initialVerticalTop,
                    Left = Monitor.Bounds.Left,
                    Height = initialVerticalHeight,
                    AppBarMessageKey = BuildMessageKey("Right"),
                    IsDocked = !_cleanStart
                };
            }

            private string BuildMessageKey(string borderType) => $"NetBannerNG-AppBar-{borderType}-{_monitorIdentity}";

            private static string BuildMonitorIdentity(Monitor monitor)
            {
                if (!string.IsNullOrWhiteSpace(monitor.Name))
                {
                    return $"{monitor.Name}{monitor.Handle}";
                }

                return $"{monitor.Bounds.X},{monitor.Bounds.Y},{monitor.Bounds.Width},{monitor.Bounds.Height}";
            }
        }

        internal static void Init(bool clean = true)
        {
            _cleanStart = clean;
            System.Windows.Application.Current.MainWindow = new Launcher();
            System.Windows.Application.Current.MainWindow.Show();
        }

        internal static void InitiateAllBorders()
        {
            if (_isInitiated)
            {
                return;
            }

            ResetPreviousMonitors();
            ReconcileMonitorGroups(PreviousMonitors, _cleanStart);
            ShowGroups();
            _isInitiated = true;
            _cleanStart = true;
        }

        internal static void Refresh()
        {
            ResetPreviousMonitors();
            ReconcileMonitorGroups(PreviousMonitors, clean: false);
            ShowGroups();
            lock (MonitorGroupsSync)
            {
                _isInitiated = MonitorGroups.Count > 0;
            }
        }

        internal static void CloseAllBorders()
        {
            var groupsToClose = SnapshotMonitorGroups(clear: true);
            lock (MonitorGroupsSync)
            {
                _isInitiated = false;
            }

            foreach (var group in groupsToClose)
            {
                group.Close();
            }
        }

        internal static void SendTop() => SetTopMost(true);

        internal static void SendBottom() => SetTopMost(false);

        private static void ShowGroups()
        {
            List<BorderLaunchEntry> launchPlan;
            Dictionary<string, MonitorBorderGroup> groupsById;
            lock (MonitorGroupsSync)
            {
                groupsById = MonitorGroups.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
                launchPlan = MonitorGroups.Values
                    .SelectMany(group => group.CreateLaunchEntries())
                    .OrderByDescending(entry => entry.IsPrimaryMonitor)
                    .ThenBy(entry => entry.MonitorY)
                    .ThenBy(entry => entry.MonitorX)
                    .ThenBy(entry => entry.WindowOrder)
                    .ToList();
            }

            var stopwatch = Stopwatch.StartNew();
            long firstBannerShownAtMs = -1;

            AppBarFunctions.BeginBatch();
            try
            {
                foreach (var launchEntry in launchPlan)
                {
                    if (!TryGetLaunchGroup(launchEntry, groupsById, out var group))
                    {
                        continue;
                    }

                    Exception? error = null;
                    if (launchEntry.Window == null || !group.TryShowWindow(launchEntry.Window, out error))
                    {
                        if (error != null)
                        {
                            Debug.WriteLine($"[EVT:{EventIds.GroupShowFailure}][MonitorGroup][Show][{group.GroupId}] Window={launchEntry.Window!.GetType().Name} failed: {error}");
                        }
                    }
                }

                foreach (var launchEntry in launchPlan)
                {
                    if (!TryGetLaunchGroup(launchEntry, groupsById, out _) || launchEntry.Window == null || !launchEntry.Window.IsVisible)
                    {
                        continue;
                    }

                    launchEntry.Window.Render(true);
                    var shownAtMs = stopwatch.ElapsedMilliseconds;
                    if (firstBannerShownAtMs < 0 && launchEntry.Window is Banner)
                    {
                        firstBannerShownAtMs = shownAtMs;
                        Debug.WriteLine($"[RenderPerf] First Banner shown at +{firstBannerShownAtMs}ms");
                    }
                }
            }
            finally
            {
                AppBarFunctions.EndBatch();
            }

            foreach (var group in SnapshotMonitorGroups())
            {
                group.ApplyPostDockVisualState();
            }
            _cleanStart = true;
        }

        private static void SetTopMost(bool topMost)
        {
            foreach (var group in SnapshotMonitorGroups())
            {
                group.SetTopMost(topMost);
            }
        }

        private static void ResetPreviousMonitors()
        {
            PreviousMonitors.Clear();
            PreviousMonitors.AddRange(Monitor.AllMonitors);
        }

        private static void ReconcileMonitorGroups(IEnumerable<Monitor> monitors, bool clean)
        {
            var nextMonitors = monitors.ToList();
            var nextIds = nextMonitors
                .Select(BuildGroupId)
                .ToHashSet(StringComparer.Ordinal);

            List<MonitorBorderGroup> groupsToRemove;
            lock (MonitorGroupsSync)
            {
                groupsToRemove = MonitorGroups.Where(entry => !nextIds.Contains(entry.Key)).Select(entry => entry.Value).ToList();
            }

            foreach (var group in groupsToRemove)
            {
                try
                {
                    group.Close();
                    lock (MonitorGroupsSync)
                    {
                        _ = MonitorGroups.Remove(group.GroupId);
                    }
                }
                catch (Exception ex)
                {
                    LogMonitorGroupFailure(EventIds.GroupRemoveFailure, "Remove", group.GroupId, ex);
                }
            }

            foreach (var monitor in nextMonitors)
            {
                var groupId = BuildGroupId(monitor);
                MonitorBorderGroup? existingGroup;
                lock (MonitorGroupsSync)
                {
                    _ = MonitorGroups.TryGetValue(groupId, out existingGroup);
                }

                if (existingGroup != null && existingGroup.MatchesMonitor(monitor))
                {
                    try
                    {
                        existingGroup.SyncMonitor(monitor);
                    }
                    catch (Exception ex)
                    {
                        LogMonitorGroupFailure(EventIds.GroupUpdateFailure, "Update", existingGroup.GroupId, ex);
                    }
                    continue;
                }

                try
                {
                    var createdGroup = new MonitorBorderGroup(monitor, clean);
                    lock (MonitorGroupsSync)
                    {
                        MonitorGroups[groupId] = createdGroup;
                    }
                }
                catch (Exception ex)
                {
                    LogMonitorGroupFailure(EventIds.GroupAddFailure, "Add", groupId, ex);
                }
            }
        }

        private static void LogMonitorGroupFailure(int eventId, string stage, string groupId, Exception ex) =>
            Debug.WriteLine($"[EVT:{eventId}][MonitorGroup][{stage}][{groupId}] failed: {ex}");

        private static bool TryGetLaunchGroup(BorderLaunchEntry launchEntry, IDictionary<string, MonitorBorderGroup> groupsById, out MonitorBorderGroup group)
        {
            group = null!;
            return launchEntry.GroupId != null && groupsById.TryGetValue(launchEntry.GroupId, out group);
        }

        private static List<MonitorBorderGroup> SnapshotMonitorGroups(bool clear = false)
        {
            lock (MonitorGroupsSync)
            {
                var groups = MonitorGroups.Values.ToList();
                if (clear)
                {
                    MonitorGroups.Clear();
                }

                return groups;
            }
        }

        private static string BuildGroupId(Monitor monitor)
        {
            if (!string.IsNullOrWhiteSpace(monitor.Name))
            {
                return $"{monitor.Name}{monitor.Handle}";
            }

            return $"{monitor.Bounds.X},{monitor.Bounds.Y},{monitor.Bounds.Width},{monitor.Bounds.Height}";
        }
    }
}
