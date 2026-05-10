using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NetBannerNG.Utils
{
    internal static class NetworkHelper
    {
        // ReSharper disable once InconsistentNaming
        internal static string GetPhysicalIPAddress()
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!IsUp(networkInterface) || !IsPhysical(networkInterface))
                {
                    continue;
                }

                var ipProperties = networkInterface.GetIPProperties();
                if (!HasSpecifiedGateway(ipProperties))
                {
                    continue;
                }

                foreach (var unicastAddress in ipProperties.UnicastAddresses)
                {
                    if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return unicastAddress.Address.ToString();
                    }
                }
            }

            return string.Empty;
        }

        private static bool IsUp(NetworkInterface ni) => ni.OperationalStatus == OperationalStatus.Up;

        private static bool IsPhysical(NetworkInterface ni) =>
            // If it is wired or wireless it is not virtual
            ni.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet;

        private static bool HasSpecifiedGateway(IPInterfaceProperties properties)
        {
            foreach (var gatewayAddress in properties.GatewayAddresses)
            {
                if (gatewayAddress?.Address is { } address && !IPAddress.Any.Equals(address))
                {
                    return true;
                }
            }

            return false;
        }
    }
}