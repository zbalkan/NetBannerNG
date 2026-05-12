using NetBannerNG.Utils;

namespace NetBannerNG.Borders
{
    /// <summary>
    ///     Interaction logic for
    /// </summary>
    public partial class RightBar : BorderBase
    {
        internal RightBar()
        {
            InitializeComponent();
        }

        internal override void Render(bool needsResize = false)
        {
            ReadSettings();
            if (!needsResize)
            {
                return;
            }

            this.DockRight();
        }

        protected override void ReadSettings()
        {
            Background = Settings.Instance.CustomBackgroundColor;
            Width = Settings.Instance.BorderSize;
        }

        private void BorderBase_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Width = 0;
            Height = 0;
            this.Undock();
        }
    }
}