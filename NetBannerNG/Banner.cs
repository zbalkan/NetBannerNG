using System;
using System.Drawing;
using System.Windows.Forms;

namespace NetBannerNG
{
    public partial class Banner : Form
    {
        public Label ClassificationLabel { get; set; }
        public Label ConditionLabel { get; set; }

        public Banner()
        {
            AppBarHelper.PreventShowDesktop(this.Handle);
            InitializeComponent();
        }

        public static Banner Error()
        {
            return new Banner()
            {
                ClassificationLabel = new ClassificationLabel() { Text = "Classification not configured" } ,
                BackColor = Color.White,
                ForeColor = Color.Black
            };
        }

        #region ALIGNING
        private void CenterClassification()
        {
            ClassificationLabel.Left = (this.Width - ClassificationLabel.Width) / 2;
            ClassificationLabel.Top = (this.Height - ClassificationLabel.Height) / 2;
        }

        private void AlignCon()
        {
            if (ConditionLabel == null) return;
            ConditionLabel.Left = (this.Width - ConditionLabel.Width) - 30;
            ConditionLabel.Top = (this.Height - ConditionLabel.Height) / 2;
        } 
        #endregion

        #region EVENTS
        private void Banner_Load(object sender, EventArgs e)
        {
            Controls.Add(ClassificationLabel);
            if(ConditionLabel != null) Controls.Add(ConditionLabel);

            AppBarHelper.AppBarMessage = "NetBannerNG";
            AppBarHelper.SetAppBar(this, AppBarEdge.Top);

            CenterClassification();
            AlignCon();
        }

        private void Banner_SizeChanged(object sender, EventArgs e)
        {
            CenterClassification();
            AlignCon();
        }

        private void Banner_FormClosing(object sender, FormClosingEventArgs e)
        {
            AppBarHelper.SetAppBar(this, AppBarEdge.None);
        } 
        #endregion
    }
}
