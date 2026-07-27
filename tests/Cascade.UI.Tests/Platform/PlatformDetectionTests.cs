namespace Cascade.UI.Tests.Platform;

/// <summary>
/// Tests for <see cref="Cascade.UI.Platform"/> static detection properties.
/// </summary>
public class PlatformDetectionTests
{
    [Test]
    public async Task Current_ReturnsValidPlatformKind()
    {
        var current = UI.Platform.Current;
        await Assert.That(Enum.IsDefined(current)).IsTrue();
    }

    [Test]
    public async Task ExactlyOnePlatformIsTrue()
    {
        int trueCount = 0;
        if (UI.Platform.IsWindows) { trueCount++; }
        if (UI.Platform.IsMacOS) { trueCount++; }
        if (UI.Platform.IsLinux) { trueCount++; }

        await Assert.That(trueCount).IsEqualTo(1);
    }

    [Test]
    public async Task IsWindows_MatchesCurrent()
    {
        bool expected = UI.Platform.Current == PlatformKind.Windows;
        await Assert.That(UI.Platform.IsWindows).IsEqualTo(expected);
    }

    [Test]
    public async Task IsMacOS_MatchesCurrent()
    {
        bool expected = UI.Platform.Current == PlatformKind.MacOS;
        await Assert.That(UI.Platform.IsMacOS).IsEqualTo(expected);
    }

    [Test]
    public async Task IsLinux_MatchesCurrent()
    {
        bool expected = UI.Platform.Current == PlatformKind.Linux;
        await Assert.That(UI.Platform.IsLinux).IsEqualTo(expected);
    }

    [Test]
    public async Task RuntimeVersion_IsNotEmpty()
    {
        string version = UI.Platform.RuntimeVersion;
        await Assert.That(version).IsNotNull();
        await Assert.That(version.Length > 0).IsTrue();
    }

    [Test]
    public async Task RuntimeVersion_ContainsMajorVersion()
    {
        // .NET 10+ should have a version that starts with a digit
        string version = UI.Platform.RuntimeVersion;
        await Assert.That(char.IsDigit(version[0])).IsTrue();
    }

    [Test]
    public async Task OsVersion_IsNotEmpty()
    {
        string osVersion = UI.Platform.OsVersion;
        await Assert.That(osVersion).IsNotNull();
        await Assert.That(osVersion.Length > 0).IsTrue();
    }

    [Test]
    public async Task OsVersion_ContainsPlatformIdentifier()
    {
        string osVersion = UI.Platform.OsVersion;

        if (UI.Platform.IsWindows)
        {
            await Assert.That(osVersion).Contains("Windows");
        }
        else if (UI.Platform.IsMacOS)
        {
            await Assert.That(osVersion).Contains("macOS");
        }
        // Linux uses RuntimeInformation.OSDescription which varies
    }

    [Test]
    public async Task IsNativeAot_ReturnsBool()
    {
        // In test context, we're running on CoreCLR, so NativeAOT should be false
        bool isAot = UI.Platform.IsNativeAot;
        await Assert.That(isAot).IsEqualTo(false);
    }

    [Test]
    public async Task LinuxDesktopEnvironment_NullOnNonLinux()
    {
        if (!UI.Platform.IsLinux)
        {
            await Assert.That(UI.Platform.LinuxDesktopEnvironment).IsNull();
        }
        else
        {
            // On Linux, it may or may not be set — just verify no exception
            string? de = UI.Platform.LinuxDesktopEnvironment;
            // de can be null or a non-empty string
            bool valid = de is null || de.Length > 0;
            await Assert.That(valid).IsTrue();
        }
    }

    [Test]
    public async Task IsWayland_FalseOnNonLinux()
    {
        if (!UI.Platform.IsLinux)
        {
            await Assert.That(UI.Platform.IsWayland).IsEqualTo(false);
        }
        else
        {
            // On Linux, just verify it returns a bool without throwing
            bool isWayland = UI.Platform.IsWayland;
            await Assert.That(isWayland == true || isWayland == false).IsTrue();
        }
    }

    [Test]
    public async Task PlatformProperties_AreConsistentAcrossMultipleCalls()
    {
        // All properties are cached — verify consistency
        var current1 = UI.Platform.Current;
        var current2 = UI.Platform.Current;
        await Assert.That(current1).IsEqualTo(current2);

        var version1 = UI.Platform.OsVersion;
        var version2 = UI.Platform.OsVersion;
        await Assert.That(version1).IsEqualTo(version2);
    }
}
