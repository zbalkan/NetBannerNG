using System.Windows;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG.Utils
{
    internal static class MonitorIdentity
    {
        internal static string BuildGroupId(Monitor monitor) => BuildGroupId(monitor.Name, monitor.Bounds);

        internal static string BuildGroupId(string monitorName, Rect bounds)
        {
            if (!string.IsNullOrWhiteSpace(monitorName))
            {
                return monitorName;
            }

            return $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}";
        }
    }
}
