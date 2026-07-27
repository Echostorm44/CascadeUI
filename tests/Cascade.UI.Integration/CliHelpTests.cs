using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// Help output for every <c>cascade mcp</c> verb must print without any
/// running instance (WP-3502 acceptance criterion). These tests run the real
/// CLI executable; per-verb help is handled before instance discovery, so the
/// presence or absence of a live app cannot affect the result.
/// </summary>
[NotInParallel("CliIntegration")]
public class CliHelpTests
{
    private static readonly string[] AllVerbs =
    [
        "info", "tree", "screenshot", "click", "drag", "focus",
        "type", "scroll", "inspect", "find", "accessibility", "diagnostics",
    ];

    [Test]
    public async Task McpHelp_PrintsSubcommands_ExitZero()
    {
        var result = await CliTestHarness.RunCliAsync("mcp", "--help");
        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.StdOut).Contains("Subcommands:");
    }

    [Test]
    public async Task EveryVerb_Help_PrintsUsage_ExitZero()
    {
        foreach (string verb in AllVerbs)
        {
            var result = await CliTestHarness.RunCliAsync("mcp", verb, "--help");
            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(result.StdOut).Contains($"cascade mcp {verb}");
        }
    }

    [Test]
    public async Task EveryVerb_ShortHelp_ExitZero()
    {
        foreach (string verb in AllVerbs)
        {
            var result = await CliTestHarness.RunCliAsync("mcp", verb, "-h");
            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(result.StdOut).Contains("Usage:");
        }
    }

    [Test]
    public async Task UnknownVerb_Help_ExitNonZero()
    {
        var result = await CliTestHarness.RunCliAsync("mcp", "nonsense", "--help");
        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(result.StdErr).Contains("Unknown subcommand");
    }
}
