using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using NASA_Lunabotics_Control_Hub.ViewModels;

namespace NASA_Lunabotics_Control_Hub.Components
{
    /// <summary>
    /// Handles mode state communication over TCP with the rover's octane_network package
    /// Sends mode commands and receives state updates using the lean binary protocol
    /// Also receives UDP heartbeats for connection health monitoring
    /// </summary>
    public class NetworkModeClient : IDisposable
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private CancellationTokenSource _cancelSource;
        private Thread? _receiveThread;
        private UdpClient? _udpClient;
        private Thread? _udpReceiveThread;

        // Configuration - match octane_network settings
        private string _roverIpAddress = "octane.local"; // Resolved via mDNS — works across network changes
        private int _tcpPort = 5000; // TCP port for mode commands
        private int _udpPort = 5001; // UDP port for heartbeat

        // Current state
        public string CurrentState { get; private set; } = "UNKNOWN";
        public bool IsConnected { get; private set; } = false;
        public DateTime LastHeartbeat { get; private set; }

        // Events
        public event Action<string>? StateChanged;
        public event Action<bool>? ConnectionChanged;
        public event Action? HeartbeatReceived; // Triggered when heartbeat arrives

        public NetworkModeClient()
        {
            _cancelSource = new CancellationTokenSource();
            LastHeartbeat = DateTime.MinValue;
            Console.WriteLine($"[NetworkModeClient] Initialized - Ready to connect to {_roverIpAddress}:{_tcpPort}");
        }

        /// <summary>
        /// Connect to the rover (TCP + UDP heartbeat listener)
        /// </summary>
        public async Task ConnectAsync(string? address = null)
        {
            try
            {
                if (address != null)
                    _roverIpAddress = address;

                if (_client != null && _client.Connected)
                {
                    Console.WriteLine("[NetworkModeClient] Already connected");
                    return;
                }

                // Start TCP connection
                _client = new TcpClient();
                await _client.ConnectAsync(_roverIpAddress, _tcpPort);
                _stream = _client.GetStream();

                _receiveThread = new Thread(ReceiveLoop);
                _receiveThread.IsBackground = true;
                _receiveThread.Start();

                // Start UDP heartbeat listener
                StartHeartbeatListener();

                IsConnected = true;
                LastHeartbeat = DateTime.UtcNow; // grace period — timeout counts from connect, not epoch
                ConnectionChanged?.Invoke(true);

                Console.WriteLine($"[NetworkModeClient] Connected to {_roverIpAddress}:{_tcpPort}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkModeClient] Connection failed: {ex.Message}");
                Disconnect();
            }
        }

