using NetBannerNG.Utils;

namespace NetBannerNG.Borders
{
    /// <summary>
    ///     Interaction logic for
    /// </summary>
    public partial class BottomBar : BorderBase
    {
        internal BottomBar()
        {
            InitializeComponent();
        }

        internal override void Render(bool needsResize = false) =>
            RenderWindow(needsResize, () => this.DockBottom());

        protected override void ReadSettings()
        {
            Background = Settings.Instance.CustomBackgroundColor;
            Height = Settings.Instance.BorderSize;
        }

    }
}