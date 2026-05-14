using System;
using System.Collections.Generic;
using NetBannerNG.Utils;

namespace NetBannerNG.Services
{
    internal interface IFullscreenSuppressionService
    {
        event Action<IReadOnlyDictionary<string, bool>>? SuppressionUpdated;
        void Start();
        void Stop();
    }

    internal sealed class FullscreenSuppressionService : IFullscreenSuppressionService
    {
        internal Func<string, System.Threading.Tasks.Task>? EventLogSinkAsync
        {
            get => WindowWatcher.EventLogSinkAsync;
            set => WindowWatcher.EventLogSinkAsync = value;
        }

        public event Action<IReadOnlyDictionary<string, bool>>? SuppressionUpdated;

        public void Start()
        {
            WindowWatcher.FullscreenSuppressionUpdated += OnFullscreenSuppressionUpdated;
            WindowWatcher.Watch();
        }

        public void Stop()
        {
            WindowWatcher.FullscreenSuppressionUpdated -= OnFullscreenSuppressionUpdated;
            WindowWatcher.Unwatch();
        }

        private void OnFullscreenSuppressionUpdated(IReadOnlyDictionary<string, bool> suppressionByGroup) =>
            SuppressionUpdated?.Invoke(suppressionByGroup);
    }
}
