using System.Windows;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG.Services
{
    internal interface IMonitorIdentity
    {
        string BuildGroupId(Monitor monitor);
        string BuildGroupId(string monitorName, Rect bounds);
    }
}
