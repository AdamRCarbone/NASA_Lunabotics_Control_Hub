using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using SDL2;
using System.Collections.Generic;

namespace NASA_Lunabotics_Control_Hub.Controls
{
    public partial class JoystickControl : UserControl
    {
        #region Fields
        private Border _boundBorder;
        private Ellipse _knob;
        private bool _isDragging;
        private Vector _position;
        private Point _center;
        private double _radius;
        private HashSet<Key> _activeKeys = new HashSet<Key>();
        private Vector _inputVelocity = Vector.Zero;
        private bool _isUsingKeyboard;
        private bool _isUsingGamepad;
        private IntPtr _controller = IntPtr.Zero;
        private DispatcherTimer _inputTimer;
        private Vector _mouseReleaseVelocity = Vector.Zero;
        private const double Acceleration = 0.15;
        private const float GamepadDeadzone = 0.1f;
        private const float GamepadCurveExponent = 1.5f;
        #endregion

        #region Enums
        public enum Shape { Circle, Square }
        #endregion

        #region Styled Properties
        public static readonly StyledProperty<Shape> BoundShapeProperty =
            AvaloniaProperty.Register<JoystickControl, Shape>(nameof(BoundShape), Shape.Circle);

        public static readonly StyledProperty<double> BoundSizeProperty =
            AvaloniaProperty.Register<JoystickControl, double>(nameof(BoundSize), 150.0);

        public static readonly StyledProperty<double> BoundBorderThicknessProperty =
            AvaloniaProperty.Register<JoystickControl, double>(nameof(BoundBorderThickness), 3.0);

        public static readonly StyledProperty<IBrush> BoundBorderBrushProperty =
            AvaloniaProperty.Register<JoystickControl, IBrush>(nameof(BoundBorderBrush), Brushes.Blue);

        public static readonly StyledProperty<IBrush> BoundBackgroundProperty =
            AvaloniaProperty.Register<JoystickControl, IBrush>(nameof(BoundBackground), Brushes.LightBlue);

        public static readonly StyledProperty<double> KnobSizeProperty =
            AvaloniaProperty.Register<JoystickControl, double>(nameof(KnobSize), 40.0);

        public static readonly StyledProperty<double> KnobBorderSizeProperty =
            AvaloniaProperty.Register<JoystickControl, double>(nameof(KnobBorderSize), 2.0);

        public static readonly StyledProperty<IBrush> KnobFillProperty =
            AvaloniaProperty.Register<JoystickControl, IBrush>(nameof(KnobFill), Brushes.DarkBlue);

        public static readonly StyledProperty<IBrush> KnobStrokeProperty =
            AvaloniaProperty.Register<JoystickControl, IBrush>(nameof(KnobStroke), Brushes.Black);

        public static readonly StyledProperty<double> XOffsetProperty =
            AvaloniaProperty.Register<JoystickControl, double>(nameof(XOffset), 0.0);

        public static readonly StyledProperty<double> YOffsetProperty =
            AvaloniaProperty.Register<JoystickControl, double>(nameof(YOffset), 0.0);

        public static readonly StyledProperty<double> SofteningProperty =
            AvaloniaProperty.Register<JoystickControl, double>(nameof(Softening), 0.2);

        public static readonly StyledProperty<Vector> PositionProperty =
            AvaloniaProperty.Register<JoystickControl, Vector>(nameof(Position), default(Vector));

        public static readonly StyledProperty<Vector> RawPositionProperty =
            AvaloniaProperty.Register<JoystickControl, Vector>(nameof(RawPosition), default(Vector));

        public static readonly StyledProperty<double> MaxSpeedProperty =
            AvaloniaProperty.Register<JoystickControl, double>(nameof(MaxSpeed), 0.05);

        public static readonly StyledProperty<double> DecayProperty =
            AvaloniaProperty.Register<JoystickControl, double>(nameof(Decay), 0.25);

        public static readonly StyledProperty<Key> UpKeyProperty =
            AvaloniaProperty.Register<JoystickControl, Key>(nameof(UpKey), Key.None);

        public static readonly StyledProperty<Key> DownKeyProperty =
            AvaloniaProperty.Register<JoystickControl, Key>(nameof(DownKey), Key.None);

        public static readonly StyledProperty<Key> LeftKeyProperty =
            AvaloniaProperty.Register<JoystickControl, Key>(nameof(LeftKey), Key.None);

