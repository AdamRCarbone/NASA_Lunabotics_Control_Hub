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
        // WLAN API constants
        private const int WLAN_CLIENT_VERSION = 0x00000002;
        private const int ERROR_SUCCESS = 0;

        // WLAN interface info structure
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WLAN_INTERFACE_INFO
        {
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strInterfaceDescription;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WLAN_INTERFACE_INFO_LIST
        {
            public int dwNumberOfItems;
            public int dwIndex;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public WLAN_INTERFACE_INFO[] InterfaceInfo;
        }

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern int WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr pClientHandle);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern int WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern int WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

        /// <summary>
        /// Get the SSID for a wireless network interface by matching GUID
        /// </summary>
        public static string GetWiFiSSID(NetworkInterface nic)
        {
            var nic_guid = nic.Id; // NetworkInterface.Id contains the GUID string

            try
            {
                IntPtr clientHandle = IntPtr.Zero;
                IntPtr interfaceList = IntPtr.Zero;

                try
                {
                    uint negotiatedVersion;
                    int result = WlanOpenHandle(WLAN_CLIENT_VERSION, IntPtr.Zero, out negotiatedVersion, out clientHandle);
                    if (result != ERROR_SUCCESS)
                    {
                        Console.WriteLine($"[NetworkHelper] WlanOpenHandle failed: {result}");
                        return nic.Name;
                    }

                    result = WlanEnumInterfaces(clientHandle, IntPtr.Zero, out interfaceList);
                    if (result != ERROR_SUCCESS)
                    {
                        Console.WriteLine($"[NetworkHelper] WlanEnumInterfaces failed: {result}");
                        return nic.Name;
                    }

                    var list = Marshal.PtrToStructure<WLAN_INTERFACE_INFO_LIST>(interfaceList);
                    Console.WriteLine($"[NetworkHelper] Found {list.dwNumberOfItems} WLAN interfaces");

                    var infoPtr = interfaceList + Marshal.SizeOf<int>() * 2;
                    for (int i = 0; i < list.dwNumberOfItems; i++)
                    {
                        var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(infoPtr);
                        Console.WriteLine($"[NetworkHelper] WLAN Interface: {info.strInterfaceDescription} - Profile: {info.strProfileName}");

                        // Match by GUID
                        if (info.InterfaceGuid.ToString().Equals(nic_guid.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[NetworkHelper] GUID matched! SSID: {info.strProfileName}");
                            if (!string.IsNullOrWhiteSpace(info.strProfileName))
                                return info.strProfileName;
                        }

                        infoPtr += Marshal.SizeOf<WLAN_INTERFACE_INFO>();
                    }
                }
                finally
                {
                    if (interfaceList != IntPtr.Zero)
                        Marshal.FreeHGlobal(interfaceList);
                    if (clientHandle != IntPtr.Zero)
                        WlanCloseHandle(clientHandle, IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkHelper] WLAN API error: {ex.Message}");
            }

            // Fall back to adapter name
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
                    string ssid = GetWiFiSSID(nic);
                    displayName = $"WiFi: {ssid} ({ip})";
                    Console.WriteLine($"[NetworkHelper] WiFi {nic.Name} -> SSID: {ssid}");
                }
                else
                {
                    displayName = $"Ethernet: {nic.Name} ({ip})";
                    Console.WriteLine($"[NetworkHelper] Ethernet {nic.Name}");
                }

                interfaces.Add((displayName, nic.Name));
            }

            Console.WriteLine($"[NetworkHelper] Total interfaces found: {interfaces.Count}");
            return interfaces;
        }
    }
}
