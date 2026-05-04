using NetBannerNG.Borders;
using NetBannerNG.Utils;

namespace NetBannerNG
{
    internal static class BorderManager
    {
        private static readonly List<Common.Native.Monitor> PreviousMonitors = new();
        private static bool _isInitiated;
        private static bool _cleanStart;

        internal static void Init(bool clean = true)
        {
            _cleanStart = clean;
            App.Current.MainWindow = new Launcher();
            App.Current.MainWindow.Show();
        }

        internal static void InitiateAllBorders()
        {
            if (_isInitiated)
            {
                return;
            }

            ResetPreviousMonitors();
            var monitors = PreviousMonitors.ToList();
            InitiateBordersInMonitors(monitors, _cleanStart);
            _isInitiated = true;
        }

        internal static void InitiateBordersInMonitors(List<Common.Native.Monitor> monitors, bool clean = true)
        {
            foreach (var monitor in monitors)
            {
                var banner = new Banner
                {
                    Owner = App.Current.MainWindow,
                    Top = monitor.WorkingArea.Top,
                    Left = monitor.WorkingArea.Left,
                    Width = monitor.WorkingArea.Width,
                    IsDocked = !clean
                };

                if (Settings.Instance.DisableBorders)
                {
                    continue;
                }

                var bottom = new BottomBar
                {
                    Owner = App.Current.MainWindow,
                    Top = monitor.WorkingArea.Top,
                    Left = monitor.WorkingArea.Left,
                    Width = monitor.WorkingArea.Width,
                    IsDocked = !clean
                };

                var left = new LeftBar
                {
                    Owner = App.Current.MainWindow,
                    Top = monitor.WorkingArea.Top,
                    Left = monitor.WorkingArea.Left,
                    Height = monitor.WorkingArea.Height,
                    IsDocked = !clean
                };

                var right = new RightBar
                {
                    Owner = App.Current.MainWindow,
                    Top = monitor.WorkingArea.Top,
                    Left = monitor.WorkingArea.Left,
                    Height = monitor.WorkingArea.Height,
                    IsDocked = !clean
                };
            }

            // The order must be Top (Banner), Bottom, Left, Right to prevent racing conditions.
            var orderedWindows = App.Current.MainWindow
                .OwnedWindows
                .Cast<BorderBase>()
                .OrderByDescending(w => w.GetMonitor().IsPrimary)
                .ThenBy(w => w.GetMonitor().Bounds.Y)
                .ThenBy(w => w.GetMonitor().Bounds.X)
                .ThenBy(w => w.GetType().Name)
                .Where(mainWindowOwnedWindow => !mainWindowOwnedWindow.IsVisible)
                .ToList();
            foreach (var mainWindowOwnedWindow in orderedWindows)
            {
                mainWindowOwnedWindow.Show();
            }
            _cleanStart = true;
        }

        internal static void Refresh()
        {
            CloseAllBorders();
            InitiateAllBorders();
        }

        internal static void CloseAllBorders()
        {
            var ownedWindows = GetOwnedBorders();
            if (ownedWindows.Count == 0)
            {
                return;
            }

            CloseBorders(ownedWindows);
            _isInitiated = false;
        }

        internal static void SendTop() => SetTopMost(true);

        internal static void SendBottom() => SetTopMost(false);

        private static List<BorderBase> GetOwnedBorders() => App.Current.MainWindow?.OwnedWindows.Cast<BorderBase>().ToList() ?? new List<BorderBase>();

        private static void SetTopMost(bool topMost)
        {
            var ownedWindows = GetOwnedBorders();
            if (ownedWindows.Count == 0)
            {
                return;
            }

            foreach (var window in ownedWindows)
            {
                window.Topmost = topMost;
            }
        }

        private static void ResetPreviousMonitors()
        {
            PreviousMonitors.Clear();
            PreviousMonitors.AddRange(Common.Native.Monitor.AllMonitors);
        }

        private static void CloseBorders(IEnumerable<BorderBase> borders)
        {
            foreach (var border in borders.ToList())
            {
                border.Close();
            }
        }
    }
}