        public static readonly StyledProperty<Key> RightKeyProperty =
            AvaloniaProperty.Register<JoystickControl, Key>(nameof(RightKey), Key.None);
        #endregion

        #region Property Accessors
        public Shape BoundShape
        {
            get => GetValue(BoundShapeProperty);
            set => SetValue(BoundShapeProperty, value);
        }

        public double BoundSize
        {
            get => GetValue(BoundSizeProperty);
            set => SetValue(BoundSizeProperty, value);
        }

        public double BoundBorderThickness
        {
            get => GetValue(BoundBorderThicknessProperty);
            set => SetValue(BoundBorderThicknessProperty, value);
        }

        public IBrush BoundBorderBrush
        {
            get => GetValue(BoundBorderBrushProperty);
            set => SetValue(BoundBorderBrushProperty, value);
        }

        public IBrush BoundBackground
        {
            get => GetValue(BoundBackgroundProperty);
            set => SetValue(BoundBackgroundProperty, value);
        }

        public double KnobSize
        {
            get => GetValue(KnobSizeProperty);
            set => SetValue(KnobSizeProperty, value);
        }

        public double KnobBorderSize
        {
            get => GetValue(KnobBorderSizeProperty);
            set => SetValue(KnobBorderSizeProperty, value);
        }

        public IBrush KnobFill
        {
            get => GetValue(KnobFillProperty);
            set => SetValue(KnobFillProperty, value);
        }

        public IBrush KnobStroke
        {
            get => GetValue(KnobStrokeProperty);
            set => SetValue(KnobStrokeProperty, value);
        }

        public double XOffset
        {
            get => GetValue(XOffsetProperty);
            set => SetValue(XOffsetProperty, value);
        }

        public double YOffset
        {
            get => GetValue(YOffsetProperty);
            set => SetValue(YOffsetProperty, value);
        }

        public double Softening
        {
            get => GetValue(SofteningProperty);
            set => SetValue(SofteningProperty, value);
        }

        public Vector Position
        {
            get => GetValue(PositionProperty);
            private set => SetValue(PositionProperty, value);
        }

        public Vector RawPosition
        {
            get => GetValue(RawPositionProperty);
            private set => SetValue(RawPositionProperty, value);
        }

        public double MaxSpeed
        {
            get => GetValue(MaxSpeedProperty);
            set => SetValue(MaxSpeedProperty, value);
        }

        public double Decay
        {
            get => GetValue(DecayProperty);
            set => SetValue(DecayProperty, value);
        }

        public Key UpKey
        {
            get => GetValue(UpKeyProperty);
            set => SetValue(UpKeyProperty, value);
        }

        public Key DownKey
        {
            get => GetValue(DownKeyProperty);
            set => SetValue(DownKeyProperty, value);
        }

        public Key LeftKey
        {
            get => GetValue(LeftKeyProperty);
            set => SetValue(LeftKeyProperty, value);
        }

        public Key RightKey
        {
            get => GetValue(RightKeyProperty);
            set => SetValue(RightKeyProperty, value);
        }
        #endregion

        #region Constructor
        public JoystickControl()
        {
            InitializeComponent();
            InitGamepad();
            _inputTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _inputTimer.Tick += InputTimer_Tick;
            _inputTimer.Start();
        }
        #endregion

        #region Initialization
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _boundBorder = this.FindControl<Border>("BoundBorder");
            _knob = this.FindControl<Ellipse>("Knob");

            if (_boundBorder != null && _knob != null)
            {
                _knob.PointerPressed += Knob_PointerPressed;
                _knob.PointerMoved += Knob_PointerMoved;
                _knob.PointerReleased += Knob_PointerReleased;

                this.GetObservable(BoundShapeProperty).Subscribe(_ => UpdateVisuals());
                this.GetObservable(BoundSizeProperty).Subscribe(_ => UpdateVisuals());
                this.GetObservable(BoundBorderThicknessProperty).Subscribe(_ => UpdateVisuals());
                this.GetObservable(BoundBorderBrushProperty).Subscribe(_ => UpdateVisuals());
                this.GetObservable(BoundBackgroundProperty).Subscribe(_ => UpdateVisuals());
                this.GetObservable(KnobSizeProperty).Subscribe(_ => UpdateVisuals());
                this.GetObservable(KnobBorderSizeProperty).Subscribe(_ => UpdateVisuals());
                this.GetObservable(KnobFillProperty).Subscribe(_ => UpdateVisuals());
                this.GetObservable(KnobStrokeProperty).Subscribe(_ => UpdateVisuals());
                this.GetObservable(XOffsetProperty).Subscribe(_ => UpdatePosition());
                this.GetObservable(YOffsetProperty).Subscribe(_ => UpdatePosition());

                UpdateVisuals();
            }
        }

