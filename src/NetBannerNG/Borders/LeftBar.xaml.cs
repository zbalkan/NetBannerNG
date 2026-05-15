using NetBannerNG.Utils;

namespace NetBannerNG.Borders
{
    /// <summary>
    ///     Interaction logic for
    /// </summary>
    public partial class LeftBar : BorderBase
    {
        internal LeftBar()
        {
            InitializeComponent();
        }

        internal override void Render(bool needsResize = false) =>
            RenderWindow(needsResize, () => this.DockLeft());

        protected override void ReadSettings()
        {
            Background = Settings.Instance.CustomBackgroundColor;
            Width = Settings.Instance.BorderSize;
        }
    }
}