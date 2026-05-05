using NetBannerNG.Common.Extensions;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.ServiceProcess;

[assembly: CLSCompliant(true)]

namespace NetBannerNG.Service
{
    /// <summary>
    ///     Windows service application to provision the NetBannerNG.
    ///     The application is designed to work as both service and console application.
    /// </summary>
    /// <see href="https://erikengberg.com/named-pipes-in-net-6-with-tray-icon-and-service/"/>
    public static class Program
    {
        // TODO: Add crash reporting, e.g. https://github.com/getsentry/sentry-dotnet
        // TODO: Move business logic to service, Add Utils to Common first, then remove accordingly.
        public static EventLogManager Log { get; } = new();

        public static void Main(string[] args)
        {
            if (!AreValidArguments(args))
            {
                Console.WriteLine("Invalid startup arguments.");
                return;
            }

            AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) => CurrentDomain_FirstChanceException(sender, eventArgs);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            StartLogger();
            Debug.WriteLine("Starting Service...");

            if (!Environment.UserInteractive)
            {
                Log.LogInformation(EventLogCatalog.ServiceStartedService);
                using var serviceHost = new ServiceHost();
                ServiceBase.Run(serviceHost);
            }
            else
            {
                Log.LogInformation(EventLogCatalog.ServiceStartedInteractive);
                PrintConsoleHeader();
                ServiceHost.Run(args);
                Console.WriteLine("Interactive debug mode: service host is running. Press Enter to stop.");
                Console.ReadLine();
                ServiceHost.Abort();
            }
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

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "--debug"
            };

            return args.All(allowed.Contains);
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