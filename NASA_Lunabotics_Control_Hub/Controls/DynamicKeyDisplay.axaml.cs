using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using System.Collections.Generic;
using System.Linq;

namespace NASA_Lunabotics_Control_Hub.Controls
{
    public partial class DynamicKeyDisplay : UserControl
    {
        private readonly StackPanel _keysStackPanel;

        public DynamicKeyDisplay()
        {
            InitializeComponent();

            _keysStackPanel = this.Find<StackPanel>("KeysWrapPanel")!;
        }

        public void UpdateActiveKeys(IEnumerable<Key> activeKeys)
        {
            _keysStackPanel.Children.Clear();

            foreach (var key in activeKeys)
            {
                var keyCap = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#00643C")),
                    BorderBrush = new SolidColorBrush(Color.Parse("#007A4A")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12, 6),
                    MinWidth = 36,
                    Height = 28,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };

                var text = new TextBlock
                {
                    Text = FormatKeyName(key),
                    Foreground = Brushes.White,
                    FontSize = 11,
                    FontWeight = FontWeight.Bold,
                    FontFamily = new FontFamily("Consolas"),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };

                keyCap.Child = text;
                _keysStackPanel.Children.Add(keyCap);
            }

            if (_keysStackPanel.Children.Count == 0)
            {
                var placeholder = new TextBlock
                {
                    Text = "No keys pressed",
                    Foreground = new SolidColorBrush(Color.Parse("#4A4A4A")),
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                    FontStyle = FontStyle.Italic,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                _keysStackPanel.Children.Add(placeholder);
            }
        }

        private string FormatKeyName(Key key)
        {
            return key switch
            {
                Key.Left => "←",
                Key.Right => "→",
                Key.Up => "↑",
                Key.Down => "↓",
                Key.W => "W",
                Key.A => "A",
                Key.S => "S",
                Key.D => "D",
                Key.Space => "SPC",
                Key.LeftShift => "SHIFT",
                Key.LeftCtrl => "CTRL",
                Key.LeftAlt => "ALT",
                _ => key.ToString().ToUpper()
            };
        }
    }
}
