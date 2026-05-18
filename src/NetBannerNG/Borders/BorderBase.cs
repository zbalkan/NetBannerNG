using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using NetBannerNG.Utils;

namespace NetBannerNG.Borders
{
    public abstract class BorderBase : Window
    {
        internal bool IsDocked { get; set; }
        internal string AppBarMessageKey { get; set; } = string.Empty;

        protected BorderBase() => DpiChanged += OnBorderDpiChanged;

        private void OnBorderDpiChanged(object sender, DpiChangedEventArgs e)
        {
            // WPF's WM_DPICHANGED handling rescales the window by the DPI ratio, which leaves
            // the bar at the wrong pixel rect for its monitor. Trigger a fresh re-dock so
            // AbSetPos recomputes pixel/DIP coordinates against the current monitor matrix.
            // Background priority lets WPF finish its own DPI bookkeeping first.
            if (!IsDocked)
            {
                return;
            }

            _ = Dispatcher.BeginInvoke(new Action(() => Render(true)), DispatcherPriority.Background);
        }

        internal abstract void Render(bool needsResize = false);

        protected abstract void ReadSettings();

        protected void RenderWindow(bool needsResize, Action layoutAction)
        {
            if (layoutAction is null)
            {
                throw new ArgumentNullException(nameof(layoutAction));
            }

            ReadSettings();
            if (!needsResize)
            {
                return;
            }

            layoutAction();
        }

        protected void RefreshSettings()
        {
            Settings.Instance.Refresh();
            DataContext = Settings.Instance;
        }

        protected static double CalculateFontSize(
            double barHeight,
            double topMargin,
            double bottomMargin,
            double minFontSize = 8,
            double maxFontSize = 72,
            double fontScale = 0.8)
        {
            var usableHeight = Math.Max(0, barHeight - topMargin - bottomMargin);
            var fontSize = usableHeight * fontScale;

            return Math.Max(minFontSize, Math.Min(maxFontSize, fontSize));
        }

        internal void ApplyPostDockVisualState() => this.HideFromTaskBar();

        protected override void OnClosing(CancelEventArgs e)
        {
            this.Undock();
            Width = 0;
            Height = 0;
            base.OnClosing(e);
        }

        /// <summary>
        /// <para>    Base event for Window_Loaded event. Override this way:</para>
        /// <para>
        ///     internal new void Window_Loaded(object sender, RoutedEventArgs e)
        ///     {
        ///         // Add your code here to run before window is drawn &amp; docked
        ///         base.Window_Loaded(sender,e);
        ///         // Add your code here to run after window is drawn &amp; docked
        ///     }
        /// </para>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal virtual void Window_Loaded(object sender, RoutedEventArgs e) => Render(true);
    }
}