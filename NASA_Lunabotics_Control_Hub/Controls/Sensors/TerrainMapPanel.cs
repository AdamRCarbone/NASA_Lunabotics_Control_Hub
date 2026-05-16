using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using NASA_Lunabotics_Control_Hub.Components;

namespace NASA_Lunabotics_Control_Hub.Controls.Sensors;

/// <summary>
/// Interactive 3D terrain map rendered from a 200×200 heightmap.
/// Scroll to zoom, left-click drag to orbit the camera.
/// Feed data by setting NetworkClient (auto-requests terrain on connect).
/// </summary>
public sealed class TerrainMapPanel : Control
{
    private NetworkModeClient?    _networkClient;
    private readonly TerrainStreamClient _terrainClient = new();

    private TerrainData? _terrain;
    private WriteableBitmap? _bitmap;
    private byte[]? _pixels;  // reusable CPU framebuffer

    // Camera state
    private float _azimuth  = MathF.PI * 0.25f;  // 45°
    private float _elevation = MathF.PI * 0.35f;  // ~20°
    private float _distance  = 12f;

    private bool _isDragging;
    private Point _dragLast;

    private static readonly Vector3 LightDir =
        Vector3.Normalize(new Vector3(-0.5f, 0.9f, -0.4f));

    public TerrainMapPanel()
    {
        _terrainClient.TerrainReceived += OnTerrainReceived;

        PointerPressed  += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved    += OnPointerMoved;
        PointerWheelChanged += OnWheel;

        ClipToBounds = true;
    }

    // ── NetworkClient wiring ─────────────────────────────────────────────────

    public NetworkModeClient? NetworkClient
    {
        get => _networkClient;
        set
        {
            if (_networkClient == value) return;
            if (_networkClient != null)
            {
                _networkClient.UnregisterUdpHandler(_terrainClient.ProcessPacket);
                _terrainClient.Stop();
            }
            _networkClient = value;
            if (value != null)
            {
                _terrainClient.Start();
                value.RegisterUdpHandler(_terrainClient.ProcessPacket);
                _ = value.SendTerrainRequestAsync();
            }
            else
            {
                _terrain = null;
                Dispatcher.UIThread.Post(InvalidateVisual);
            }
        }
    }

    // ── Terrain data ─────────────────────────────────────────────────────────

    private void OnTerrainReceived(TerrainData terrain)
    {
        // Called on background thread — render there too, then flip to UI.
        _terrain = terrain;
        Task.Run(RenderAsync);
    }

    private void RenderAsync()
    {
        int w = (int)Bounds.Width;
        int h = (int)Bounds.Height;
        if (w < 4 || h < 4) { w = 400; h = 260; }

        RenderFrame(w, h, fastMode: false);
        Dispatcher.UIThread.Post(InvalidateVisual);
    }

    // ── Camera interaction ───────────────────────────────────────────────────

