using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NetBannerNG.Borders;
using NetBannerNG.Common.AppBar;
using NetBannerNG.Services;
using NetBannerNG.Utils;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG
{
    internal sealed class MonitorSurfaceSet : IMonitorSurfaceSet
    {
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        private static readonly IMonitorLayoutPolicy LayoutPolicy = new MonitorLayoutPolicyProvider();
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
        private readonly IMonitorIdentity _monitorIdentityProvider;
        private readonly bool _cleanStart;
        private readonly string _monitorIdentity;
        private readonly List<BorderBase> _windows;
        private readonly GroupHealthPolicy _healthPolicy = new(disableThreshold: 3, disableDuration: TimeSpan.FromSeconds(30));
        private bool _isSuppressed;

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
            // Intentionally ignores WorkingArea: it shrinks every time our own bars register
            // with the shell, which would turn the orchestrator's Refresh into an unbounded
            // feedback loop (we register -> work area shrinks -> Reconcile thinks the layout
            // changed -> recreates the group -> registers again -> ...). The Bounds and
            // IsPrimary fields capture the actual monitor topology changes (resolution,
            // primary swap, monitor moved). Per-monitor DPI changes are handled in-place by
            // BorderBase.OnBorderDpiChanged, not by the catalog.
            Monitor.Bounds != monitor.Bounds || Monitor.IsPrimary != monitor.IsPrimary;

        public void ApplyPostDockVisualState()
        {
            foreach (var w in _windows)
            {
                w.ApplyPostDockVisualState();
            }
        }

        public void SetSuppressed(bool isSuppressed)
        {
            // Idempotent: foreground events repeat while a fullscreen app stays foreground;
            // we must not double up AppBarFunctions' suppression depth counter.
            if (isSuppressed == _isSuppressed)
            {
                return;
            }

            _isSuppressed = isSuppressed;
            Debug.WriteLine($"[EVT:4210][MonitorSurfaceSet][SetSuppressed] Group={GroupId} IsSuppressed={isSuppressed}");

            if (isSuppressed)
            {
                AppBarFunctions.BeginSuppression();
                foreach (var window in _windows)
                {
                    window.Topmost = false;
                    if (window.IsVisible)
                    {
                        window.Hide();
                    }
                }
            }
            else
            {
                foreach (var window in _windows)
                {
                    if (!window.IsVisible)
                    {
                        window.Show();
                        window.Render(true);
                    }
                    window.Topmost = true;
                }
                AppBarFunctions.EndSuppression();
            }
        }

        public void Close()
        {
            foreach (var window in _windows.ToList())
            {
#pragma warning disable CA1031 // Do not catch general exception types
                try { window.Close(); _healthPolicy.RecordSuccess(); }
                catch (Exception ex) { MarkFailure("Close", window.GetType().Name, ex); }
#pragma warning restore CA1031 // Do not catch general exception types
            }
        }

        public bool TryShowWindow(BorderBase window, out Exception? error)
        {
            error = null;
            if (!_healthPolicy.CanAttempt(DateTime.UtcNow))
            {
                return false;
            }

#pragma warning disable CA1031 // Do not catch general exception types
            try { ShowWindowIfNeeded(window); _healthPolicy.RecordSuccess(); return true; }
            catch (Exception ex) { MarkFailure("Show", window.GetType().Name, ex); error = ex; return false; }
#pragma warning restore CA1031 // Do not catch general exception types
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
            if (Settings.Instance.EnableBottomBanner)
            {
                yield return new BottomBanner { Owner = System.Windows.Application.Current.MainWindow, Top = Monitor.Bounds.Top, Left = Monitor.Bounds.Left, Width = Monitor.Bounds.Width, AppBarMessageKey = BuildMessageKey("BottomBanner"), IsDocked = !_cleanStart };
            }
            else if (!Settings.Instance.DisableBorders)
            {
                yield return new BottomBar { Owner = System.Windows.Application.Current.MainWindow, Top = Monitor.Bounds.Top, Left = Monitor.Bounds.Left, Width = Monitor.Bounds.Width, AppBarMessageKey = BuildMessageKey("Bottom"), IsDocked = !_cleanStart };
            }

            if (Settings.Instance.DisableBorders)
            {
                yield break;
            }

            var top = LayoutPolicy.GetVerticalTop(Monitor); var height = LayoutPolicy.GetVerticalHeight(Monitor);
            yield return new LeftBar { Owner = System.Windows.Application.Current.MainWindow, Top = top, Left = Monitor.Bounds.Left, Height = height, AppBarMessageKey = BuildMessageKey("Left"), IsDocked = !_cleanStart };
            yield return new RightBar { Owner = System.Windows.Application.Current.MainWindow, Top = top, Left = Monitor.Bounds.Right - Settings.Instance.BorderSize, Height = height, AppBarMessageKey = BuildMessageKey("Right"), IsDocked = !_cleanStart };
        }

        private string BuildMessageKey(string borderType) => $"NetBannerNG-AppBar-{borderType}-{_monitorIdentity}";
    }
}