using System.Windows.Forms;
using System.Drawing;

namespace NetBannerNG
{
    public class ConditionLabel : Label
    {
        public ConditionLabel()
        {
            AutoSize = true;
            Name = "ConLabel";
            Size = new Size(20, 14);
            Font = new Font("Segoe UI", 12, FontStyle.Regular);
        }
    }
}
