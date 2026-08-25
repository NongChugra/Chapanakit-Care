using System;
using System.Collections.Generic;
using System.IO;

namespace ChapanakitCare.Desktop;

public sealed record DesktopServerLaunchPlan(
    string FileName,
    IReadOnlyDictionary<string, string> EnvironmentVariables)
{
    public static DesktopServerLaunchPlan Create(string desktopExecutablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(desktopExecutablePath);

        var directory = Path.GetDirectoryName(desktopExecutablePath)
            ?? throw new ArgumentException("The desktop executable must have a directory.", nameof(desktopExecutablePath));

        return new DesktopServerLaunchPlan(
            Path.Combine(directory, "ChapanakitCare.exe"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CHAPANAKIT_NO_BROWSER"] = "1"
            });
    }
}
