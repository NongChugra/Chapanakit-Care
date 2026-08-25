using System;
using System.IO;

namespace ChapanakitCare.Desktop;

public static class DesktopBrowserProfile
{
    public static string GetPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ChapanakitCare",
        "WebView2");
}
