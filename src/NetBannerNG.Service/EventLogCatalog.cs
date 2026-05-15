using System.Globalization;

namespace NetBannerNG.Service
{
    internal static class EventLogCatalog
    {
        public static readonly EventDefinition ServiceStartedService = new(1000, "NetBannerNG Service is started. Mode=Service");
        public static readonly EventDefinition ServiceStartedInteractive = new(1001, "NetBannerNG Service is started. Mode=Interactive");

        public static readonly EventDefinition ServiceThreadStarted = new(2000, "Service host thread started.");
        public static readonly EventDefinition ServiceAbortRequested = new(2001, "Service abort requested.");
        public static readonly EventDefinition NamedPipeServerCreated = new(2002, "Named pipe server created.");
        public static readonly EventDefinition NamedPipeServerInitialized = new(2003, "Named pipe server initialized.");
        public static readonly EventDefinition ChildRestartByWatchdog = new(2004, "Child process not found. Watchdog is restarting NetBannerNG.");
        public static readonly EventDefinition WatchdogStateTransition = new(2005, "Watchdog state transition {0} -> {1}. Reason={2}");
        public static readonly EventDefinition WatchdogBackoffScheduled = new(2006, "Watchdog backoff scheduled. Reason={0}, Failures={1}, DelaySeconds={2:F2}, TotalFailedLaunches={3}");
        public static readonly EventDefinition WatchdogHealthCounters = new(2007, "Watchdog health counters. ConnectionChurn={0}, FailedLaunches={1}, DeniedClients={2}, DeniedInbound={3}");
        public static readonly EventDefinition WatchdogLoopOverrun = new(2008, "Watchdog loop overrun detected. DurationMs={0:F2}");

        public static readonly EventDefinition PipeTimeoutCallback = new(3000, "{0} at {1}: execution timed out after {2} seconds, with: {3}.");
        public static readonly EventDefinition PipeTimeoutTaskCompleted = new(3001, "Task completed.");
        public static readonly EventDefinition PipeClientConnected = new(3002, "Client {0} is now connected!");
        public static readonly EventDefinition PipeSessionAdminQueryFailed = new(3003, "Failed to query active session admin token. Falling back to false. {0}");
        public static readonly EventDefinition PipeBootstrapDisconnected = new(3004, "Client disconnected before bootstrap message could be sent. {0}");
        public static readonly EventDefinition PipeBootstrapClientNotConnected = new(3005, "Client is not connected while sending bootstrap message. {0}");
        public static readonly EventDefinition PipeClientDisconnected = new(3006, "Client {0} disconnected");
        public static readonly EventDefinition PipeAutoRestartDisabled = new(3007, "Automatic child process restart on disconnect is disabled.");
        public static readonly EventDefinition PipeExceptionOccurred = new(3008, "Exception occurred in pipe: {0}");
        public static readonly EventDefinition PipeInboundRejected = new(3009, "Rejected invalid inbound pipe message.");
        public static readonly EventDefinition PipeClientForwardedLog = new(3010, "Event log from client {0}:{1}{2}");
        public static readonly EventDefinition PipeUnknownActionType = new(3011, "Unknown Action Type: {0}");
        public static readonly EventDefinition PipeClientAuthorizationAccepted = new(3012, "Accepted pipe client for session {0}. Pipe={1}");
        public static readonly EventDefinition PipeClientAuthorizationRejected = new(3013, "Rejected pipe client for session {0}. Pipe={1}");
        public static readonly EventDefinition PipeInboundRejectedUnauthorizedSession = new(3014, "Rejected inbound message from unauthorized/unbound client. ExpectedSession={0}, Pipe={1}");
        public static readonly EventDefinition PipeInboundIdentityRevalidationFailed = new(3015, "Rejected inbound message after identity revalidation failed. ExpectedSession={0}, Pipe={1}");
        public static readonly EventDefinition PipeInboundSessionRevalidationFailed = new(3016, "Rejected inbound message after session revalidation failed. ExpectedSession={0}, ActiveSession={1}, Pipe={2}");

        public static readonly EventDefinition ProcessStarting = new(4000, "Starting process: {0}");
        public static readonly EventDefinition ProcessStartedSuccessfully = new(4001, "Started process: {0}");
        public static readonly EventDefinition ProcessStartFailed = new(4002, "Failed to start process: {0}. Error: {1}");
        public static readonly EventDefinition ProcessFailedToKill = new(4003, "Failed to kill process PID={0}. Error: {1}");
        public static readonly EventDefinition SessionChangedReinitializingPipe = new(4004, "Interactive session changed from {0} to {1}; reinitializing per-session pipe server.");
        public static readonly EventDefinition ProcessIdentityValidationFailed = new(4005, "Process identity validation failed for PID={0}. ErrorType={1}");
        public static readonly EventDefinition ProcessCommandLineUnavailable = new(4006, "Command-line unavailable for tracked process PID={0}; relying on tracked launch metadata.");

        public static readonly EventDefinition UnhandledException = new(9000, "Unhandled exception captured. {0}");
    }

    internal readonly struct EventDefinition
    {
        public int EventId { get; }
        public string Template { get; }

        public EventDefinition(int eventId, string template)
        {
            EventId = eventId;
            Template = template;
        }

        public string Format(params object[] args) =>
            args == null || args.Length == 0
                ? Template
                : string.Format(CultureInfo.InvariantCulture, Template, args);
    }
}