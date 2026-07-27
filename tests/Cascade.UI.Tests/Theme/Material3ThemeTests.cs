using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class Material3ThemeTests
{
    // ── Construction ─────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_DefaultsToLightMode()
    {
        var theme = new Material3Theme();
        await Assert.That(theme.Colors.Primary).IsEqualTo(MaterialTokens.LightPrimary);
    }

    [Test]
    public async Task Material3Theme_LightMode_UsesLightColors()
    {
        var theme = new Material3Theme(ThemeMode.Light);
        await Assert.That(theme.Colors.Primary).IsEqualTo(MaterialTokens.LightPrimary);
        await Assert.That(theme.Colors.Background).IsEqualTo(MaterialTokens.LightBackground);
        await Assert.That(theme.Colors.Text).IsEqualTo(MaterialTokens.LightText);
    }

    [Test]
    public async Task Material3Theme_DarkMode_UsesDarkColors()
    {
        var theme = new Material3Theme(ThemeMode.Dark);
        await Assert.That(theme.Colors.Primary).IsEqualTo(MaterialTokens.DarkPrimary);
        await Assert.That(theme.Colors.Background).IsEqualTo(MaterialTokens.DarkBackground);
        await Assert.That(theme.Colors.Text).IsEqualTo(MaterialTokens.DarkText);
    }

    [Test]
    public async Task Material3Theme_LightColors_MatchSpec()
    {
        var theme = new Material3Theme(ThemeMode.Light);
        var c = theme.LightColors;

        await Assert.That(c.Primary).IsEqualTo(new ColorValue("#6750A4"));
        await Assert.That(c.PrimaryText).IsEqualTo(new ColorValue("#6750A4"));
        await Assert.That(c.TextOnPrimary).IsEqualTo(new ColorValue("#FFFFFF"));
        await Assert.That(c.Background).IsEqualTo(new ColorValue("#FFFBFE"));
        await Assert.That(c.Surface).IsEqualTo(new ColorValue("#FFFBFE"));
        await Assert.That(c.SurfaceAlt).IsEqualTo(new ColorValue("#F4EFF4"));
        await Assert.That(c.Border).IsEqualTo(new ColorValue("#79747E"));
        await Assert.That(c.Text).IsEqualTo(new ColorValue("#1C1B1F"));
        await Assert.That(c.TextMuted).IsEqualTo(new ColorValue("#49454F"));
        await Assert.That(c.Danger).IsEqualTo(new ColorValue("#B3261E"));
        await Assert.That(c.Success).IsEqualTo(new ColorValue("#386A20"));
        await Assert.That(c.Warning).IsEqualTo(new ColorValue("#7D5700"));
        await Assert.That(c.Focus).IsEqualTo(new ColorValue("#6750A4"));
    }

    [Test]
    public async Task Material3Theme_DarkColors_MatchSpec()
    {
        var theme = new Material3Theme(ThemeMode.Dark);
        var c = theme.DarkColors;

        await Assert.That(c.Primary).IsEqualTo(new ColorValue("#D0BCFF"));
        await Assert.That(c.PrimaryText).IsEqualTo(new ColorValue("#D0BCFF"));
        await Assert.That(c.TextOnPrimary).IsEqualTo(new ColorValue("#381E72"));
        await Assert.That(c.Background).IsEqualTo(new ColorValue("#1C1B1F"));
        await Assert.That(c.Surface).IsEqualTo(new ColorValue("#1C1B1F"));
        await Assert.That(c.SurfaceAlt).IsEqualTo(new ColorValue("#2B2930"));
        await Assert.That(c.Border).IsEqualTo(new ColorValue("#938F99"));
        await Assert.That(c.Text).IsEqualTo(new ColorValue("#E6E1E5"));
        await Assert.That(c.TextMuted).IsEqualTo(new ColorValue("#CAC4D0"));
        await Assert.That(c.Danger).IsEqualTo(new ColorValue("#F2B8B5"));
        await Assert.That(c.Success).IsEqualTo(new ColorValue("#A5D6A7"));
        await Assert.That(c.Warning).IsEqualTo(new ColorValue("#FFD54F"));
        await Assert.That(c.Focus).IsEqualTo(new ColorValue("#D0BCFF"));
    }

    // ── Typography ──────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Typography_UsesRoboto()
    {
        var theme = new Material3Theme();
        // FontFamily is a sealed class — compare structural properties, not reference
        await Assert.That(theme.Typography.FontFamily.Kind).IsEqualTo(FontFamilyKind.Bundled);
        await Assert.That(theme.Typography.FontFamily.BundledName).IsEqualTo("Roboto");
    }

    [Test]
    public async Task Material3Theme_Typography_ScaleMatchesSpec()
    {
        var ts = MaterialTokens.Typography.Scale;

        await Assert.That(ts.Display.Size).IsEqualTo(45f);
        await Assert.That(ts.H1.Size).IsEqualTo(32f);
        await Assert.That(ts.H2.Size).IsEqualTo(24f);
        await Assert.That(ts.H3.Size).IsEqualTo(16f);
        await Assert.That(ts.Body.Size).IsEqualTo(14f);
        await Assert.That(ts.BodySmall.Size).IsEqualTo(12f);
        await Assert.That(ts.Caption.Size).IsEqualTo(11f);
        await Assert.That(ts.Code.Size).IsEqualTo(13f);
    }

    // ── Spacing and Radius ──────────────────────────────────────

    [Test]
    public async Task Material3Theme_Spacing_Base4()
    {
        var theme = new Material3Theme();
        await Assert.That(theme.Spacing.Base).IsEqualTo(4f);
    }

    [Test]
    public async Task Material3Theme_Radius_MatchesSpec()
    {
        var r = MaterialTokens.Radius;

        await Assert.That(r.None).IsEqualTo(0f);
        await Assert.That(r.Sm).IsEqualTo(4f);
        await Assert.That(r.Base).IsEqualTo(8f);
        await Assert.That(r.Md).IsEqualTo(12f);
        await Assert.That(r.Lg).IsEqualTo(16f);
        await Assert.That(r.Xl).IsEqualTo(28f);
        await Assert.That(r.Full).IsEqualTo(9999f);
    }

    // ── Button ──────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Button_DefaultIsFilledPill()
    {
        var theme = new Material3Theme();
        var b = theme.Button;

        await Assert.That(b.Height).IsEqualTo(40f);
        await Assert.That(b.PaddingH).IsEqualTo(24f);
        await Assert.That(b.Radius).IsEqualTo(MaterialTokens.Radius.Full);
        await Assert.That(b.TextColor).IsEqualTo(theme.Colors.TextOnPrimary);
    }

    [Test]
    public async Task Material3Theme_Button_StateLayerPattern()
    {
        var theme = new Material3Theme();
        var b = theme.Button;

        await Assert.That(b.Hover.StateLayerOpacity).IsEqualTo(0.08f);
        await Assert.That(b.Pressed.StateLayerOpacity).IsEqualTo(0.12f);
        await Assert.That(b.Disabled.BackgroundOpacity).IsEqualTo(0.12f);
        await Assert.That(b.Disabled.TextOpacity).IsEqualTo(0.38f);
    }

    [Test]
    public async Task Material3Theme_Button_OutlineVariant()
    {
        var theme = new Material3Theme();
        var outline = theme.Button.Variants["outline"];

        await Assert.That(outline.TextColor).IsEqualTo(theme.Colors.Primary);
        await Assert.That(outline.BorderWidth).IsEqualTo(1f);
        await Assert.That(outline.Radius).IsEqualTo(MaterialTokens.Radius.Full);
    }

    [Test]
    public async Task Material3Theme_Button_GhostVariant()
    {
        var theme = new Material3Theme();
        var ghost = theme.Button.Variants["ghost"];

        await Assert.That(ghost.Background!.Kind).IsEqualTo(BrushKind.Solid);
        await Assert.That(ghost.Background!.Color).IsEqualTo(ColorValue.Transparent);
        await Assert.That(ghost.PaddingH).IsEqualTo(12f);
        await Assert.That(ghost.BorderWidth).IsEqualTo(0f);
        await Assert.That(ghost.Shadow).IsEqualTo(ShadowSpec.None);
    }

    [Test]
    public async Task Material3Theme_Button_SubtleVariant_LightMode()
    {
        var theme = new Material3Theme(ThemeMode.Light);
        var subtle = theme.Button.Variants["subtle"];

        await Assert.That(subtle.Background!.Kind).IsEqualTo(BrushKind.Solid);
        await Assert.That(subtle.Background!.Color).IsEqualTo(MaterialTokens.SecondaryContainerLight);
        await Assert.That(subtle.TextColor).IsEqualTo(MaterialTokens.OnSecondaryContainerLight);
    }

    [Test]
    public async Task Material3Theme_Button_DestructiveVariant()
    {
        var theme = new Material3Theme();
        var destructive = theme.Button.Variants["destructive"];

        await Assert.That(destructive.Background!.Kind).IsEqualTo(BrushKind.Solid);
        await Assert.That(destructive.Background!.Color).IsEqualTo(theme.Colors.Danger);
        await Assert.That(destructive.TextColor).IsEqualTo(theme.Colors.TextOnPrimary);
    }

    // ── TextInput ───────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_TextInput_OutlinedStyle56dp()
    {
        var theme = new Material3Theme();
        var ti = theme.TextInput;

        await Assert.That(ti.Height).IsEqualTo(56f);
        await Assert.That(ti.Background).IsEqualTo(ColorValue.Transparent);
        await Assert.That(ti.BorderColor).IsEqualTo(theme.Colors.Border);
        await Assert.That(ti.BorderWidth).IsEqualTo(1f);
    }

    [Test]
    public async Task Material3Theme_TextInput_NoFocusRing()
    {
        var theme = new Material3Theme();
        var ti = theme.TextInput;

        await Assert.That(ti.FocusRingWidth).IsEqualTo(0f);
        await Assert.That(ti.FocusRingColor).IsEqualTo(ColorValue.Transparent);
        await Assert.That(ti.FocusBorderWidth).IsEqualTo(2f);
        await Assert.That(ti.FocusBorderColor).IsEqualTo(theme.Colors.Primary);
    }

    // ── Checkbox ────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Checkbox_18pxWithBorder()
    {
        var theme = new Material3Theme();
        var cb = theme.Checkbox;

        await Assert.That(cb.Size).IsEqualTo(18f);
        await Assert.That(cb.BorderWidth).IsEqualTo(2f);
        await Assert.That(cb.Background).IsEqualTo(ColorValue.Transparent);
        await Assert.That(cb.CheckedBg).IsEqualTo(theme.Colors.Primary);
    }

    // ── Toggle ──────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Toggle_LargerTrack()
    {
        var theme = new Material3Theme();
        var t = theme.Toggle;

        await Assert.That(t.TrackWidth).IsEqualTo(52f);
        await Assert.That(t.TrackHeight).IsEqualTo(32f);
        await Assert.That(t.ThumbSize).IsEqualTo(24f);
    }

    [Test]
    public async Task Material3Theme_Toggle_OutlinedBorder()
    {
        var theme = new Material3Theme();
        var t = theme.Toggle;

        await Assert.That(t.TrackBorderColor).IsEqualTo(theme.Colors.Border);
        await Assert.That(t.TrackBorderWidth).IsEqualTo(2f);
    }

    // ── Slider ──────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Slider_NoThumbShadow()
    {
        var theme = new Material3Theme();
        var s = theme.Slider;

        await Assert.That(s.ThumbShadow).IsEqualTo(ShadowSpec.None);
        await Assert.That(s.ThumbWidth).IsEqualTo(20f);
        await Assert.That(s.ThumbHeight).IsEqualTo(20f);
        await Assert.That(s.ThumbHover.StateLayerOpacity).IsEqualTo(0.08f);
    }

    // ── Select ──────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Select_56dpHeight()
    {
        var theme = new Material3Theme();
        var s = theme.Select;

        await Assert.That(s.Height).IsEqualTo(56f);
        await Assert.That(s.Background).IsEqualTo(ColorValue.Transparent);
    }

    // ── Tabs ────────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Tabs_48dpWithIndicator()
    {
        var theme = new Material3Theme();
        var tabs = theme.Tabs;

        await Assert.That(tabs.Height).IsEqualTo(48f);
        await Assert.That(tabs.IndicatorHeight).IsEqualTo(3f);
        await Assert.That(tabs.IndicatorColor).IsEqualTo(theme.Colors.Primary);
    }

    // ── Progress ────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Progress_4pxBarWith3pxRing()
    {
        var theme = new Material3Theme();
        var p = theme.Progress;

        await Assert.That(p.BarHeight).IsEqualTo(4f);
        await Assert.That(p.RingStrokeWidth).IsEqualTo(3f);
        await Assert.That(p.FillColor).IsEqualTo(theme.Colors.Primary);
    }

    // ── Badge ───────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Badge_DangerColoredPill()
    {
        var theme = new Material3Theme();
        var b = theme.Badge;

        await Assert.That(b.Radius).IsEqualTo(MaterialTokens.Radius.Full);
        await Assert.That(b.Background).IsEqualTo(theme.Colors.Danger);
        await Assert.That(b.DotSize).IsEqualTo(6f);
    }

    // ── Card ────────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Card_SurfaceWithSmallShadow()
    {
        var theme = new Material3Theme();
        var card = theme.Card;

        await Assert.That(card.Background).IsEqualTo(theme.Colors.Surface);
        await Assert.That(card.Shadow).IsEqualTo(MaterialTokens.ShadowSm);
        await Assert.That(card.Radius).IsEqualTo(MaterialTokens.Radius.Md);
    }

    // ── Tooltip ─────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Tooltip_InvertedColors()
    {
        var theme = new Material3Theme();
        var tip = theme.Tooltip;

        await Assert.That(tip.Background).IsEqualTo(theme.Colors.Text);
        await Assert.That(tip.TextColor).IsEqualTo(theme.Colors.Surface);
        await Assert.That(tip.ArrowSize).IsEqualTo(0f);
    }

    // ── Dialog ──────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Dialog_XlRadius560MaxWidth()
    {
        var theme = new Material3Theme();
        var d = theme.Dialog;

        await Assert.That(d.Radius).IsEqualTo(MaterialTokens.Radius.Xl);
        await Assert.That(d.MaxWidth).IsEqualTo(560f);
        await Assert.That(d.EnterScale).IsEqualTo(0.87f);
        await Assert.That(d.BackdropBlur).IsEqualTo(0f);
    }

    // ── NavBar ──────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_NavBar_64dp()
    {
        var theme = new Material3Theme();
        var nav = theme.NavBar;

        await Assert.That(nav.Height).IsEqualTo(64f);
        await Assert.That(nav.Background).IsEqualTo(theme.Colors.Surface);
        await Assert.That(nav.BorderBottom).IsEqualTo(ColorValue.Transparent);
    }

    // ── Scroll ──────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Scroll_RoundedThumbs()
    {
        var theme = new Material3Theme();
        var s = theme.Scroll;

        await Assert.That(s.TrackThumbRadius).IsEqualTo(7f);
        await Assert.That(s.OverlayRadius).IsEqualTo(3f);
    }

    [Test]
    public async Task Material3Theme_Scroll_DarkModeUsesCorrectThumbColors()
    {
        var theme = new Material3Theme(ThemeMode.Dark);
        var s = theme.Scroll;

        await Assert.That(s.OverlayThumbColor).IsEqualTo(MaterialTokens.DarkText38);
        await Assert.That(s.OverlayThumbHoverColor).IsEqualTo(MaterialTokens.DarkText56);
    }

    [Test]
    public async Task Material3Theme_Scroll_LightModeUsesCorrectThumbColors()
    {
        var theme = new Material3Theme(ThemeMode.Light);
        var s = theme.Scroll;

        await Assert.That(s.OverlayThumbColor).IsEqualTo(MaterialTokens.LightText38);
        await Assert.That(s.OverlayThumbHoverColor).IsEqualTo(MaterialTokens.LightText56);
    }

    // ── IconAnimation ───────────────────────────────────────────

    [Test]
    public async Task Material3Theme_IconAnimation_MorphTransition()
    {
        var theme = new Material3Theme();
        var icon = theme.IconAnimation;

        await Assert.That(icon.DefaultTransition).IsEqualTo(IconTransition.Morph);
        await Assert.That(icon.TransitionModel).IsEqualTo(AnimationModel.MaterialStandard);
    }

    // ── Caret ───────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Caret_TextColorSmoothBlink()
    {
        var theme = new Material3Theme();
        var c = theme.Caret;

        await Assert.That(c.Color).IsEqualTo(theme.Colors.Text);
        await Assert.That(c.Width).IsEqualTo(2f);
        await Assert.That(c.SmoothBlink).IsTrue();
    }

    // ── Password ────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Password_StrengthIndicatorEnabled()
    {
        var theme = new Material3Theme();
        var p = theme.Password;

        await Assert.That(p.ShowStrengthIndicator).IsTrue();
        await Assert.That(p.ShowRevealToggle).IsTrue();
        await Assert.That(p.MaskCharacter).IsEqualTo('●');
    }

    // ── Spinner ─────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Spinner_ArcStyle()
    {
        var spinner = MaterialTokens.Spinner;
        await Assert.That(spinner.Thickness).IsEqualTo(3f);
    }

    // ── Motion ──────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Motion_StandardIs300ms()
    {
        var theme = new Material3Theme();
        await Assert.That(theme.Motion.Default).IsEqualTo(MaterialTokens.MotionDefault);
        await Assert.That(theme.Motion.Enter).IsEqualTo(MaterialTokens.MotionEnter);
        await Assert.That(theme.Motion.Exit).IsEqualTo(MaterialTokens.MotionExit);
    }

    // ── Cross-mode consistency ──────────────────────────────────

    [Test]
    public async Task Material3Theme_BothModes_ShareTypography()
    {
        var light = new Material3Theme(ThemeMode.Light);
        var dark = new Material3Theme(ThemeMode.Dark);

        await Assert.That(light.Typography.FontFamily).IsEqualTo(dark.Typography.FontFamily);
        await Assert.That(light.Typography.Scale.Body.Size).IsEqualTo(dark.Typography.Scale.Body.Size);
    }

    [Test]
    public async Task Material3Theme_BothModes_ShareRadiusAndSpacing()
    {
        var light = new Material3Theme(ThemeMode.Light);
        var dark = new Material3Theme(ThemeMode.Dark);

        await Assert.That(light.Radius.Full).IsEqualTo(dark.Radius.Full);
        await Assert.That(light.Spacing.Base).IsEqualTo(dark.Spacing.Base);
    }

    [Test]
    public async Task Material3Theme_LightAndDark_HaveDifferentPrimary()
    {
        var light = new Material3Theme(ThemeMode.Light);
        var dark = new Material3Theme(ThemeMode.Dark);

        await Assert.That(light.Colors.Primary).IsNotEqualTo(dark.Colors.Primary);
    }

    // ── Shadows ─────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Shadows_MatchSpec()
    {
        var theme = new Material3Theme();
        await Assert.That(theme.Shadows.Sm).IsEqualTo(MaterialTokens.ShadowSm);
        await Assert.That(theme.Shadows.Md).IsEqualTo(MaterialTokens.ShadowMd);
        await Assert.That(theme.Shadows.Lg).IsEqualTo(MaterialTokens.ShadowLg);
        await Assert.That(theme.Shadows.Xl).IsEqualTo(MaterialTokens.ShadowXl);
    }

    // ── Chart ───────────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Chart_UsesThemeColors()
    {
        var theme = new Material3Theme();
        var chart = theme.Chart;

        await Assert.That(chart.GridLine).IsEqualTo(theme.Colors.Border);
        await Assert.That(chart.Axis).IsEqualTo(theme.Colors.TextMuted);
        await Assert.That(chart.BarRadius).IsEqualTo(MaterialTokens.Radius.Sm);
    }

    // ── Dialog backdrop ─────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Dialog_BackdropColorIs32PercentBlack()
    {
        var theme = new Material3Theme();
        var expected = new ColorValue("#000000").Opacity(0.32f);
        await Assert.That(theme.Dialog.BackdropColor).IsEqualTo(expected);
    }

    // ── Toggle thumb ────────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Toggle_ThumbIsWhite()
    {
        var theme = new Material3Theme();
        await Assert.That(theme.Toggle.ThumbColor).IsEqualTo(new ColorValue("#FFFBFE"));
    }

    // ── Select dropdown ─────────────────────────────────────────

    [Test]
    public async Task Material3Theme_Select_DropdownProperties()
    {
        var theme = new Material3Theme();
        var s = theme.Select;

        await Assert.That(s.DropdownRadius).IsEqualTo(MaterialTokens.Radius.Md);
        await Assert.That(s.ItemHeight).IsEqualTo(48f);
        await Assert.That(s.ItemPaddingH).IsEqualTo(16f);
        await Assert.That(s.DropdownMaxHeight).IsEqualTo(360f);
    }

    // ── Subtle variant dark mode ────────────────────────────────

    [Test]
    public async Task Material3Theme_Button_SubtleVariant_DarkMode()
    {
        var theme = new Material3Theme(ThemeMode.Dark);
        var subtle = theme.Button.Variants["subtle"];

        await Assert.That(subtle.Background!.Kind).IsEqualTo(BrushKind.Solid);
        await Assert.That(subtle.Background!.Color).IsEqualTo(theme.Colors.SurfaceAlt);
        await Assert.That(subtle.TextColor).IsEqualTo(theme.Colors.Text);
    }

    // ── Motion emphasis and subtle ──────────────────────────────

    [Test]
    public async Task Material3Theme_Motion_EmphasisAndSubtle()
    {
        var theme = new Material3Theme();
        await Assert.That(theme.Motion.Emphasis).IsEqualTo(MaterialTokens.MotionEmphasis);
        await Assert.That(theme.Motion.Subtle).IsEqualTo(MaterialTokens.MotionSubtle);
    }

    // ── Caret and Password details ──────────────────────────────

    [Test]
    public async Task Material3Theme_Caret_BlinkInterval1060ms()
    {
        var theme = new Material3Theme();
        await Assert.That(theme.Caret.BlinkInterval).IsEqualTo(Duration.Ms(1060));
    }

    [Test]
    public async Task Material3Theme_Password_StrengthBarHeight()
    {
        var theme = new Material3Theme();
        await Assert.That(theme.Password.StrengthBarHeight).IsEqualTo(4f);
        await Assert.That(theme.Password.RevealToggleSize).IsEqualTo(18f);
    }

    // ── High contrast ───────────────────────────────────────────

    [Test]
    public async Task Material3Theme_HighContrastLight_IsNotNull()
    {
        var theme = new Material3Theme();
        await Assert.That(theme.HighContrastLightColors).IsNotNull();
        await Assert.That(theme.HighContrastLightColors!.Text).IsEqualTo(new ColorValue("#000000"));
        await Assert.That(theme.HighContrastLightColors!.Border).IsEqualTo(new ColorValue("#000000"));
    }

    [Test]
    public async Task Material3Theme_HighContrastDark_IsNotNull()
    {
        var theme = new Material3Theme();
        await Assert.That(theme.HighContrastDarkColors).IsNotNull();
        await Assert.That(theme.HighContrastDarkColors!.Text).IsEqualTo(new ColorValue("#FFFFFF"));
        await Assert.That(theme.HighContrastDarkColors!.Border).IsEqualTo(new ColorValue("#FFFFFF"));
    }

    // ── ThemeMode.System fallback ───────────────────────────────

    [Test]
    public async Task Material3Theme_SystemMode_FallsBackToLight()
    {
        var system = new Material3Theme(ThemeMode.System);
        var light = new Material3Theme(ThemeMode.Light);

        await Assert.That(system.Colors.Primary).IsEqualTo(light.Colors.Primary);
    }

    // ── Subtle colors use Opacity ───────────────────────────────

    [Test]
    public async Task Material3Theme_SubtleColors_MatchOpacitySpec()
    {
        var theme = new Material3Theme(ThemeMode.Light);
        var expected = new ColorValue("#B3261E").Opacity(0.12f);
        await Assert.That(theme.Colors.DangerSubtle).IsEqualTo(expected);
    }
}