        private void InitGamepad()
        {
            SDL.SDL_Init(SDL.SDL_INIT_GAMECONTROLLER);
            for (int i = 0; i < SDL.SDL_NumJoysticks(); ++i)
            {
                if (SDL.SDL_IsGameController(i) == SDL.SDL_bool.SDL_TRUE)
                {
                    _controller = SDL.SDL_GameControllerOpen(i);
                    break;
                }
            }
        }
        #endregion

        #region Visual Updates
        private void UpdateVisuals()
        {
            if (_boundBorder == null || _knob == null)
                return;

            _boundBorder.Width = _boundBorder.Height = BoundSize;
            _boundBorder.BorderThickness = new Thickness(BoundBorderThickness);
            _boundBorder.BorderBrush = BoundBorderBrush;
            _boundBorder.Background = BoundBackground;
            _radius = (BoundSize - BoundBorderThickness - KnobSize / 2) / 2;

            if (BoundShape == Shape.Circle)
            {
                _boundBorder.CornerRadius = new CornerRadius(BoundSize / 2);
            }
            else
            {
                _boundBorder.CornerRadius = new CornerRadius(0);
            }

            _knob.Width = _knob.Height = KnobSize;
            _knob.StrokeThickness = KnobBorderSize;
            _knob.Fill = KnobFill;
            _knob.Stroke = KnobStroke;

            _center = new Point(BoundSize / 2, BoundSize / 2);
            UpdatePosition();
        }
        #endregion

