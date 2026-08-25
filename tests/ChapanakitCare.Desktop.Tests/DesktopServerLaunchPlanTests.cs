using ChapanakitCare.Desktop;

namespace ChapanakitCare.Desktop.Tests;

public sealed class DesktopServerLaunchPlanTests
{
    [Fact]
    public void Embedded_browser_profile_is_outside_the_application_folder()
    {
        var profile = DesktopBrowserProfile.GetPath();

        Assert.EndsWith(Path.Combine("ChapanakitCare", "WebView2"), profile, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("artifacts", profile, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sibling_web_host_is_started_without_opening_a_browser()
    {
        var plan = DesktopServerLaunchPlan.Create(@"C:\Apps\ChapanakitCare.Desktop.exe");

        Assert.Equal(@"C:\Apps\ChapanakitCare.exe", plan.FileName);
        Assert.Equal("1", plan.EnvironmentVariables["CHAPANAKIT_NO_BROWSER"]);
    }
}
