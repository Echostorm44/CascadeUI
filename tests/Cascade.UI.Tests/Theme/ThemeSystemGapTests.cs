using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

[NotInParallel("ThemeSwitcher")]
public class ThemeSystemGapTests
{
    // ── WideGamutColor ──────────────────────────────────────────────

    [Test]
    public async Task WideGamutColor_P3_CreatesValidColor()
    {
        var color = WideGamutColor.P3(1f, 0f, 0f);

        await Assert.That(color.A).IsEqualTo(1.0f);
        await Assert.That(color).IsNotEqualTo(ColorValue.Transparent);
    }

    [Test]
    public async Task WideGamutColor_OkLch_CreatesValidColor()
    {
        var color = WideGamutColor.OkLch(0.7f, 0.15f, 30f);

        await Assert.That(color.A).IsEqualTo(1.0f);
        await Assert.That(color).IsNotEqualTo(ColorValue.Transparent);
    }

    [Test]
    public async Task WideGamutColor_Rec2020_CreatesValidColor()
    {
        var color = WideGamutColor.Rec2020(0.5f, 0.5f, 0.5f);

        await Assert.That(color.A).IsEqualTo(1.0f);
        await Assert.That(color).IsNotEqualTo(ColorValue.Transparent);
    }

    [Test]
    public async Task WideGamutColor_OkLchHdr_CreatesValidColor()
    {
        var color = WideGamutColor.OkLchHdr(1.2f, 0.1f, 200f);

        await Assert.That(color.A).IsEqualTo(1.0f);
        await Assert.That(color).IsNotEqualTo(ColorValue.Transparent);
    }

    [Test]
    public async Task WideGamutColor_P3_MatchesColorValueP3()
    {
        var fromHelper = WideGamutColor.P3(0.8f, 0.2f, 0.5f, 0.9f);
        var fromDirect = ColorValue.P3(0.8f, 0.2f, 0.5f, 0.9f);

        await Assert.That(fromHelper).IsEqualTo(fromDirect);
    }

    [Test]
    public async Task WideGamutColor_IsWideGamutDisplay_DefaultsFalse()
    {
        await Assert.That(WideGamutColor.IsWideGamutDisplay).IsFalse();
    }

    // ── ThemeSwitcher ───────────────────────────────────────────────

    [Test]
    public async Task ThemeSwitcher_Apply_ChangesCurrentTheme()
    {
        ThemeSwitcher.Reset();
        var theme = new AppleTheme();

        ThemeSwitcher.Apply(theme);

        await Assert.That(ThemeSwitcher.Current).IsSameReferenceAs(theme);
    }

    [Test]
    public async Task ThemeSwitcher_SetDarkMode_UpdatesFlag()
    {
        ThemeSwitcher.Reset();

        ThemeSwitcher.SetDarkMode(true);

        await Assert.That(ThemeSwitcher.IsDarkMode).IsTrue();
    }

    [Test]
    public async Task ThemeSwitcher_ToggleDarkMode_Toggles()
    {
        ThemeSwitcher.Reset();
        await Assert.That(ThemeSwitcher.IsDarkMode).IsFalse();

        ThemeSwitcher.ToggleDarkMode();

        await Assert.That(ThemeSwitcher.IsDarkMode).IsTrue();

        ThemeSwitcher.ToggleDarkMode();

        await Assert.That(ThemeSwitcher.IsDarkMode).IsFalse();
    }

    [Test]
    public async Task ThemeSwitcher_SetHighContrast_UpdatesFlag()
    {
        ThemeSwitcher.Reset();

        ThemeSwitcher.SetHighContrast(true);

        await Assert.That(ThemeSwitcher.UseHighContrast).IsTrue();
    }

    [Test]
    public async Task ThemeSwitcher_ActiveColors_ReturnsDarkWhenDarkMode()
    {
        ThemeSwitcher.Reset();
        var theme = new FluentTheme();
        ThemeSwitcher.Apply(theme);
        ThemeSwitcher.SetDarkMode(true);

        var colors = ThemeSwitcher.ActiveColors;

        await Assert.That(colors).IsEqualTo(theme.DarkColors);
    }

    [Test]
    public async Task ThemeSwitcher_ActiveColors_ReturnsLightWhenLightMode()
    {
        ThemeSwitcher.Reset();
        var theme = new FluentTheme();
        ThemeSwitcher.Apply(theme);
        ThemeSwitcher.SetDarkMode(false);

        var colors = ThemeSwitcher.ActiveColors;

        await Assert.That(colors).IsEqualTo(theme.LightColors);
    }

