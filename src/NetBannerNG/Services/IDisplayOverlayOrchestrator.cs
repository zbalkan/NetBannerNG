using System.Collections.Generic;

namespace NetBannerNG.Services
{
    internal interface IDisplayOverlayOrchestrator
    {
        void Init(bool clean);
        void InitiateAllSurfaces();
        void Refresh();
        void BeginShutdown();
        void CloseAllSurfaces();
        void ApplyFullscreenSuppressionStates(IReadOnlyDictionary<string, FullscreenSuppressionState> suppressionByGroup);
    }

    internal sealed class StaticDisplayOverlayOrchestrator : IDisplayOverlayOrchestrator
    {
        private static readonly DisplayOverlayOrchestratorRuntime Runtime = new();

        public void Init(bool clean) => Runtime.Init(clean);

        public void InitiateAllSurfaces() => Runtime.InitiateAllSurfaces();

        public void Refresh() => Runtime.Refresh();

        public void BeginShutdown() => Runtime.BeginShutdown();

        public void CloseAllSurfaces() => Runtime.CloseAllSurfaces();

        public void ApplyFullscreenSuppressionStates(IReadOnlyDictionary<string, FullscreenSuppressionState> suppressionByGroup) =>
            Runtime.ApplyFullscreenSuppressionStates(suppressionByGroup);
    }
}
