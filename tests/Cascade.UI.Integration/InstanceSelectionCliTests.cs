using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// Two instances of one app register under the same name, so <c>--app Name</c> could only reach
/// whichever was focused. <c>info</c> now prints a <c>Name#pid</c> selector for each instance,
/// and <c>--app Name#pid</c> / <c>--app #pid</c> drive that process.
/// </summary>
[NotInParallel("CliIntegration")]
public class InstanceSelectionCliTests
{
    [Test]
    public async Task TwoInstancesOfOneApp_EachIsReachableByItsPid()
    {
        string appId = CliTestHarness.NewFixtureAppId();
        using var dialogs = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "dialogs" });
        using var menus = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "menus" });
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), dialogs.Id);
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), menus.Id);

            var info = await CliTestHarness.RunCliAsync("mcp", "info", "--app", appId);
            await Assert.That(info.StdOut).Contains($"Select:  --app {appId}#{dialogs.Id}");
            await Assert.That(info.StdOut).Contains($"Select:  --app {appId}#{menus.Id}");

            // "Delete clip" is a button only the dialogs view has. Each pid reaches its own window,
            // whichever of the two is focused.
            var inDialogs = await CliTestHarness.RunCliAsync("mcp", "find", "Delete clip", "--by", "label", "--app", $"{appId}#{dialogs.Id}");
            var inMenus = await CliTestHarness.RunCliAsync("mcp", "find", "Delete clip", "--by", "label", "--app", $"{appId}#{menus.Id}");
            var byBarePid = await CliTestHarness.RunCliAsync("mcp", "find", "Delete clip", "--by", "label", "--app", $"#{dialogs.Id}");

            await Assert.That(inDialogs.ExitCode).IsEqualTo(0);
            await Assert.That(inDialogs.StdOut).Contains("\"total_matches\":1");
            await Assert.That(inMenus.ExitCode).IsEqualTo(0);
            await Assert.That(inMenus.StdOut).Contains("\"total_matches\":0");
            await Assert.That(byBarePid.StdOut).Contains("\"total_matches\":1")
                .Because($"--app #{dialogs.Id} printed [{byBarePid.StdOut}] [{byBarePid.StdErr}]");

            // A pid that is not one of the app's instances is not silently replaced by another.
            var wrong = await CliTestHarness.RunCliAsync("mcp", "find", "Delete clip", "--app", $"{appId}#{int.MaxValue}");
            await Assert.That(wrong.ExitCode).IsNotEqualTo(0);
        }
        finally
        {
            dialogs.Kill();
            menus.Kill();
        }
    }
}