    private void OnPointerPressed(object? s, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _dragLast   = e.GetPosition(this);
            e.Pointer.Capture(this);
        }
    }

    private void OnPointerReleased(object? s, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            // Full-quality re-render after drag ends
            Task.Run(RenderAsync);
        }
    }

    private void OnPointerMoved(object? s, PointerEventArgs e)
    {
        if (!_isDragging) return;

        var pos   = e.GetPosition(this);
        var delta = pos - _dragLast;
        _dragLast = pos;

        _azimuth   -= (float)(delta.X * 0.01);
        _elevation  = Math.Clamp(_elevation + (float)(delta.Y * 0.008), 0.05f, MathF.PI * 0.49f);

        int w = (int)Bounds.Width;
        int h = (int)Bounds.Height;
        if (w > 0 && h > 0)
        {
            RenderFrame(w, h, fastMode: true); // fast subsample while dragging
            Dispatcher.UIThread.Post(InvalidateVisual);
        }
    }

    private void OnWheel(object? s, PointerWheelEventArgs e)
    {
        _distance = Math.Clamp(_distance - (float)(e.Delta.Y * 0.8f), 2f, 40f);
        Task.Run(RenderAsync);
    }

    // ── Avalonia render ──────────────────────────────────────────────────────

    public override void Render(DrawingContext ctx)
    {
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);

        if (_bitmap == null || _terrain == null)
        {
            ctx.FillRectangle(new SolidColorBrush(Color.Parse("#1A1A1A")), bounds);
            var ft = new FormattedText(
                "Awaiting terrain data…",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                12,
                new SolidColorBrush(Color.Parse("#606060")));
            ctx.DrawText(ft, new Point(bounds.Width / 2 - ft.Width / 2, bounds.Height / 2 - ft.Height / 2));
            DrawHint(ctx, bounds);
            return;
        }

        ctx.DrawImage(_bitmap, bounds);
        DrawHint(ctx, bounds);
    }

    private static void DrawHint(DrawingContext ctx, Rect bounds)
    {
        var ft = new FormattedText(
            "Drag: rotate  ·  Scroll: zoom",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            9,
            new SolidColorBrush(Color.Parse("#404040")));
        ctx.DrawText(ft, new Point(bounds.Width - ft.Width - 4, bounds.Height - ft.Height - 2));
    }

    // ── Software 3-D renderer ────────────────────────────────────────────────

    private void RenderFrame(int w, int h, bool fastMode)
    {
        var terrain = _terrain;
        if (terrain == null) return;

        int stride = w * 4;
        if (_pixels == null || _pixels.Length != stride * h)
            _pixels = new byte[stride * h];

        // Clear to dark background
        var bg = _pixels.AsSpan();
        for (int i = 0; i < bg.Length; i += 4)
        {
            _pixels[i + 0] = 0x12; // B
            _pixels[i + 1] = 0x12; // G
            _pixels[i + 2] = 0x12; // R
            _pixels[i + 3] = 0xFF; // A
        }

        // Camera matrix
        float sinA = MathF.Sin(_azimuth);
        float cosA = MathF.Cos(_azimuth);
        float sinE = MathF.Sin(_elevation);
        float cosE = MathF.Cos(_elevation);

        var eye = new Vector3(
            _distance * cosE * sinA,
            _distance * sinE,
            _distance * cosE * cosA);

        var view = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 3f, (float)w / h, 0.1f, 100f);
        var vp = view * proj;

        int gridW = terrain.Width;
        int gridH = terrain.Height;
        float cellM = terrain.CellMeters;

        // Height range for gradient
        float minH = float.MaxValue, maxH = float.MinValue;
        foreach (var hv in terrain.HeightMap)
        {
            if (hv < minH) minH = hv;
            if (hv > maxH) maxH = hv;
        }
        float rangeH = maxH - minH + 0.001f;

        // Project all vertices
        int vertCount = gridW * gridH;
        var sx = new float[vertCount];
        var sy = new float[vertCount];
        var valid = new bool[vertCount];

        for (int row = 0; row < gridH; row++)
        {
            for (int col = 0; col < gridW; col++)
            {
                int idx = row * gridW + col;
                float wx = (col - gridW / 2f) * cellM;
                float wy = terrain.HeightMap[idx];
                float wz = (row - gridH / 2f) * cellM;

                var clip = Vector4.Transform(new Vector4(wx, wy, wz, 1f), vp);
                if (clip.W > 0.001f)
                {
                    sx[idx] = (clip.X / clip.W + 1f) * 0.5f * w;
                    sy[idx] = (1f - clip.Y / clip.W) * 0.5f * h;
                    valid[idx] = true;
                }
            }
        }

        // Painter's algorithm: traverse rows/cols back-to-front based on camera direction
        int step  = fastMode ? 4 : 1;
        int rFrom = cosA >= 0 ? 0            : gridH - 1 - step;
        int rTo   = cosA >= 0 ? gridH - step : -1;
        int rDir  = cosA >= 0 ? step         : -step;
        int cFrom = sinA >= 0 ? 0            : gridW - 1 - step;
        int cTo   = sinA >= 0 ? gridW - step : -1;
        int cDir  = sinA >= 0 ? step         : -step;

        for (int row = rFrom; row != rTo; row += rDir)
        {
            for (int col = cFrom; col != cTo; col += cDir)
            {
                int i00 = row          * gridW + col;
                int i10 = row          * gridW + (col + step);
                int i01 = (row + step) * gridW + col;
                int i11 = (row + step) * gridW + (col + step);

                if (i10 >= vertCount || i01 >= vertCount || i11 >= vertCount) continue;
                if (!valid[i00] || !valid[i10] || !valid[i01] || !valid[i11]) continue;

                // Face color
                float avgH = (terrain.HeightMap[i00] + terrain.HeightMap[i10] +
                              terrain.HeightMap[i01] + terrain.HeightMap[i11]) * 0.25f;
                float t = (avgH - minH) / rangeH;

                (byte r, byte g, byte b) = HeightColor(t);

                // Rock / crater / wall overlays
                float rock   = (terrain.Rocks[i00]   + terrain.Rocks[i10]   + terrain.Rocks[i01]   + terrain.Rocks[i11])   * 0.25f / 255f;
                float crater = (terrain.Craters[i00] + terrain.Craters[i10] + terrain.Craters[i01] + terrain.Craters[i11]) * 0.25f / 255f;
                float wall   = (terrain.Walls[i00]   + terrain.Walls[i10]   + terrain.Walls[i01]   + terrain.Walls[i11])   * 0.25f / 255f;

                if (rock > 0.15f)
                {
                    r = Lerp(r, 100, rock * 0.7f);
                    g = Lerp(g,  78, rock * 0.7f);
                    b = Lerp(b,  55, rock * 0.7f);
                }
                if (crater > 0.15f)
                {
                    r = Lerp(r, 25, crater * 0.6f);
                    g = Lerp(g, 25, crater * 0.6f);
                    b = Lerp(b, 45, crater * 0.6f);
                }
                if (wall > 0.15f)
                {
                    r = Lerp(r, 220, wall * 0.8f);
                    g = Lerp(g, 195, wall * 0.5f);
                    b = Lerp(b, 140, wall * 0.4f);
                }

                // Directional lighting from face normal
                float wx00 = (col          - gridW / 2f) * cellM;
                float wz00 = (row          - gridH / 2f) * cellM;
                float wx10 = ((col + step) - gridW / 2f) * cellM;
                float wz01 = ((row + step) - gridH / 2f) * cellM;

                var v00 = new Vector3(wx00, terrain.HeightMap[i00], wz00);
                var v10 = new Vector3(wx10, terrain.HeightMap[i10], wz00);
                var v01 = new Vector3(wx00, terrain.HeightMap[i01], wz01);
                var normal = Vector3.Normalize(Vector3.Cross(v01 - v00, v10 - v00));
                float bright = Math.Clamp(Vector3.Dot(normal, LightDir) * 0.75f + 0.35f, 0.2f, 1.0f);

                r = (byte)(r * bright);
                g = (byte)(g * bright);
                b = (byte)(b * bright);

                // Draw two triangles for this quad
                FillTriangle(_pixels, stride, w, h,
                    sx[i00], sy[i00], sx[i10], sy[i10], sx[i01], sy[i01], r, g, b);
                FillTriangle(_pixels, stride, w, h,
                    sx[i10], sy[i10], sx[i11], sy[i11], sx[i01], sy[i01], r, g, b);
            }
        }

        // Robot marker at grid center
        int ci = (gridH / 2) * gridW + (gridW / 2);
        if (valid[ci])
        {
            int mx = (int)sx[ci];
            int my = (int)sy[ci];
            DrawCross(_pixels, stride, w, h, mx, my, 0xFF, 0x30, 0x30);
        }

        // Commit to WriteableBitmap
        var size = new PixelSize(w, h);
        if (_bitmap == null || _bitmap.PixelSize != size)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(size, new Avalonia.Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        }

        using var fb = _bitmap.Lock();
        Marshal.Copy(_pixels, 0, fb.Address, _pixels.Length);
    }

    // ── Rasterization helpers ─────────────────────────────────────────────────

    private static (byte r, byte g, byte b) HeightColor(float t)
    {
        // 5-stop gradient: deep blue → teal → green → yellow → red-orange
        ReadOnlySpan<(byte r, byte g, byte b)> stops = [
            (15,  35, 130),
            (20, 110, 160),
            (55, 160,  55),
            (185, 185, 35),
            (210,  55,  20),
        ];
        float scaled = t * (stops.Length - 1);
        int   lo     = (int)scaled;
        float frac   = scaled - lo;
        if (lo >= stops.Length - 1) return stops[stops.Length - 1];
        var (r0, g0, b0) = stops[lo];
        var (r1, g1, b1) = stops[lo + 1];
        return (
            (byte)(r0 + (r1 - r0) * frac),
            (byte)(g0 + (g1 - g0) * frac),
            (byte)(b0 + (b1 - b0) * frac));
    }

    private static byte Lerp(byte a, int b, float t)
        => (byte)(a + (b - a) * Math.Clamp(t, 0f, 1f));

    private static void FillTriangle(
        byte[] fb, int stride, int w, int h,
        float x0, float y0,
        float x1, float y1,
        float x2, float y2,
        byte R, byte G, byte B)
    {
        // Sort vertices by Y
        if (y1 < y0) { (x0, y0, x1, y1) = (x1, y1, x0, y0); }
        if (y2 < y0) { (x0, y0, x2, y2) = (x2, y2, x0, y0); }
        if (y2 < y1) { (x1, y1, x2, y2) = (x2, y2, x1, y1); }

        int iy0 = (int)MathF.Ceiling(y0);
        int iy2 = (int)MathF.Ceiling(y2) - 1;
        iy0 = Math.Max(iy0, 0);
        iy2 = Math.Min(iy2, h - 1);

        float dy02 = y2 - y0 + 0.0001f;
        float dy01 = y1 - y0 + 0.0001f;
        float dy12 = y2 - y1 + 0.0001f;

        for (int y = iy0; y <= iy2; y++)
        {
            float alpha = (y - y0) / dy02;
            float lx = x0 + (x2 - x0) * alpha;

            float rx;
            if (y <= y1)
                rx = x0 + (x1 - x0) * ((y - y0) / dy01);
            else
                rx = x1 + (x2 - x1) * ((y - y1) / dy12);

            int xl = (int)MathF.Ceiling(Math.Min(lx, rx));
            int xr = (int)MathF.Ceiling(Math.Max(lx, rx)) - 1;
            xl = Math.Max(xl, 0);
            xr = Math.Min(xr, w - 1);

            int rowOff = y * stride;
            for (int x = xl; x <= xr; x++)
            {
                int off = rowOff + x * 4;
                fb[off + 0] = B;
                fb[off + 1] = G;
                fb[off + 2] = R;
                fb[off + 3] = 0xFF;
            }
        }
    }

    private static void DrawCross(byte[] fb, int stride, int w, int h, int cx, int cy, byte R, byte G, byte B)
    {
        for (int d = -5; d <= 5; d++)
        {
            SetPixel(fb, stride, w, h, cx + d, cy, R, G, B);
            SetPixel(fb, stride, w, h, cx, cy + d, R, G, B);
        }
        // Slightly thicker — draw adjacent row too
        for (int d = -5; d <= 5; d++)
        {
            SetPixel(fb, stride, w, h, cx + d, cy + 1, R, G, B);
            SetPixel(fb, stride, w, h, cx + 1, cy + d, R, G, B);
        }
    }

    private static void SetPixel(byte[] fb, int stride, int w, int h, int x, int y, byte R, byte G, byte B)
    {
        if (x < 0 || x >= w || y < 0 || y >= h) return;
        int off = y * stride + x * 4;
        fb[off + 0] = B;
        fb[off + 1] = G;
        fb[off + 2] = R;
        fb[off + 3] = 0xFF;
    }
}
