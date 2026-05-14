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
        public void Init(bool clean) => DisplayOverlayOrchestrator.Init(clean);

        public void InitiateAllBorders() => DisplayOverlayOrchestrator.InitiateAllBorders();

        public void Refresh() => DisplayOverlayOrchestrator.Refresh();

        public void BeginShutdown() => DisplayOverlayOrchestrator.BeginShutdown();

        public void CloseAllBorders() => DisplayOverlayOrchestrator.CloseAllBorders();

        public void ApplyFullscreenSuppressionStates(IReadOnlyDictionary<string, bool> suppressionByGroup) =>
            DisplayOverlayOrchestrator.ApplyFullscreenSuppressionStates(suppressionByGroup);
    }
}
