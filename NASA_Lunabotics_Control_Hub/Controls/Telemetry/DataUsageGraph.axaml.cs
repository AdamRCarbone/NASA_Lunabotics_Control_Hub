using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

namespace NASA_Lunabotics_Control_Hub.Controls.Telemetry
{
    public partial class DataUsageGraph : UserControl
    {
        private readonly Canvas _chartCanvas;
        private readonly List<double> _guiInputHistory = new(); // GUI → Rover (commands)
        private readonly List<double> _networkHistory = new(); // Network telemetry (faint background)
        private const int HistorySize = 40;

        private DispatcherTimer _updateTimer;
        private double _totalUpload = 0;
        private double _totalDownload = 0;
        private double _currentUploadRate = 0;
        private double _currentDownloadRate = 0;

        // UI elements for displaying values
        private TextBlock _uploadRateText;
        private TextBlock _downloadRateText;
        private TextBlock _totalUploadText;
        private TextBlock _totalDownloadText;

        // Network interface monitoring
        private string? _selectedInterfaceName;
        private long _lastBytesSent = 0;
        private long _lastBytesReceived = 0;

        public DataUsageGraph()
        {
            InitializeComponent();

            _chartCanvas = this.Find<Canvas>("ChartCanvas")!;
            _uploadRateText = this.Find<TextBlock>("UploadRateText")!;
            _downloadRateText = this.Find<TextBlock>("DownloadRateText")!;
            _totalUploadText = this.Find<TextBlock>("TotalUploadText")!;
            _totalDownloadText = this.Find<TextBlock>("TotalDownloadText")!;

            // Initialize history with zeros
            for (int i = 0; i < HistorySize; i++)
            {
                _guiInputHistory.Add(0);
                _networkHistory.Add(0);
            }

            // Setup update timer
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _updateTimer.Tick += UpdateChart;
            _updateTimer.Start();
        }

        /// <summary>
        /// Set which network interface to monitor
        /// </summary>
        public void SetNetworkInterface(string interfaceName)
        {
            _selectedInterfaceName = interfaceName;
            _lastBytesSent = 0;
            _lastBytesReceived = 0;
            _totalUpload = 0;
            _totalDownload = 0;

            Console.WriteLine($"[DataUsageGraph] Monitoring interface: {interfaceName}");
        }

        private void UpdateChart(object? sender, EventArgs e)
        {
            if (_selectedInterfaceName == null)
            {
                // No interface selected - show zeros
                _guiInputHistory.RemoveAt(0);
                _guiInputHistory.Add(0);
                _networkHistory.RemoveAt(0);
                _networkHistory.Add(0);
                DrawChart();
                return;
            }

            // Get network interface by name
            var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Name == _selectedInterfaceName);

            if (networkInterface == null || networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                // Interface not available - show zeros
                _guiInputHistory.RemoveAt(0);
                _guiInputHistory.Add(0);
                _networkHistory.RemoveAt(0);
                _networkHistory.Add(0);
                DrawChart();
                return;
            }

            // Get byte counters
            var ipStats = networkInterface.GetIPStatistics();
            long currentBytesSent = ipStats.BytesSent;
            long currentBytesReceived = ipStats.BytesReceived;

            // Calculate rates (bytes per 200ms interval)
            double bytesSentThisInterval = 0;
            double bytesReceivedThisInterval = 0;

            if (_lastBytesSent > 0)
            {
                bytesSentThisInterval = currentBytesSent - _lastBytesSent;
                bytesReceivedThisInterval = currentBytesReceived - _lastBytesReceived;
            }

            _lastBytesSent = currentBytesSent;
            _lastBytesReceived = currentBytesReceived;

            // Convert to KB/s for display (bytes per 200ms * 5 = bytes per second, / 1024 = KB/s)
            _currentUploadRate = (bytesSentThisInterval * 5) / 1024.0;
            _currentDownloadRate = (bytesReceivedThisInterval * 5) / 1024.0;

            _totalUpload += bytesSentThisInterval / 1024.0;
            _totalDownload += bytesReceivedThisInterval / 1024.0;

            // Update the text displays
            _uploadRateText.Text = $"{_currentUploadRate:F1} KB/s";
            _downloadRateText.Text = $"{_currentDownloadRate:F1} KB/s";
            _totalUploadText.Text = $"{_totalUpload:F2} KB";
            _totalDownloadText.Text = $"{_totalDownload:F2} KB";

            // Split upload into GUI commands (higher frequency) and background telemetry
            // Assume ~30% of upload is GUI commands, rest is background/other
            double newGuiInput = _currentUploadRate * 0.3; // GUI commands
            double newNetwork = _currentDownloadRate; // Telemetry download

            _guiInputHistory.RemoveAt(0);
            _guiInputHistory.Add(newGuiInput);

            _networkHistory.RemoveAt(0);
            _networkHistory.Add(newNetwork);

            DrawChart();
        }

        private void DrawChart()
        {
            if (_chartCanvas.Children.Count > 0)
                _chartCanvas.Children.Clear();

            double width = _chartCanvas.Bounds.Width > 0 ? _chartCanvas.Bounds.Width : 280;
            double height = _chartCanvas.Bounds.Height > 0 ? _chartCanvas.Bounds.Height : 100;
            double barWidth = width / HistorySize;

            // Draw faint network telemetry first (background layer - blue, very transparent)
            for (int i = 0; i < HistorySize; i++)
            {
                double networkHeight = Math.Min(_networkHistory[i] * 8, height * 0.6);
                var networkRect = new Rectangle
                {
                    Fill = new SolidColorBrush(Color.Parse("#223A5F")), // Very faint blue
                    Width = barWidth - 2,
                    Height = networkHeight
                };
                Canvas.SetLeft(networkRect, i * barWidth + 1);
                Canvas.SetTop(networkRect, height - networkHeight);
                _chartCanvas.Children.Add(networkRect);
            }

            // Draw GUI command traffic overlay (bright green, more prominent)
            for (int i = 0; i < HistorySize; i++)
            {
                double guiHeight = Math.Min(_guiInputHistory[i] * 10, height);
                var guiRect = new Rectangle
                {
                    Fill = new SolidColorBrush(Color.Parse("#1B4D3E")),
                    Width = barWidth - 2,
                    Height = guiHeight
                };
                Canvas.SetLeft(guiRect, i * barWidth + 1);
                Canvas.SetTop(guiRect, height - guiHeight);
                _chartCanvas.Children.Add(guiRect);
            }

            // Draw center line
            var centerLine = new Rectangle
            {
                Fill = new SolidColorBrush(Color.Parse("#333333")),
                Width = width,
                Height = 1
            };
            Canvas.SetLeft(centerLine, 0);
            Canvas.SetTop(centerLine, height / 2);
            _chartCanvas.Children.Add(centerLine);
        }

        /// <summary>
        /// Get current upload rate (KB/s) for display in UI
        /// </summary>
        public double CurrentUploadRate => _currentUploadRate;

        /// <summary>
        /// Get current download rate (KB/s) for display in UI
        /// </summary>
        public double CurrentDownloadRate => _currentDownloadRate;

        /// <summary>
        /// Get total upload (KB) for display in UI
        /// </summary>
        public double TotalUpload => _totalUpload;

        /// <summary>
        /// Get total download (KB) for display in UI
        /// </summary>
        public double TotalDownload => _totalDownload;
    }
}
