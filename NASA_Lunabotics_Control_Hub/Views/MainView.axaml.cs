using Avalonia.Controls;
using Avalonia.Interactivity;
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

            DataContext = _mainViewModel;
        }

        private void StandbyButton_Click(object? sender, RoutedEventArgs e)
        {
            _mainViewModel.OnModeSelected("Standby");
        }

        private void ManualButton_Click(object? sender, RoutedEventArgs e)
        {
            _mainViewModel.OnModeSelected("Manual");
        }

        private void AutonomousButton_Click(object? sender, RoutedEventArgs e)
        {
            _mainViewModel.OnModeSelected("Autonomous");
        }

        private void FaultResetButton_Click(object? sender, RoutedEventArgs e)
        {
            _mainViewModel.OnModeSelected("Fault Reset");
        }
    }
}
