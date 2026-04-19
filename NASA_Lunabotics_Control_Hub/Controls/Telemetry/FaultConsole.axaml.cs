using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace NASA_Lunabotics_Control_Hub.Controls.Telemetry
{
    public partial class FaultConsole : UserControl
    {
        private readonly StackPanel _faultList;
        private readonly ScrollViewer _scrollArea;

        public FaultConsole()
        {
            InitializeComponent();

            _faultList = this.Find<StackPanel>("FaultList")!;
            _scrollArea = this.Find<ScrollViewer>("FaultScrollArea")!;
        }

        public void AddFault(string faultType, string message, string severity = "WARNING")
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");

            IBrush severityBrush = severity switch
            {
                "CRITICAL" => new SolidColorBrush(Color.Parse("#EF4444")),
                "ERROR" => new SolidColorBrush(Color.Parse("#F97316")),
                "WARNING" => new SolidColorBrush(Color.Parse("#EAB308")),
                _ => new SolidColorBrush(Color.Parse("#A1A1AA"))
            };

            var faultEntry = new Border
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(0, 2, 0, 2)
            };

            var lineText = new TextBlock
            {
                Text = $"$ [{timestamp}] {faultType}: {message}",
                Foreground = severityBrush,
                FontSize = 9,
                FontFamily = new FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap
            };

            faultEntry.Child = lineText;

            _faultList.Children.Add(faultEntry);

            // Auto-scroll to bottom
            _scrollArea.ScrollToEnd();
        }

        public void ClearFaults()
        {
            _faultList.Children.Clear();
            _faultList.Children.Add(new TextBlock
            {
                Text = "$ Clear. Ready for monitoring...",
                Foreground = new SolidColorBrush(Color.Parse("#4A4A4A")),
                FontSize = 9,
                FontFamily = new FontFamily("Consolas"),
                FontStyle = FontStyle.Italic
            });
        }
    }
}
