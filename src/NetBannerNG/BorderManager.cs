using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using NetBannerNG.Borders;
using NetBannerNG.Common;
using NetBannerNG.Common.AppBar;
using NetBannerNG.Services;
using NetBannerNG.Utils;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG
{
    public static class BorderManager
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

        private static readonly Dictionary<string, MonitorSurfaceSet> MonitorSurfaces = new(StringComparer.Ordinal);
        private static readonly object MonitorSurfacesSync = new();
        private static readonly List<Monitor> PreviousMonitors = new();
        private static bool _isInitiated;
        private static bool _cleanStart;
        private static volatile bool _shutdownInProgress;

        private sealed class BorderLaunchEntry

        {
            internal BorderBase? Window { get; set; }
            internal string? GroupId { get; set; }
            internal bool IsPrimaryMonitor { get; set; }
            internal double MonitorY { get; set; }
            internal double MonitorX { get; set; }
            internal int WindowOrder { get; set; }
        }

        /// <summary>
        /// Per-monitor surface aggregate (banner + bars) with health-guarded operations.
        /// Group orchestrates lifecycle while each window still owns its own render/dock logic.
        /// </summary>
        private sealed class MonitorSurfaceSet
        {
            private readonly bool _cleanStart;
            private readonly string _monitorIdentity;
            private readonly List<BorderBase> _windows;
            private readonly GroupHealthPolicy _healthPolicy = new(disableThreshold: 3, disableDuration: TimeSpan.FromSeconds(30));

            internal MonitorSurfaceSet(Monitor monitor, bool cleanStart)
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

            internal void SetBarsVisibility(bool isVisible)
            {
                foreach (var window in _windows)
                {
                    if (window is not LeftBar and not RightBar and not BottomBar)
                    {
                        continue;
                    }

                    if (isVisible)
                    {
                        ShowWindowIfNeeded(window);
                        window.Render(true);
                    }
                    else if (window.IsVisible)
                    {
                        window.Hide();
                    }
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
                    case Banner or BottomBanner:
                        window.Width = monitor.Bounds.Width;
                        break;

                    case LeftBar or RightBar:
                        window.Top = monitor.Bounds.Top + Settings.Instance.BannerSize;
                        window.Height = Math.Max(1, monitor.Bounds.Height - Settings.Instance.BannerSize - Settings.Instance.BorderSize);
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

                if (Settings.Instance.EnableBottomBanner)
                {
                    yield return new BottomBanner
                    {
                        Owner = System.Windows.Application.Current.MainWindow,
                        Top = Monitor.Bounds.Top,
                        Left = Monitor.Bounds.Left,
                        Width = Monitor.Bounds.Width,
                        AppBarMessageKey = BuildMessageKey("BottomBanner"),
                        IsDocked = !_cleanStart
                    };
                }
                else if (!Settings.Instance.DisableBorders)
                {
                    yield return new BottomBar
                    {
                        Owner = System.Windows.Application.Current.MainWindow,
                        Top = Monitor.Bounds.Top,
                        Left = Monitor.Bounds.Left,
                        Width = Monitor.Bounds.Width,
                        AppBarMessageKey = BuildMessageKey("Bottom"),
                        IsDocked = !_cleanStart
                    };
                }

                if (Settings.Instance.DisableBorders)
                {
                    yield break;
                }

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
                    Left = Monitor.Bounds.Right - Settings.Instance.BorderSize,
                    Height = initialVerticalHeight,
                    AppBarMessageKey = BuildMessageKey("Right"),
                    IsDocked = !_cleanStart
                };
            }

            private string BuildMessageKey(string borderType) => $"NetBannerNG-AppBar-{borderType}-{_monitorIdentity}";

            private static string BuildMonitorIdentity(Monitor monitor) => MonitorIdentity.BuildGroupId(monitor.Name, monitor.Bounds);
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
            var groupsToShow = ReconcileMonitorSurfaces(PreviousMonitors, _cleanStart);
            ShowGroups(groupsToShow);
            _isInitiated = true;
            _cleanStart = true;
        }

        internal static void Refresh()
        {
            if (_shutdownInProgress)
            {
                return;
            }

            ResetPreviousMonitors();
            var groupsToShow = ReconcileMonitorSurfaces(PreviousMonitors, clean: false);
            ShowGroups(groupsToShow);
            lock (MonitorSurfacesSync)
            {
                _isInitiated = MonitorSurfaces.Count > 0;
            }
        }

        internal static void BeginShutdown() => _shutdownInProgress = true;

        internal static void CloseAllBorders()
        {
            var groupsToClose = SnapshotMonitorSurfaces(clear: true);
            lock (MonitorSurfacesSync)
            {
                _isInitiated = false;
            }

            foreach (var group in groupsToClose)
            {
                group.Close();
            }
        }

        internal static void SendTop() => SetFullscreenSuppressedState(isFullscreen: false);

        internal static void SendBottom() => SetFullscreenSuppressedState(isFullscreen: true);

        internal static void SetMonitorFullscreenSuppressedState(Monitor monitor, bool isFullscreen)
        {
            var groupId = MonitorIdentity.BuildGroupId(monitor);
            MonitorSurfaceSet? group;
            lock (MonitorSurfacesSync)
            {
                _ = MonitorSurfaces.TryGetValue(groupId, out group);
            }

            if (group != null)
            {
                group.SetTopMost(!isFullscreen);
                group.SetBarsVisibility(!isFullscreen);
            }
        }

        private static void ShowGroups(IEnumerable<MonitorSurfaceSet> groups)
        {
            var orderedGroups = groups
                .OrderByDescending(group => group.Monitor.IsPrimary)
                .ThenBy(group => group.Monitor.Bounds.Y)
                .ThenBy(group => group.Monitor.Bounds.X)
                .ToList();

            var stopwatch = Stopwatch.StartNew();
            long firstBannerShownAtMs = -1;

            foreach (var group in orderedGroups)
            {
                var launchEntries = group.CreateLaunchEntries()
                    .OrderBy(entry => entry.WindowOrder)
                    .ToList();

                AppBarFunctions.BeginBatch();
                try
                {
                    foreach (var launchEntry in launchEntries)
                    {
                        Exception? error = null;
                        if (launchEntry.Window == null || !group.TryShowWindow(launchEntry.Window, out error))
                        {
                            if (error != null)
                            {
                                Debug.WriteLine($"[EVT:{EventIds.GroupShowFailure}][MonitorGroup][Show][{group.GroupId}] Window={launchEntry!.Window!.GetType().Name} failed: {error}");
                            }
                        }
                    }

                    foreach (var launchEntry in launchEntries)
                    {
                        if (launchEntry.Window == null || !launchEntry.Window.IsVisible)
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

                group.ApplyPostDockVisualState();
            }

            _cleanStart = true;
        }

        private static void SetFullscreenSuppressedState(bool isFullscreen)
        {
            foreach (var group in SnapshotMonitorSurfaces())
            {
                group.SetTopMost(!isFullscreen);
                group.SetBarsVisibility(!isFullscreen);
            }
        }

        private static void ResetPreviousMonitors()
        {
            PreviousMonitors.Clear();
            PreviousMonitors.AddRange(Monitor.AllMonitors);
        }

        private static List<MonitorSurfaceSet> ReconcileMonitorSurfaces(IEnumerable<Monitor> monitors, bool clean)
        {
            var nextMonitors = monitors.ToList();
            var nextIds = nextMonitors
                .Select(MonitorIdentity.BuildGroupId)
                .ToHashSet(StringComparer.Ordinal);
            var groupsToShow = new List<MonitorSurfaceSet>();

            List<MonitorSurfaceSet> groupsToRemove;
            lock (MonitorSurfacesSync)
            {
                groupsToRemove = MonitorSurfaces.Where(entry => !nextIds.Contains(entry.Key)).Select(entry => entry.Value).ToList();
            }

            foreach (var group in groupsToRemove)
            {
                try
                {
                    group.Close();
                    lock (MonitorSurfacesSync)
                    {
                        _ = MonitorSurfaces.Remove(group.GroupId);
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
                MonitorSurfaceSet? existingGroup;
                var shouldSyncExistingGroup = false;
                lock (MonitorSurfacesSync)
                {
                    _ = MonitorSurfaces.TryGetValue(groupId, out existingGroup);
                    shouldSyncExistingGroup = existingGroup != null && existingGroup.MatchesMonitor(monitor);
                }

                if (shouldSyncExistingGroup && existingGroup != null)
                {
                    try
                    {
                        if (HasMonitorLayoutChanged(existingGroup.Monitor, monitor))
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
                    var createdGroup = new MonitorSurfaceSet(monitor, clean);
                    lock (MonitorSurfacesSync)
                    {
                        MonitorSurfaces[groupId] = createdGroup;
                        groupsToShow.Add(createdGroup);
                    }
                }
                catch (Exception ex)
                {
                    LogMonitorGroupFailure(EventIds.GroupAddFailure, "Add", groupId, ex);
                }
            }

            return groupsToShow;
        }

        public static bool HasMonitorLayoutChanged(Monitor previous, Monitor next) =>
            HasMonitorLayoutChanged(previous.Bounds, previous.WorkingArea, previous.IsPrimary, next.Bounds, next.WorkingArea, next.IsPrimary);

        public static bool HasMonitorLayoutChanged(Rect previousBounds, Rect previousWorkingArea, bool previousIsPrimary, Rect nextBounds, Rect nextWorkingArea, bool nextIsPrimary) =>
            previousBounds != nextBounds ||
            previousWorkingArea != nextWorkingArea ||
            previousIsPrimary != nextIsPrimary;

        private static void LogMonitorGroupFailure(int eventId, string stage, string groupId, Exception ex) =>
            Debug.WriteLine($"[EVT:{eventId}][MonitorGroup][{stage}][{groupId}] failed: {ex}");

        private static List<MonitorSurfaceSet> SnapshotMonitorSurfaces(bool clear = false)
        {
            lock (MonitorSurfacesSync)
            {
                var groups = MonitorSurfaces.Values.ToList();
                if (clear)
                {
                    MonitorSurfaces.Clear();
                }

                return groups;
            }
        }

        public static string BuildGroupId(Monitor monitor) => MonitorIdentity.BuildGroupId(monitor);

        public static string BuildGroupId(string monitorName, Rect bounds) => MonitorIdentity.BuildGroupId(monitorName, bounds);
    }
}
