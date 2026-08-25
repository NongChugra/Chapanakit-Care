using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ChapanakitCare.Desktop;

public partial class MainWindow : Window
{
    private const string LocalUrl = "http://127.0.0.1:5188";
    private Process? localServer;

    public MainWindow()
    {
        InitializeComponent();
        Browser.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = DesktopBrowserProfile.GetPath()
        };
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var plan = DesktopServerLaunchPlan.Create(Path.Combine(AppContext.BaseDirectory, "ChapanakitCare.Desktop.exe"));
            if (!File.Exists(plan.FileName))
            {
                throw new FileNotFoundException("The local Chapanakit Care server was not found beside the desktop application.", plan.FileName);
            }

            var startInfo = new ProcessStartInfo(plan.FileName)
            {
                UseShellExecute = false,
                WorkingDirectory = AppContext.BaseDirectory
            };
            foreach (var variable in plan.EnvironmentVariables)
            {
                startInfo.Environment[variable.Key] = variable.Value;
            }

            localServer = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The local Chapanakit Care server could not be started.");

            await Browser.EnsureCoreWebView2Async();
            Browser.CoreWebView2.Navigate(LocalUrl);
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(DesktopBrowserProfile.GetPath());
            File.WriteAllText(Path.Combine(DesktopBrowserProfile.GetPath(), "startup-error.log"), exception.ToString());
            MessageBox.Show(
                $"Chapanakit Care could not start.\n\n{exception.Message}",
                "Chapanakit Care",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (localServer is { HasExited: false })
        {
            localServer.Kill(entireProcessTree: true);
        }

        localServer?.Dispose();
    }
}
