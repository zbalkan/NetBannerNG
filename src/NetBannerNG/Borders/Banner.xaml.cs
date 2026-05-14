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

        internal override void Render(bool needsResize = false) =>
            RenderWindow(needsResize, () => {
                Height = Settings.Instance.BannerSize;
                this.DockTop(RootGrid);
            });

        protected override void ReadSettings()
        {
            RefreshSettings();

            MinHeight = Settings.Instance.BannerSize;
            MaxHeight = Settings.Instance.BannerSize;
            TbClassification.FontSize = CalculateFontSize(
                Settings.Instance.BannerSize,
                topMargin: TbClassification.Margin.Top,
                bottomMargin: TbClassification.Margin.Bottom,
                fontScale: 0.8);
        }
    }
}