        #region Input Handling
        private void Knob_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                _inputVelocity = Vector.Zero;
                _mouseReleaseVelocity = Vector.Zero;
                UpdateKnobPosition(e.GetPosition(_boundBorder));
                e.Handled = true;
            }
        }

        private void Knob_PointerMoved(object sender, PointerEventArgs e)
        {
            if (_isDragging)
            {
                UpdateKnobPosition(e.GetPosition(_boundBorder));
                e.Handled = true;
            }
        }

        private void Knob_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _isDragging = false;
            _mouseReleaseVelocity = Vector.Zero;
            e.Handled = true;
        }

        public void HandleKeyDown(Key key)
        {
            if (_activeKeys.Add(key) && (key == UpKey || key == DownKey || key == LeftKey || key == RightKey))
            {
                _isUsingKeyboard = true;
                _mouseReleaseVelocity = Vector.Zero;
            }
        }

        public void HandleKeyUp(Key key)
        {
            if (_activeKeys.Remove(key))
            {
                if (_activeKeys.Count == 0)
                {
                    _isUsingKeyboard = false;
                    _inputVelocity = Vector.Zero;
                }
            }
        }

        private Vector? GetGamepadInput()
        {
            if (_controller == IntPtr.Zero)
                return null;

            SDL.SDL_GameControllerUpdate();

            short axisX = SDL.SDL_GameControllerGetAxis(_controller, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX);
            short axisY = SDL.SDL_GameControllerGetAxis(_controller, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY);

            float normX = axisX / 32768f;
            float normY = axisY / 32768f;

            float magnitude = (float)Math.Sqrt(normX * normX + normY * normY);
            if (magnitude < GamepadDeadzone)
            {
                _isUsingGamepad = false;
                return Vector.Zero;
            }

            if (magnitude > 1.0f)
            {
                normX /= magnitude;
                normY /= magnitude;
                magnitude = 1.0f;
            }
            float scaledMagnitude = (float)Math.Pow(magnitude, GamepadCurveExponent);
            normX *= scaledMagnitude / magnitude;
            normY *= scaledMagnitude / magnitude;

            _isUsingGamepad = true;
            return new Vector(Math.Round(normX, 3), -Math.Round(normY, 3));
        }
        #endregion

        #region Position Updates
        private void UpdateKnobPosition(Point mousePos)
        {
            var offset = mousePos - _center;
            double length = Math.Sqrt(offset.X * offset.X + offset.Y * offset.Y);

            RawPosition = new Vector(Math.Round(_position.X, 3), Math.Round(_position.Y, 3));

            if (BoundShape == Shape.Square)
            {
                offset = new Point(
                    Math.Clamp(offset.X, -_radius, _radius),
                    Math.Clamp(offset.Y, -_radius, _radius));
                length = Math.Sqrt(offset.X * offset.X + offset.Y * offset.Y);
            }

            if (length > _radius)
            {
                offset = new Point(
                    offset.X / length * _radius,
                    offset.Y / length * _radius);
            }

            _position = new Vector(
                Math.Round(offset.X / _radius, 3),
                Math.Round(-offset.Y / _radius, 3));
            UpdatePosition();
        }

        int Total_Joystick_Range = 1;
        int Round_Joystick_Decimals = 2;
        private void UpdatePosition()
        {
            var adjustedPosition = new Vector(
                Math.Round(_position.X * Total_Joystick_Range + XOffset, Round_Joystick_Decimals),
                Math.Round(_position.Y * Total_Joystick_Range + YOffset, Round_Joystick_Decimals));

            var knobPos = new Point(
                _center.X + _position.X * _radius,
                _center.Y - _position.Y * _radius);

            _knob.RenderTransform = new TranslateTransform
            {
                X = knobPos.X - BoundSize / 2,
                Y = knobPos.Y - BoundSize / 2
            };

            Position = adjustedPosition;
            RawPosition = new Vector(Math.Round(_position.X, 3), Math.Round(_position.Y, 3));
        }

        private static Vector VectorLerp(Vector a, Vector b, double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            return new Vector(
                Math.Round(a.X + (b.X - a.X) * t, 3),
                Math.Round(a.Y + (b.Y - a.Y) * t, 3));
        }
        #endregion

        #region Input Processing
        private void InputTimer_Tick(object sender, EventArgs e)
        {
            Vector input = Vector.Zero;

            if (_isUsingKeyboard)
            {
                double xInput = 0;
                double yInput = 0;

                if (_activeKeys.Contains(UpKey))
                    yInput += 1;
                if (_activeKeys.Contains(DownKey))
                    yInput -= 1;
                if (_activeKeys.Contains(LeftKey))
                    xInput -= 1;
                if (_activeKeys.Contains(RightKey))
                    xInput += 1;

                if (xInput != 0 || yInput != 0)
                {
                    double length = Math.Sqrt(xInput * xInput + yInput * yInput);
                    if (length > 1)
                    {
                        xInput /= length;
                        yInput /= length;
                    }
                    input = new Vector(Math.Round(xInput, 3), Math.Round(yInput, 3));
                }
            }

            var gamepadInput = GetGamepadInput();
            if (gamepadInput.HasValue && gamepadInput.Value != Vector.Zero)
            {
                input = gamepadInput.Value;
                _mouseReleaseVelocity = Vector.Zero;
            }

            if (input != Vector.Zero)
            {
                _inputVelocity = VectorLerp(_inputVelocity, input * MaxSpeed, Acceleration);
            }
            else
            {
                _inputVelocity = Vector.Zero;
            }

            if (!_isDragging && !_isUsingKeyboard && !_isUsingGamepad && _position != Vector.Zero)
            {
                _position *= (1.0 - Decay);
                if (_position.Length < 0.01)
                    _position = Vector.Zero;
                RawPosition = new Vector(Math.Round(_position.X, 3), Math.Round(_position.Y, 3));
                UpdatePosition();
            }

            Vector totalVelocity = _inputVelocity + _mouseReleaseVelocity;
            if (totalVelocity != Vector.Zero)
            {
                _position += totalVelocity;

                if (BoundShape == Shape.Circle)
                {
                    double length = _position.Length;
                    if (length > 1.0)
                    {
                        _position /= length;
                    }
                }
                else
                {
                    _position = new Vector(
                        Math.Clamp(Math.Round(_position.X, 3), -1.0, 1.0),
                        Math.Clamp(Math.Round(_position.Y, 3), -1.0, 1.0));
                }

                RawPosition = new Vector(Math.Round(_position.X, 3), Math.Round(_position.Y, 3));
                UpdatePosition();
            }
        }
        #endregion
    }
}