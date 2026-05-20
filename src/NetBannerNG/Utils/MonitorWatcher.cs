using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using NetBannerNG.Common.AppBar;

namespace NetBannerNG.Utils
{
    internal static class MonitorWatcher
    {
        private static readonly object Sync = new();
        private static Action _onDisplaySettingsChanged = () => { };
        private static bool _isWatching;
        private static DispatcherOperation? _pendingRefresh;

        internal static void Watch(Action onDisplaySettingsChanged)
        {
            lock (Sync)
            {
                if (_isWatching)
                {
                    return;
                }

                _onDisplaySettingsChanged = onDisplaySettingsChanged ?? (() => { });
                SystemEvents.DisplaySettingsChanged += ScreenHandler!;
                _isWatching = true;
            }
        }

        internal static void Unwatch()
        {
            lock (Sync)
            {
                if (!_isWatching)
                {
                    return;
                }

                SystemEvents.DisplaySettingsChanged -= ScreenHandler!;
                _isWatching = false;
                _onDisplaySettingsChanged = () => { };
                _pendingRefresh?.Abort();
                _pendingRefresh = null;
            }
        }

        private static void ScreenHandler(object sender, EventArgs e)
        {
            // Suppress ABN_POSCHANGED on all bars immediately so they are not repositioned
            // with transitional work-area values while the resolution change settles.
            AppBarFunctions.SuppressAbnPosChanged();

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            // Coalesce rapid DisplaySettingsChanged events (Windows can fire several during a
            // single resolution change transition) into one Refresh. Aborting the previous
            // pending dispatch and queuing a replacement means only the final, settled monitor
            // state triggers the rebuild, preventing multiple teardown/rebuild cycles that
            // cause blinking.
            lock (Sync)
            {
                _pendingRefresh?.Abort();
                _pendingRefresh = dispatcher.BeginInvoke(_onDisplaySettingsChanged, DispatcherPriority.Background);
            }
        }
    }
}