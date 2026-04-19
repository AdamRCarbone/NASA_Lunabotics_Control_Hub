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
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

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

            // Populate network selector with available interfaces
            PopulateNetworkSelector();

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
            Console.WriteLine($"[MainView] ROS state confirmation: {state}");

            string modeName = state.ToLower() switch
            {
                "standby" => "Standby",
                "manual" => "Manual",
                "autonomous" => "Autonomous",
                "fault" => "Fault Reset",
                _ => state
            };

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
            var heartbeatRing = this.FindControl<Border>("HeartbeatRing");
            if (heartbeatRing != null)
            {
                heartbeatRing.Opacity = 1;
                var timer = new System.Threading.Timer(_ =>
                {
                    Dispatcher.UIThread.Post(() => heartbeatRing.Opacity = 0);
                }, null, 500, System.Threading.Timeout.Infinite);
            }
        }

        private void KeyUpdateTimer_Tick(object? sender, EventArgs e)
        {
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
            _mainViewModel.OnModeSelected(mode);
            await _networkClient.SendModeCommandAsync(mode);
        }

        private void OnNetworkSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (NetworkSelector.SelectedItem is ComboBoxItem item)
            {
                string content = item.Content?.ToString() ?? "";
                string ipAddress = ExtractIpAddress(content);

                _networkClient.SetRoverIp(ipAddress);
                Console.WriteLine($"[MainView] Network changed to: {ipAddress}");
            }
        }

        private string ExtractIpAddress(string content)
        {
            if (content.Contains("Auto-detect"))
                return "192.168.1.100";

            if (content.Contains("Rover @"))
            {
                var parts = content.Split('@');
                if (parts.Length > 1)
                    return parts[1].Trim();
            }

            var colonIndex = content.LastIndexOf(':');
            if (colonIndex > 0)
                return content.Substring(colonIndex + 1).Trim();

            return "192.168.1.100";
        }

        private void PopulateNetworkSelector()
        {
            NetworkSelector.Items.Clear();

            NetworkSelector.Items.Add(new ComboBoxItem { Content = "Auto-detect (Rover IP: 192.168.1.100)" });

            string[] roverIps = { "192.168.1.100", "10.0.0.100" };
            foreach (var ip in roverIps)
            {
                NetworkSelector.Items.Add(new ComboBoxItem { Content = $"Rover @ {ip}" });
            }

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                     networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                {
                    var ipProperties = networkInterface.GetIPProperties();
                    foreach (var unicast in ipProperties.UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            var ip = unicast.Address.ToString();
                            NetworkSelector.Items.Add(new ComboBoxItem { Content = $"{networkInterface.Name}: {ip}" });
                        }
                    }
                }
            }

            NetworkSelector.SelectedIndex = 0;
            Console.WriteLine($"[MainView] Network selector populated with {NetworkSelector.Items.Count} options");
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
