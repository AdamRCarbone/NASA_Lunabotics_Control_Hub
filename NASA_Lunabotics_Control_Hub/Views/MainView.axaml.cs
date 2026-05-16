using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NASA_Lunabotics_Control_Hub.Components;
using NASA_Lunabotics_Control_Hub.Controls;
using NASA_Lunabotics_Control_Hub.Controls.Manual;
using NASA_Lunabotics_Control_Hub.Controls.Sensors;
using NASA_Lunabotics_Control_Hub.Controls.Telemetry;
using NASA_Lunabotics_Control_Hub.Helpers;
using NASA_Lunabotics_Control_Hub.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;

namespace NASA_Lunabotics_Control_Hub.Views
{
    public partial class MainView : UserControl
    {
        private MainViewModel _mainViewModel;
        private JoystickViewModel _joystickViewModel;
        private DispatcherTimer _keyUpdateTimer;
        private DispatcherTimer? _heartbeatFadeTimer;
        private NetworkModeClient _networkClient;
        private const bool IgnoreHeartbeatTimeout = false;

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
            _networkClient.AccelReceived += (ax, ay, az) => _mainViewModel.UpdateAccel(ax, ay, az);
            _networkClient.PoseReceived += (x, y, theta) => _mainViewModel.UpdatePose(x, y, theta);
            _networkClient.TagsReceived += (count, ids, dists, angles) => _mainViewModel.UpdateTags(count, ids, dists, angles);

            DataContext = _mainViewModel;

