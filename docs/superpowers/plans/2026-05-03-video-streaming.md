# Video Streaming Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add live JPEG video streaming over UDP to the Control Hub, with TCP-based stream requests, RGB/Depth toggle, Quality and FPS sliders, and a Mosaic camera button.

**Architecture:** A new `VideoStreamClient` listens on UDP port 5002 and reassembles chunked JPEG frames. Stream requests (source, variant, quality, fps) are sent over the existing TCP connection via a new `EncodeVideoRequest` protocol method. A new `VideoPanel` UserControl owns the client, hosts the stream controls in the viewport header bar, and renders decoded frames. Camera card clicks trigger stream requests through a `VideoStreamRequested` event on `MainViewModel`.

**Tech Stack:** C# / .NET 8 / Avalonia UI / ReactiveUI / `System.Net.Sockets.UdpClient` / `Avalonia.Media.Imaging.Bitmap`

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| Modify | `Components/NetworkProtocol.cs` | Add TYPE_VIDEO constant + EncodeVideoRequest |
| Modify | `Components/NetworkModeClient.cs` | Add SendVideoRequestAsync |
| **Create** | `Components/VideoStreamClient.cs` | UDP receive + JPEG frame reassembly |
| Modify | `ViewModels/MainViewModel.cs` | ActiveStreamSourceId, CurrentFrame, HasActiveStream, VideoStreamRequested event |
| Modify | `Converters/ViewportIdToDisplayNameConverter.cs` | Add "mosaic" mapping |
| Modify | `Controls/Cameras/CameraStatusCard.axaml.cs` | Add SourceId property; call RequestVideoStream on click |
| **Create** | `Controls/Video/VideoPanel.axaml` | Header bar (toggle + sliders + stop) + Image + placeholder |
| **Create** | `Controls/Video/VideoPanel.axaml.cs` | VideoStreamClient ownership, debounce, JPEG decode, stream wiring |
| Modify | `Views/MainView.axaml` | Replace center placeholder with VideoPanel; add Mosaic card; set SourceId on all camera cards |
| Modify | `Views/MainView.axaml.cs` | Inject NetworkClient into VideoPanel; clear stream on disconnect |

---

## Task 1: Protocol — EncodeVideoRequest + mosaic display name

**Files:**
- Modify: `NASA_Lunabotics_Control_Hub/Components/NetworkProtocol.cs`
- Modify: `NASA_Lunabotics_Control_Hub/Converters/ViewportIdToDisplayNameConverter.cs`

- [ ] **Step 1: Add video constants to NetworkProtocol.cs**

In `NetworkProtocol.cs`, after the existing `TYPE_MANIPULATOR` constant (line 22), add:

```csharp
public const byte TYPE_VIDEO = 0x56;          // 'V'
public const byte VARIANT_RGB = 0x52;         // 'R'
public const byte VARIANT_DEPTH = 0x44;       // 'D'
public const byte VIDEO_SOURCE_STOP = 0xFF;
```

- [ ] **Step 2: Add EncodeVideoRequest method to NetworkProtocol.cs**

Add this method after `EncodeManipulator` (before `EncodeTelemetry`):

```csharp
/// <summary>
/// Encode a video stream request (Ground → Rover)
/// Format: [O][V][4][source_id][variant][quality][fps][CRC8] — 8 bytes
/// </summary>
public static byte[] EncodeVideoRequest(byte sourceId, byte variant, byte quality, byte fps)
{
    var frame = new byte[8];
    frame[0] = MAGIC;
    frame[1] = TYPE_VIDEO;
    frame[2] = 0x04;
    frame[3] = sourceId;
    frame[4] = variant;
    frame[5] = quality;
    frame[6] = fps;
    frame[7] = CalcCrc8(frame, 7);
    return frame;
}
```

- [ ] **Step 3: Verify the stop-all encoding matches the spec**

The spec gives a known stop-all vector: `[0x4F][0x56][0x04][0xFF][0x52][0x00][0x0A][CRC]`.
Add a temporary Console.WriteLine in `MainView()` constructor, build and run, confirm output, then remove it:

```csharp
// Temporary verification — remove after confirming
var stopFrame = NetworkProtocol.EncodeVideoRequest(0xFF, 0x52, 0x00, 0x0A);
Console.WriteLine($"[EncodeVideoRequest] stop-all: {BitConverter.ToString(stopFrame)}");
// Expected: 4F-56-04-FF-52-00-0A-38
```

