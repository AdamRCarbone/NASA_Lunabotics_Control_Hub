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
            this.KeyDown += MainWindow_KeyDown;
            this.KeyUp += MainWindow_KeyUp;
        }

        private void InitializeComponent()
        {
            Content = new MainView();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (_mainView != null)
            {
                var joystick1 = _mainView.FindControl<JoystickControl>("Joystick1");
                var joystick2 = _mainView.FindControl<JoystickControl>("Joystick2");

                joystick1?.HandleKeyDown(e.Key);
                joystick2?.HandleKeyDown(e.Key);
            }
            e.Handled = true;
        }

        private void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (_mainView != null)
            {
                var joystick1 = _mainView.FindControl<JoystickControl>("Joystick1");
                var joystick2 = _mainView.FindControl<JoystickControl>("Joystick2");

                joystick1?.HandleKeyUp(e.Key);
                joystick2?.HandleKeyUp(e.Key);
            }
            e.Handled = true;
        }
    }
}