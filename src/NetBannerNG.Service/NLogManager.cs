using NLog;

namespace NetBannerNG.Service
{
    public class NLogManager : ILogManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public void LogDebug(string message)
        {
            Logger.Debug(message);
        }

        public void LogError(string message)
        {
            Logger.Log(new LogEventInfo(LogLevel.Error, Logger.Name, message));
        }

        public void LogInformation(string message)
        {
            Logger.Info(message);
        }

        public void LogWarning(string message)
        {
            Logger.Warn(message);
        }
    }
}