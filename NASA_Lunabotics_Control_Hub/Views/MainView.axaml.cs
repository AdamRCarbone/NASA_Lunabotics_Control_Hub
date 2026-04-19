using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NASA_Lunabotics_Control_Hub.Controls;
using NASA_Lunabotics_Control_Hub.Controls.Controls;
using NASA_Lunabotics_Control_Hub.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NASA_Lunabotics_Control_Hub.Views
{
    public partial class MainView : UserControl
    {
        private MainViewModel _mainViewModel;
        private JoystickViewModel _joystickViewModel;
        private DispatcherTimer _keyUpdateTimer;

        public MainView()
        {
            InitializeComponent();

            _mainViewModel = new MainViewModel();
            _joystickViewModel = new JoystickViewModel(_mainViewModel);

            DataContext = _mainViewModel;

            // Setup key update timer for KeyInputGrid
            _keyUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _keyUpdateTimer.Tick += KeyUpdateTimer_Tick;
            _keyUpdateTimer.Start();
        }

        private void KeyUpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Update DynamicKeyDisplay with current active keys from the hidden JoystickControl
            var dynamicKeyDisplay = this.FindControl<DynamicKeyDisplay>("DynamicKeyDisplay");
            var joystick = this.FindControl<JoystickControl>("KeyTrackingJoystick");

            if (dynamicKeyDisplay != null && joystick != null)
            {
                dynamicKeyDisplay.UpdateActiveKeys(joystick.GetActiveKeys());
            }
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

        public void HandleKeyDown(Key key)
        {
            var joystick = this.FindControl<Controls.JoystickControl>("KeyTrackingJoystick");
            joystick?.HandleKeyDown(key);
        }

        public void HandleKeyUp(Key key)
        {
            var joystick = this.FindControl<Controls.JoystickControl>("KeyTrackingJoystick");
            joystick?.HandleKeyUp(key);
        }
    }
}
