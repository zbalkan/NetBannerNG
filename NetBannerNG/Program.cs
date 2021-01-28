using System;
using System.Drawing;
using System.Windows.Forms;

namespace NetBannerNG
{

    public static class Program
    {
        private static Banner banner;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var mutex = new System.Threading.Mutex(true, "UniqueAppId", out bool result);

            if (!result) return;

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
                banner = new Banner(new ClassificationMark()
                {
                    ClassificationName = "Classification not configured",
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black
                });
            }

            Application.ApplicationExit += new EventHandler(Application_ApplicationExit);
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);

            Application.Run(banner);

            GC.KeepAlive(mutex);                // mutex shouldn't be released - important line
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            AppBarHelper.SetAppBar(banner, AppBarEdge.None);
        }

        private static void Application_ApplicationExit(object sender, EventArgs e)
        {
            AppBarHelper.SetAppBar(banner, AppBarEdge.None);
        }
    }
}
