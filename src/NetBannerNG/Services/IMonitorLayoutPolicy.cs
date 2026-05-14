using NetBannerNG.Borders;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG.Services
{
    internal interface IMonitorLayoutPolicy
    {
        void ApplyMonitorBounds(BorderBase window, Monitor monitor);
        double GetVerticalTop(Monitor monitor);
        double GetVerticalHeight(Monitor monitor);
    }
}
