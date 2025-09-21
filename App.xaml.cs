using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel; // MainThread
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace CryptoPulse;

[SupportedOSPlatform("windows10.0.17763.0")]
public partial class App : Application
{
    // Path for the runtime log file (stored in temp folder)
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "cryptopulse.log");

    // Quick static logger method so I can write anywhere without setting up logging frameworks
    public static void Log(string msg)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        }
        catch
        {
            // If writing fails (e.g., file locked) just ignore it
        }
    }

    public App()
    {
        InitializeComponent();
        Log("[BOOT] App .ctor");

        // Catch any global unhandled exceptions and dump them into log
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            Log($"[FATAL] Unhandled: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (s, e) =>
            Log($"[ERROR] Unobserved: {e.Exception}");

#if WINDOWS
        // Extra handler for WinUI-specific exceptions
        Microsoft.UI.Xaml.Application.Current.UnhandledException += (s, e) =>
            Log($"[FATAL] WinUI: {e.Exception}");
#endif
    }

    // Main entry point for the app window
    protected override Window CreateWindow(IActivationState? activationState)
    {
        Log("[BOOT] CreateWindow begin");

        var win = new Window
        {
            Title = "CryptoPulse"
        };

        try
        {
            // Make sure dependency injection container is alive
            if (AppHost.Services == null)
            {
                Log("[BOOT] AppHost.Services is null.");
                win.Page = CreateDiagnosticsPage(
                    "⚠️ Dependency injection not initialized.\n" +
                    "Check MauiProgram.cs → AppHost.Initialize(app.Services);"
                );
                return win;
            }

            Log("[BOOT] Resolving MainPage…");
            var page = AppHost.Services.GetService(typeof(MainPage)) as Page;

            // If MainPage failed to resolve, show diagnostics page instead
            if (page == null)
            {
                Log("[BOOT] MainPage service resolution returned null.");
                win.Page = CreateDiagnosticsPage(
                    "⚠️ Failed to resolve MainPage from DI.\n" +
                    "Check MauiProgram.cs service registrations."
                );
                return win;
            }

            Log("[BOOT] MainPage loaded.");
            win.Page = page;
        }
        catch (Exception ex)
        {
            // If something blows up completely, show a fallback diagnostics page
            Log($"[BOOT] TryLoadMainPage error: {ex}");
            win.Page = CreateDiagnosticsPage($"❌ Error loading MainPage:\n{ex.Message}");
        }

        return win;
    }

    // Builds a simple diagnostics page with log location and button to open folder
    private ContentPage CreateDiagnosticsPage(string message)
    {
        var status = new Label
        {
            Text = message,
            TextColor = Colors.White,
            FontSize = 14
        };
        var info = new Label
        {
            Text = $"Log: {LogPath}",
            TextColor = Colors.Silver,
            FontSize = 12
        };

        var openLogBtn = new Button
        {
            Text = "Open log folder",
            BackgroundColor = Color.FromArgb("#1f2a44"),
            TextColor = Colors.White,
            CornerRadius = 10
        };
        openLogBtn.Clicked += (_, __) =>
        {
            try
            {
                var folder = Path.GetDirectoryName(LogPath)!;
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            catch (Exception ex) { Log($"[BOOT] Open log folder error: {ex}"); }
        };

        return new ContentPage
        {
            BackgroundColor = Color.FromArgb("#0b0f1a"),
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = "CryptoPulse — Diagnostics",
                        TextColor = Colors.White,
                        FontSize = 22,
                        FontAttributes = FontAttributes.Bold
                    },
                    status,
                    openLogBtn,
                    info
                }
            }
        };
    }
}
