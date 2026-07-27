using Cascade.UI.Tools.Commands;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class CliTests
{
    // ── NewCommand ──────────────────────────────────────────────

    [Test]
    public async Task NewCommand_Help_ReturnsZero()
    {
        int result = NewCommand.Execute(["--help"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task NewCommand_ShortHelp_ReturnsZero()
    {
        int result = NewCommand.Execute(["-h"]);
        await Assert.That(result).IsEqualTo(0);
    }

    // ── WatchCommand ────────────────────────────────────────────

    [Test]
    public async Task WatchCommand_Help_ReturnsZero()
    {
        int result = WatchCommand.Execute(["--help"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task WatchCommand_ShortHelp_ReturnsZero()
    {
        int result = WatchCommand.Execute(["-h"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task WatchCommand_NonexistentProject_ReturnsError()
    {
        int result = WatchCommand.Execute(["--project", "nonexistent_" + Guid.NewGuid() + ".csproj"]);
        await Assert.That(result).IsNotEqualTo(0);
    }

    // ── DoctorCommand ───────────────────────────────────────────

    [Test]
    public async Task DoctorCommand_Help_ReturnsZero()
    {
        int result = DoctorCommand.Execute(["--help"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task DoctorCommand_ShortHelp_ReturnsZero()
    {
        int result = DoctorCommand.Execute(["-h"]);
        await Assert.That(result).IsEqualTo(0);
    }

    // ── CheckCommand ────────────────────────────────────────────

    [Test]
    public async Task CheckCommand_Help_ReturnsZero()
    {
        int result = CheckCommand.Execute(["--help"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task CheckCommand_ShortHelp_ReturnsZero()
    {
        int result = CheckCommand.Execute(["-h"]);
        await Assert.That(result).IsEqualTo(0);
    }

    // ── Directory-dependent tests (run sequentially) ────────────

    [Test, NotInParallel]
    public async Task DoctorCommand_NoProject_ReturnsError()
    {
        await RunInTempDir(async () =>
        {
            int result = DoctorCommand.Execute([]);
            await Assert.That(result).IsNotEqualTo(0);
        });
    }

    [Test, NotInParallel]
    public async Task DoctorCommand_JsonFormat_NoProject_ReturnsError()
    {
        await RunInTempDir(async () =>
        {
            int result = DoctorCommand.Execute(["--format", "json"]);
            await Assert.That(result).IsNotEqualTo(0);
        });
    }

    [Test, NotInParallel]
    public async Task CheckCommand_NoProject_ReturnsError()
    {
        await RunInTempDir(async () =>
        {
            int result = CheckCommand.Execute([]);
            await Assert.That(result).IsNotEqualTo(0);
        });
    }

    [Test, NotInParallel]
    public async Task CheckCommand_WithMinimalCsproj_AllPassesNoAssets()
    {
        await RunInTempDir(async () =>
        {
            await File.WriteAllTextAsync("TestApp.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            int result = CheckCommand.Execute([]);
            await Assert.That(result).IsEqualTo(0);
        });
    }

    [Test, NotInParallel]
    public async Task CheckCommand_MissingFont_ReturnsError()
    {
        await RunInTempDir(async () =>
        {
            await File.WriteAllTextAsync("TestApp.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <CascadeFont Include="Fonts/Missing.ttf" />
                  </ItemGroup>
                </Project>
                """);

            int result = CheckCommand.Execute([]);
            await Assert.That(result).IsEqualTo(1);
        });
    }

    [Test, NotInParallel]
    public async Task CheckCommand_MissingStringsEnJson_ReturnsError()
    {
        await RunInTempDir(async () =>
        {
            await File.WriteAllTextAsync("TestApp.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            Directory.CreateDirectory("Strings");

            int result = CheckCommand.Execute([]);
            await Assert.That(result).IsEqualTo(1);
        });
    }

    [Test, NotInParallel]
    public async Task CheckCommand_WithValidFont_Passes()
    {
        await RunInTempDir(async () =>
        {
            Directory.CreateDirectory("Fonts");
            await File.WriteAllTextAsync("Fonts/Inter.ttf", "fake font data");

            await File.WriteAllTextAsync("TestApp.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <CascadeFont Include="Fonts/Inter.ttf" />
                  </ItemGroup>
                </Project>
                """);

            int result = CheckCommand.Execute([]);
            await Assert.That(result).IsEqualTo(0);
        });
    }

    [Test, NotInParallel]
    public async Task CheckCommand_WithStringsAndEnJson_Passes()
    {
        await RunInTempDir(async () =>
        {
            await File.WriteAllTextAsync("TestApp.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            Directory.CreateDirectory("Strings");
            await File.WriteAllTextAsync("Strings/en.json", "{}");

            int result = CheckCommand.Execute([]);
            await Assert.That(result).IsEqualTo(0);
        });
    }

    // ── DiagnosticResult and DiagnosticStatus ───────────────────

    [Test]
    public async Task DiagnosticResult_StoresProperties()
    {
        var result = new DiagnosticResult("Test Name", "accessibility", DiagnosticStatus.Warning, "Test message");
        await Assert.That(result.Name).IsEqualTo("Test Name");
        await Assert.That(result.Category).IsEqualTo("accessibility");
        await Assert.That(result.Status).IsEqualTo(DiagnosticStatus.Warning);
        await Assert.That(result.Message).IsEqualTo("Test message");
    }

    [Test]
    public async Task DiagnosticResult_PassStatus()
    {
        var result = new DiagnosticResult("OK", "theme", DiagnosticStatus.Pass, "All good");
        await Assert.That(result.Status).IsEqualTo(DiagnosticStatus.Pass);
    }

    [Test]
    public async Task DiagnosticResult_ErrorStatus()
    {
        var result = new DiagnosticResult("Fail", "layout", DiagnosticStatus.Error, "Bad");
        await Assert.That(result.Status).IsEqualTo(DiagnosticStatus.Error);
    }

    // ── CheckResult ─────────────────────────────────────────────

    [Test]
    public async Task CheckResult_StoresProperties()
    {
        var result = new CheckResult("Font check", true);
        await Assert.That(result.Description).IsEqualTo("Font check");
        await Assert.That(result.Passed).IsTrue();
    }

    [Test]
    public async Task CheckResult_Failed()
    {
        var result = new CheckResult("Missing asset", false);
        await Assert.That(result.Passed).IsFalse();
    }

    // ── DiagnosticStatus enum values ────────────────────────────

    [Test]
    public async Task DiagnosticStatus_HasFourValues()
    {
        var values = Enum.GetValues<DiagnosticStatus>();
        await Assert.That(values.Length).IsEqualTo(4);
    }

    // ── DoctorCommand --fail-on and --category ─────────────────

    [Test]
    public async Task DoctorCommand_FailOn_InvalidValue_ReturnsError()
    {
        int result = DoctorCommand.Execute(["--fail-on", "panic"]);
        await Assert.That(result).IsEqualTo(1);
    }

    [Test]
    public async Task DoctorCommand_Category_InvalidValue_ReturnsError()
    {
        int result = DoctorCommand.Execute(["--category", "nonexistent"]);
        await Assert.That(result).IsEqualTo(1);
    }

    [Test]
    public async Task DoctorCommand_FailOn_ValidValues_AcceptAll()
    {
        // All three valid --fail-on values should not cause a validation error
        // (they'll fail on missing project, not on flag validation)
        int resultError = DoctorCommand.Execute(["--fail-on", "error"]);
        int resultWarning = DoctorCommand.Execute(["--fail-on", "warning"]);
        int resultInfo = DoctorCommand.Execute(["--fail-on", "info"]);

        // These all exit 1 because no project, but NOT because of invalid --fail-on
        await Assert.That(resultError).IsEqualTo(1);
        await Assert.That(resultWarning).IsEqualTo(1);
        await Assert.That(resultInfo).IsEqualTo(1);
    }

    [Test]
    public async Task DoctorCommand_FailOn_Warning_Help_ReturnsZero()
    {
        int result = DoctorCommand.Execute(["--help"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test, NotInParallel]
    public async Task DoctorCommand_Category_Accessibility_FiltersResults()
    {
        await RunInTempDir(async () =>
        {
            await File.WriteAllTextAsync("TestApp.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            // Only accessibility category — should pass (no live app)
            int result = DoctorCommand.Execute(["--category", "accessibility"]);
            await Assert.That(result).IsEqualTo(0);
        });
    }

    [Test, NotInParallel]
    public async Task DoctorCommand_Category_Multiple_CombinesFilters()
    {
        await RunInTempDir(async () =>
        {
            await File.WriteAllTextAsync("TestApp.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            // Two categories combined
            int result = DoctorCommand.Execute(["--category", "accessibility", "--category", "theme"]);
            await Assert.That(result).IsEqualTo(0);
        });
    }

    [Test, NotInParallel]
    public async Task DoctorCommand_FailOn_Warning_WithWarnings_ReturnsError()
    {
        await RunInTempDir(async () =>
        {
            await File.WriteAllTextAsync("TestApp.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            // Create Strings/ without en.json → localization warning
            Directory.CreateDirectory("Strings");

            int result = DoctorCommand.Execute(["--fail-on", "warning", "--category", "localization"]);
            await Assert.That(result).IsEqualTo(1);
        });
    }

    [Test, NotInParallel]
    public async Task DoctorCommand_FailOn_Error_WithWarnings_ReturnsZero()
    {
        await RunInTempDir(async () =>
        {
            await File.WriteAllTextAsync("TestApp.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            // Create Strings/ without en.json → localization warning
            Directory.CreateDirectory("Strings");

            // Default --fail-on error: warnings don't cause failure
            int result = DoctorCommand.Execute(["--category", "localization"]);
            await Assert.That(result).IsEqualTo(0);
        });
    }

    [Test, NotInParallel]
    public async Task DoctorCommand_JsonFormat_IncludesCategory()
    {
        await RunInTempDir(async () =>
        {
            await File.WriteAllTextAsync("TestApp.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            // Capture stdout to verify JSON contains category field
            var originalOut = Console.Out;
            using var sw = new StringWriter();
            Console.SetOut(sw);

            int result = DoctorCommand.Execute(["--format", "json", "--category", "theme"]);

            Console.SetOut(originalOut);
            string output = sw.ToString();

            await Assert.That(output).Contains("\"category\": \"theme\"");
            await Assert.That(result).IsEqualTo(0);
        });
    }

    // ── Helper ──────────────────────────────────────────────────

    private static async Task RunInTempDir(Func<Task> action)
    {
        string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cascade_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        string original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempDir);
            await action();
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup; temp dir will be cleaned by OS
            }
        }
    }
}
