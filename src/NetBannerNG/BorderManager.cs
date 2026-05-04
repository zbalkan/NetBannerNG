using NetBannerNG.Borders;
using NetBannerNG.Common.AppBar;
using NetBannerNG.Utils;
using System.Diagnostics;

namespace NetBannerNG
{
    internal static class BorderManager
    {
        private static readonly List<Common.Native.Monitor> PreviousMonitors = new();
        private static bool _isInitiated;
        private static bool _cleanStart;

        private sealed class BorderLaunchEntry
        {
            internal required BorderBase Window { get; init; }
            internal required bool IsPrimaryMonitor { get; init; }
            internal required int MonitorY { get; init; }
            internal required int MonitorX { get; init; }
            internal required int WindowOrder { get; init; }
        }

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
            var launchPlan = BuildLaunchPlan(monitors, clean)
                .OrderByDescending(entry => entry.IsPrimaryMonitor)
                .ThenBy(entry => entry.MonitorY)
                .ThenBy(entry => entry.MonitorX)
                .ThenBy(entry => entry.WindowOrder)
                .ToList();

            var stopwatch = Stopwatch.StartNew();
            long firstBannerShownAtMs = -1;

            AppBarFunctions.BeginBatch();
            try
            {
                foreach (var launchEntry in launchPlan)
                {
                    launchEntry.Window.Show();
                    var shownAtMs = stopwatch.ElapsedMilliseconds;
                    Debug.WriteLine($"[RenderPerf] Show {launchEntry.Window.GetType().Name} at +{shownAtMs}ms");
                    if (firstBannerShownAtMs < 0 && launchEntry.Window is Banner)
                    {
                        firstBannerShownAtMs = shownAtMs;
                        Debug.WriteLine($"[RenderPerf] First Banner shown at +{firstBannerShownAtMs}ms");
                    }
                }
            }
            finally
            {
                AppBarFunctions.EndBatch();
            }

            foreach (var launchEntry in launchPlan)
            {
                launchEntry.Window.ApplyPostDockVisualState();
            }

            Debug.WriteLine($"[RenderPerf] Final appbar dock completed at +{stopwatch.ElapsedMilliseconds}ms");
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

        private static List<BorderLaunchEntry> BuildLaunchPlan(List<Common.Native.Monitor> monitors, bool clean)
        {
            var launchPlan = new List<BorderLaunchEntry>(monitors.Count * 4);
            foreach (var monitor in monitors)
            {
                launchPlan.Add(new BorderLaunchEntry
                {
                    Window = new Banner
                    {
                        Owner = App.Current.MainWindow,
                        Top = monitor.WorkingArea.Top,
                        Left = monitor.WorkingArea.Left,
                        Width = monitor.WorkingArea.Width,
                        IsDocked = !clean
                    },
                    IsPrimaryMonitor = monitor.IsPrimary,
                    MonitorY = monitor.Bounds.Y,
                    MonitorX = monitor.Bounds.X,
                    WindowOrder = 0
                });

                if (Settings.Instance.DisableBorders)
                {
                    continue;
                }

                launchPlan.Add(new BorderLaunchEntry
                {
                    Window = new BottomBar
                    {
                        Owner = App.Current.MainWindow,
                        Top = monitor.WorkingArea.Top,
                        Left = monitor.WorkingArea.Left,
                        Width = monitor.WorkingArea.Width,
                        IsDocked = !clean
                    },
                    IsPrimaryMonitor = monitor.IsPrimary,
                    MonitorY = monitor.Bounds.Y,
                    MonitorX = monitor.Bounds.X,
                    WindowOrder = 1
                });

                launchPlan.Add(new BorderLaunchEntry
                {
                    Window = new LeftBar
                    {
                        Owner = App.Current.MainWindow,
                        Top = monitor.WorkingArea.Top,
                        Left = monitor.WorkingArea.Left,
                        Height = monitor.WorkingArea.Height,
                        IsDocked = !clean
                    },
                    IsPrimaryMonitor = monitor.IsPrimary,
                    MonitorY = monitor.Bounds.Y,
                    MonitorX = monitor.Bounds.X,
                    WindowOrder = 2
                });

                launchPlan.Add(new BorderLaunchEntry
                {
                    Window = new RightBar
                    {
                        Owner = App.Current.MainWindow,
                        Top = monitor.WorkingArea.Top,
                        Left = monitor.WorkingArea.Left,
                        Height = monitor.WorkingArea.Height,
                        IsDocked = !clean
                    },
                    IsPrimaryMonitor = monitor.IsPrimary,
                    MonitorY = monitor.Bounds.Y,
                    MonitorX = monitor.Bounds.X,
                    WindowOrder = 3
                });
            }

            return launchPlan;
        }

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
