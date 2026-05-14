using System;
using System.Collections.Generic;
using NetBannerNG.Utils;

namespace NetBannerNG.Services
{
    internal interface IForegroundWindowWatcher
    {
        event Action<IReadOnlyDictionary<string, FullscreenSuppressionState>>? FullscreenSuppressionUpdated;
        Func<string, System.Threading.Tasks.Task>? EventLogSinkAsync { get; set; }
        void Watch();
        void Unwatch();
    }

    internal sealed class StaticForegroundWindowWatcher : IForegroundWindowWatcher
    {
        public event Action<IReadOnlyDictionary<string, FullscreenSuppressionState>>? FullscreenSuppressionUpdated
        {
            add => WindowWatcher.FullscreenSuppressionUpdated += value;
            remove => WindowWatcher.FullscreenSuppressionUpdated -= value;
        }

        public Func<string, System.Threading.Tasks.Task>? EventLogSinkAsync
        {
            get => WindowWatcher.EventLogSinkAsync;
            set => WindowWatcher.EventLogSinkAsync = value;
        }

        public void Watch() => WindowWatcher.Watch();

        public void Unwatch() => WindowWatcher.Unwatch();
    }

    internal interface IFullscreenSuppressionService
    {
        event Action<IReadOnlyDictionary<string, FullscreenSuppressionState>>? SuppressionUpdated;
        void Start();
        void Stop();
    }

    internal sealed class FullscreenSuppressionService : IFullscreenSuppressionService
    {
        private readonly IForegroundWindowWatcher _foregroundWindowWatcher;

        internal FullscreenSuppressionService()
            : this(new StaticForegroundWindowWatcher())
        {
        }

        internal FullscreenSuppressionService(IForegroundWindowWatcher foregroundWindowWatcher)
        {
            _foregroundWindowWatcher = foregroundWindowWatcher;
        }

        internal Func<string, System.Threading.Tasks.Task>? EventLogSinkAsync
        {
            get => _foregroundWindowWatcher.EventLogSinkAsync;
            set => _foregroundWindowWatcher.EventLogSinkAsync = value;
        }

        public event Action<IReadOnlyDictionary<string, FullscreenSuppressionState>>? SuppressionUpdated;

        public void Start()
        {
            _foregroundWindowWatcher.FullscreenSuppressionUpdated += OnFullscreenSuppressionUpdated;
            _foregroundWindowWatcher.Watch();
        }

        public void Stop()
        {
            _foregroundWindowWatcher.FullscreenSuppressionUpdated -= OnFullscreenSuppressionUpdated;
            _foregroundWindowWatcher.Unwatch();
        }

        private void OnFullscreenSuppressionUpdated(IReadOnlyDictionary<string, FullscreenSuppressionState> suppressionByGroup) =>
            SuppressionUpdated?.Invoke(suppressionByGroup);
    }
}