    [Test]
    public async Task ThemeSwitcher_ActiveColors_ReturnsHighContrastWhenEnabled()
    {
        ThemeSwitcher.Reset();
        var theme = new FluentTheme();
        ThemeSwitcher.Apply(theme);
        ThemeSwitcher.SetHighContrast(true);
        ThemeSwitcher.SetDarkMode(false);

        var colors = ThemeSwitcher.ActiveColors;

        await Assert.That(colors).IsEqualTo(theme.HighContrastLightColors!);
    }

    [Test]
    public async Task ThemeSwitcher_ActiveColors_ReturnsHighContrastDarkWhenBothEnabled()
    {
        ThemeSwitcher.Reset();
        var theme = new FluentTheme();
        ThemeSwitcher.Apply(theme);
        ThemeSwitcher.SetHighContrast(true);
        ThemeSwitcher.SetDarkMode(true);

        var colors = ThemeSwitcher.ActiveColors;

        await Assert.That(colors).IsEqualTo(theme.HighContrastDarkColors!);
    }

    [Test]
    public async Task ThemeSwitcher_Subscribe_NotifiesOnChange()
    {
        ThemeSwitcher.Reset();
        int callCount = 0;
        using var sub = ThemeSwitcher.Subscribe(() => callCount++);

        ThemeSwitcher.Apply(new AppleTheme());

        await Assert.That(callCount).IsEqualTo(1);
    }

    [Test]
    public async Task ThemeSwitcher_Subscribe_Dispose_StopsNotification()
    {
        ThemeSwitcher.Reset();
        int callCount = 0;
        var sub = ThemeSwitcher.Subscribe(() => callCount++);
        sub.Dispose();

        ThemeSwitcher.Apply(new AppleTheme());

        await Assert.That(callCount).IsEqualTo(0);
    }

    [Test]
    public async Task ThemeSwitcher_NoOpWhenSameDarkMode()
    {
        ThemeSwitcher.Reset();
        int callCount = 0;
        using var sub = ThemeSwitcher.Subscribe(() => callCount++);

        ThemeSwitcher.SetDarkMode(false);

        await Assert.That(callCount).IsEqualTo(0);
    }

    [Test]
    public async Task ThemeSwitcher_NoOpWhenSameHighContrast()
    {
        ThemeSwitcher.Reset();
        int callCount = 0;
        using var sub = ThemeSwitcher.Subscribe(() => callCount++);

        ThemeSwitcher.SetHighContrast(false);

        await Assert.That(callCount).IsEqualTo(0);
    }

    [Test]
    public async Task ThemeSwitcher_Subscribe_NotifiesOnDarkModeChange()
    {
        ThemeSwitcher.Reset();
        int callCount = 0;
        using var sub = ThemeSwitcher.Subscribe(() => callCount++);

        ThemeSwitcher.SetDarkMode(true);

        await Assert.That(callCount).IsEqualTo(1);
    }

    [Test]
    public async Task ThemeSwitcher_Subscribe_NotifiesOnHighContrastChange()
    {
        ThemeSwitcher.Reset();
        int callCount = 0;
        using var sub = ThemeSwitcher.Subscribe(() => callCount++);

        ThemeSwitcher.SetHighContrast(true);

        await Assert.That(callCount).IsEqualTo(1);
    }

    // ── HighContrastDetector ────────────────────────────────────────

    [Test]
    public async Task HighContrastDetector_DefaultIsFalse()
    {
        HighContrastDetector.Reset();

        await Assert.That(HighContrastDetector.IsHighContrastEnabled).IsFalse();
    }

    [Test]
    public async Task HighContrastDetector_ManualOverride()
    {
        ThemeSwitcher.Reset();
        HighContrastDetector.Reset();

        HighContrastDetector.SetOverride(true);

        await Assert.That(HighContrastDetector.IsHighContrastEnabled).IsTrue();
        await Assert.That(ThemeSwitcher.UseHighContrast).IsTrue();
    }

    [Test]
    public async Task HighContrastDetector_ClearOverride()
    {
        ThemeSwitcher.Reset();
        HighContrastDetector.Reset();
        HighContrastDetector.SetOverride(true);

        HighContrastDetector.SetOverride(null);

        await Assert.That(HighContrastDetector.IsHighContrastEnabled).IsFalse();
    }
}
