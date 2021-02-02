using System;
using System.Threading;
using System.Windows.Forms;

namespace NetBannerNG
{

    public static class Program
    {
        private static Banner banner;
        private static Dispatcher dispatcher;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var mutex = new Mutex(true, "NetBannerNG", out bool result);

            if (!result) return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            dispatcher = new Dispatcher();
            banner = dispatcher.DrawBanner();

            Application.ApplicationExit += new EventHandler(Application_ApplicationExit);
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(MyHandler);

            Application.Run(banner);

            GC.KeepAlive(mutex);                // mutex shouldn't be released - important line
        }

        private static void MyHandler(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show(((Exception)e.ExceptionObject).Message, "An Unhandled Error Occured.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Run(Banner.Error());
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
