using System;
using NetBannerNG.Utils;

namespace NetBannerNG.Borders
{
    /// <summary>
    ///     Interaction logic for
    /// </summary>
    public partial class Banner : BorderBase
    {
        internal Banner()
        {
            InitializeComponent();
            Render();
        }

        internal override void Render(bool needsResize = false)
        {
            ReadSettings();
            if (!needsResize)
            {
                return;
            }

            Height = Settings.Instance.BannerSize;
            this.DockTop(RootGrid);
        }

        protected override void ReadSettings()
        {
            RefreshDataContext();

            MinHeight = Settings.Instance.BannerSize;
            MaxHeight = Settings.Instance.BannerSize;
            TbClassification.FontSize = CalculateFontSize(Settings.Instance.BannerSize, topMargin: TbClassification.Margin.Top, bottomMargin: TbClassification.Margin.Bottom);
        }

        public static double CalculateFontSize(
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

        private void RefreshDataContext()
        {
            Settings.Instance.Refresh();
            DataContext = Settings.Instance;
        }

        private void BorderBase_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Width = 0;
            Height = 0;
            this.Undock();
        }

        private void BorderBase_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {

        }
    }
}