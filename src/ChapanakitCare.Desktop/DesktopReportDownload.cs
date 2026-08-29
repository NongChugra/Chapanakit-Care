using System;
using System.IO;

namespace ChapanakitCare.Desktop;

public sealed record DesktopReportDownload(string DestinationPath)
{
    public static DesktopReportDownload? TryCreate(Uri requestUri, string downloadsFolder)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadsFolder);

        if (!string.Equals(requestUri.AbsolutePath, "/Reports", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(requestUri.Query, "?handler=AllMembers", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new DesktopReportDownload(Path.Combine(downloadsFolder, "all-members.pdf"));
    }
}
