using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;

namespace NASA_Lunabotics_Control_Hub.Controls.Telemetry
{
    public partial class DataUsageGraph : UserControl
    {
        private readonly Canvas _chartCanvas;
        private readonly List<double> _guiInputHistory = new();  // GUI → Rover (commands)
        private readonly List<double> _networkHistory = new();   // Network telemetry (faint background)
        private const int HistorySize = 40;

        private DispatcherTimer _updateTimer;
        private Random _rng = new Random();
        private double _totalUpload = 0;
        private double _totalDownload = 0;
        private double _currentUploadRate = 0;
        private double _currentDownloadRate = 0;

        public DataUsageGraph()
        {
            InitializeComponent();

            _chartCanvas = this.Find<Canvas>("ChartCanvas")!;

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

        private void UpdateChart(object? sender, EventArgs e)
        {
            // Simulate GUI command traffic (when user interacts with controls)
            // In real usage, this would come from actual command sending
            double newGuiInput = _rng.NextDouble() * 3;  // Base traffic
            if (_rng.Next(100) < 30)  // 30% chance of burst (simulating button/slider use)
                newGuiInput += _rng.NextDouble() * 7;

            // Simulate network telemetry (faint background traffic)
            double newNetwork = _rng.NextDouble() * 5;  // Constant telemetry stream

            _guiInputHistory.RemoveAt(0);
            _guiInputHistory.Add(newGuiInput);

            _networkHistory.RemoveAt(0);
            _networkHistory.Add(newNetwork);

            _currentUploadRate = newGuiInput;
            _currentDownloadRate = newNetwork;

            _totalUpload += newGuiInput / 5;  // Adjust for 200ms interval
            _totalDownload += newNetwork / 5;

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
                    Fill = new SolidColorBrush(Color.Parse("#223A5F")),  // Very faint blue
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
    }
}
