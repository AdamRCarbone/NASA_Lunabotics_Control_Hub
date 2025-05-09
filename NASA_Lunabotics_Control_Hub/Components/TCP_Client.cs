using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NASA_Lunabotics_Control_Hub.Components
{
    internal class TCP_Client
    {
        public async Task SendDataAsync(string ipAddress, int port, string message)
        {
            try
            {
                using TcpClient client = new TcpClient();
                await client.ConnectAsync(ipAddress, port);

                //Get stream
                NetworkStream stream = client.GetStream();

                //Covert (int to string, serializing), string to bytes
                byte[] data = Encoding.UTF8.GetBytes(message);

                //Send data
                await stream.WriteAsync(data, 0, data.Length);

                //Receive response
                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Received: {response}");

                stream.Close();
                client.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        // Example for sending an integer
        public async Task SendIntegerAsync(string ipAddress, int port, int value)
        {
            // Convert integer to string or bytes (choose one)
            string message = value.ToString();
            await SendDataAsync(ipAddress, port, message);
            // Alternatively, send as bytes:
            // byte[] data = BitConverter.GetBytes(value);
            // (Ensure robot parses it correctly)
        }
    }
}
