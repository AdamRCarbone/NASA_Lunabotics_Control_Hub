using Avalonia.Controls;
using NASA_Lunabotics_Control_Hub.Controls;
using NASA_Lunabotics_Control_Hub.ViewModels;
using System.Diagnostics;

namespace NASA_Lunabotics_Control_Hub.Views
{
    public partial class MainView : UserControl
    {
        private MainViewModel _mainViewModel;
        private JoystickViewModel _joystickViewModel;

        public MainView()
        {
            InitializeComponent();

            _mainViewModel = new MainViewModel();
            _joystickViewModel = new JoystickViewModel(_mainViewModel);

            DataContext = _joystickViewModel;

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