using System.Collections.Generic;
using System.Windows;
using NetBannerNG.Services;
using NetBannerNG.Utils;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG
{
    public static class DisplayOverlayOrchestrator
    {
        private static readonly IDisplayOverlayOrchestrator Runtime = new DisplayOverlayOrchestratorRuntime();
        private static readonly IMonitorIdentity MonitorIdentityProvider = new MonitorIdentityProvider();

        internal static void Init(bool clean = true) => Runtime.Init(clean);

        internal static void InitiateAllSurfaces() => Runtime.InitiateAllSurfaces();

        internal static void Refresh() => Runtime.Refresh();

        internal static void BeginShutdown() => Runtime.BeginShutdown();

        internal static void CloseAllSurfaces() => Runtime.CloseAllSurfaces();

        internal static void ApplyFullscreenSuppressionStates(IReadOnlyDictionary<string, FullscreenSuppressionState> suppressionByGroup) => Runtime.ApplyFullscreenSuppressionStates(suppressionByGroup);

        public static bool HasMonitorLayoutChanged(Monitor previous, Monitor next) =>
            HasMonitorLayoutChanged(previous.Bounds, previous.WorkingArea, previous.IsPrimary, next.Bounds, next.WorkingArea, next.IsPrimary);

        public static bool HasMonitorLayoutChanged(Rect previousBounds, Rect previousWorkingArea, bool previousIsPrimary, Rect nextBounds, Rect nextWorkingArea, bool nextIsPrimary) =>
            previousBounds != nextBounds || previousWorkingArea != nextWorkingArea || previousIsPrimary != nextIsPrimary;

        public static string BuildGroupId(Monitor monitor) => MonitorIdentityProvider.BuildGroupId(monitor);

        public static string BuildGroupId(string monitorName, Rect bounds) => MonitorIdentityProvider.BuildGroupId(monitorName, bounds);
    }
}