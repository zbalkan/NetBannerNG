using System;
using NetBannerNG.Utils;

namespace NetBannerNG.Borders
{
    /// <summary>
    ///     Interaction logic for
    /// </summary>
    public partial class BottomBanner : BorderBase
    {
        internal BottomBanner()
        {
            InitializeComponent();
            RefreshDataContext();
        }

        internal override void Render(bool needsResize = false)
        {
            ReadSettings();
            if (!needsResize)
            {
                return;
            }

            Height = Settings.Instance.BannerSize;
            this.DockBottom(RootGrid);
        }

        protected override void ReadSettings()
        {
            RefreshDataContext();

            MinHeight = Settings.Instance.BannerSize;
            MaxHeight = Settings.Instance.BannerSize;
            TbClassification.FontSize = CalculateFontSize(Settings.Instance.BannerSize, topMargin: 2, bottomMargin: 2);
        }

        public static double CalculateFontSize(
            double barHeight,
            double topMargin,
            double bottomMargin,
            double minFontSize = 8,
            double maxFontSize = 72,
            double fontScale = 0.9)
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
    }
}