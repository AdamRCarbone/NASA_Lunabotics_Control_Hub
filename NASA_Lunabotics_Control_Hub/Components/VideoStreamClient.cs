using System;
using System.Collections.Generic;

namespace NASA_Lunabotics_Control_Hub.Components;

// Pure reassembler — no socket ownership. Feed packets via ProcessPacket().
// NetworkModeClient owns the UDP socket and routes packets here.
public class VideoStreamClient : IDisposable
{
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
    }

    public void Stop()
    {
        lock (_pendingLock) { _pending.Clear(); }
        _hasCurrentSeq = false;
    }

    // Called by the shared UDP dispatcher for every packet that arrives on port 5002.
    // Skips terrain packets (source_id=7) — those are handled by TerrainStreamClient.
    public void ProcessPacket(byte[] data)
    {
        if (data.Length < 9) return;
        if (data[0] != 0x4F || data[1] != 0x56) return;
        if (data[2] == 0x07) return; // terrain source — not ours

        ushort seq       = (ushort)((data[4] << 8) | data[5]);
        byte   chunkIdx  = data[6];
        byte   chunkTotal = data[7];
        int    payloadLen = data.Length - 8;

        if (payloadLen <= 0 || chunkIdx >= chunkTotal || chunkTotal == 0) return;

        byte[]? assembled = null;
        lock (_pendingLock)
        {
            if (_hasCurrentSeq && seq != _currentSeq)
                _pending.Remove(_currentSeq);
            _currentSeq    = seq;
            _hasCurrentSeq = true;

            if (!_pending.TryGetValue(seq, out var buf))
            {
                buf = new FrameBuffer
                {
                    Chunks   = new byte[chunkTotal][],
                    Total    = chunkTotal,
                    Received = 0
                };
            }

            if (buf.Chunks[chunkIdx] != null) return;

            var chunk = new byte[payloadLen];
            Array.Copy(data, 8, chunk, 0, payloadLen);
            buf.Chunks[chunkIdx] = chunk;
            buf.Received++;
            _pending[seq] = buf;

            if (buf.Received < buf.Total) return;

            _pending.Remove(seq);
            _hasCurrentSeq = false;

            int total = 0;
            foreach (var c in buf.Chunks) total += c.Length;
            assembled = new byte[total];
            int off = 0;
            foreach (var c in buf.Chunks) { Array.Copy(c, 0, assembled, off, c.Length); off += c.Length; }
        }
        FrameDecoded?.Invoke(assembled);
    }

    public void Dispose() => Stop();
}
