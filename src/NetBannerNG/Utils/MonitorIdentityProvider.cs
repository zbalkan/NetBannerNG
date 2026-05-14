using NetBannerNG.Services;
using System.Windows;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG.Utils
{
    internal sealed class MonitorIdentityProvider : IMonitorIdentity
    {
        public string BuildGroupId(Monitor monitor) => MonitorIdentity.BuildGroupId(monitor);

        public string BuildGroupId(string monitorName, Rect bounds) => MonitorIdentity.BuildGroupId(monitorName, bounds);
    }
}
