using Avalonia.Controls;
using Avalonia.Input;
using NASA_Lunabotics_Control_Hub.Controls;
using NASA_Lunabotics_Control_Hub.Views;

namespace NASA_Lunabotics_Control_Hub.Views
{
    public partial class MainWindow : Window
    {
        private MainView _mainView;

        public MainWindow()
        {
            InitializeComponent();
            _mainView = Content as MainView;
            this.AddHandler(InputElement.KeyDownEvent, MainWindow_KeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            this.AddHandler(InputElement.KeyUpEvent, MainWindow_KeyUp, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }

        private void InitializeComponent()
        {
            Content = new MainView();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (_mainView != null)
            {
                _mainView.HandleKeyDown(e.Key);

                var joystick1 = _mainView.FindControl<JoystickControl>("Joystick1");
                var joystick2 = _mainView.FindControl<JoystickControl>("Joystick2");

                joystick1?.HandleKeyDown(e.Key);
                joystick2?.HandleKeyDown(e.Key);
            }
            if (e.Key is Key.W or Key.A or Key.S or Key.D
                      or Key.I or Key.J or Key.K or Key.L)
                e.Handled = true;
        }

        private void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (_mainView != null)
            {
                _mainView.HandleKeyUp(e.Key);

                var joystick1 = _mainView.FindControl<JoystickControl>("Joystick1");
                var joystick2 = _mainView.FindControl<JoystickControl>("Joystick2");

                joystick1?.HandleKeyUp(e.Key);
                joystick2?.HandleKeyUp(e.Key);
            }
            if (e.Key is Key.W or Key.A or Key.S or Key.D
                      or Key.I or Key.J or Key.K or Key.L)
                e.Handled = true;
        }
    }
}
