using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NetBannerNG.Utils
{
    internal static class NetworkHelper
    {
        // ReSharper disable once InconsistentNaming
        internal static string GetPhysicalIPAddress()
        {
            var result = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => IsUp(ni) && IsPhysical(ni))
                .FirstOrDefault(ni => IsSpecified(ni.GetIPProperties().GatewayAddresses.FirstOrDefault()))?
                .GetIPProperties()
                .UnicastAddresses
                .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)
                ?.Address
                .ToString();

            return result ?? string.Empty;
        }

        private static bool IsUp(NetworkInterface ni)
        {
            return ni.OperationalStatus == OperationalStatus.Up;
        }

        private static bool IsPhysical(NetworkInterface ni)
        {
            // If it is wired or wireleess it is not virtual
            return ni.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet;
        }

        private static bool IsSpecified(GatewayIPAddressInformation addr)
        {
            return addr?.Address.ToString().Equals("0.0.0.0", StringComparison.Ordinal) == false;
        }
    }
}