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
