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

        internal override void Render(bool needsResize = false) =>
            RenderWindow(needsResize, () => this.DockRight(null, true));

        protected override void ReadSettings()
        {
            Background = Settings.Instance.CustomBackgroundColor;
            Width = Settings.Instance.BorderSize;
        }

    }
}