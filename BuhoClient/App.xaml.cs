using System.Configuration;
using System.Data;
using System.Windows;
using System;

namespace BuhoClient;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);
            Console.WriteLine("BuhoClient application starting...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during startup: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            MessageBox.Show($"Application startup error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Console.WriteLine($"BuhoClient application exiting with code: {e.ApplicationExitCode}");
        base.OnExit(e);
    }
}

