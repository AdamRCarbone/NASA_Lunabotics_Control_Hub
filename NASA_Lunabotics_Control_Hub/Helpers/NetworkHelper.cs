using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace NASA_Lunabotics_Control_Hub.Helpers
{
    /// <summary>
    /// Helper class to get network interface information including WiFi SSIDs
    /// Uses multiple methods to retrieve SSID reliably
    /// </summary>
    public static class NetworkHelper
    {
        /// <summary>
        /// Try to get SSID using netsh command (most reliable on Windows)
        /// </summary>
        private static string GetSSIDFromNetsh(string interfaceName)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"wlan show interfaces name=\"{interfaceName}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // Parse SSID from output
                    foreach (var line in output.Split('\n'))
                    {
                        if (line.Contains("SSID") || line.Contains("BSSID"))
                        {
                            // Extract the SSID value
                            var parts = line.Split(':');
                            if (parts.Length > 1)
                            {
                                string ssid = parts[1].Trim();
                                // Skip BSSID lines, we want SSID
                                if (!line.Contains("BSSID") && !string.IsNullOrEmpty(ssid))
                                {
                                    return ssid;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkHelper] netsh failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get a display-friendly name for the network interface
        /// Tries multiple methods to get the SSID for WiFi
        /// </summary>
        public static string GetInterfaceDisplayName(NetworkInterface nic)
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            {
                // Method 1: Try netsh (most reliable)
                string ssid = GetSSIDFromNetsh(nic.Name);
                if (!string.IsNullOrEmpty(ssid))
                {
                    Console.WriteLine($"[NetworkHelper] Found SSID via netsh: {ssid}");
                    return ssid;
                }

                // Method 2: Check if Name is already the SSID
                // On some systems, nic.Name contains the network name
                if (!nic.Name.Contains("Wireless") && !nic.Name.Contains("Adapter") &&
                    nic.Name.Length < 32 && !nic.Name.Contains("Connection"))
                {
                    Console.WriteLine($"[NetworkHelper] Using adapter name as SSID: {nic.Name}");
                    return nic.Name;
                }

                // Method 3: Fallback to generic WiFi name
                Console.WriteLine($"[NetworkHelper] Could not get SSID, using 'WiFi'");
                return "WiFi";
            }

            return nic.Name;
        }

        /// <summary>
        /// Get formatted network interface list with SSIDs for WiFi
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
                    string wifiName = GetInterfaceDisplayName(nic);
                    displayName = $"{wifiName} ({ip})";
                    Console.WriteLine($"[NetworkHelper] WiFi {nic.Name} -> Display: {wifiName}");
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
