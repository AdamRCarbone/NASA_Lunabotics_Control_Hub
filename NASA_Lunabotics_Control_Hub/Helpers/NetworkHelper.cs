using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace NASA_Lunabotics_Control_Hub.Helpers
{
    /// <summary>
    /// Helper class to get network interface information including WiFi SSIDs
    /// </summary>
    public static class NetworkHelper
    {
        // Use NetworkInterface.Name directly - it often contains the SSID on Windows
        // or fall back to the friendly name

        /// <summary>
        /// Get a display-friendly name for the network interface
        /// For WiFi, try to extract or find the SSID
        /// </summary>
        public static string GetInterfaceDisplayName(NetworkInterface nic)
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            {
                // On Windows, the Name property often IS the SSID for connected WiFi
                // or contains helpful info like "Wi-Fi"
                string name = nic.Name;

                // If the name looks like an adapter name (contains "Wireless", "Adapter", etc),
                // try to get more info
                if (name.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Adapter", StringComparison.OrdinalIgnoreCase))
                {
                    // Use the OperationalStatus description or fallback to a generic name
                    // The SSID info is typically not easily accessible without admin privileges
                    return name;
                }

                // Otherwise, the name might already be the SSID (e.g., "MyWiFiNetwork")
                return name;
            }

            return nic.Name;
        }

        /// <summary>
        /// Get formatted network interface list
        /// </summary>
        public static List<(string DisplayName, string InterfaceName)> GetNetworkInterfaces()
        {
            var interfaces = new List<(string, string)>();

            Console.WriteLine("[NetworkHelper] Enumerating network interfaces...");

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                Console.WriteLine($"[NetworkHelper] Checking: {nic.Name} - Type: {nic.NetworkInterfaceType} - Status: {nic.OperationalStatus}");

                if (nic.OperationalStatus != OperationalStatus.Up)
                {
                    Console.WriteLine($"[NetworkHelper] Skipping {nic.Name} - not up");
                    continue;
                }

                if (nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                {
                    Console.WriteLine($"[NetworkHelper] Skipping {nic.Name} - wrong type");
                    continue;
                }

                var ipProps = nic.GetIPProperties();
                var unicast = ipProps.UnicastAddresses
                    .FirstOrDefault(u => u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                string ip = unicast != null ? unicast.Address.ToString() : "0.0.0.0";

                string displayName;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                {
                    string nicName = GetInterfaceDisplayName(nic);
                    displayName = $"{nicName} ({ip})";
                    Console.WriteLine($"[NetworkHelper] WiFi {nic.Name} -> Display: {nicName}");
                }
                else
                {
                    displayName = $"{nic.Name} ({ip})";
                    Console.WriteLine($"[NetworkHelper] Ethernet {nic.Name}");
                }

                interfaces.Add((displayName, nic.Name));
            }

            Console.WriteLine($"[NetworkHelper] Total interfaces found: {interfaces.Count}");
            return interfaces;
        }
    }
}
