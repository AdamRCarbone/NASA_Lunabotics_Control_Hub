using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Threading;
using NASA_Lunabotics_Control_Hub.ViewModels;

namespace NASA_Lunabotics_Control_Hub.Components
{
    public class UDP_Client
    {
        private JoystickViewModel _model;
        private const string IP_Address = "192.168.0.69";
        private const int Port = 5006;

        public void SetModel(JoystickViewModel model)
        {
            _model = model;
        }

        public async Task CallMethodFromUIThread(Action action)
        {
            await Dispatcher.UIThread.InvokeAsync(action);
        }

        public async Task SendDataAsync(string ipAddress, int port, string message)
        {
            try
            {
                using UdpClient udpClient = new UdpClient();
                udpClient.EnableBroadcast = true;
                byte[] data = Encoding.UTF8.GetBytes(message);
                await udpClient.SendAsync(data, data.Length, ipAddress, port);
                Console.WriteLine($"Sent: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        public async Task SendByteStringAsync(string byteString)
        {
            try
            {
                var ipAddress = IP_Address;
                var port = Port;

                using UdpClient udpClient = new UdpClient();
                udpClient.EnableBroadcast = true;

                byte[] packet1 = BitConverter.GetBytes(int.Parse(byteString.Substring(0, 3)));
                byte[] packet2 = BitConverter.GetBytes(int.Parse(byteString.Substring(3, 3)));
                byte[] packet3 = Encoding.ASCII.GetBytes(byteString.Substring(6, 1));

                byte[] fullPacket = new byte[3]
                {
                    packet1[0],
                    packet2[0],
                    packet3[0]
                };

                await udpClient.SendAsync(fullPacket, fullPacket.Length, ipAddress, port);

                Console.WriteLine($"Sent 3-byte packet: [{BitConverter.ToString(fullPacket)}]");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendByteStringAsync: {ex.Message}");
            }
        }

        public static string ConvertJoystickString(string input, int bucketControlState)
        {
            MatchCollection matches = Regex.Matches(input, @"[+-]\d{2}");

            if (matches.Count != 4)
                throw new ArgumentException("Input must contain exactly 4 signed two-digit values like '+00-99+00+99'.");

            int[] values = new int[4];
            for (int i = 0; i < 4; i++)
            {
                values[i] = int.Parse(matches[i].Value);
            }

            int mappedLeftY = MapToRange(values[1], -99, 99, 0, 124);
            int mappedRightY = MapToRange(values[3], -99, 99, 0, 124);

            return $"{mappedLeftY:D3}{mappedRightY:D3}{bucketControlState}";
        }

        private static int MapToRange(int value, int fromMin, int fromMax, int toMin, int toMax)
        {
            value = Math.Clamp(value, fromMin, fromMax);
            return (int)Math.Round((double)(value - fromMin) / (fromMax - fromMin) * (toMax - toMin) + toMin);
        }
    }
}