using System;
using System.ComponentModel;
using System.Windows;
using NetBannerNG.Utils;

namespace NetBannerNG.Borders
{
    public abstract class BorderBase : Window
    {
        internal bool IsDocked { get; set; }
        internal string AppBarMessageKey { get; set; } = string.Empty;

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
            Width = 0;
            Height = 0;
            this.Undock();
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
        internal void Window_Loaded(object sender, RoutedEventArgs e) => Render(true);
    }
}