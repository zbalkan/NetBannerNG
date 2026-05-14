using System.Collections.Generic;

namespace NetBannerNG.Services
{
    internal interface IDisplayOverlayOrchestrator
    {
        void Init(bool clean);
        void InitiateAllBorders();
        void Refresh();
        void BeginShutdown();
        void CloseAllBorders();
        void ApplyFullscreenSuppressionStates(IReadOnlyDictionary<string, bool> suppressionByGroup);
    }

    internal sealed class StaticDisplayOverlayOrchestrator : IDisplayOverlayOrchestrator
    {
        private static readonly DisplayOverlayOrchestratorRuntime Runtime = new();

        public void Init(bool clean) => Runtime.Init(clean);

        public void InitiateAllBorders() => Runtime.InitiateAllBorders();

        public void Refresh() => Runtime.Refresh();

        public void BeginShutdown() => Runtime.BeginShutdown();

        public void CloseAllBorders() => Runtime.CloseAllBorders();

        public void ApplyFullscreenSuppressionStates(IReadOnlyDictionary<string, bool> suppressionByGroup) =>
            Runtime.ApplyFullscreenSuppressionStates(suppressionByGroup);
    }
}
