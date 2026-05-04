using Microsoft.Win32;
using System.Windows;

namespace NetBannerNG.Utils
{
    internal static class MonitorWatcher
    {
        private static readonly List<Action> ActionsToTrigger = new();
        private static bool _isWatching;

        internal static void Watch()
        {
            SystemEvents.DisplaySettingsChanged += ScreenHandler!;
            _isWatching = true;
        }

        internal static void Unwatch()
        {
            if (_isWatching) SystemEvents.DisplaySettingsChanged -= ScreenHandler!;
        }

        private static void ScreenHandler(object sender, EventArgs e)
        {
            foreach (var action in ActionsToTrigger)
            {
                Application.Current.Dispatcher.Invoke(action);
            }
        }

        // TODO: Convert to event delegate
        internal static void SetTrigger(Action action)
        {
            ActionsToTrigger.Add(action);
        }
    }
}