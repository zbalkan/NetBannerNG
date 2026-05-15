using NetBannerNG.Borders;
using NetBannerNG.Services;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG.Utils
{
    internal sealed class MonitorLayoutPolicyProvider : IMonitorLayoutPolicy
    {
        public void ApplyMonitorBounds(BorderBase window, Monitor monitor) => MonitorLayoutPolicy.ApplyMonitorBounds(window, monitor);

        public double GetVerticalTop(Monitor monitor) => MonitorLayoutPolicy.GetVerticalTop(monitor);

        public double GetVerticalHeight(Monitor monitor) => MonitorLayoutPolicy.GetVerticalHeight(monitor);
    }
}