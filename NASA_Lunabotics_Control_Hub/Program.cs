using System;
using Avalonia;
using Avalonia.ReactiveUI;

namespace NASA_Lunabotics_Control_Hub;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI();

        builder.StartWithClassicDesktopLifetime(args);
    }
}
