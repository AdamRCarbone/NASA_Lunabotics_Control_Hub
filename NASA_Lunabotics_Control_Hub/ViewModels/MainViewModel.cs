using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using NASA_Lunabotics_Control_Hub.Components;
using ReactiveUI;

namespace NASA_Lunabotics_Control_Hub.ViewModels
{
    public enum ModeState { Idle, Pending, Confirmed }

    public class MainViewModel : ViewModelBase
    {
        private Vector _joystickPosition;
        private string _currentMode = "";
        private ModeState _standbyStatus = ModeState.Idle;
        private ModeState _manualStatus = ModeState.Idle;
        private ModeState _autonomousStatus = ModeState.Idle;
        private ModeState _faultResetStatus = ModeState.Idle;
        private bool _isTransitioning = false;
        private string _activeViewport = "map";

        public void SetConnected(bool connected) { IsConnected = connected; }
        public void SetCurrentMode(string mode) { CurrentMode = mode; }
    private bool _isConnected = false;

        public bool IsConnected { get; private set; }

    public string ConnectionStatusText => IsConnected ? "CONNECTED" : "OFFLINE";
    public string ConnectionColor => IsConnected ? "#00643C" : "#DC2626"; // Green or Red

        public string ActiveViewport
        {
            get => _activeViewport;
            private set => this.RaiseAndSetIfChanged(ref _activeViewport, value);
        }

        public MainViewModel()
        {
        }

        public Vector JoystickPosition
        {
            get => _joystickPosition;
            set => this.RaiseAndSetIfChanged(ref _joystickPosition, value);
        }
        public string CurrentMode { get; private set; }
        public ModeState StandbyStatus
        {
            get => _standbyStatus;
            private set => this.RaiseAndSetIfChanged(ref _standbyStatus, value);
        }

        public ModeState ManualStatus
        {
            get => _manualStatus;
            private set => this.RaiseAndSetIfChanged(ref _manualStatus, value);
        }

        public ModeState AutonomousStatus
        {
            get => _autonomousStatus;
            private set => this.RaiseAndSetIfChanged(ref _autonomousStatus, value);
        }

        public ModeState FaultResetStatus
        {
            get => _faultResetStatus;
            private set => this.RaiseAndSetIfChanged(ref _faultResetStatus, value);
        }

        public bool IsTransitioning
        {
            get => _isTransitioning;
            private set => this.RaiseAndSetIfChanged(ref _isTransitioning, value);
        }

        public void OnModeSelected(string mode)
        {
            if (mode == _currentMode) return;

            // Set ALL modes to Idle first (grey dots, grey backgrounds)
            StandbyStatus = ModeState.Idle;
            ManualStatus = ModeState.Idle;
            AutonomousStatus = ModeState.Idle;
            FaultResetStatus = ModeState.Idle;

            // Set the selected mode to Pending (red LED, but dark green bg for selection)
            switch (mode)
            {
                case "Standby": StandbyStatus = ModeState.Pending; break;
                case "Manual": ManualStatus = ModeState.Pending; break;
                case "Autonomous": AutonomousStatus = ModeState.Pending; break;
                case "Fault Reset": FaultResetStatus = ModeState.Pending; break;
            }

            CurrentMode = mode;
        }

        public void SetModeState(string mode, ModeState state)
        {
            // Clear all states first
            StandbyStatus = ModeState.Idle;
            ManualStatus = ModeState.Idle;
            AutonomousStatus = ModeState.Idle;
            FaultResetStatus = ModeState.Idle;

            // Set the requested mode's state
            switch (mode)
            {
                case "Standby": StandbyStatus = state; break;
                case "Manual": ManualStatus = state; break;
                case "Autonomous": AutonomousStatus = state; break;
                case "Fault Reset": FaultResetStatus = state; break;
            }
        }

        public void OnViewportSelected(string viewportId)
        {
            ActiveViewport = viewportId;
        }
    }

    public class JoystickViewModel : INotifyPropertyChanged
    {
        private UDP_Client _udpClient;
        private MainViewModel _mainViewModel;
        private int SCALAR_POS = 99;

        public JoystickViewModel(MainViewModel mainViewModel)
        {
            _udpClient = new UDP_Client();
            _udpClient.SetModel(this); // Set the JoystickViewModel in UDP_Client
            _mainViewModel = mainViewModel;

            // Subscribe to MainViewModel's PropertyChanged event
            _mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
        }

        private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.JoystickPosition))
            {
                // Update coordinates based on JoystickPosition (assuming joystickId = 1 for simplicity)
                UpdateFromPosition(_mainViewModel.JoystickPosition, joystickId: 1);
            }
        }

        private int _xCoord_1;
        private int _yCoord_1;
        private int _xCoord_2;
        private int _yCoord_2;

        private string _sendString;
        public string SendString
        {
            get => _sendString;
            set
            {
                if (_sendString != value)
                {
                    _sendString = value;
                    OnPropertyChanged();
                }
            }
        }

        public int XCoord_1
        {
            get => _xCoord_1;
            set
            {
                if (_xCoord_1 != value)
                {
                    _xCoord_1 = value;
                    OnPropertyChanged();
                    Update_SendString();
                    _udpClient.SendPacketsForTesting();
                }
            }
        }

        public int YCoord_1
        {
            get => _yCoord_1;
            set
            {
                if (_yCoord_1 != value)
                {
                    _yCoord_1 = value;
                    OnPropertyChanged();
                    Update_SendString();
                    _udpClient.SendPacketsForTesting();
                }
            }
        }

        public int XCoord_2
        {
            get => _xCoord_2;
            set
            {
                if (_xCoord_2 != value)
                {
                    _xCoord_2 = value;
                    OnPropertyChanged();
                    Update_SendString();
                    _udpClient.SendPacketsForTesting();
                }
            }
        }

        public int YCoord_2
        {
            get => _yCoord_2;
            set
            {
                if (_yCoord_2 != value)
                {
                    _yCoord_2 = value;
                    OnPropertyChanged();
                    Update_SendString();
                    _udpClient.SendPacketsForTesting();
                }
            }
        }

        public void UpdateFromPosition(Vector position, int joystickId)
        {
            if (joystickId == 1)
            {
                XCoord_1 = (int)(position.X * SCALAR_POS);
                YCoord_1 = (int)(position.Y * SCALAR_POS);
            }
            else if (joystickId == 2)
            {
                XCoord_2 = (int)(position.X * SCALAR_POS);
                YCoord_2 = (int)(position.Y * SCALAR_POS);
            }
        }

        public void Update_SendString()
        {
            int x1 = Math.Clamp(XCoord_1, -99, 99);
            int y1 = Math.Clamp(YCoord_1, -99, 99);
            int x2 = Math.Clamp(XCoord_2, -99, 99);
            int y2 = Math.Clamp(YCoord_2, -99, 99);

            string x1Str = x1 >= 0 ? $"+{x1:D2}" : $"-{Math.Abs(x1):D2}";
            string y1Str = y1 >= 0 ? $"+{y1:D2}" : $"-{Math.Abs(y1):D2}";
            string x2Str = x2 >= 0 ? $"+{x2:D2}" : $"-{Math.Abs(x2):D2}";
            string y2Str = y2 >= 0 ? $"+{y2:D2}" : $"-{Math.Abs(y2):D2}";

            SendString = $"{x1Str}{y1Str}{x2Str}{y2Str}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}