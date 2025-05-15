using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NASA_Lunabotics_Control_Hub.Controls
{
    public partial class VerticalSliderControl : UserControl
    {
        public VerticalSliderControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}