        /// <summary>
        /// Start UDP listener for heartbeat packets
        /// </summary>
        private void StartHeartbeatListener()
        {
            try
            {
                _udpClient = new UdpClient(_udpPort);
                Console.WriteLine($"[NetworkModeClient] Heartbeat listener started on UDP port {_udpPort}");

                _udpReceiveThread = new Thread(UdpReceiveLoop);
                _udpReceiveThread.IsBackground = true;
                _udpReceiveThread.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkModeClient] Failed to start heartbeat listener: {ex.Message}");
            }
        }

        /// <summary>
        /// UDP receive loop - listens for heartbeat packets
        /// </summary>
        private void UdpReceiveLoop()
        {
            while (!_cancelSource.Token.IsCancellationRequested && _udpClient != null)
            {
                try
                {
                    // Receive UDP packet (blocking call)
                    var task = _udpClient.ReceiveAsync();
                    if (!task.Wait(100)) // 100ms timeout
                        continue;

                    var result = task.Result;
                    byte[] data = result.Buffer;

                    // Validate heartbeat frame: [MAGIC][STATE][SEQ_HI][SEQ_LO][CRC] = 5 bytes
                    if (data.Length == 5 && data[0] == 0x4F)
                    {
                        // Verify CRC
                        byte calculatedCrc = CalcCrc8(data, 4);
                        if (calculatedCrc == data[4])
                        {
                            // Valid heartbeat received
                            byte state = data[1];

                            LastHeartbeat = DateTime.UtcNow;

                            // Trigger heartbeat event on UI thread
                            Dispatcher.UIThread.Post(() => HeartbeatReceived?.Invoke());
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    break; // UDP client closed
                }
                catch (Exception ex) when (ex is TimeoutException || ex is AggregateException)
                {
                    // Timeout or other expected exception, continue loop
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NetworkModeClient] UDP receive error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Disconnect from the rover
        /// </summary>
        public void Disconnect()
        {
            _cancelSource.Cancel();

            _receiveThread?.Join(1000);
            _udpReceiveThread?.Join(1000);

            _stream?.Dispose();
            _client?.Close();
            _client?.Dispose();
            _udpClient?.Close();
            _udpClient?.Dispose();

            _client = null;
            _stream = null;
            _udpClient = null;

            IsConnected = false;
            LastHeartbeat = DateTime.MinValue;
            ConnectionChanged?.Invoke(false);

            Console.WriteLine("[NetworkModeClient] Disconnected");
        }

        /// <summary>
        /// Check if heartbeat timeout has occurred (call from timer)
        /// </summary>
        public bool IsHeartbeatTimeout(int timeoutSeconds = 6)
        {
            if (LastHeartbeat == DateTime.MinValue)
                return true;

            return (DateTime.UtcNow - LastHeartbeat).TotalSeconds > timeoutSeconds;
        }

        /// <summary>
        /// Send mode command to rover using binary protocol
        /// </summary>
        public async Task SendModeCommandAsync(string mode)
        {
            try
            {
                if (_client == null || !_client.Connected || _stream == null)
                {
                    Console.WriteLine("[NetworkModeClient] Not connected - cannot send command");
                    return;
                }

                // Map GUI mode to binary mode code
                char modeCode = mode.ToLower() switch
                {
                    "standby" => '0',
                    "manual" => '1',
                    "autonomous" => '2',
                    "fault reset" => '3',
                    _ => '0'
                };

                // Encode command using binary protocol
                byte[] frame = NetworkProtocol.EncodeCommand(modeCode, estop: false);

                await _stream.WriteAsync(frame, 0, frame.Length, _cancelSource.Token);
                await _stream.FlushAsync(_cancelSource.Token);

                Console.WriteLine($"[NetworkModeClient] Sent mode command: {mode} (code: {modeCode})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkModeClient] Error sending command: {ex.Message}");
                Disconnect();
            }
        }

        public async Task SendManipulatorCommandAsync(byte keyBitfield)
        {
            if (_client == null || !_client.Connected || _stream == null)
                return;
            try
            {
                var frame = NetworkProtocol.EncodeManipulator(keyBitfield);
                await _stream.WriteAsync(frame, 0, frame.Length, _cancelSource.Token);
                await _stream.FlushAsync(_cancelSource.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkModeClient] Manipulator send error: {ex.Message}");
            }
        }

        private void ReceiveLoop()
        {
            byte[] buffer = new byte[4096];
            byte[] frameBuffer = new byte[4096]; // Buffer for accumulating frames
            int frameBufferPos = 0;

            while (!_cancelSource.Token.IsCancellationRequested && _stream != null)
            {
                try
                {
                    int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        // Connection closed
                        break;
                    }

                    // Copy to frame buffer
                    Array.Copy(buffer, 0, frameBuffer, frameBufferPos, bytesRead);
                    frameBufferPos += bytesRead;

                    // Process complete frames
                    int offset = 0;
                    while (offset < frameBufferPos)
                    {
                        // Need at least 3 bytes for header
                        if (frameBufferPos - offset < 3)
                            break;

                        // Parse header to get frame length
                        byte magic = frameBuffer[offset];
                        byte msgType = frameBuffer[offset + 1];
                        byte payloadLen = frameBuffer[offset + 2];

                        // Calculate total frame size
                        int expectedLen = 3 + payloadLen + 1; // header + payload + crc

                        // Check if we have complete frame
                        if (frameBufferPos - offset < expectedLen)
                            break;

                        // Extract frame and decode
                        byte[] frame = new byte[expectedLen];
                        Array.Copy(frameBuffer, offset, frame, 0, expectedLen);

                        var msg = NetworkProtocol.DecodeFrame(frame);
                        if (msg != null)
                        {
                            ProcessDecodedMessage(msg);
                        }

                        offset += expectedLen;
                    }

                    // Move remaining bytes to start of buffer
                    if (offset > 0 && offset < frameBufferPos)
                    {
                        int remaining = frameBufferPos - offset;
                        Array.Copy(frameBuffer, offset, frameBuffer, 0, remaining);
                    }
                    frameBufferPos -= offset;
                }
                catch (Exception ex) when (ex is ObjectDisposedException || ex is OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NetworkModeClient] Receive error: {ex.Message}");
                    break;
                }
            }

            if (IsConnected)
            {
                Disconnect();
            }
        }

        private void ProcessDecodedMessage(DecodedMessage msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case "telemetry":
                        // Map state code to string
                        string state = msg.State switch
                        {
                            (byte)'0' => "STANDBY",
                            (byte)'1' => "MANUAL",
                            (byte)'2' => "AUTONOMOUS",
                            (byte)'3' => "FAULT",
                            _ => "UNKNOWN"
                        };

                        if (state != CurrentState)
                        {
                            string previousState = CurrentState;
                            CurrentState = state;

                            Console.WriteLine($"[NetworkModeClient] Received state update: {state} (was {previousState})");

                            // Raise event on UI thread
                            Dispatcher.UIThread.Post(() => StateChanged?.Invoke(state));
                        }
                        break;

                    case "ack":
                        Console.WriteLine($"[NetworkModeClient] Received ACK: success={msg.Success}");
                        break;

                    case "fault":
                        char faultChar = msg.FaultChar;
                        string severity = msg.Severity switch
                        {
                            (byte)'0' => "info",
                            (byte)'1' => "warning",
                            (byte)'2' => "critical",
                            _ => "unknown"
                        };
                        Console.WriteLine($"[NetworkModeClient] Received fault alert: {faultChar} ({severity})");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkModeClient] Error processing message: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _cancelSource.Cancel();
            Disconnect();
            _cancelSource.Dispose();
        }

        /// <summary>
        /// Calculate CRC-8 over first N bytes of data
        /// </summary>
        private static byte CalcCrc8(byte[] data, int length)
        {
            byte crc = 0;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x80) != 0)
                        crc = (byte)((crc << 1) ^ 0x07);
                    else
                        crc = (byte)(crc << 1);
                }
            }
            return crc;
        }
    }
}