- [ ] **Step 4: Add "mosaic" to ViewportIdToDisplayNameConverter.cs**

In the switch expression, add before the `_ =>` fallthrough:

```csharp
"mosaic" => "Mosaic (All Cameras)",
```

- [ ] **Step 5: Build and confirm no errors**

```
dotnet build NASA_Lunabotics_Control_Hub/NASA_Lunabotics_Control_Hub.csproj
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add NASA_Lunabotics_Control_Hub/Components/NetworkProtocol.cs \
        NASA_Lunabotics_Control_Hub/Converters/ViewportIdToDisplayNameConverter.cs
git commit -m "feat: add video protocol encoder and mosaic display name"
```

---

## Task 2: NetworkModeClient — SendVideoRequestAsync

**Files:**
- Modify: `NASA_Lunabotics_Control_Hub/Components/NetworkModeClient.cs`

- [ ] **Step 1: Add SendVideoRequestAsync after SendManipulatorCommandAsync (line 173)**

```csharp
public async Task SendVideoRequestAsync(byte sourceId, byte variant, byte quality, byte fps)
{
    if (_client == null || !_client.Connected || _stream == null)
        return;
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
```

- [ ] **Step 2: Build and confirm no errors**

```
dotnet build NASA_Lunabotics_Control_Hub/NASA_Lunabotics_Control_Hub.csproj
```

- [ ] **Step 3: Commit**

```bash
git add NASA_Lunabotics_Control_Hub/Components/NetworkModeClient.cs
git commit -m "feat: add SendVideoRequestAsync to NetworkModeClient"
```

---

## Task 3: VideoStreamClient — UDP receiver + frame reassembly

**Files:**
- Create: `NASA_Lunabotics_Control_Hub/Components/VideoStreamClient.cs`

- [ ] **Step 1: Create VideoStreamClient.cs**

```csharp
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

    public event Action<byte[]>? FrameDecoded;

    private struct FrameBuffer
    {
        public byte[][] Chunks;
        public byte Total;
        public int Received;
    }

    public void Start()
    {
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
        _pending.Clear();
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

        // New seq while a different one is in-flight — discard the incomplete frame
        if (_hasCurrentSeq && seq != _currentSeq)
        {
            _pending.Remove(_currentSeq);
        }
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

        // Frame complete — concatenate and fire
        _pending.Remove(seq);
        _hasCurrentSeq = false;

        int totalLen = 0;
        foreach (var c in buf.Chunks) totalLen += c.Length;
        var jpeg = new byte[totalLen];
        int offset = 0;
        foreach (var c in buf.Chunks)
        {
            Array.Copy(c, 0, jpeg, offset, c.Length);
            offset += c.Length;
        }
        FrameDecoded?.Invoke(jpeg);
    }

    public void Dispose() => Stop();
}
```

- [ ] **Step 2: Build and confirm no errors**

```
dotnet build NASA_Lunabotics_Control_Hub/NASA_Lunabotics_Control_Hub.csproj
```

- [ ] **Step 3: Commit**

```bash
git add NASA_Lunabotics_Control_Hub/Components/VideoStreamClient.cs
git commit -m "feat: add VideoStreamClient with UDP frame reassembly"
```

---

## Task 4: MainViewModel — stream state

**Files:**
- Modify: `NASA_Lunabotics_Control_Hub/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Add `using Avalonia.Media.Imaging;` at the top of MainViewModel.cs**

After the existing `using` statements:

```csharp
using Avalonia.Media.Imaging;
```

- [ ] **Step 2: Add backing fields after `_activeViewport` (line 24)**

```csharp
private byte? _activeStreamSourceId;
private Bitmap? _currentFrame;
```

- [ ] **Step 3: Add properties and event after the `ActiveViewport` property (after line 47)**

```csharp
public byte? ActiveStreamSourceId
{
    get => _activeStreamSourceId;
    private set => this.RaiseAndSetIfChanged(ref _activeStreamSourceId, value);
}

public Bitmap? CurrentFrame
{
    get => _currentFrame;
    set
    {
        this.RaiseAndSetIfChanged(ref _currentFrame, value);
        this.RaisePropertyChanged(nameof(HasActiveStream));
    }
}

public bool HasActiveStream => _currentFrame != null;

