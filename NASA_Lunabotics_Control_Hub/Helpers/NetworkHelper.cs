using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace NASA_Lunabotics_Control_Hub.Helpers
{
    public static class NetworkHelper
    {
        // SSID lookup has no cross-platform BCL API, so it shells out to the
        // per-OS network CLI. Dispatch at runtime so a single build serves both
        // Windows and macOS; unsupported platforms fall through to null.
        private static string? GetSSID(string interfaceName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetSSIDFromNetsh(interfaceName);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return GetSSIDFromNetworkSetup(interfaceName);
            return null;
        }

        private static string? RunCommand(string fileName, string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return null;

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkHelper] {fileName} failed: {ex.Message}");
                return null;
            }
        }

        private static string? GetSSIDFromNetsh(string interfaceName)
        {
            string? output = RunCommand("netsh", "wlan show interfaces");
            if (output == null) return null;

            // Find the block for this interface, then extract its SSID
            bool inBlock = false;
            foreach (var line in output.Split('\n'))
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("Name") && trimmed.Contains(":"))
                {
                    string blockName = trimmed.Split(new char[] { ':' }, 2)[1].Trim();
                    inBlock = blockName.Equals(interfaceName, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inBlock) continue;

                // Match "SSID" but not "BSSID"
                if (trimmed.StartsWith("SSID") && !trimmed.StartsWith("BSSID") && trimmed.Contains(":"))
                {
                    string ssid = trimmed.Split(new char[] { ':' }, 2)[1].Trim();
                    if (!string.IsNullOrEmpty(ssid))
                        return ssid;
                    break;
                }
            }
            return null;
        }

        // macOS: `airport -I` was removed in recent releases, so query the named
        // Wi-Fi device directly. Output is "Current Wi-Fi Network: <ssid>", or a
        // "not associated"/permission message that yields no usable SSID.
        private static string? GetSSIDFromNetworkSetup(string interfaceName)
        {
            string? output = RunCommand("/usr/sbin/networksetup", $"-getairportnetwork {interfaceName}");
            if (output == null) return null;

            int idx = output.IndexOf(':');
            if (idx < 0) return null;

            string ssid = output.Substring(idx + 1).Trim();
            if (string.IsNullOrEmpty(ssid) || ssid.Contains("not associated", StringComparison.OrdinalIgnoreCase))
                return null;

            return ssid;
        }

        public static string GetInterfaceDisplayName(NetworkInterface nic)
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            {
                string? ssid = GetSSID(nic.Name);
                if (!string.IsNullOrEmpty(ssid))
                    return ssid;

                // Fallback: hardware description is more useful than "WiFi"
                return nic.Description;
            }
            return nic.Name;
        }

        public static List<(string DisplayName, string InterfaceName)> GetNetworkInterfaces()
        {
            var interfaces = new List<(string, string)>();

            // Find the adapter that has a default gateway — that's the primary one
            var gatewayAdapters = new HashSet<string>(
                NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                n.GetIPProperties().GatewayAddresses.Count > 0)
                    .Select(n => n.Name));

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                    continue;

                var ipProps = nic.GetIPProperties();
                var unicast = ipProps.UnicastAddresses
                    .FirstOrDefault(u => u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                string ip = unicast?.Address.ToString() ?? "0.0.0.0";
                string displayName = $"{GetInterfaceDisplayName(nic)} ({ip})";

                // Primary adapter goes first in the list
                if (gatewayAdapters.Contains(nic.Name))
                    interfaces.Insert(0, (displayName, nic.Name));
                else
                    interfaces.Add((displayName, nic.Name));
            }

            Console.WriteLine($"[NetworkHelper] Found {interfaces.Count} interface(s)");
            return interfaces;
        }

        /// <summary>Returns the interface name of the primary adapter (has default gateway).</summary>
        public static string? GetPrimaryInterfaceName()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            (n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                             n.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                            n.GetIPProperties().GatewayAddresses.Count > 0)
                .Select(n => n.Name)
                .FirstOrDefault();
        }
    }
}
