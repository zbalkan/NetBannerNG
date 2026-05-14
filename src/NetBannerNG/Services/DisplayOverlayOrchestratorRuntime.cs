using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NetBannerNG.Common.AppBar;
using NetBannerNG.Utils;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG.Services
{
    internal sealed class DisplayOverlayOrchestratorRuntime : IDisplayOverlayOrchestrator
    {
        private readonly IMonitorLayoutPolicy _layoutPolicy;
        private readonly IMonitorSurfaceCatalog _surfaceCatalog;
        private readonly List<Monitor> _previousMonitors = new();
        private bool _isInitiated;
        private bool _cleanStart;
        private volatile bool _shutdownInProgress;

        internal DisplayOverlayOrchestratorRuntime()
            : this(new MonitorIdentityProvider(), new MonitorLayoutPolicyProvider())
        {
        }

        internal DisplayOverlayOrchestratorRuntime(IMonitorIdentity monitorIdentity, IMonitorLayoutPolicy layoutPolicy)
        {
            _layoutPolicy = layoutPolicy;
            _surfaceCatalog = new MonitorSurfaceCatalog(monitorIdentity);
        }

        public void Init(bool clean)
        {
            _cleanStart = clean;
            System.Windows.Application.Current.MainWindow = new Launcher();
            System.Windows.Application.Current.MainWindow.Show();
        }

        public void InitiateAllSurfaces()
        {
            if (_isInitiated)
            {
                return;
            }

            ResetPreviousMonitors();
            ShowGroups(_surfaceCatalog.Reconcile(_previousMonitors, _cleanStart));
            _isInitiated = true;
            _cleanStart = true;
        }

        public void Refresh()
        {
            if (_shutdownInProgress)
            {
                return;
            }

            ResetPreviousMonitors();
            ShowGroups(_surfaceCatalog.Reconcile(_previousMonitors, clean: false));
            _isInitiated = _surfaceCatalog.Count > 0;
        }

        public void BeginShutdown() => _shutdownInProgress = true;

        public void CloseAllSurfaces()
        {
            var groups = _surfaceCatalog.Snapshot(clear: true);
            _isInitiated = false;
            foreach (var g in groups)
            {
                g.Close();
            }
        }

        public void ApplyFullscreenSuppressionStates(IReadOnlyDictionary<string, bool> suppressionByGroup)
        {
            foreach (var group in _surfaceCatalog.Snapshot())
            {
                var isFullscreen = suppressionByGroup.TryGetValue(group.GroupId, out var suppressed) && suppressed;
                group.SetTopMost(!isFullscreen);
                group.SetBarsVisibility(!isFullscreen);
            }
        }

        private void ShowGroups(IEnumerable<DisplayOverlayOrchestrator.MonitorSurfaceSet> groups)
        {
            var orderedGroups = groups.OrderByDescending(g => g.Monitor.IsPrimary).ThenBy(g => g.Monitor.Bounds.Y).ThenBy(g => g.Monitor.Bounds.X).ToList();
            var stopwatch = Stopwatch.StartNew();
            long firstBannerShownAtMs = -1;

            foreach (var group in orderedGroups)
            {
                var launchEntries = group.CreateLaunchEntries().ToList();
                AppBarFunctions.BeginBatch();
                try
                {
                    foreach (var window in launchEntries)
                    {
                        if (!group.TryShowWindow(window, out var error) && error != null)
                        {
                            Debug.WriteLine($"[EVT:4103][MonitorGroup][Show][{group.GroupId}] Window={window.GetType().Name} failed: {error}");
                        }
                    }

                    foreach (var window in launchEntries.Where(w => w.IsVisible))
                    {
                        _layoutPolicy.ApplyMonitorBounds(window, group.Monitor);
                        window.Render(true);
                        var shownAtMs = stopwatch.ElapsedMilliseconds;
                        if (firstBannerShownAtMs < 0 && window is Borders.Banner)
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

        private void ResetPreviousMonitors()
        {
            _previousMonitors.Clear();
            _previousMonitors.AddRange(Monitor.AllMonitors);
        }
    }
}
