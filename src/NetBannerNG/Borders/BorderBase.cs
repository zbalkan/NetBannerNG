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

        internal void ApplyPostDockVisualState() => this.HideFromTaskBar();

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