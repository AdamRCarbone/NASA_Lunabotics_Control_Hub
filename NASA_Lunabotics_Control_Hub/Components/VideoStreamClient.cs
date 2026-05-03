using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NASA_Lunabotics_Control_Hub.Components;

public class VideoStreamClient : IDisposable
{
    private UdpClient? _udp;
    private CancellationTokenSource _cts = new();

    private ushort _currentSeq;
    private bool _hasCurrentSeq;
    private readonly Dictionary<ushort, FrameBuffer> _pending = new();
    private readonly object _pendingLock = new();

    public event Action<byte[]>? FrameDecoded;

    private struct FrameBuffer
    {
        public byte[][] Chunks;
        public byte Total;
        public int Received;
    }

    public void Start()
    {
        lock (_pendingLock) { _pending.Clear(); }
        _hasCurrentSeq = false;
        _cts = new CancellationTokenSource();
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, 5002));
        Task.Run(ReceiveLoop, _cts.Token);
    }

    public void Stop()
    {
        _cts.Cancel();
        _udp?.Close();
        _udp?.Dispose();
        _udp = null;
        _hasCurrentSeq = false;
    }

    private async Task ReceiveLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (_udp == null) break;
                var result = await _udp.ReceiveAsync(_cts.Token);
                ProcessPacket(result.Buffer);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"[VideoStreamClient] Receive error: {ex.Message}");
            }
        }
    }

    private void ProcessPacket(byte[] data)
    {
        // UDP frame header: [magic][type][source_id][variant][seq_hi][seq_lo][chunk_idx][chunk_total][jpeg...]
        if (data.Length < 9) return;
        if (data[0] != 0x4F || data[1] != 0x56) return;

        ushort seq = (ushort)((data[4] << 8) | data[5]);
        byte chunkIdx = data[6];
        byte chunkTotal = data[7];
        int jpegLen = data.Length - 8;

        if (jpegLen <= 0 || chunkIdx >= chunkTotal || chunkTotal == 0) return;

        byte[]? jpeg = null;
        lock (_pendingLock)
        {
            // New seq while a different one is in-flight — discard the incomplete frame
            if (_hasCurrentSeq && seq != _currentSeq)
                _pending.Remove(_currentSeq);
            _currentSeq = seq;
            _hasCurrentSeq = true;

            if (!_pending.TryGetValue(seq, out var buf))
            {
                buf = new FrameBuffer
                {
                    Chunks = new byte[chunkTotal][],
                    Total = chunkTotal,
                    Received = 0
                };
            }

            if (buf.Chunks[chunkIdx] != null) return; // duplicate chunk, skip

            var chunk = new byte[jpegLen];
            Array.Copy(data, 8, chunk, 0, jpegLen);
            buf.Chunks[chunkIdx] = chunk;
            buf.Received++;
            _pending[seq] = buf;

            if (buf.Received < buf.Total) return;

            // Frame complete — concatenate and fire outside the lock
            _pending.Remove(seq);
            _hasCurrentSeq = false;

            int totalLen = 0;
            foreach (var c in buf.Chunks) totalLen += c.Length;
            jpeg = new byte[totalLen];
            int offset = 0;
            foreach (var c in buf.Chunks)
            {
                Array.Copy(c, 0, jpeg, offset, c.Length);
                offset += c.Length;
            }
        }
        FrameDecoded?.Invoke(jpeg);
    }

    public void Dispose() => Stop();
}
