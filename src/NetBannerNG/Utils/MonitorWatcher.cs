using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace NetBannerNG.Utils
{
    internal static class MonitorWatcher
    {
        private static Action _onDisplaySettingsChanged = () => { };
        private static bool _isWatching;

        internal static void Watch(Action onDisplaySettingsChanged)
        {
            if (_isWatching)
            {
                return;
            }

            _onDisplaySettingsChanged = onDisplaySettingsChanged ?? (() => { });
            SystemEvents.DisplaySettingsChanged += ScreenHandler!;
            _isWatching = true;
        }

        internal static void Unwatch()
        {
            if (!_isWatching)
            {
                return;
            }

            SystemEvents.DisplaySettingsChanged -= ScreenHandler!;
            _isWatching = false;
        }

        private static void ScreenHandler(object sender, EventArgs e)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            _ = dispatcher.BeginInvoke(_onDisplaySettingsChanged, DispatcherPriority.Background);
        }
    }
}