using System;
using System.IO;
using System.Threading;
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

    private volatile byte[]? _latestJpeg;
    private int _uiFramePending;

    public NetworkModeClient? NetworkClient
    {
        get => _networkClient;
        set
        {
            if (_networkClient == value) return;
            _networkClient?.UnregisterUdpHandler(_videoClient.ProcessPacket);
            _networkClient = value;
            _networkClient?.RegisterUdpHandler(_videoClient.ProcessPacket);
        }
    }

    public VideoPanel()
    {
        InitializeComponent();

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounce.Tick += OnDebounce;

        _videoClient.FrameDecoded += OnFrameDecoded;

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
        _videoClient.Stop();
        _videoClient.Start();
        _ = _networkClient.SendVideoRequestAsync(sourceId, _variant, (byte)_quality, (byte)_fps);
        UpdateStatusText("CONNECTING...", Brushes.Orange);
    }

    private void OnFrameDecoded(byte[] jpeg)
    {
        // Always overwrite with the newest frame; if a UI dispatch is already pending
        // it will pick up this latest buffer instead of queuing another one.
        _latestJpeg = jpeg;
        if (Interlocked.CompareExchange(ref _uiFramePending, 1, 0) == 1) return;

        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _uiFramePending, 0);
            var jpegToShow = _latestJpeg;
            if (_vm == null || jpegToShow == null) return;

            _networkClient?.BumpHeartbeat();

            try
            {
                var oldFrame = _vm.CurrentFrame;
                _vm.CurrentFrame = new Bitmap(new MemoryStream(jpegToShow));
                oldFrame?.Dispose();

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
        _videoClient.Stop();
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
        _variant = (sender as RadioButton)?.Name == "DepthButton"
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
        if (_vm?.ActiveStreamSourceId == null) return;
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
