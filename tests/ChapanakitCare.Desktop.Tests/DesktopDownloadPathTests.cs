using ChapanakitCare.Desktop;

namespace ChapanakitCare.Desktop.Tests;

public sealed class DesktopDownloadPathTests
{
    [Fact]
    public void All_members_report_url_is_saved_with_the_official_file_name()
    {
        var request = DesktopReportDownload.TryCreate(
            new Uri("http://127.0.0.1:5188/Reports?handler=AllMembers"),
            @"C:\Users\Demo\Downloads");

        Assert.NotNull(request);
        Assert.Equal(@"C:\Users\Demo\Downloads\all-members.pdf", request.DestinationPath);
    }

    [Fact]
    public void Report_download_path_uses_the_user_downloads_folder()
    {
        var path = DesktopDownloadPath.Create("all-members.pdf", @"C:\Users\Demo\Downloads");

        Assert.Equal(@"C:\Users\Demo\Downloads\all-members.pdf", path);
    }
}
