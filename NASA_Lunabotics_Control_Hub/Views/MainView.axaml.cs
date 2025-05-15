using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using NASA_Lunabotics_Control_Hub.Controls;
using NASA_Lunabotics_Control_Hub.ViewModels;
using System.Diagnostics;

namespace NASA_Lunabotics_Control_Hub.Views
{
    public partial class MainView : UserControl
    {
        private readonly MainViewModel _mainViewModel;
        private readonly JoystickViewModel _joystickViewModel;

        public MainView()
        {
            // Initialize view models
            _mainViewModel = new MainViewModel();
            _joystickViewModel = new JoystickViewModel(_mainViewModel);

            // Set DataContext to JoystickViewModel for BucketControlButtons and other bindings
            DataContext = _joystickViewModel;

            InitializeComponent();

            // Set up joystick event handlers
            SetupJoystickEventHandlers();
        }

        private void SetupJoystickEventHandlers()
        {
            Joystick1.PropertyChanged += (s, e) =>
            {
                if (e.Property == JoystickControl.PositionProperty)
                {
                    Debug.WriteLine($"Joystick_1 Position Changed: {Joystick1.Position}");
                    _joystickViewModel.UpdateFromPosition(Joystick1.Position, 1);
                    _mainViewModel.JoystickPosition = Joystick1.Position;
                }
            };

            Joystick2.PropertyChanged += (s, e) =>
            {
                if (e.Property == JoystickControl.PositionProperty)
                {
                    Debug.WriteLine($"Joystick_2 Position Changed: {Joystick2.Position}");
                    _joystickViewModel.UpdateFromPosition(Joystick2.Position, 2);
                }
            };
        }
    }
}