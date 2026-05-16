using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace NASA_Lunabotics_Control_Hub.Components;

// Reassembles terrain UDP frames (source_id=0x07, variant=0x54='T'),
// zlib-decompresses them, and parses the 200×200 heightmap payload.
// Feed raw UDP packets via ProcessPacket(); NetworkModeClient owns the socket.
public sealed class TerrainStreamClient : IDisposable
{
    private ushort _currentSeq;
    private bool _hasCurrentSeq;
    private readonly Dictionary<ushort, FrameBuffer> _pending = new();
    private readonly object _pendingLock = new();

    public event Action<TerrainData>? TerrainReceived;

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

    // Called by the shared UDP dispatcher on a background thread.
    public void ProcessPacket(byte[] data)
    {
        if (data.Length < 9) return;
        if (data[0] != 0x4F || data[1] != 0x56) return;
        if (data[2] != 0x07 || data[3] != 0x54) return; // must be source=7, variant='T'

        ushort seq        = (ushort)((data[4] << 8) | data[5]);
        byte   chunkIdx   = data[6];
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

        try
        {
            var raw     = ZlibDecompress(assembled);
            var terrain = ParseTerrain(raw);
            if (terrain != null) TerrainReceived?.Invoke(terrain);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TerrainStreamClient] Parse error: {ex.Message}");
        }
    }

    private static byte[] ZlibDecompress(byte[] data)
    {
        // Skip the 2-byte zlib header that DeflateStream doesn't handle.
        using var ms  = new MemoryStream(data, 2, data.Length - 2);
        using var ds  = new DeflateStream(ms, CompressionMode.Decompress);
        using var out_ = new MemoryStream();
        ds.CopyTo(out_);
        return out_.ToArray();
    }

    private static TerrainData? ParseTerrain(byte[] raw)
    {
        if (raw.Length < 4) return null;

        // byte[0]=version, byte[1]=flags, bytes[2-3]=reserved
        byte flags = raw[1];
        if ((flags & 0x01) == 0) return null; // no terrain section

        int off = 4;
        if (off + 12 > raw.Length) return null;

        uint gridW  = BitConverter.ToUInt32(raw, off); off += 4;
        uint gridH  = BitConverter.ToUInt32(raw, off); off += 4;
        float cellM = BitConverter.ToSingle(raw, off); off += 4;

        int n         = (int)(gridW * gridH);
        int floatBytes = n * 4;
        if (off + floatBytes + n * 3 > raw.Length) return null;

        float[] heightMap = new float[n];
        Buffer.BlockCopy(raw, off, heightMap, 0, floatBytes); off += floatBytes;

        byte[] rocks   = new byte[n]; Array.Copy(raw, off, rocks,   0, n); off += n;
        byte[] craters = new byte[n]; Array.Copy(raw, off, craters, 0, n); off += n;
        byte[] walls   = new byte[n]; Array.Copy(raw, off, walls,   0, n);

        return new TerrainData
        {
            Width      = (int)gridW,
            Height     = (int)gridH,
            CellMeters = cellM,
            HeightMap  = heightMap,
            Rocks      = rocks,
            Craters    = craters,
            Walls      = walls,
        };
    }

    public void Dispose() => Stop();
}
