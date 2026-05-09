using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using NetBannerNG.Common.Extensions;

[assembly: CLSCompliant(true)]

namespace NetBannerNG.Service
{
    /// <summary>
    ///     Windows service application to provision the NetBannerNG.
    ///     The application runs as a Windows service; interactive hosting is available only in Debug builds.
    /// </summary>
    /// <see href="https://erikengberg.com/named-pipes-in-net-6-with-tray-icon-and-service/"/>
    public static class Program
    {
        public static EventLogManager Log { get; } = new();

        public static void Main(string[] args)
        {
            if (!AreValidArguments(args))
            {
                Console.WriteLine("Invalid startup arguments.");
                return;
            }

#if DEBUG
            AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) => CurrentDomain_FirstChanceException(sender, eventArgs);
#endif
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            StartLogger();
            Debug.WriteLine("Starting Service...");

            if (!Environment.UserInteractive)
            {
                Log.LogInformation(EventLogCatalog.ServiceStartedService);
                using var serviceHost = new ServiceHost();
                ServiceBase.Run(serviceHost);
                return;
            }

#if DEBUG
            if (!args.Contains("--debug", StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine("Interactive mode is only available with --debug.");
                return;
            }

            Log.LogInformation(EventLogCatalog.ServiceStartedInteractive);
            PrintConsoleHeader();
            ServiceHost.Run(args);
            Console.WriteLine("Interactive debug mode: service host is running. Press Enter to stop.");
            Console.ReadLine();
            ServiceHost.Abort();
#else
            Console.WriteLine("Interactive mode is unavailable in Release builds. Run as a Windows service.");
#endif
        }

        private static void CurrentDomain_FirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {
            if (e != null && e.Exception != null)
            {
                if (IsExpectedTransient(e.Exception))
                {
                    return;
                }
                Dump(e.Exception);
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e != null && e.ExceptionObject != null)
            {
                Dump((Exception)e.ExceptionObject);
            }
        }

        private static bool IsExpectedTransient(Exception exception) =>
            exception is OperationCanceledException ||
            (exception is COMException comEx && comEx.HResult == unchecked((int)0x80005000)) ||
            (exception is IOException ioEx && (ioEx.Message.Contains("Pipe is broken") ||
                                              ioEx.Message.Contains("Integrity of the message is broken")));

        private static bool AreValidArguments(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return true;
            }

#if DEBUG
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "--debug"
            };

            return args.All(allowed.Contains);
#else
            return false;
#endif
        }

        private static void Dump(Exception e)
        {
            var messageStack = e.GetMessageStack();
            Log.LogError(EventLogCatalog.UnhandledException, messageStack);
            var path = Path.Combine(Path.GetTempPath(), $"netbannerng-svcdump-{Guid.NewGuid()}");
            File.WriteAllText(path, messageStack);
            if (Environment.UserInteractive)
            {
                Console.WriteLine($"Dump file is saved to path: {path}");
                Console.WriteLine(messageStack);
            }
        }

        private static void PrintConsoleHeader()
        {
            Console.WriteLine($"Hostname: {Environment.MachineName}");
            Console.WriteLine($"Domain: {Environment.UserDomainName}");
            Console.WriteLine("User interactive mode");
        }

        private static void StartLogger() => EventLogManager.Initialize();
    }
}