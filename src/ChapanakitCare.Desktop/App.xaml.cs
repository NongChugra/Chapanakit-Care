using System;
using System.IO;
using System.Windows;

namespace ChapanakitCare.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            WriteStartupError(args.Exception);
            args.Handled = true;
            MessageBox.Show(args.Exception.Message, "Chapanakit Care", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        };
        try
        {
            MainWindow = new MainWindow();
            MainWindow.Show();
        }
        catch (Exception exception)
        {
            WriteStartupError(exception);
            MessageBox.Show(exception.Message, "Chapanakit Care", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static void WriteStartupError(Exception exception)
    {
        var directory = DesktopBrowserProfile.GetPath();
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "startup-error.log"), exception.ToString());
    }
}
