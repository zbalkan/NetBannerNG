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
            AppDomain.CurrentDomain.AssemblyLoad += new AssemblyLoadEventHandler(CurrentDomain_AssemblyLoad);
            Application.ApplicationExit += new EventHandler(Application_ApplicationExit);
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(GlobalExceptionHandler);

            dispatcher = new Dispatcher();
            banner = dispatcher.DrawBanner();
            Application.Run(banner);

            GC.KeepAlive(mutex);                // mutex shouldn't be released - important line
        }

        private static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs e)
        {
            Logger.LogInformation("NetBannerNG started.");
        }

        private static void GlobalExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.LogError(((Exception)e.ExceptionObject).Message + Environment.NewLine + ((Exception)e.ExceptionObject).InnerException.Message ?? string.Empty);
            Application.Run(Banner.Error());
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            AppBarHelper.SetAppBar(banner, AppBarEdge.None);
            Logger.LogInformation("NetBannerNG exited successfully.");
        }

        private static void Application_ApplicationExit(object sender, EventArgs e)
        {
            AppBarHelper.SetAppBar(banner, AppBarEdge.None);
            Logger.LogInformation("NetBannerNG exited successfully.");
        }
    }
}
