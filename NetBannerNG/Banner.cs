using System;
using System.Drawing;
using System.Windows.Forms;

namespace NetBannerNG
{
    public partial class Banner : Form
    {
        private Label ClassificationLabel;

        protected Banner()
        {
            AppBarHelper.PreventShowDesktop(this.Handle);
            InitializeComponent();
        }

        public Banner(ClassificationMark classification)
        {
            PaintBanner(classification.BackgroundColor);
            WriteClassification(classification.ClassificationName.ToUpperInvariant(), classification.ForeColor);
            AppBarHelper.PreventShowDesktop(this.Handle);
            InitializeComponent();
        }

        public Banner(ClassificationMark classification, string caveat) : this()
        {
            PaintBanner(classification.BackgroundColor);
            WriteClassification($"{classification.ClassificationName.ToUpperInvariant()} RELEASABLE TO {caveat.ToUpperInvariant()}", classification.ForeColor);
            AppBarHelper.PreventShowDesktop(this.Handle);
            InitializeComponent();
        }

        public static Banner Error()
        {
            return new Banner(new ClassificationMark()
            {
                ClassificationName = "Classification not configured",
                BackgroundColor = Color.White,
                ForeColor = Color.Black
            });
        }

        private void WriteClassification(string classification, Color foreColor)
        {
            ClassificationLabel = new Label
            {
                AutoSize = true,
                Name = "ClassificationLabel",
                Size = new Size(20, 20),
                Anchor = AnchorStyles.None,
                Text = classification,
                Font = new Font("Segoe UI Semibold", 14, FontStyle.Bold),
                ForeColor = foreColor
            };

            Controls.Add(ClassificationLabel);
        }

        private void PaintBanner(Color backgroundColor)
        {
            BackColor = backgroundColor;
        }

        private void CenterClassification()
        {
            ClassificationLabel.Left = (this.Width - ClassificationLabel.Width) / 2;
            ClassificationLabel.Top = (this.Height - ClassificationLabel.Height) / 2;
        }

        private void Banner_Load(object sender, EventArgs e)
        {
            CenterClassification();
            AppBarHelper.AppBarMessage = "NetBannerNG";
            AppBarHelper.SetAppBar(this, AppBarEdge.Top);

            this.FormClosing += new FormClosingEventHandler(Banner_FormClosing);
            this.SizeChanged += new EventHandler(Banner_SizeChanged);
        }

        private void Banner_SizeChanged(object sender, EventArgs e)
        {
            CenterClassification();
        }

        private void Banner_FormClosing(object sender, FormClosingEventArgs e)
        {
            AppBarHelper.SetAppBar(this, AppBarEdge.None);
        }
    }
}
