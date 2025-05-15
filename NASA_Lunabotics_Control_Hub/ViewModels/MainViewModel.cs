using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using NASA_Lunabotics_Control_Hub.Components;
using ReactiveUI;

namespace NASA_Lunabotics_Control_Hub.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private Vector _joystickPosition;

        public Vector JoystickPosition
        {
            get => _joystickPosition;
            set => this.RaiseAndSetIfChanged(ref _joystickPosition, value);
        }
    }

    public class JoystickViewModel : INotifyPropertyChanged
    {
        private readonly UDP_Client _udpClient;
        private readonly MainViewModel _mainViewModel;
        private const int SCALAR_POS = 99;

        public JoystickViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _udpClient = new UDP_Client();
            _udpClient.SetModel(this);
            _sendString = "+00+00+00+00"; // Initialize to prevent null
            BucketControlState = 1; // Default to neutral
            _mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
        }

        private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e?.PropertyName == nameof(MainViewModel.JoystickPosition))
            {
                UpdateFromPosition(_mainViewModel.JoystickPosition, joystickId: 1);
            }
        }

        private int _xCoord_1;
        private int _yCoord_1;
        private int _xCoord_2;
        private int _yCoord_2;
        private int _bucketControlState;
        private string _sendString;
        private string _byteString;

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

        public string ByteString
        {
            get => _byteString;
            set
            {
                if (_byteString != value)
                {
                    _byteString = value;
                    OnPropertyChanged();
                    _udpClient.SendByteStringAsync(_byteString);
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
                    ByteString = UDP_Client.ConvertJoystickString(SendString, BucketControlState);
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
                    ByteString = UDP_Client.ConvertJoystickString(SendString, BucketControlState);
                }
            }
        }

        public int BucketControlState
        {
            get => _bucketControlState;
            set
            {
                if (_bucketControlState != value)
                {
                    _bucketControlState = value;
                    OnPropertyChanged();
                    ByteString = UDP_Client.ConvertJoystickString(SendString, BucketControlState);
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