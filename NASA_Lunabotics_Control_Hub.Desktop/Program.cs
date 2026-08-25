using System;
using System.IO;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.ReactiveUI;

namespace NASA_Lunabotics_Control_Hub.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        RegisterNativeLibraryResolver();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // ppy.SDL2-CS ships a native SDL2 only for Windows; on macOS the loader must
    // find Homebrew's libSDL2, which lives outside the default dlopen search
    // path. Redirect just the SDL2 probe to the Homebrew locations so the app
    // runs without a DYLD_LIBRARY_PATH wrapper. No-op on other platforms.
    private static void RegisterNativeLibraryResolver()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        NativeLibrary.SetDllImportResolver(typeof(SDL2.SDL).Assembly, (name, assembly, searchPath) =>
        {
            if (!name.Contains("SDL2", StringComparison.OrdinalIgnoreCase))
                return IntPtr.Zero;

            // arm64 Homebrew first, then Intel Homebrew.
            foreach (var candidate in new[] { "/opt/homebrew/lib/libSDL2.dylib", "/usr/local/lib/libSDL2.dylib" })
            {
                if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
                    return handle;
            }

            // Fall back to the default resolver (e.g. libSDL2 already on the path).
            return IntPtr.Zero;
        });
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
