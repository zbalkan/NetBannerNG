using NetBannerNG.Common.Native;
using NetBannerNG.Utils;
using System.Diagnostics;
using System.IO;

namespace NetBannerNG.Services
{
    internal sealed class AppLifecycleService
    {
        private static readonly string TmpFilePath = Path.Combine(UserHelper.UserTempPath, "netbannerng-pipe.tmp");

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

        internal async Task InitializeRuntimeAsync()
        {
            BorderManager.Init(await IsClearStartAsync());
            BorderManager.InitiateAllBorders();
            await PinClearStartAsync();
            WindowWatcher.Watch();
            MonitorWatcher.Watch();
            MonitorWatcher.SetTrigger(BorderManager.Refresh);
            ProcessHelper.Protect();
        }

        internal async Task ShutdownRuntimeAsync()
        {
            BorderManager.CloseAllBorders();
            await PinClearShutdownAsync();
            WindowWatcher.Unwatch();
            MonitorWatcher.Unwatch();
            if (Client is not null)
            {
                await Client.DisposeAsync();
            }

            ProcessHelper.Unprotect();
        }

        private static async Task<bool> IsClearStartAsync() => !await Task.Run(() => File.Exists(TmpFilePath));

        private static async Task PinClearShutdownAsync()
        {
            if (!await Task.Run(() => File.Exists(TmpFilePath)))
            {
                return;
            }

            File.Delete(TmpFilePath);
        }

        private static Task PinClearStartAsync() => Task.Run(() => File.WriteAllText(TmpFilePath, "1"));
    }
}
