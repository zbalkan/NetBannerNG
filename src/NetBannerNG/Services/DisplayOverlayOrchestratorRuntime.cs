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
        private readonly List<Monitor> _previousMonitors = new();
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        private readonly IMonitorSurfaceCatalog _surfaceCatalog;
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
        private bool _cleanStart;
        private bool _isInitiated;
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

        public void ApplyFullscreenSuppressionStates(IReadOnlyDictionary<string, FullscreenSuppressionState> suppressionByGroup)
        {
            var suppressedCount = suppressionByGroup.Count(kv => kv.Value.IsSuppressed);
            var appTaggedCount = suppressionByGroup.Count(kv => kv.Value.IsSuppressed && !string.IsNullOrWhiteSpace(kv.Value.AppName));
            Debug.WriteLine($"[EVT:4204][OverlayOrchestrator][Suppression][Apply] Updates={suppressionByGroup.Count} Suppressed={suppressedCount} Tagged={appTaggedCount}");
            foreach (var group in _surfaceCatalog.Snapshot())
            {
                var isFullscreen = suppressionByGroup.TryGetValue(group.GroupId, out var state) && state.IsSuppressed;
                group.SetSuppressed(isFullscreen);
            }
        }

        public void BeginShutdown()
        {
            _shutdownInProgress = true;
            Debug.WriteLine($"[EVT:4208][OverlayOrchestrator][Shutdown][Begin] CatalogCount={_surfaceCatalog.Count} Initiated={_isInitiated}");
        }

        public void CloseAllSurfaces()
        {
            var groups = _surfaceCatalog.Snapshot(clear: true);
            Debug.WriteLine($"[EVT:4203][OverlayOrchestrator][Shutdown][CloseAll] Groups={groups.Count}");
            _isInitiated = false;
            foreach (var g in groups)
            {
                g.Close();
            }
        }

        public void Init(bool clean)
        {
            _shutdownInProgress = false;
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
            var groupsToShow = _surfaceCatalog.Reconcile(_previousMonitors, _cleanStart);
            Debug.WriteLine($"[EVT:4201][OverlayOrchestrator][Reconcile][Initiate] MonitorCount={_previousMonitors.Count} GroupsToShow={groupsToShow.Count} CleanStart={_cleanStart}");
            ShowGroups(groupsToShow);
            _isInitiated = true;
            _cleanStart = true;
        }

        public void Refresh()
        {
            if (_shutdownInProgress)
            {
                Debug.WriteLine("[EVT:4207][OverlayOrchestrator][Refresh][Skipped] Reason=ShutdownInProgress");
                return;
            }

            // One batch around the whole reconcile so the unregister-then-register sequence
            // of any monitor whose layout changed does NOT broadcast ABN_POSCHANGED to bars
            // on other monitors. Per-monitor MonitorSurfaceSets stay isolated: only the set
            // whose layout actually changed gets torn down and rebuilt; everything else
            // remains untouched on screen.
            using (AppBarFunctions.Batch())
            {
                ResetPreviousMonitors();
                var groupsToShow = _surfaceCatalog.Reconcile(_previousMonitors, clean: false);
                Debug.WriteLine($"[EVT:4202][OverlayOrchestrator][Reconcile][Refresh] MonitorCount={_previousMonitors.Count} GroupsToShow={groupsToShow.Count} CatalogCount={_surfaceCatalog.Count}");
                ShowGroups(groupsToShow);
            }
            _isInitiated = _surfaceCatalog.Count > 0;
        }
        private void ResetPreviousMonitors()
        {
            _previousMonitors.Clear();
            _previousMonitors.AddRange(Monitor.AllMonitors);
        }

        private void ShowGroups(IEnumerable<IMonitorSurfaceSet> groups)
        {
            var orderedGroups = groups.OrderByDescending(g => g.Monitor.IsPrimary).ThenBy(g => g.Monitor.Bounds.Y).ThenBy(g => g.Monitor.Bounds.X).ToList();
            if (orderedGroups.Count == 0)
            {
                Debug.WriteLine("[EVT:4209][OverlayOrchestrator][ShowGroups][NoOp] Reason=NoGroups");
                return;
            }
            var stopwatch = Stopwatch.StartNew();
            long firstBannerShownAtMs = -1;

            foreach (var group in orderedGroups)
            {
                var launchEntries = group.CreateLaunchEntries().ToList();
                using (AppBarFunctions.Batch())
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

                group.ApplyPostDockVisualState();
            }

            _cleanStart = true;
        }
    }
}