public event Action<byte>? VideoStreamRequested;
```

- [ ] **Step 4: Add RequestVideoStream and StopStream methods after `OnViewportSelected` (after line 133)**

```csharp
public void RequestVideoStream(byte sourceId)
{
    ActiveStreamSourceId = sourceId;
    VideoStreamRequested?.Invoke(sourceId);
}

public void StopStream()
{
    ActiveStreamSourceId = null;
    CurrentFrame = null;
}
```

- [ ] **Step 5: Build and confirm no errors**

```
dotnet build NASA_Lunabotics_Control_Hub/NASA_Lunabotics_Control_Hub.csproj
```

- [ ] **Step 6: Commit**

```bash
git add NASA_Lunabotics_Control_Hub/ViewModels/MainViewModel.cs
git commit -m "feat: add stream state and VideoStreamRequested event to MainViewModel"
```

---

## Task 5: CameraStatusCard — add SourceId, update click handler

**Files:**
- Modify: `NASA_Lunabotics_Control_Hub/Controls/Cameras/CameraStatusCard.axaml.cs`

- [ ] **Step 1: Add SourceId StyledProperty after the ViewportIdProperty declaration (after line 25)**

```csharp
public static readonly StyledProperty<byte> SourceIdProperty =
    AvaloniaProperty.Register<CameraStatusCard, byte>(nameof(SourceId));
