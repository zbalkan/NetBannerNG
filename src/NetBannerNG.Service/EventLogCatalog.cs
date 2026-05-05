namespace NetBannerNG.Service
{
    public static class EventLogCatalog
    {
        public static readonly EventDefinition ServiceStartedService = new(1000, "NetBannerNG Service is started. Mode=Service");
        public static readonly EventDefinition ServiceStartedInteractive = new(1001, "NetBannerNG Service is started. Mode=Interactive");

        public static readonly EventDefinition ServiceThreadStarted = new(2000, "Service host thread started.");
        public static readonly EventDefinition ServiceAbortRequested = new(2001, "Service abort requested.");
        public static readonly EventDefinition NamedPipeServerCreated = new(2002, "Named pipe server created.");
        public static readonly EventDefinition NamedPipeServerInitialized = new(2003, "Named pipe server initialized.");
        public static readonly EventDefinition ChildRestartByWatchdog = new(2004, "Child process not found. Watchdog is restarting NetBannerNG.");

        public static readonly EventDefinition UnhandledException = new(9000, "Unhandled exception captured. {0}");
    }

    public readonly struct EventDefinition
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
                : string.Format(Template, args);
    }
}
