using EnvDTE;
using EnvDTE80;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace NetBannerNG
{
    public class Program
    {
        private static Banner banner;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (RegistryHelper.ConnectRegistry())
            {
                var classification = RegistryHelper.GetClassification();
                banner = RegistryHelper.IsCaveatsEnabled() ? new Banner(classification, RegistryHelper.GetCaveat()) : new Banner(classification);
                RegistryHelper.DisconnectRegistry();
            }
            else
            {
                banner = new Banner(new ClassificationMark() { ClassificationName = "Classification not configured", BackgroundColor = Color.White, ForeColor = Color.Black });
            }

            Application.Run(banner);
        }

        private void Application_ThreadExit(Object sender, EventArgs e)
        {

            MessageBox.Show("You are in the Application.ThreadExit event.");
        }
    }
}
