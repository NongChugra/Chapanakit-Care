using System.IO;

namespace ChapanakitCare.Desktop;

public static class DesktopDownloadPath
{
    public static string Create(string suggestedFileName, string downloadsFolder)
    {
        var fileName = Path.GetFileName(suggestedFileName);
        return Path.Combine(downloadsFolder, string.IsNullOrWhiteSpace(fileName) ? "report.pdf" : fileName);
    }
}
