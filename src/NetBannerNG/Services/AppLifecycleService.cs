using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NetBannerNG.Common.Native;
using NetBannerNG.Utils;

namespace NetBannerNG.Services
{
    internal sealed class AppLifecycleService
    {
        private static string TmpFilePath => Path.Combine(UserHelper.UserTempPath, "netbannerng-pipe.tmp");

        internal NamedPipeClient? Client { get; private set; }

        internal bool EnsureSingleInstance() => ProcessHelper.EnsureSingleInstance();

        internal static void TryLaunchDebugger(string[] args)
        {
            if (args.Length == 1 && args[0] == "--debug" && !Debugger.IsAttached)
            {
                _ = Debugger.Launch();
            }
        }

        internal async Task<bool> InitializePipeClientAsync()
        {
            Client = new NamedPipeClient();
            return await Client.InitializeAsync();
        }

        internal Task InitializeRuntimeAsync()
        {
            BorderManager.Init(IsClearStart());
            BorderManager.InitiateAllBorders();
            PinClearStart();
            WindowWatcher.Watch();
            MonitorWatcher.Watch(BorderManager.Refresh);
            ProcessHelper.Protect();
            return Task.CompletedTask;
        }

        internal async Task ShutdownRuntimeAsync()
        {
            BorderManager.CloseAllBorders();
            PinClearShutdown();
            WindowWatcher.Unwatch();
            MonitorWatcher.Unwatch();
            if (Client is not null)
            {
                await Client.DisposeAsync();
            }

            ProcessHelper.Unprotect();
        }

        private static bool IsClearStart() => !File.Exists(TmpFilePath);

        private static void PinClearShutdown()
        {
            if (!File.Exists(TmpFilePath))
            {
                return;
            }

            File.Delete(TmpFilePath);
        }

        private static void PinClearStart() => File.WriteAllText(TmpFilePath, "1");
    }
}