using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
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
        private byte? _activeStreamSourceId;
        private Bitmap? _currentFrame;

        // IMU
        private float _velX, _velY, _velZ;
        private DateTime _lastAccelTime = DateTime.MinValue;
        private string _accelXText = "--";
        private string _accelYText = "--";
        private string _accelZText = "--";
        private string _velXText = "--";
        private string _velYText = "--";
        private string _velZText = "--";

        public string AccelXText { get => _accelXText; private set => this.RaiseAndSetIfChanged(ref _accelXText, value); }
        public string AccelYText { get => _accelYText; private set => this.RaiseAndSetIfChanged(ref _accelYText, value); }
        public string AccelZText { get => _accelZText; private set => this.RaiseAndSetIfChanged(ref _accelZText, value); }
        public string VelXText  { get => _velXText;  private set => this.RaiseAndSetIfChanged(ref _velXText,  value); }
        public string VelYText  { get => _velYText;  private set => this.RaiseAndSetIfChanged(ref _velYText,  value); }
        public string VelZText  { get => _velZText;  private set => this.RaiseAndSetIfChanged(ref _velZText,  value); }

        public void UpdateAccel(float ax, float ay, float az)
        {
            var now = DateTime.UtcNow;
            if (_lastAccelTime != DateTime.MinValue)
            {
                float dt = (float)(now - _lastAccelTime).TotalSeconds;
                _velX += ax * dt;
                _velY += ay * dt;
                _velZ += az * dt;
            }
            _lastAccelTime = now;

            AccelXText = $"{ax:+0.000;-0.000}";
            AccelYText = $"{ay:+0.000;-0.000}";
            AccelZText = $"{az:+0.000;-0.000}";
            VelXText   = $"{_velX:+0.000;-0.000}";
            VelYText   = $"{_velY:+0.000;-0.000}";
            VelZText   = $"{_velZ:+0.000;-0.000}";
        }

        public void ResetImu()
        {
            _velX = _velY = _velZ = 0;
            _lastAccelTime = DateTime.MinValue;
            AccelXText = "--"; AccelYText = "--"; AccelZText = "--";
            VelXText   = "--"; VelYText   = "--"; VelZText   = "--";
        }

        // Localization (marker L)
        private DateTime _lastPoseTime = DateTime.MinValue;
        private string _poseXText = "--";
        private string _poseYText = "--";
        private string _poseThetaText = "--";
        private bool _hasPose = false;

        public string PoseXText     { get => _poseXText;     private set => this.RaiseAndSetIfChanged(ref _poseXText,     value); }
        public string PoseYText     { get => _poseYText;     private set => this.RaiseAndSetIfChanged(ref _poseYText,     value); }
        public string PoseThetaText { get => _poseThetaText; private set => this.RaiseAndSetIfChanged(ref _poseThetaText, value); }
        public bool   HasPose       { get => _hasPose;       private set => this.RaiseAndSetIfChanged(ref _hasPose,       value); }

        public void UpdatePose(float x, float y, float theta)
        {
            _lastPoseTime = DateTime.UtcNow;
            PoseXText     = $"{x:F2}";
            PoseYText     = $"{y:F2}";
            PoseThetaText = $"{theta:F3}";
            HasPose       = true;
        }

        public void ClearPoseIfStale(double timeoutSeconds = 3.0)
        {
            if (_lastPoseTime == DateTime.MinValue) return;
            if ((DateTime.UtcNow - _lastPoseTime).TotalSeconds > timeoutSeconds)
            {
                _lastPoseTime = DateTime.MinValue;
                PoseXText = "--"; PoseYText = "--"; PoseThetaText = "--";
                HasPose = false;
            }
        }

        // AprilTag observations (marker G)
        private DateTime _lastTagTime = DateTime.MinValue;
        private string _tag1Label = ""; private string _tag1Stats = ""; private bool _tag1Visible = false;
        private string _tag2Label = ""; private string _tag2Stats = ""; private bool _tag2Visible = false;
        private string _tag3Label = ""; private string _tag3Stats = ""; private bool _tag3Visible = false;

        public string Tag1Label   { get => _tag1Label;   private set => this.RaiseAndSetIfChanged(ref _tag1Label,   value); }
        public string Tag1Stats   { get => _tag1Stats;   private set => this.RaiseAndSetIfChanged(ref _tag1Stats,   value); }
        public string Tag2Label   { get => _tag2Label;   private set => this.RaiseAndSetIfChanged(ref _tag2Label,   value); }
        public string Tag2Stats   { get => _tag2Stats;   private set => this.RaiseAndSetIfChanged(ref _tag2Stats,   value); }
        public string Tag3Label   { get => _tag3Label;   private set => this.RaiseAndSetIfChanged(ref _tag3Label,   value); }
        public string Tag3Stats   { get => _tag3Stats;   private set => this.RaiseAndSetIfChanged(ref _tag3Stats,   value); }
        public bool   Tag1Visible { get => _tag1Visible; private set { this.RaiseAndSetIfChanged(ref _tag1Visible, value); this.RaisePropertyChanged(nameof(NoTagsVisible)); } }
        public bool   Tag2Visible { get => _tag2Visible; private set { this.RaiseAndSetIfChanged(ref _tag2Visible, value); this.RaisePropertyChanged(nameof(NoTagsVisible)); } }
        public bool   Tag3Visible { get => _tag3Visible; private set { this.RaiseAndSetIfChanged(ref _tag3Visible, value); this.RaisePropertyChanged(nameof(NoTagsVisible)); } }
        public bool   NoTagsVisible => !_tag1Visible && !_tag2Visible && !_tag3Visible;

        public void UpdateTags(byte count, byte[] ids, float[] dists, float[] angles)
        {
            _lastTagTime = DateTime.UtcNow;
            Tag1Visible = count >= 1;
            Tag2Visible = count >= 2;
            Tag3Visible = count >= 3;
            if (count >= 1) { Tag1Label = $"TAG #{ids[0]}"; Tag1Stats = $"{dists[0]:F2}m   {angles[0]:F1}°"; }
            if (count >= 2) { Tag2Label = $"TAG #{ids[1]}"; Tag2Stats = $"{dists[1]:F2}m   {angles[1]:F1}°"; }
            if (count >= 3) { Tag3Label = $"TAG #{ids[2]}"; Tag3Stats = $"{dists[2]:F2}m   {angles[2]:F1}°"; }
        }

        public void ClearTagsIfStale(double timeoutSeconds = 3.0)
        {
            if (_lastTagTime == DateTime.MinValue) return;
            if ((DateTime.UtcNow - _lastTagTime).TotalSeconds > timeoutSeconds)
            {
                _lastTagTime = DateTime.MinValue;
                Tag1Visible = false; Tag2Visible = false; Tag3Visible = false;
            }
        }

        public void ResetLocalization()
        {
            _lastPoseTime = DateTime.MinValue;
            PoseXText = "--"; PoseYText = "--"; PoseThetaText = "--";
            HasPose = false;
            _lastTagTime = DateTime.MinValue;
            Tag1Visible = false; Tag2Visible = false; Tag3Visible = false;
        }

        public void SetConnected(bool connected) { IsConnected = connected; }
        public void SetCurrentMode(string mode) { CurrentMode = mode; }

        private bool _isConnected = false;
        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                this.RaiseAndSetIfChanged(ref _isConnected, value);
                this.RaisePropertyChanged(nameof(ConnectionStatusText));
                this.RaisePropertyChanged(nameof(ConnectionColor));
            }
        }

        public string ConnectionStatusText => IsConnected ? "CONNECTED" : "OFFLINE";
        public string ConnectionColor => IsConnected ? "#00643C" : "#DC2626";

        public string ActiveViewport
        {
            get => _activeViewport;
            private set => this.RaiseAndSetIfChanged(ref _activeViewport, value);
        }

        public byte? ActiveStreamSourceId
        {
            get => _activeStreamSourceId;
            private set => this.RaiseAndSetIfChanged(ref _activeStreamSourceId, value);
        }

        public Bitmap? CurrentFrame
        {
            get => _currentFrame;
            set
            {
                this.RaiseAndSetIfChanged(ref _currentFrame, value);
                this.RaisePropertyChanged(nameof(HasActiveStream));
            }
        }

        public bool HasActiveStream => _currentFrame != null;

        public event Action<byte>? VideoStreamRequested;

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

        public void RequestVideoStream(byte sourceId)
        {
            ActiveStreamSourceId = sourceId;
            VideoStreamRequested?.Invoke(sourceId);
        }

        public void StopStream()
        {
            ActiveStreamSourceId = null;
            CurrentFrame = null;
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