```

- [ ] **Step 2: Add SourceId CLR property after the `ViewportId` property (after line 61)**

```csharp
public byte SourceId
{
    get => GetValue(SourceIdProperty);
    set => SetValue(SourceIdProperty, value);
}
```

- [ ] **Step 3: Update RootBorder_PointerPressed to also call RequestVideoStream**

Replace the existing `RootBorder_PointerPressed` method (lines 107-115) with:

```csharp
private void RootBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
{
    if (DataContext is MainViewModel vm)
    {
        vm.OnViewportSelected(ViewportId);
        vm.RequestVideoStream(SourceId);
    }
}
```

- [ ] **Step 4: Build and confirm no errors**

```
dotnet build NASA_Lunabotics_Control_Hub/NASA_Lunabotics_Control_Hub.csproj
```

- [ ] **Step 5: Commit**

```bash
git add NASA_Lunabotics_Control_Hub/Controls/Cameras/CameraStatusCard.axaml.cs
git commit -m "feat: add SourceId to CameraStatusCard and wire RequestVideoStream on click"
```

---

## Task 6: VideoPanel UserControl

**Files:**
- Create: `NASA_Lunabotics_Control_Hub/Controls/Video/VideoPanel.axaml`
- Create: `NASA_Lunabotics_Control_Hub/Controls/Video/VideoPanel.axaml.cs`

- [ ] **Step 1: Create VideoPanel.axaml**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:NASA_Lunabotics_Control_Hub.ViewModels"
             x:Class="NASA_Lunabotics_Control_Hub.Controls.Video.VideoPanel"
             x:DataType="vm:MainViewModel">
  <Grid RowDefinitions="Auto,*">

    <!-- Header bar: viewport name + stream controls -->
    <Grid Grid.Row="0" ColumnDefinitions="*,Auto" Margin="0,0,0,8">
      <TextBlock Grid.Column="0"
                 Text="{Binding ActiveViewport, Converter={StaticResource ViewportIdToDisplayName}}"
                 Classes="section-header" VerticalAlignment="Center"/>
      <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="8" VerticalAlignment="Center">

        <!-- RGB / Depth segmented toggle -->
        <Border Background="#1A1A1A" BorderBrush="#333333" BorderThickness="1" CornerRadius="4" Padding="2">
          <StackPanel Orientation="Horizontal" Spacing="2">
            <RadioButton x:Name="RgbButton" Content="RGB" GroupName="StreamVariant"
                         IsChecked="True" Checked="OnVariantChanged"
                         Padding="8,2" FontSize="10" FontFamily="Consolas" FontWeight="Bold"/>
            <RadioButton x:Name="DepthButton" Content="DEPTH" GroupName="StreamVariant"
                         Checked="OnVariantChanged"
                         Padding="8,2" FontSize="10" FontFamily="Consolas" FontWeight="Bold"/>
          </StackPanel>
        </Border>

        <!-- Quality slider -->
        <TextBlock Text="Quality" Foreground="#606060" FontSize="10" FontFamily="Consolas"
                   VerticalAlignment="Center"/>
        <Slider x:Name="QualitySlider" Minimum="1" Maximum="100" Value="70" Width="80"
                VerticalAlignment="Center" ValueChanged="OnQualityChanged"/>
        <TextBlock x:Name="QualityLabel" Text="70" Foreground="#C0C0C0" FontSize="10"
                   FontFamily="Consolas" VerticalAlignment="Center" Width="24"/>

        <!-- FPS slider -->
        <TextBlock Text="FPS" Foreground="#606060" FontSize="10" FontFamily="Consolas"
                   VerticalAlignment="Center"/>
        <Slider x:Name="FpsSlider" Minimum="1" Maximum="30" Value="10" Width="60"
                VerticalAlignment="Center" ValueChanged="OnFpsChanged"/>
        <TextBlock x:Name="FpsLabel" Text="10" Foreground="#C0C0C0" FontSize="10"
                   FontFamily="Consolas" VerticalAlignment="Center" Width="24"/>

        <!-- Stop All -->
        <Button Content="■ STOP" Click="OnStopClicked"
                Background="#2D1414" Foreground="#DC2626"
                Padding="8,4" FontSize="10" FontFamily="Consolas" FontWeight="Bold"
                CornerRadius="4"/>
      </StackPanel>
    </Grid>

    <!-- Video area -->
    <Border Grid.Row="1" Background="#0F0F0F" BorderBrush="#333333" BorderThickness="1">
      <Grid>
        <!-- Placeholder shown when no stream is active -->
        <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center"
                    IsVisible="{Binding !HasActiveStream}">
          <TextBlock
            Text="{Binding ActiveViewport, Converter={StaticResource ViewportIdToDisplayName}, StringFormat='{}{0} Feed'}"
            Foreground="#606060" FontSize="20" FontWeight="Bold" Margin="0,24,0,0"
            HorizontalAlignment="Center"/>
          <TextBlock
            Text="{Binding ActiveViewport, Converter={StaticResource ViewportIdToDisplayName}, StringFormat='Waiting for {0}'}"
            Foreground="#404040" FontSize="14" HorizontalAlignment="Center"/>
        </StackPanel>

        <!-- Live video frame -->
        <Image Source="{Binding CurrentFrame}" Stretch="Uniform"
               IsVisible="{Binding HasActiveStream}"/>

        <!-- Stream status overlay (top-left) -->
        <Border Background="#1A1A1A80" BorderBrush="#333333" BorderThickness="1"
                CornerRadius="6" Padding="10" Margin="12"
                HorizontalAlignment="Left" VerticalAlignment="Top">
          <StackPanel Spacing="3">
            <TextBlock x:Name="StreamStatusText" Text="STOPPED"
                       Foreground="#DC2626" FontSize="10"
                       FontFamily="Consolas" FontWeight="Bold"/>
            <TextBlock Text="{Binding ActiveViewport, StringFormat='source: {0}'}"
                       Foreground="#606060" FontSize="9" FontFamily="Consolas"/>
          </StackPanel>
        </Border>
      </Grid>
    </Border>

  </Grid>
</UserControl>
```

- [ ] **Step 2: Create VideoPanel.axaml.cs**

