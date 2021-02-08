using System.Diagnostics;

namespace NetBannerNG
{
    public static class Logger
    {
        public static void LogInformation(string message)
        {
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = "Application";
                eventLog.WriteEntry(message, EventLogEntryType.Information, 101, 1);
            }
        }

        public static void LogError(string message)
        {
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = "Application";
                eventLog.WriteEntry(message, EventLogEntryType.Error, 105, 1);
            }
        }
    }
}
