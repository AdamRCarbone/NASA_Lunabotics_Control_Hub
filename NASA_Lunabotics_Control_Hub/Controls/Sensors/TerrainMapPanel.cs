using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using NASA_Lunabotics_Control_Hub.Components;

namespace NASA_Lunabotics_Control_Hub.Controls.Sensors;

/// <summary>
/// Composite control: header bar with label + STOP button, then the 3D terrain renderer below.
/// Set NetworkClient to start streaming; STOP button clears it and fires TerrainStopped.
/// </summary>
public sealed class TerrainMapPanel : Grid
{
    private readonly TerrainRenderer _renderer;
    private NetworkModeClient? _networkClient;

    public event Action? TerrainStopped;

    public TerrainMapPanel()
    {
        RowDefinitions = RowDefinitions.Parse("Auto,*");

        // ── Header bar ─────────────────────────────────────────────────────
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions = ColumnDefinitions.Parse("*,Auto");

        var label = new TextBlock
        {
            Text    = "3D TERRAIN MAP",
            Classes = { "section-header" },
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 0);
        headerGrid.Children.Add(label);

        var stopContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        stopContent.Children.Add(new TextBlock
        {
            Text = "■",
            FontSize = 8,
            VerticalAlignment = VerticalAlignment.Center,
        });
        stopContent.Children.Add(new TextBlock
        {
            Text             = "STOP",
            FontSize         = 10,
            FontFamily       = new FontFamily("Consolas"),
            FontWeight       = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var stopBtn = new Button
        {
            Content       = stopContent,
            Background    = new SolidColorBrush(Color.Parse("#2D1414")),
            Foreground    = new SolidColorBrush(Color.Parse("#DC2626")),
            CornerRadius  = new CornerRadius(4),
            Padding       = new Thickness(8, 0),
            Height        = 24,
            FontSize      = 10,
            FontFamily    = new FontFamily("Consolas"),
            FontWeight    = FontWeight.Bold,
            Cursor        = new Cursor(StandardCursorType.Hand),
        };
        stopBtn.Click += (_, _) => OnStop();
        Grid.SetColumn(stopBtn, 1);
        headerGrid.Children.Add(stopBtn);

        var header = new Border
        {
            Background      = new SolidColorBrush(Color.Parse("#141414")),
            BorderBrush     = new SolidColorBrush(Color.Parse("#2A2A2A")),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(6),
            Padding         = new Thickness(10, 6),
            Margin          = new Thickness(0, 0, 0, 8),
            Child           = headerGrid,
        };
        Grid.SetRow(header, 0);
        Children.Add(header);

        // ── Renderer ───────────────────────────────────────────────────────
        _renderer = new TerrainRenderer();
        Grid.SetRow(_renderer, 1);
        Children.Add(_renderer);
    }

    public NetworkModeClient? NetworkClient
    {
        get => _networkClient;
        set
        {
            _networkClient    = value;
            _renderer.Client  = value;
        }
    }

    private void OnStop()
    {
        _renderer.Client = null;
        _networkClient   = null;
        TerrainStopped?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Inner class: the raw 3-D rendering surface
    // ═══════════════════════════════════════════════════════════════════════
    private sealed class TerrainRenderer : Control
    {
        private readonly TerrainStreamClient _tc = new();
        private NetworkModeClient? _client;
        private TerrainData? _terrain;
        private WriteableBitmap? _bitmap;
        private byte[]? _pixels;

        // Camera state
        private float _azimuth   = MathF.PI * 0.25f;
        private float _elevation = MathF.PI * 0.35f;
        private float _distance  = 12f;
        private bool _isDragging;
        private Point _dragLast;

        private static readonly Vector3 LightDir =
            Vector3.Normalize(new Vector3(-0.5f, 0.9f, -0.4f));

        public TerrainRenderer()
        {
            _tc.TerrainReceived += t => { _terrain = t; Task.Run(RenderAsync); };
            PointerPressed      += OnPressed;
            PointerReleased     += OnReleased;
            PointerMoved        += OnMoved;
            PointerWheelChanged += OnWheel;
            ClipToBounds         = true;
        }

        public NetworkModeClient? Client
        {
            get => _client;
            set
            {
                if (_client != null)
                {
                    _client.UnregisterUdpHandler(_tc.ProcessPacket);
                    _tc.Stop();
                }
                _client = value;
                if (value != null)
                {
                    _tc.Start();
                    value.RegisterUdpHandler(_tc.ProcessPacket);
                    _ = value.SendTerrainRequestAsync();
                }
                else
                {
                    _terrain = null;
                    Dispatcher.UIThread.Post(InvalidateVisual);
                }
            }
        }

        // ── Mouse ────────────────────────────────────────────────────────

        private void OnPressed(object? s, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                _dragLast   = e.GetPosition(this);
                e.Pointer.Capture(this);
            }
        }

        private void OnReleased(object? s, PointerReleasedEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;
            e.Pointer.Capture(null);
            Task.Run(RenderAsync);
        }

        private void OnMoved(object? s, PointerEventArgs e)
        {
            if (!_isDragging) return;
            var pos   = e.GetPosition(this);
            var delta = pos - _dragLast;
            _dragLast  = pos;
            _azimuth  -= (float)(delta.X * 0.01);
            _elevation  = Math.Clamp(_elevation + (float)(delta.Y * 0.008), 0.05f, MathF.PI * 0.49f);

            int w = (int)Bounds.Width, h = (int)Bounds.Height;
            if (w > 0 && h > 0) { RenderFrame(w, h, fastMode: true); Dispatcher.UIThread.Post(InvalidateVisual); }
        }

        private void OnWheel(object? s, PointerWheelEventArgs e)
        {
            _distance = Math.Clamp(_distance - (float)(e.Delta.Y * 0.8f), 2f, 40f);
            Task.Run(RenderAsync);
        }

        // ── Avalonia render ──────────────────────────────────────────────

        public override void Render(DrawingContext ctx)
        {
            var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);

            if (_bitmap == null || _terrain == null)
            {
                ctx.FillRectangle(new SolidColorBrush(Color.Parse("#0F0F0F")), bounds);

                string msg = _client == null ? "Select 3D Map to start terrain stream"
                                             : "Awaiting terrain data…";
                var ft = new FormattedText(
                    msg,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default, 12,
                    new SolidColorBrush(Color.Parse("#606060")));
                ctx.DrawText(ft, new Point(
                    bounds.Width / 2 - ft.Width / 2,
                    bounds.Height / 2 - ft.Height / 2));

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
                Typeface.Default, 9,
                new SolidColorBrush(Color.Parse("#303030")));
            ctx.DrawText(ft, new Point(bounds.Width - ft.Width - 4, bounds.Height - ft.Height - 2));
        }

        // ── Renderer ─────────────────────────────────────────────────────

        private void RenderAsync()
        {
            int w = (int)Bounds.Width, h = (int)Bounds.Height;
            if (w < 4 || h < 4) { w = 400; h = 240; }
            RenderFrame(w, h, fastMode: false);
            Dispatcher.UIThread.Post(InvalidateVisual);
        }

        private void RenderFrame(int w, int h, bool fastMode)
        {
            var terrain = _terrain;
            if (terrain == null) return;

            int stride = w * 4;
            if (_pixels == null || _pixels.Length != stride * h)
                _pixels = new byte[stride * h];

            // Clear
            for (int i = 0; i < _pixels.Length; i += 4)
            { _pixels[i]=0x0F; _pixels[i+1]=0x0F; _pixels[i+2]=0x0F; _pixels[i+3]=0xFF; }

            // Camera
            float sinA = MathF.Sin(_azimuth), cosA = MathF.Cos(_azimuth);
            float sinE = MathF.Sin(_elevation), cosE = MathF.Cos(_elevation);
            var eye  = new Vector3(_distance * cosE * sinA, _distance * sinE, _distance * cosE * cosA);
            var view = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY);
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, (float)w / h, 0.1f, 100f);
            var vp   = view * proj;

            int gW = terrain.Width, gH = terrain.Height;
            float cellM = terrain.CellMeters;

            float minH = float.MaxValue, maxH = float.MinValue;
            foreach (var hv in terrain.HeightMap) { if (hv < minH) minH = hv; if (hv > maxH) maxH = hv; }
            float rangeH = maxH - minH + 0.001f;

            // Project vertices
            int n  = gW * gH;
            var sx = new float[n]; var sy = new float[n]; var ok = new bool[n];
            for (int row = 0; row < gH; row++)
                for (int col = 0; col < gW; col++)
                {
                    int idx  = row * gW + col;
                    var clip = Vector4.Transform(
                        new Vector4((col - gW / 2f) * cellM, terrain.HeightMap[idx], (row - gH / 2f) * cellM, 1f), vp);
                    if (clip.W > 0.001f)
                    {
                        sx[idx] = (clip.X / clip.W + 1f) * 0.5f * w;
                        sy[idx] = (1f - clip.Y / clip.W) * 0.5f * h;
                        ok[idx] = true;
                    }
                }

            // Back-to-front traversal
            int step  = fastMode ? 4 : 1;
            int rFrom = cosA >= 0 ? 0 : gH - 1 - step, rTo = cosA >= 0 ? gH - step : -1, rDir = cosA >= 0 ? step : -step;
            int cFrom = sinA >= 0 ? 0 : gW - 1 - step, cTo = sinA >= 0 ? gW - step : -1, cDir = sinA >= 0 ? step : -step;

            for (int row = rFrom; row != rTo; row += rDir)
                for (int col = cFrom; col != cTo; col += cDir)
                {
                    int i00 = row * gW + col, i10 = row * gW + col + step;
                    int i01 = (row + step) * gW + col, i11 = (row + step) * gW + col + step;
                    if (i10 >= n || i01 >= n || i11 >= n) continue;
                    if (!ok[i00] || !ok[i10] || !ok[i01] || !ok[i11]) continue;

                    float avgH = (terrain.HeightMap[i00] + terrain.HeightMap[i10] +
                                  terrain.HeightMap[i01] + terrain.HeightMap[i11]) * 0.25f;
                    float t = (avgH - minH) / rangeH;
                    (byte r, byte g, byte b) = HeightColor(t);

                    float rock   = (terrain.Rocks[i00]   + terrain.Rocks[i10]   + terrain.Rocks[i01]   + terrain.Rocks[i11])   * 0.25f / 255f;
                    float crater = (terrain.Craters[i00] + terrain.Craters[i10] + terrain.Craters[i01] + terrain.Craters[i11]) * 0.25f / 255f;
                    float wall   = (terrain.Walls[i00]   + terrain.Walls[i10]   + terrain.Walls[i01]   + terrain.Walls[i11])   * 0.25f / 255f;
                    if (rock   > 0.15f) { r = Lerp(r,100,rock*0.7f);   g = Lerp(g,78,rock*0.7f);   b = Lerp(b,55,rock*0.7f); }
                    if (crater > 0.15f) { r = Lerp(r, 25,crater*0.6f); g = Lerp(g,25,crater*0.6f); b = Lerp(b,45,crater*0.6f); }
                    if (wall   > 0.15f) { r = Lerp(r,220,wall*0.8f);   g = Lerp(g,195,wall*0.5f);  b = Lerp(b,140,wall*0.4f); }

                    // Lighting
                    float wx00 = (col - gW / 2f) * cellM, wz00 = (row - gH / 2f) * cellM;
                    float wx10 = (col + step - gW / 2f) * cellM, wz01 = (row + step - gH / 2f) * cellM;
                    var v00 = new Vector3(wx00, terrain.HeightMap[i00], wz00);
                    var normal = Vector3.Normalize(Vector3.Cross(
                        new Vector3(wx00, terrain.HeightMap[i01], wz01) - v00,
                        new Vector3(wx10, terrain.HeightMap[i10], wz00) - v00));
                    float bright = Math.Clamp(Vector3.Dot(normal, LightDir) * 0.75f + 0.35f, 0.2f, 1.0f);
                    r = (byte)(r * bright); g = (byte)(g * bright); b = (byte)(b * bright);

                    FillTri(_pixels, stride, w, h, sx[i00],sy[i00], sx[i10],sy[i10], sx[i01],sy[i01], r,g,b);
                    FillTri(_pixels, stride, w, h, sx[i10],sy[i10], sx[i11],sy[i11], sx[i01],sy[i01], r,g,b);
                }

            // Robot marker
            int ci = (gH / 2) * gW + (gW / 2);
            if (ok[ci]) DrawCross(_pixels, stride, w, h, (int)sx[ci], (int)sy[ci]);

            var size = new PixelSize(w, h);
            if (_bitmap == null || _bitmap.PixelSize != size)
            { _bitmap?.Dispose(); _bitmap = new WriteableBitmap(size, new Avalonia.Vector(96,96), PixelFormat.Bgra8888, AlphaFormat.Premul); }

            using var fb = _bitmap.Lock();
            Marshal.Copy(_pixels, 0, fb.Address, _pixels.Length);
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static (byte r, byte g, byte b) HeightColor(float t)
        {
            ReadOnlySpan<(byte r, byte g, byte b)> stops =
            [
                (15,  35, 130), (20, 110, 160), (55, 160, 55), (185, 185, 35), (210, 55, 20),
            ];
            float sc = t * (stops.Length - 1);
            int lo   = Math.Clamp((int)sc, 0, stops.Length - 2);
            float fr = sc - lo;
            var (r0,g0,b0) = stops[lo]; var (r1,g1,b1) = stops[lo+1];
            return ((byte)(r0+(r1-r0)*fr), (byte)(g0+(g1-g0)*fr), (byte)(b0+(b1-b0)*fr));
        }

        private static byte Lerp(byte a, int b, float t)
            => (byte)(a + (b - a) * Math.Clamp(t, 0f, 1f));

        private static void FillTri(byte[] fb, int stride, int w, int h,
            float x0,float y0, float x1,float y1, float x2,float y2, byte R,byte G,byte B)
        {
            if (y1 < y0) { (x0,y0,x1,y1) = (x1,y1,x0,y0); }
            if (y2 < y0) { (x0,y0,x2,y2) = (x2,y2,x0,y0); }
            if (y2 < y1) { (x1,y1,x2,y2) = (x2,y2,x1,y1); }
            int iy0 = Math.Max((int)MathF.Ceiling(y0), 0);
            int iy2 = Math.Min((int)MathF.Ceiling(y2) - 1, h - 1);
            float dy02 = y2-y0+1e-4f, dy01 = y1-y0+1e-4f, dy12 = y2-y1+1e-4f;
            for (int y = iy0; y <= iy2; y++)
            {
                float lx = x0 + (x2-x0)*((y-y0)/dy02);
                float rx = y <= y1 ? x0+(x1-x0)*((y-y0)/dy01) : x1+(x2-x1)*((y-y1)/dy12);
                int xl = Math.Max((int)MathF.Ceiling(Math.Min(lx,rx)), 0);
                int xr = Math.Min((int)MathF.Ceiling(Math.Max(lx,rx))-1, w-1);
                int ro = y * stride;
                for (int x = xl; x <= xr; x++) { int o=ro+x*4; fb[o]=B; fb[o+1]=G; fb[o+2]=R; fb[o+3]=0xFF; }
            }
        }

        private static void DrawCross(byte[] fb, int stride, int w, int h, int cx, int cy)
        {
            for (int d = -5; d <= 5; d++)
            {
                Px(fb, stride, w, h, cx+d, cy);   Px(fb, stride, w, h, cx+d, cy+1);
                Px(fb, stride, w, h, cx, cy+d);   Px(fb, stride, w, h, cx+1, cy+d);
            }
        }

        private static void Px(byte[] fb, int stride, int w, int h, int x, int y)
        {
            if (x < 0 || x >= w || y < 0 || y >= h) return;
            int o = y*stride+x*4; fb[o]=0x30; fb[o+1]=0x30; fb[o+2]=0xFF; fb[o+3]=0xFF;
        }
    }
}