```csharp
using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NASA_Lunabotics_Control_Hub.Components;
using NASA_Lunabotics_Control_Hub.ViewModels;

namespace NASA_Lunabotics_Control_Hub.Controls.Video;

public partial class VideoPanel : UserControl
{
    private NetworkModeClient? _networkClient;
    private readonly VideoStreamClient _videoClient = new();
    private readonly DispatcherTimer _debounce;
    private MainViewModel? _vm;

    private byte _variant = NetworkProtocol.VARIANT_RGB;
    private int _quality = 70;
    private int _fps = 10;
    private int _frameCount;
    private DateTime _fpsWindowStart = DateTime.UtcNow;

    public NetworkModeClient? NetworkClient
    {
        get => _networkClient;
        set => _networkClient = value;
    }

    public VideoPanel()
    {
        InitializeComponent();

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounce.Tick += OnDebounce;

        _videoClient.FrameDecoded += OnFrameDecoded;
        _videoClient.Start();

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
            _vm.VideoStreamRequested -= HandleVideoStreamRequested;

        _vm = DataContext as MainViewModel;

        if (_vm != null)
            _vm.VideoStreamRequested += HandleVideoStreamRequested;
    }

    private void HandleVideoStreamRequested(byte sourceId)
    {
        if (_networkClient == null) return;
        _ = _networkClient.SendVideoRequestAsync(sourceId, _variant, (byte)_quality, (byte)_fps);
        UpdateStatusText("CONNECTING...", Brushes.Orange);
    }

    private void OnFrameDecoded(byte[] jpeg)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_vm == null) return;
            try
            {
                _vm.CurrentFrame = new Bitmap(new MemoryStream(jpeg));

                _frameCount++;
                var elapsed = (DateTime.UtcNow - _fpsWindowStart).TotalSeconds;
                if (elapsed >= 1.0)
                {
                    int actualFps = (int)(_frameCount / elapsed);
                    _frameCount = 0;
                    _fpsWindowStart = DateTime.UtcNow;
                    UpdateStatusText($"STREAMING {actualFps} fps", Brushes.LimeGreen);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VideoPanel] Frame decode error: {ex.Message}");
            }
        });
    }

    public void ClearStream()
    {
        _vm?.StopStream();
        UpdateStatusText("STOPPED", Brushes.Red);
    }

    private void UpdateStatusText(string text, IBrush brush)
    {
        var label = this.FindControl<TextBlock>("StreamStatusText");
        if (label == null) return;
        label.Text = text;
        label.Foreground = brush;
    }

    private void OnVariantChanged(object? sender, RoutedEventArgs e)
    {
        var depthBtn = this.FindControl<RadioButton>("DepthButton");
        _variant = depthBtn?.IsChecked == true
            ? NetworkProtocol.VARIANT_DEPTH
            : NetworkProtocol.VARIANT_RGB;
        ScheduleResend();
    }

    private void OnQualityChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        _quality = (int)e.NewValue;
        var label = this.FindControl<TextBlock>("QualityLabel");
        if (label != null) label.Text = _quality.ToString();
        ScheduleResend();
    }

    private void OnFpsChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        _fps = (int)e.NewValue;
        var label = this.FindControl<TextBlock>("FpsLabel");
        if (label != null) label.Text = _fps.ToString();
        ScheduleResend();
    }

    private void ScheduleResend()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnDebounce(object? sender, EventArgs e)
    {
        _debounce.Stop();
        if (_vm?.ActiveStreamSourceId == null || _networkClient == null) return;
        _ = _networkClient.SendVideoRequestAsync(
            _vm.ActiveStreamSourceId.Value, _variant, (byte)_quality, (byte)_fps);
    }

    private void OnStopClicked(object? sender, RoutedEventArgs e)
    {
        _debounce.Stop();
        if (_networkClient != null)
            _ = _networkClient.SendVideoRequestAsync(NetworkProtocol.VIDEO_SOURCE_STOP, _variant, 0, 0);
        ClearStream();
    }
}
```

- [ ] **Step 3: Build and confirm no errors**

```
dotnet build NASA_Lunabotics_Control_Hub/NASA_Lunabotics_Control_Hub.csproj
```

- [ ] **Step 4: Commit**

```bash
git add NASA_Lunabotics_Control_Hub/Controls/Video/
git commit -m "feat: add VideoPanel UserControl with stream controls and UDP frame display"
```

---

## Task 7: MainView.axaml — wire VideoPanel + add Mosaic card + set SourceId

**Files:**
- Modify: `NASA_Lunabotics_Control_Hub/Views/MainView.axaml`

- [ ] **Step 1: Add the video namespace declaration**

In the `<UserControl ...>` opening tag, add after the existing `xmlns:cameras` line:

```xml
xmlns:video="clr-namespace:NASA_Lunabotics_Control_Hub.Controls.Video"
```

- [ ] **Step 2: Replace the CENTER VIEWPORT inner content**

Replace everything inside `<Border Grid.Column="1" Margin="4" Classes="card-primary">` (the current Grid with header TextBlock, video Border placeholder, and stats overlay Border) with:

```xml
<Border Grid.Column="1" Margin="4" Classes="card-primary">
  <video:VideoPanel x:Name="VideoPanel" Margin="12"/>
</Border>
```

The VideoPanel handles the header row, video area, and stats overlay internally.

