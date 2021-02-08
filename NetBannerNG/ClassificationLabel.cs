using System.Windows.Forms;
using System.Drawing;

namespace NetBannerNG
{
    public class ClassificationLabel : Label
    {
        public ClassificationLabel()
        {
            AutoSize = true;
            Name = "ClassificationLabel";
            Size = new Size(20, 20);
            Dock = DockStyle.None;
            Font = new Font("Segoe UI Semibold", 14, FontStyle.Bold);
        }
    }
}
