using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
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
            Browser.CoreWebView2.NavigationStarting += OnNavigationStarting;
            Browser.CoreWebView2.DownloadStarting += OnDownloadStarting;
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

    private async void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var requestUri)) return;

        var downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var report = DesktopReportDownload.TryCreate(requestUri, downloadsFolder);
        if (report is null) return;

        e.Cancel = true;
        Directory.CreateDirectory(downloadsFolder);
        var destination = report.DestinationPath;
        if (File.Exists(destination))
        {
            destination = Path.Combine(downloadsFolder, $"{Path.GetFileNameWithoutExtension(destination)}-{DateTime.Now:yyyyMMdd-HHmmss}{Path.GetExtension(destination)}");
        }

        try
        {
            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(requestUri);
            await File.WriteAllBytesAsync(destination, bytes);
            MessageBox.Show($"บันทึกรายงานแล้ว\n{destination}", "ดาวน์โหลด PDF", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"ไม่สามารถดาวน์โหลดรายงานได้\n\n{exception.Message}", "ดาวน์โหลด PDF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        var downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(downloadsFolder);
        var destination = DesktopDownloadPath.Create(e.ResultFilePath, downloadsFolder);
        if (File.Exists(destination))
        {
            destination = Path.Combine(downloadsFolder, $"{Path.GetFileNameWithoutExtension(destination)}-{DateTime.Now:yyyyMMdd-HHmmss}{Path.GetExtension(destination)}");
        }
        e.ResultFilePath = destination;
        e.Handled = true;
        e.DownloadOperation.StateChanged += (_, _) =>
        {
            if (e.DownloadOperation.State == CoreWebView2DownloadState.Completed)
            {
                Dispatcher.BeginInvoke(() => MessageBox.Show($"บันทึกรายงานแล้ว\n{destination}", "ดาวน์โหลด PDF", MessageBoxButton.OK, MessageBoxImage.Information));
            }
        };
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