            // Watch for viewport selection changes to toggle terrain / video panels
            _mainViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.ActiveViewport))
                    OnActiveViewportChanged();
            };

            // Populate network selector with available interfaces
            PopulateNetworkSelector();
            NetworkSelector.DropDownOpened += (_, _) =>
            {
                string? current = (NetworkSelector.SelectedItem as ComboBoxItem)?.Tag as string;
                PopulateNetworkSelector(current);
            };


            // Setup key update timer for manual control
            _keyUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _keyUpdateTimer.Tick += KeyUpdateTimer_Tick;
            _keyUpdateTimer.Start();
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
            Dispatcher.UIThread.Post(() =>
            {
                _mainViewModel.SetConnected(isConnected);
                UpdateConnectButton(isConnected);

                var videoPanel   = this.FindControl<Controls.Video.VideoPanel>("VideoPanel");
                var terrainPanel = this.FindControl<TerrainMapPanel>("TerrainPanel");
                if (isConnected)
                {
                    if (videoPanel != null) videoPanel.NetworkClient = _networkClient;
                    // Terrain panel gets its NetworkClient only when user selects "map" viewport.
                    if (terrainPanel != null)
                        terrainPanel.TerrainStopped += OnTerrainStopped;
                }
                else
                {
                    if (videoPanel   != null) videoPanel.NetworkClient   = null;
                    if (terrainPanel != null)
                    {
                        terrainPanel.NetworkClient  = null;
                        terrainPanel.TerrainStopped -= OnTerrainStopped;
                        terrainPanel.IsVisible       = false;
                        videoPanel!.IsVisible        = true;
                    }
                    _mainViewModel.SetModeState("", NASA_Lunabotics_Control_Hub.ViewModels.ModeState.Idle);
                    videoPanel?.ClearStream();
                    _mainViewModel.ResetImu();
                    _mainViewModel.ResetLocalization();
                }
            });
        }

        private void OnTerrainStopped()
        {
            // User clicked STOP on the terrain panel — switch back to the video viewport.
            _mainViewModel.OnViewportSelected("");
            var terrainPanel = this.FindControl<TerrainMapPanel>("TerrainPanel");
            var videoPanel   = this.FindControl<Controls.Video.VideoPanel>("VideoPanel");
            if (terrainPanel != null) terrainPanel.IsVisible = false;
            if (videoPanel   != null) videoPanel.IsVisible   = true;
        }

        private void OnActiveViewportChanged()
        {
            Dispatcher.UIThread.Post(() =>
            {
                bool isMap       = _mainViewModel.ActiveViewport == "map";
                var terrainPanel = this.FindControl<TerrainMapPanel>("TerrainPanel");
                var videoPanel   = this.FindControl<Controls.Video.VideoPanel>("VideoPanel");

                if (terrainPanel != null) terrainPanel.IsVisible = isMap;
                if (videoPanel   != null) videoPanel.IsVisible   = !isMap;

                if (isMap && _networkClient.IsConnected && terrainPanel != null)
                    terrainPanel.NetworkClient = _networkClient;
            });
        }

        private void OnHeartbeatReceived()
        {
            var heartbeatRing = this.FindControl<Border>("HeartbeatRing");
            if (heartbeatRing == null) return;

            heartbeatRing.Opacity = 1;

            if (_heartbeatFadeTimer == null)
            {
                _heartbeatFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _heartbeatFadeTimer.Tick += (_, _) =>
                {
                    heartbeatRing.Opacity = 0;
                    _heartbeatFadeTimer.Stop();
                };
            }

            _heartbeatFadeTimer.Stop();
            _heartbeatFadeTimer.Start();
        }

        private void KeyUpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Disconnect if heartbeat stops arriving (covers rover crash where TCP lingers)
            if (!IgnoreHeartbeatTimeout && _networkClient.IsConnected && _networkClient.IsHeartbeatTimeout())
                _networkClient.Disconnect();

            _mainViewModel.ClearTagsIfStale();

            var joystick      = this.FindControl<Controls.JoystickControl>("KeyTrackingJoystick");
            var manualControl = this.FindControl<ManualControl>("ManualControlCard");

            if (joystick == null || manualControl == null) return;

            manualControl.IsActive = (_mainViewModel.ManualStatus != NASA_Lunabotics_Control_Hub.ViewModels.ModeState.Idle);
            manualControl.IsLive   = manualControl.IsActive && _networkClient.IsConnected;

            var activeKeys = joystick.GetActiveKeys();
            manualControl.UpdateFromJoystick(activeKeys);
            manualControl.Tick(0.050);

            if (manualControl.IsLive)
            {
                byte bitfield = manualControl.GetKeyBitfield(activeKeys);
                _ = _networkClient.SendManipulatorCommandAsync(bitfield, manualControl.SpeedModifier);
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
                // Network selector only controls which interface the data usage graph monitors.
                // Rover is always resolved via mDNS (octane.local) — no IP to update.
                UpdateDataUsageGraphInterface();
            }
        }

        private void UpdateDataUsageGraphInterface()
        {
            var dataUsageGraph = this.FindControl<DataUsageGraph>("DataUsageGraph");
            if (dataUsageGraph == null || NetworkSelector.SelectedItem is not ComboBoxItem selectedItem)
                return;

            if (selectedItem.Tag is string tag)
                dataUsageGraph.SetNetworkInterface(tag);
        }

        private void PopulateNetworkSelector(string? preferredInterface = null)
        {
            NetworkSelector.Items.Clear();

            var interfaces = NetworkHelper.GetNetworkInterfaces();
            string? primaryName = NetworkHelper.GetPrimaryInterfaceName();
            string? target = preferredInterface ?? primaryName;

            int defaultIndex = 0;
            foreach (var (displayName, interfaceName) in interfaces)
            {
                int idx = NetworkSelector.Items.Count;
                NetworkSelector.Items.Add(new ComboBoxItem
                {
                    Content = displayName,
                    Tag = interfaceName
                });
                if (interfaceName == target)
                    defaultIndex = idx;
            }

            NetworkSelector.SelectedIndex = interfaces.Count > 0 ? defaultIndex : -1;
            Console.WriteLine($"[MainView] Network selector: {interfaces.Count} adapter(s), selected={target}");

            UpdateDataUsageGraphInterface();
        }

        private void ConnectButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_networkClient.IsConnected)
            {
                _networkClient.Disconnect();
            }
            else
            {
                var addressBox = this.FindControl<Avalonia.Controls.TextBox>("RoverAddressInput");
                string address = addressBox?.Text?.Trim() ?? "octane.local";
                if (string.IsNullOrWhiteSpace(address)) address = "octane.local";

                UpdateConnectButton(null);
                _ = _networkClient.ConnectAsync(address);
            }
        }

        private void UpdateConnectButton(bool? connected)
        {
            if (connected == null)
            {
                ConnectButton.Content = "Connecting...";
                ConnectButton.IsEnabled = false;
                ConnectButton.Background = Avalonia.Media.Brush.Parse("#555555");
            }
            else if (connected == true)
            {
                ConnectButton.Content = "Disconnect";
                ConnectButton.IsEnabled = true;
                ConnectButton.Background = Avalonia.Media.Brush.Parse("#DC2626");
            }
            else
            {
                ConnectButton.Content = "Connect";
                ConnectButton.IsEnabled = true;
                ConnectButton.Background = Avalonia.Media.Brush.Parse("#00643C");
            }
        }

        private void ResetImuButton_Click(object? sender, RoutedEventArgs e)
        {
            _mainViewModel.ResetImu();
        }

        public void HandleKeyDown(Key key)
        {
            var joystick      = this.FindControl<Controls.JoystickControl>("KeyTrackingJoystick");
            var manualControl = this.FindControl<ManualControl>("ManualControlCard");
            joystick?.HandleKeyDown(key);
            manualControl?.HandleKeyDown(key);
        }

        public void HandleKeyUp(Key key)
        {
            var joystick      = this.FindControl<Controls.JoystickControl>("KeyTrackingJoystick");
            var manualControl = this.FindControl<ManualControl>("ManualControlCard");
            joystick?.HandleKeyUp(key);
            manualControl?.HandleKeyUp(key);
        }
    }
}
