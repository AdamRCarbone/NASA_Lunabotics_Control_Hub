using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using NASA_Lunabotics_Control_Hub.ViewModels;

namespace NASA_Lunabotics_Control_Hub.Components
{
    public class UDP_Client
    {
        private JoystickViewModel _model;

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

        public async Task SendIntegerAsync(string ipAddress, int port, int value)
        {
            string message = value.ToString();
            await SendDataAsync(ipAddress, port, message);
        }

        private string IP_Address = "192.168.0.150";
        private int Port = 8888;

        public async Task SendPacketsForTesting()
        {
            try
            {
                if (_model == null)
                {
                    Console.WriteLine("Error: JoystickViewModel is not set.");
                    return;
                }
                await SendDataAsync(IP_Address, Port, _model.SendString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendPacketsForTesting: {ex.Message}");
            }
        }
    }
}