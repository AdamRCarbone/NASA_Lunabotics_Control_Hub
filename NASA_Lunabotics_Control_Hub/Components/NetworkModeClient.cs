using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Handles TCP control/telemetry and owns the shared UDP socket on port 5002.
    /// Video and terrain reassemblers register via RegisterUdpHandler / UnregisterUdpHandler
    /// so that both streams can coexist on the same port.
    /// </summary>
    public class NetworkModeClient : IDisposable
    {
        // ── TCP ──────────────────────────────────────────────────────────────
        private TcpClient? _client;
        private NetworkStream? _stream;
        private CancellationTokenSource _cancelSource;
        private Thread? _receiveThread;

        private string _roverIpAddress = "octane.local";
        private const int TcpPort = 5000;

        public string CurrentState { get; private set; } = "UNKNOWN";
        public bool IsConnected { get; private set; } = false;
        public DateTime LastHeartbeat { get; private set; }

        public event Action<string>? StateChanged;
        public event Action<bool>?   ConnectionChanged;
        public event Action?         HeartbeatReceived;
        public event Action<float, float, float>? AccelReceived;
        public event Action<float, float, float>? PoseReceived;   // x, y, theta
        public event Action<byte, byte[], float[], float[]>? TagsReceived; // count, ids, dists, angles

        // ── UDP shared dispatcher ─────────────────────────────────────────────
        private UdpClient? _udp;
        private CancellationTokenSource _udpCts = new();
        private readonly List<Action<byte[]>> _udpHandlers = new();
        private readonly object _udpLock = new();

        /// <summary>Register a handler to receive raw UDP packets from port 5002.</summary>
        public void RegisterUdpHandler(Action<byte[]> handler)
        {
            lock (_udpLock) _udpHandlers.Add(handler);
        }

        /// <summary>Unregister a previously registered UDP handler.</summary>
        public void UnregisterUdpHandler(Action<byte[]> handler)
        {
            lock (_udpLock) _udpHandlers.Remove(handler);
        }

        private void StartUdp()
        {
            _udpCts = new CancellationTokenSource();
            _udp    = new UdpClient(new IPEndPoint(IPAddress.Any, 5002));
            Task.Run(UdpReceiveLoop, _udpCts.Token);
        }

        private void StopUdp()
        {
            _udpCts.Cancel();
            _udp?.Close();
            _udp?.Dispose();
            _udp = null;
        }

        private async Task UdpReceiveLoop()
        {
            var cts = _udpCts;
            var udp = _udp;
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    if (udp == null) break;
                    var result = await udp.ReceiveAsync(cts.Token);
                    DispatchUdpPacket(result.Buffer);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException)    { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NetworkModeClient] UDP receive error: {ex.Message}");
                }
            }
        }

        private void DispatchUdpPacket(byte[] data)
        {
            Action<byte[]>[] handlers;
            lock (_udpLock) handlers = [.. _udpHandlers];
            foreach (var h in handlers)
            {
                try { h(data); }
                catch (Exception ex) { Console.WriteLine($"[NetworkModeClient] UDP handler error: {ex.Message}"); }
            }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────
        public NetworkModeClient()
        {
            _cancelSource = new CancellationTokenSource();
            LastHeartbeat = DateTime.MinValue;
        }

        public async Task ConnectAsync(string? address = null)
        {
            try
            {
                if (address != null)
                    _roverIpAddress = address;

                if (_client != null && _client.Connected)
                    return;

                if (_cancelSource.IsCancellationRequested)
                {
                    _cancelSource.Dispose();
                    _cancelSource = new CancellationTokenSource();
                }

                _client = new TcpClient();
                await _client.ConnectAsync(_roverIpAddress, TcpPort);
                _stream = _client.GetStream();

                StartUdp();

                _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
                _receiveThread.Start();

                Console.WriteLine($"[NetworkModeClient] TCP link up to {_roverIpAddress}:{TcpPort} — waiting for heartbeat");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkModeClient] Connection failed: {ex.Message}");
                Disconnect();
            }
        }

        public void Disconnect()
        {
            _cancelSource.Cancel();
            _receiveThread?.Join(1000);

            StopUdp();

            _stream?.Dispose();
            _client?.Close();
            _client?.Dispose();
            _client        = null;
            _stream        = null;
            IsConnected    = false;
            LastHeartbeat  = DateTime.MinValue;

            ConnectionChanged?.Invoke(false);
            Console.WriteLine("[NetworkModeClient] Disconnected");
        }

        public bool IsHeartbeatTimeout(int timeoutSeconds = 6)
        {
            if (LastHeartbeat == DateTime.MinValue) return true;
            return (DateTime.UtcNow - LastHeartbeat).TotalSeconds > timeoutSeconds;
        }

        public void BumpHeartbeat() => LastHeartbeat = DateTime.UtcNow;

        // ── Send helpers ──────────────────────────────────────────────────────
        public async Task SendModeCommandAsync(string mode)
        {
            try
            {
                if (_client == null || !_client.Connected || _stream == null) return;

                char modeCode = mode.ToLower() switch
                {
                    "standby"    => '0',
                    "manual"     => '1',
                    "autonomous" => '2',
                    "fault reset"=> '3',
                    _ => '0'
                };

                byte[] frame = NetworkProtocol.EncodeCommand(modeCode, estop: false);
                await _stream.WriteAsync(frame, 0, frame.Length, _cancelSource.Token);
                await _stream.FlushAsync(_cancelSource.Token);
                Console.WriteLine($"[NetworkModeClient] Sent mode command: {mode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkModeClient] Error sending command: {ex.Message}");
                Disconnect();
            }
        }

        public async Task SendManipulatorCommandAsync(byte keyBitfield, ushort speedModifier = 100)
        {
            if (_client == null || !_client.Connected || _stream == null) return;
            try
            {
                var frame = NetworkProtocol.EncodeManipulator(keyBitfield, speedModifier);
                await _stream.WriteAsync(frame, 0, frame.Length, _cancelSource.Token);
                await _stream.FlushAsync(_cancelSource.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkModeClient] Manipulator send error: {ex.Message}");
            }
        }

        public async Task SendVideoRequestAsync(byte sourceId, byte variant, byte quality, byte fps)
        {
            if (_client == null || !_client.Connected || _stream == null) return;
            try
            {
                var frame = NetworkProtocol.EncodeVideoRequest(sourceId, variant, quality, fps);
                await _stream.WriteAsync(frame, 0, frame.Length, _cancelSource.Token);
                await _stream.FlushAsync(_cancelSource.Token);
                Console.WriteLine($"[NetworkModeClient] Video request: src={sourceId} var={variant:X2} q={quality} fps={fps}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkModeClient] Video request error: {ex.Message}");
            }
        }

        /// <summary>Request the rover to start streaming terrain data at 1 FPS.</summary>
        public Task SendTerrainRequestAsync()
            => SendVideoRequestAsync(NetworkProtocol.SOURCE_TERRAIN, NetworkProtocol.VARIANT_TERRAIN, 0x00, 0x01);

        // ── TCP receive loop ──────────────────────────────────────────────────
        private void ReceiveLoop()
        {
            byte[] buffer      = new byte[4096];
            byte[] frameBuffer = new byte[4096];
            int    framePos    = 0;

            while (!_cancelSource.Token.IsCancellationRequested && _stream != null)
            {
                try
                {
                    int read = _stream.Read(buffer, 0, buffer.Length);
                    if (read == 0) break;

                    Array.Copy(buffer, 0, frameBuffer, framePos, read);
                    framePos += read;

                    int offset = 0;
                    while (offset < framePos)
                    {
                        if (framePos - offset < 3) break;

                        byte payloadLen = frameBuffer[offset + 2];
                        int  expected   = 3 + payloadLen + 1;
                        if (framePos - offset < expected) break;

                        byte[] frame = new byte[expected];
                        Array.Copy(frameBuffer, offset, frame, 0, expected);

                        var msg = NetworkProtocol.DecodeFrame(frame);
                        if (msg != null) ProcessDecodedMessage(msg);

                        offset += expected;
                    }

                    if (offset > 0 && offset < framePos)
                    {
                        int remaining = framePos - offset;
                        Array.Copy(frameBuffer, offset, frameBuffer, 0, remaining);
                    }
                    framePos -= offset;
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

            if (IsConnected) Disconnect();
        }

        private void ProcessDecodedMessage(DecodedMessage msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case "heartbeat":
                        LastHeartbeat = DateTime.UtcNow;
                        if (!IsConnected)
                        {
                            IsConnected = true;
                            Console.WriteLine("[NetworkModeClient] First heartbeat — connection confirmed");
                            Dispatcher.UIThread.Post(() => ConnectionChanged?.Invoke(true));
                        }
                        Dispatcher.UIThread.Post(() => HeartbeatReceived?.Invoke());
                        break;

                    case "telemetry":
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
                            string prev = CurrentState;
                            CurrentState = state;
                            Console.WriteLine($"[NetworkModeClient] State: {state} (was {prev})");
                            Dispatcher.UIThread.Post(() => StateChanged?.Invoke(state));
                        }

                        if (msg.HasAccel)
                        {
                            float ax = msg.AccelX, ay = msg.AccelY, az = msg.AccelZ;
                            Dispatcher.UIThread.Post(() => AccelReceived?.Invoke(ax, ay, az));
                        }
                        if (msg.HasPose)
                        {
                            float px = msg.PoseX, py = msg.PoseY, pt = msg.PoseTheta;
                            Dispatcher.UIThread.Post(() => PoseReceived?.Invoke(px, py, pt));
                        }
                        if (msg.TagCount > 0)
                        {
                            byte   tc = msg.TagCount;
                            byte[]   ids    = msg.TagIds!;
                            float[]  dists  = msg.TagDists!;
                            float[]  angles = msg.TagAngles!;
                            Dispatcher.UIThread.Post(() => TagsReceived?.Invoke(tc, ids, dists, angles));
                        }
                        break;

                    case "ack":
                        Console.WriteLine($"[NetworkModeClient] ACK: success={msg.Success}");
                        break;

                    case "fault":
                        string sev = msg.Severity switch
                        {
                            (byte)'0' => "info",
                            (byte)'1' => "warning",
                            (byte)'2' => "critical",
                            _ => "unknown"
                        };
                        Console.WriteLine($"[NetworkModeClient] Fault: {msg.FaultChar} ({sev})");
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
    }
}