- [ ] **Step 3: Add SourceId to the 6 existing CameraStatusCard elements**

Update each card in the Near Cameras grid to include `SourceId`:

```xml
<!-- depth_cam → 0 -->
<cameras:CameraStatusCard Grid.Row="0" Grid.Column="0" CameraName="Depth Camera" CameraType="Depth"
                           StatusText="No Data" ViewportId="depth_cam" SourceId="0"
                           IsActive="{Binding ActiveViewport, Converter={StaticResource ViewportIdToIsActive}, ConverterParameter=depth_cam}"/>

<!-- left_side → 1 -->
<cameras:CameraStatusCard Grid.Row="0" Grid.Column="2" CameraName="Left Side" CameraType="RGB"
                           StatusText="No Data" ViewportId="left_side" SourceId="1"
                           IsActive="{Binding ActiveViewport, Converter={StaticResource ViewportIdToIsActive}, ConverterParameter=left_side}"/>

<!-- left_front → 2 -->
<cameras:CameraStatusCard Grid.Row="2" Grid.Column="0" CameraName="Left Front" CameraType="RGB"
                           StatusText="No Data" ViewportId="left_front" SourceId="2"
                           IsActive="{Binding ActiveViewport, Converter={StaticResource ViewportIdToIsActive}, ConverterParameter=left_front}"/>

<!-- right_side → 3 -->
<cameras:CameraStatusCard Grid.Row="2" Grid.Column="2" CameraName="Right Side" CameraType="RGB"
                           StatusText="No Data" ViewportId="right_side" SourceId="3"
                           IsActive="{Binding ActiveViewport, Converter={StaticResource ViewportIdToIsActive}, ConverterParameter=right_side}"/>

<!-- right_front → 4 -->
<cameras:CameraStatusCard Grid.Row="4" Grid.Column="0" CameraName="Right Front" CameraType="RGB"
                           StatusText="No Data" ViewportId="right_front" SourceId="4"
                           IsActive="{Binding ActiveViewport, Converter={StaticResource ViewportIdToIsActive}, ConverterParameter=right_front}"/>

<!-- back_rear → 5 -->
<cameras:CameraStatusCard Grid.Row="4" Grid.Column="2" CameraName="Back Rear" CameraType="RGB"
                           StatusText="No Data" ViewportId="back_rear" SourceId="5"
                           IsActive="{Binding ActiveViewport, Converter={StaticResource ViewportIdToIsActive}, ConverterParameter=back_rear}"/>
```

- [ ] **Step 4: Add the Mosaic card at the bottom of the Near Cameras grid**

The Near Cameras `<Grid>` currently has `RowDefinitions="*,3,*,3,*"`. Change it to `RowDefinitions="*,3,*,3,*,3,Auto"` and add the Mosaic card as a 7th entry spanning both columns:

```xml
<!-- mosaic → 6, spans both columns -->
<cameras:CameraStatusCard Grid.Row="6" Grid.Column="0" Grid.ColumnSpan="2"
                           CameraName="Mosaic" CameraType="All"
                           StatusText="All Cameras" ViewportId="mosaic" SourceId="6"
                           IsActive="{Binding ActiveViewport, Converter={StaticResource ViewportIdToIsActive}, ConverterParameter=mosaic}"/>
```

Note: `CameraType="All"` will hit the `else` branch of `UpdateVisualState()` and show grey styling (neutral). That's fine for now.

- [ ] **Step 5: Build and confirm no errors**

```
dotnet build NASA_Lunabotics_Control_Hub/NASA_Lunabotics_Control_Hub.csproj
```

- [ ] **Step 6: Commit**

```bash
git add NASA_Lunabotics_Control_Hub/Views/MainView.axaml
git commit -m "feat: replace viewport placeholder with VideoPanel; add Mosaic camera card"
```

---

## Task 8: MainView.axaml.cs — inject NetworkClient, clear stream on disconnect

**Files:**
- Modify: `NASA_Lunabotics_Control_Hub/Views/MainView.axaml.cs`

- [ ] **Step 1: Inject NetworkClient into VideoPanel when connection is confirmed**

In `OnConnectionChanged`, update the `if (isConnected)` branch. Currently the method is:

