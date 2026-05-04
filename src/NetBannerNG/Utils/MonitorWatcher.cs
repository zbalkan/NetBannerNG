using Microsoft.Win32;
using System.Windows;
using System.Windows.Threading;

namespace NetBannerNG.Utils
{
    internal static class MonitorWatcher
    {
        private static event Action? DisplaySettingsChanged;
        private static bool _isWatching;

        internal static void Watch()
        {
            if (_isWatching)
            {
                return;
            }

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
            var callbacks = DisplaySettingsChanged;
            if (callbacks == null)
            {
                return;
            }

            _ = Application.Current.Dispatcher.BeginInvoke(callbacks, DispatcherPriority.Background);
        }

        internal static void SetTrigger(Action action)
        {
            DisplaySettingsChanged += action;
        }
    }
}
