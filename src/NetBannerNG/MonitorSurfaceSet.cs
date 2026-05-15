using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NetBannerNG.Borders;
using NetBannerNG.Services;
using NetBannerNG.Utils;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG
{
    internal sealed class MonitorSurfaceSet : IMonitorSurfaceSet
    {
        private static readonly IMonitorLayoutPolicy LayoutPolicy = new MonitorLayoutPolicyProvider();
        private readonly IMonitorIdentity _monitorIdentityProvider;
        private readonly bool _cleanStart;
        private readonly string _monitorIdentity;
        private readonly List<BorderBase> _windows;
        private readonly GroupHealthPolicy _healthPolicy = new(disableThreshold: 3, disableDuration: TimeSpan.FromSeconds(30));

        internal MonitorSurfaceSet(Monitor monitor, bool cleanStart, IMonitorIdentity monitorIdentity)
        {
            Monitor = monitor;
            _monitorIdentityProvider = monitorIdentity;
            _cleanStart = cleanStart;
            _monitorIdentity = _monitorIdentityProvider.BuildGroupId(monitor.Name, monitor.Bounds);
            _windows = CreateWindows().ToList();
        }

        public Monitor Monitor { get; private set; }
        public string GroupId => _monitorIdentity;

        public IEnumerable<BorderBase> CreateLaunchEntries() => _windows;

        public bool MatchesMonitor(Monitor monitor) => _monitorIdentityProvider.BuildGroupId(monitor) == GroupId;

        public bool HasMonitorLayoutChanged(Monitor monitor) =>
            Monitor.Bounds != monitor.Bounds || Monitor.WorkingArea != monitor.WorkingArea || Monitor.IsPrimary != monitor.IsPrimary;

        public void SyncMonitor(Monitor monitor)
        {
            if (!_healthPolicy.CanAttempt(DateTime.UtcNow)) return;
            Monitor = monitor;
            var syncFailed = false;
            foreach (var window in _windows)
            {
                try
                {
                    LayoutPolicy.ApplyMonitorBounds(window, monitor);
                    window.Render(true);
                }
                catch (Exception ex)
                {
                    syncFailed = true;
                    MarkFailure("Sync", window.GetType().Name, ex);
                }
            }

            if (!syncFailed)
            {
                _healthPolicy.RecordSuccess();
            }
        }

        public void ApplyPostDockVisualState()
        { foreach (var w in _windows) w.ApplyPostDockVisualState(); }

        public void SetTopMost(bool topMost)
        { foreach (var w in _windows) w.Topmost = topMost; }

        public void SetBarsVisibility(bool isVisible)
        {
            foreach (var window in _windows)
            {
                if (window is not LeftBar and not RightBar and not BottomBar) continue;
                if (isVisible) { ShowWindowIfNeeded(window); window.Render(true); }
                else if (window.IsVisible) { window.Hide(); }
            }
        }

        public void Close()
        {
            foreach (var window in _windows.ToList())
            {
                try { window.Close(); _healthPolicy.RecordSuccess(); }
                catch (Exception ex) { MarkFailure("Close", window.GetType().Name, ex); }
            }
        }

        public bool TryShowWindow(BorderBase window, out Exception? error)
        {
            error = null;
            if (!_healthPolicy.CanAttempt(DateTime.UtcNow)) return false;
            try { ShowWindowIfNeeded(window); _healthPolicy.RecordSuccess(); return true; }
            catch (Exception ex) { MarkFailure("Show", window.GetType().Name, ex); error = ex; return false; }
        }

        private static void ShowWindowIfNeeded(BorderBase window)
        { if (!window.IsVisible) { window.Render(); window.Show(); } }

        private void MarkFailure(string stage, string windowType, Exception ex)
        {
            _healthPolicy.RecordFailure(DateTime.UtcNow);
            Debug.WriteLine($"[MonitorGroup][{stage}][{GroupId}][Health={_healthPolicy.State}][Failures={_healthPolicy.ConsecutiveFailures}] Window={windowType} Error={ex}");
        }

        private IEnumerable<BorderBase> CreateWindows()
        {
            yield return new Banner { Owner = System.Windows.Application.Current.MainWindow, Top = Monitor.Bounds.Top, Left = Monitor.Bounds.Left, Width = Monitor.Bounds.Width, AppBarMessageKey = BuildMessageKey("Banner"), IsDocked = !_cleanStart };
            if (Settings.Instance.EnableBottomBanner) yield return new BottomBanner { Owner = System.Windows.Application.Current.MainWindow, Top = Monitor.Bounds.Top, Left = Monitor.Bounds.Left, Width = Monitor.Bounds.Width, AppBarMessageKey = BuildMessageKey("BottomBanner"), IsDocked = !_cleanStart };
            else if (!Settings.Instance.DisableBorders) yield return new BottomBar { Owner = System.Windows.Application.Current.MainWindow, Top = Monitor.Bounds.Top, Left = Monitor.Bounds.Left, Width = Monitor.Bounds.Width, AppBarMessageKey = BuildMessageKey("Bottom"), IsDocked = !_cleanStart };
            if (Settings.Instance.DisableBorders) yield break;
            var top = LayoutPolicy.GetVerticalTop(Monitor); var height = LayoutPolicy.GetVerticalHeight(Monitor);
            yield return new LeftBar { Owner = System.Windows.Application.Current.MainWindow, Top = top, Left = Monitor.Bounds.Left, Height = height, AppBarMessageKey = BuildMessageKey("Left"), IsDocked = !_cleanStart };
            yield return new RightBar { Owner = System.Windows.Application.Current.MainWindow, Top = top, Left = Monitor.Bounds.Right - Settings.Instance.BorderSize, Height = height, AppBarMessageKey = BuildMessageKey("Right"), IsDocked = !_cleanStart };
        }

        private string BuildMessageKey(string borderType) => $"NetBannerNG-AppBar-{borderType}-{_monitorIdentity}";
    }
}