```csharp
private void OnConnectionChanged(bool isConnected)
{
    Dispatcher.UIThread.Post(() =>
    {
        _mainViewModel.SetConnected(isConnected);
        UpdateConnectButton(isConnected);

        if (!isConnected)
        {
            _mainViewModel.SetModeState("", NASA_Lunabotics_Control_Hub.ViewModels.ModeState.Idle);
        }
    });
}
```

Replace it with:

```csharp
private void OnConnectionChanged(bool isConnected)
{
    Dispatcher.UIThread.Post(() =>
    {
        _mainViewModel.SetConnected(isConnected);
        UpdateConnectButton(isConnected);

        var videoPanel = this.FindControl<Controls.Video.VideoPanel>("VideoPanel");
        if (isConnected)
        {
            if (videoPanel != null) videoPanel.NetworkClient = _networkClient;
        }
        else
        {
            _mainViewModel.SetModeState("", NASA_Lunabotics_Control_Hub.ViewModels.ModeState.Idle);
            videoPanel?.ClearStream();
        }
    });
}
```

- [ ] **Step 2: Build and confirm no errors**

```
dotnet build NASA_Lunabotics_Control_Hub/NASA_Lunabotics_Control_Hub.csproj
```

- [ ] **Step 3: Run the app and verify the following manually**

Launch the desktop app:
```
dotnet run --project NASA_Lunabotics_Control_Hub.Desktop
```

Verify:
1. App launches with no stream active — placeholder text shows in the center viewport
2. Header bar shows RGB/Depth toggle, Quality slider (70), FPS slider (10), ■ STOP button
3. Sliders show their numeric labels (70 and 10)
4. Clicking a camera card (without connecting) does not crash
5. Connecting to a rover and clicking a camera card → Console shows `[NetworkModeClient] Video request: src=N`
6. Stream status overlay shows "STOPPED" at start, updates to "STREAMING N fps" when frames arrive
7. Mosaic card appears at the bottom of Near Cameras, spanning the full width

- [ ] **Step 4: Commit**

```bash
git add NASA_Lunabotics_Control_Hub/Views/MainView.axaml.cs
git commit -m "feat: inject VideoPanel.NetworkClient on connect; clear stream on disconnect"
```

---

## Self-Review Notes

- **Spec coverage:** All protocol requirements covered (variant, quality, fps, CRC, stop-all). UDP reassembly matches spec (discard incomplete on new seq). Debounce 300ms. No auto-start on launch. Sliders pre-configurable before first click. ✓
- **Type consistency:** `byte sourceId` used throughout (Protocol → NetworkModeClient → VideoPanel → CameraStatusCard → MainViewModel). `byte? ActiveStreamSourceId` nullable. `Bitmap?` for nullable frame. ✓
- **Far cameras** (far_front, far_right, far_back, far_left) do NOT have SourceId set in this plan because they are April Tag detection cameras and clicking them should NOT send a video stream request. Their `RootBorder_PointerPressed` will call `vm.RequestVideoStream(0)` (default byte value) — which would accidentally start an orbbec stream. **Fix:** either set a sentinel SourceId or skip the `RequestVideoStream` call for these cards. See note below.

### ⚠️ Fix Required — Far Cameras Should Not Trigger Streams

The far cameras use AprilTag detection, not JPEG streaming. Their CameraStatusCard click must not call `RequestVideoStream`. Two options:

**Option A (recommended):** Add a `bool IsStreamable` StyledProperty to `CameraStatusCard` defaulting to `true`. Check it in `RootBorder_PointerPressed` before calling `RequestVideoStream`.

**Option B:** Set `SourceId="255"` on far cameras. Since 255 = stop-all, clicking them would accidentally stop the stream. Bad.

Use Option A. Add this before Task 8 commits:

In `CameraStatusCard.axaml.cs`, add:
```csharp
public static readonly StyledProperty<bool> IsStreamableProperty =
    AvaloniaProperty.Register<CameraStatusCard, bool>(nameof(IsStreamable), defaultValue: true);

public bool IsStreamable
{
    get => GetValue(IsStreamableProperty);
    set => SetValue(IsStreamableProperty, value);
}
```

Update `RootBorder_PointerPressed`:
```csharp
private void RootBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
{
    if (DataContext is MainViewModel vm)
    {
        vm.OnViewportSelected(ViewportId);
        if (IsStreamable)
            vm.RequestVideoStream(SourceId);
    }
}
```

Set `IsStreamable="False"` on all four far camera cards in `MainView.axaml`.
