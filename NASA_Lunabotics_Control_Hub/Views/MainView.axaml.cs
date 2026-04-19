using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NASA_Lunabotics_Control_Hub.Components;
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
        private NetworkModeClient _networkClient;

        public MainView()
        {
            InitializeComponent();

            _mainViewModel = new MainViewModel();
            _joystickViewModel = new JoystickViewModel(_mainViewModel);
            _networkClient = new NetworkModeClient();

            // Subscribe to state changes from network client
            _networkClient.StateChanged += OnRosStateReceived;
        _networkClient.ConnectionChanged += OnConnectionChanged;
        _networkClient.HeartbeatReceived += OnHeartbeatReceived;

            DataContext = _mainViewModel;

            // Setup key update timer for KeyInputGrid
            _keyUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _keyUpdateTimer.Tick += KeyUpdateTimer_Tick;
            _keyUpdateTimer.Start();

            // Auto-connect to rover on startup (optional)
            // _networkClient.ConnectAsync();
        }

        private void OnRosStateReceived(string state)
        {
            // ROS state confirmation received from network
            Console.WriteLine($"[MainView] ROS state confirmation: {state}");

            // Map ROS state to UI mode name
            string modeName = state.ToLower() switch
            {
                "standby" => "Standby",
                "manual" => "Manual",
                "autonomous" => "Autonomous",
                "fault" => "Fault Reset", // ROS FAULT state maps to Fault Reset button
                _ => state
            };

            // Update UI to show Confirmed state (green LED)
            _mainViewModel.SetModeState(modeName, NASA_Lunabotics_Control_Hub.ViewModels.ModeState.Confirmed);
            _mainViewModel.SetCurrentMode(modeName);

            Console.WriteLine($"[MainView] UI updated: {modeName} = Confirmed");
        }

        private void OnConnectionChanged(bool isConnected)
        {
            Console.WriteLine($"[MainView] Connection changed: {isConnected}");
            _mainViewModel.SetConnected(isConnected);
        }

        private void OnHeartbeatReceived()
        {
            // Trigger heartbeat pulse animation
            var heartbeatRing = this.FindControl<Border>("HeartbeatRing");
            if (heartbeatRing != null)
            {
                heartbeatRing.Opacity = 1;
                // Fade out after 0.5s using a simple timer
                var timer = new System.Threading.Timer(_ =>
                {
                    Dispatcher.UIThread.Post(() => heartbeatRing.Opacity = 0);
                }, null, 500, System.Threading.Timeout.Infinite);
            }
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
            OnModeSelected("Standby");
        }

        private void ManualButton_Click(object? sender, RoutedEventArgs e)
        {
            OnModeSelected("Manual");
        }

        private void AutonomousButton_Click(object? sender, RoutedEventArgs e)
        {
            OnModeSelected("Autonomous");
        }

        private void FaultResetButton_Click(object? sender, RoutedEventArgs e)
        {
            OnModeSelected("Fault Reset");
        }

        private async void OnModeSelected(string mode)
        {
            // Update local UI state (shows Pending - red LED, green text/bg)
            _mainViewModel.OnModeSelected(mode);

            // Send mode command to rover via TCP
            await _networkClient.SendModeCommandAsync(mode);
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
