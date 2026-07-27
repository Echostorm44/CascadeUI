using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class AppleThemeTests
{
    // ── Construction ─────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_DefaultsToLightMode()
    {
        var theme = new AppleTheme();
        await Assert.That(theme.Colors.Primary).IsEqualTo(AppleTokens.LightPrimary);
    }

    [Test]
    public async Task AppleTheme_LightMode_UsesLightColors()
    {
        var theme = new AppleTheme(ThemeMode.Light);
        await Assert.That(theme.Colors.Primary).IsEqualTo(AppleTokens.LightPrimary);
        await Assert.That(theme.Colors.Background).IsEqualTo(AppleTokens.LightBackground);
        await Assert.That(theme.Colors.Text).IsEqualTo(AppleTokens.LightText);
    }

    [Test]
    public async Task AppleTheme_DarkMode_UsesDarkColors()
    {
        var theme = new AppleTheme(ThemeMode.Dark);
        await Assert.That(theme.Colors.Primary).IsEqualTo(AppleTokens.DarkPrimary);
        await Assert.That(theme.Colors.Background).IsEqualTo(AppleTokens.DarkBackground);
        await Assert.That(theme.Colors.Text).IsEqualTo(AppleTokens.DarkText);
    }

    [Test]
    public async Task AppleTheme_SystemMode_FallsBackToLight()
    {
        var system = new AppleTheme(ThemeMode.System);
        var light = new AppleTheme(ThemeMode.Light);
        await Assert.That(system.Colors.Primary).IsEqualTo(light.Colors.Primary);
    }

    // ── Light colors ────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_LightColors_MatchSpec()
    {
        var theme = new AppleTheme(ThemeMode.Light);
        var c = theme.LightColors;

        await Assert.That(c.Primary).IsEqualTo(new ColorValue("#007AFF"));
        await Assert.That(c.PrimaryText).IsEqualTo(new ColorValue("#007AFF"));
        await Assert.That(c.TextOnPrimary).IsEqualTo(new ColorValue("#FFFFFF"));
        await Assert.That(c.Background).IsEqualTo(new ColorValue("#F2F2F7"));
        await Assert.That(c.Surface).IsEqualTo(new ColorValue("#FFFFFF"));
        await Assert.That(c.SurfaceAlt).IsEqualTo(new ColorValue("#E5E5EA"));
        await Assert.That(c.Border).IsEqualTo(new ColorValue("#C6C6C8"));
        await Assert.That(c.Text).IsEqualTo(new ColorValue("#000000"));
        await Assert.That(c.TextMuted).IsEqualTo(new ColorValue("#8E8E93"));
        await Assert.That(c.Danger).IsEqualTo(new ColorValue("#FF3B30"));
        await Assert.That(c.Success).IsEqualTo(new ColorValue("#34C759"));
        await Assert.That(c.Warning).IsEqualTo(new ColorValue("#FF9500"));
        await Assert.That(c.Focus).IsEqualTo(new ColorValue("#007AFF"));
    }

    [Test]
    public async Task AppleTheme_LightSubtleColors_UseOpacity()
    {
        var c = AppleTokens.LightColors;
        var expectedDangerSubtle = new ColorValue("#FF3B30").Opacity(0.12f);
        await Assert.That(c.DangerSubtle).IsEqualTo(expectedDangerSubtle);
    }

    // ── Dark colors ─────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_DarkColors_MatchSpec()
    {
        var theme = new AppleTheme(ThemeMode.Dark);
        var c = theme.DarkColors;

        await Assert.That(c.Primary).IsEqualTo(new ColorValue("#0A84FF"));
        await Assert.That(c.PrimaryText).IsEqualTo(new ColorValue("#0A84FF"));
        await Assert.That(c.TextOnPrimary).IsEqualTo(new ColorValue("#FFFFFF"));
        await Assert.That(c.Background).IsEqualTo(new ColorValue("#000000"));
        await Assert.That(c.Surface).IsEqualTo(new ColorValue("#1C1C1E"));
        await Assert.That(c.SurfaceAlt).IsEqualTo(new ColorValue("#2C2C2E"));
        await Assert.That(c.Border).IsEqualTo(new ColorValue("#38383A"));
        await Assert.That(c.Text).IsEqualTo(new ColorValue("#FFFFFF"));
        await Assert.That(c.TextMuted).IsEqualTo(new ColorValue("#8E8E93"));
        await Assert.That(c.Danger).IsEqualTo(new ColorValue("#FF453A"));
        await Assert.That(c.Success).IsEqualTo(new ColorValue("#30D158"));
        await Assert.That(c.Warning).IsEqualTo(new ColorValue("#FF9F0A"));
        await Assert.That(c.Focus).IsEqualTo(new ColorValue("#0A84FF"));
    }

    [Test]
    public async Task AppleTheme_DarkSubtleColors_UseOpacity015()
    {
        var c = AppleTokens.DarkColors;
        var expectedDangerSubtle = new ColorValue("#FF453A").Opacity(0.15f);
        await Assert.That(c.DangerSubtle).IsEqualTo(expectedDangerSubtle);
    }

    // ── Typography ──────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_Typography_UsesInterFont()
    {
        var theme = new AppleTheme();
        await Assert.That(theme.Typography.FontFamily.Kind).IsEqualTo(FontFamilyKind.Bundled);
        await Assert.That(theme.Typography.FontFamily.BundledName).IsEqualTo("Inter");
    }

    [Test]
    public async Task AppleTheme_Typography_ScaleMatchesSpec()
    {
        var ts = AppleTokens.Typography.Scale;

        await Assert.That(ts.Display.Size).IsEqualTo(34f);
        await Assert.That(ts.Display.Weight).IsEqualTo(FontWeight.Bold);
        await Assert.That(ts.H1.Size).IsEqualTo(28f);
        await Assert.That(ts.H1.Weight).IsEqualTo(FontWeight.Bold);
        await Assert.That(ts.H2.Size).IsEqualTo(22f);
        await Assert.That(ts.H3.Size).IsEqualTo(17f);
        await Assert.That(ts.Body.Size).IsEqualTo(17f);
        await Assert.That(ts.Body.Weight).IsEqualTo(FontWeight.Regular);
        await Assert.That(ts.BodySmall.Size).IsEqualTo(15f);
        await Assert.That(ts.Caption.Size).IsEqualTo(12f);
        await Assert.That(ts.Code.Size).IsEqualTo(14f);
    }

    // ── Spacing and Radius ──────────────────────────────────────

    [Test]
    public async Task AppleTheme_Spacing_Base4()
    {
        var theme = new AppleTheme();
        await Assert.That(theme.Spacing.Base).IsEqualTo(4f);
    }

    [Test]
    public async Task AppleTheme_Radius_MatchesSpec()
    {
        var r = AppleTokens.Radius;

        await Assert.That(r.None).IsEqualTo(0f);
        await Assert.That(r.Sm).IsEqualTo(4f);
        await Assert.That(r.Base).IsEqualTo(6f);
        await Assert.That(r.Md).IsEqualTo(10f);
        await Assert.That(r.Lg).IsEqualTo(14f);
        await Assert.That(r.Xl).IsEqualTo(20f);
        await Assert.That(r.Full).IsEqualTo(9999f);
    }

    // ── Shadows ─────────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_Shadows_MatchSpec()
    {
        var theme = new AppleTheme();
        await Assert.That(theme.Shadows.Sm).IsEqualTo(AppleTokens.ShadowSm);
        await Assert.That(theme.Shadows.Md).IsEqualTo(AppleTokens.ShadowMd);
        await Assert.That(theme.Shadows.Lg).IsEqualTo(AppleTokens.ShadowLg);
        await Assert.That(theme.Shadows.Xl).IsEqualTo(AppleTokens.ShadowXl);
    }

    // ── Motion ──────────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_Motion_SpringBased()
    {
        var theme = new AppleTheme();
        await Assert.That(theme.Motion.Default).IsEqualTo(AppleTokens.MotionDefault);
        await Assert.That(theme.Motion.Emphasis).IsEqualTo(AppleTokens.MotionEmphasis);
        await Assert.That(theme.Motion.Enter).IsEqualTo(AppleTokens.MotionEnter);
        await Assert.That(theme.Motion.Exit).IsEqualTo(AppleTokens.MotionExit);
    }

    [Test]
    public async Task AppleTheme_Personality_Standard()
    {
        var theme = new AppleTheme();
        await Assert.That(theme.Personality).IsEqualTo(MotionPersonality.Standard);
    }

    // ── Button ──────────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_Button_36pxWithBaseRadius()
    {
        var theme = new AppleTheme();
        var b = theme.Button;

        await Assert.That(b.Height).IsEqualTo(36f);
        await Assert.That(b.PaddingH).IsEqualTo(16f);
        await Assert.That(b.Radius).IsEqualTo(AppleTokens.Radius.Base);
        await Assert.That(b.TextColor).IsEqualTo(theme.Colors.TextOnPrimary);
        await Assert.That(b.Shadow).IsNotEqualTo(ShadowSpec.None);
    }

    [Test]
    public async Task AppleTheme_Button_HoverUsesOpacity_PressedUsesScale()
    {
        var theme = new AppleTheme();
        var b = theme.Button;

        await Assert.That(b.Hover.BackgroundOpacity).IsEqualTo(0.82f);
        await Assert.That(b.Hover.Scale).IsEqualTo(1.01f);
        await Assert.That(b.Pressed.BackgroundOpacity).IsEqualTo(0.75f);
        await Assert.That(b.Pressed.Scale).IsEqualTo(0.96f);
    }

    [Test]
    public async Task AppleTheme_Button_FocusRing3px()
    {
        var theme = new AppleTheme();
        await Assert.That(theme.Button.Focused.OutlineWidth).IsEqualTo(3f);
        await Assert.That(theme.Button.Focused.OutlineOffset).IsEqualTo(2f);
        await Assert.That(theme.Button.Focused.OutlineColor).IsEqualTo(theme.Colors.Focus);
    }

    [Test]
    public async Task AppleTheme_Button_OutlineVariant()
    {
        var theme = new AppleTheme();
        var outline = theme.Button.Variants["outline"];

        await Assert.That(outline.TextColor).IsEqualTo(theme.Colors.Primary);
        await Assert.That(outline.BorderWidth).IsEqualTo(1.5f);
        await Assert.That(outline.Background!.Kind).IsEqualTo(BrushKind.Solid);
        await Assert.That(outline.Background!.Color).IsEqualTo(ColorValue.Transparent);
    }

    [Test]
    public async Task AppleTheme_Button_GhostVariant()
    {
        var theme = new AppleTheme();
        var ghost = theme.Button.Variants["ghost"];

        await Assert.That(ghost.Background!.Kind).IsEqualTo(BrushKind.Solid);
        await Assert.That(ghost.Background!.Color).IsEqualTo(ColorValue.Transparent);
        await Assert.That(ghost.Border).IsNull();
        await Assert.That(ghost.BorderWidth).IsEqualTo(0f);
    }

    [Test]
    public async Task AppleTheme_Button_SubtleVariant()
    {
        var theme = new AppleTheme();
        var subtle = theme.Button.Variants["subtle"];

        await Assert.That(subtle.Background!.Kind).IsEqualTo(BrushKind.Solid);
        await Assert.That(subtle.Background!.Color).IsEqualTo(theme.Colors.SurfaceAlt);
        await Assert.That(subtle.TextColor).IsEqualTo(theme.Colors.Text);
    }

    [Test]
    public async Task AppleTheme_Button_DestructiveVariant()
    {
        var theme = new AppleTheme();
        var destructive = theme.Button.Variants["destructive"];

        await Assert.That(destructive.Background!.Kind).IsEqualTo(BrushKind.Solid);
        await Assert.That(destructive.Background!.Color).IsEqualTo(theme.Colors.Danger);
        await Assert.That(destructive.TextColor).IsEqualTo(theme.Colors.TextOnPrimary);
    }

    // ── TextInput ───────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_TextInput_36pxWithFocusRing()
    {
        var theme = new AppleTheme();
        var ti = theme.TextInput;

        await Assert.That(ti.Height).IsEqualTo(36f);
        await Assert.That(ti.PaddingH).IsEqualTo(10f);
        await Assert.That(ti.Background).IsEqualTo(theme.Colors.Surface);
        await Assert.That(ti.BorderWidth).IsEqualTo(1f);
    }

    [Test]
    public async Task AppleTheme_TextInput_FocusBorderAndRing()
    {
        var theme = new AppleTheme();
        var ti = theme.TextInput;

        await Assert.That(ti.FocusBorderColor).IsEqualTo(theme.Colors.Primary);
        // Thin 1 px accent border on focus — Apple keeps the border a hairline and
        // conveys focus mainly through the soft outer ring (no heavy 2 px border).
        await Assert.That(ti.FocusBorderWidth).IsEqualTo(1f);
        await Assert.That(ti.FocusRingWidth).IsEqualTo(3f);
        await Assert.That(ti.FocusRingColor).IsEqualTo(theme.Colors.Focus.Opacity(0.25f));
    }

    [Test]
    public async Task AppleTheme_TextInput_ErrorState()
    {
        var theme = new AppleTheme();
        var ti = theme.TextInput;

        await Assert.That(ti.ErrorBorderColor).IsEqualTo(theme.Colors.Danger);
        await Assert.That(ti.ErrorRingColor).IsEqualTo(theme.Colors.Danger.Opacity(0.20f));
    }

    // ── Checkbox ────────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_Checkbox_20pxPathDraw()
    {
        var theme = new AppleTheme();
        var cb = theme.Checkbox;

        // 20 px box with a 1.5 px hairline ring — Apple's checkbox border is a
        // thin hairline, not a heavy 2.5 px stroke (the earlier bump to 2.5 read
        // as chunky/un-Apple). The theme is the source of truth (WP-3516).
        await Assert.That(cb.Size).IsEqualTo(20f);
        await Assert.That(cb.BorderWidth).IsEqualTo(1.5f);
        await Assert.That(cb.CheckedBg).IsEqualTo(theme.Colors.Primary);
        await Assert.That(cb.FocusRingWidth).IsEqualTo(3f);
    }

    // ── Toggle ──────────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_Toggle_51x31GreenOnState()
    {
        var theme = new AppleTheme();
        var t = theme.Toggle;

        await Assert.That(t.TrackWidth).IsEqualTo(51f);
        await Assert.That(t.TrackHeight).IsEqualTo(31f);
        await Assert.That(t.TrackOnColor).IsEqualTo(theme.Colors.Success);
        await Assert.That(t.TrackOffColor).IsEqualTo(theme.Colors.Border);
    }

    [Test]
    public async Task AppleTheme_Toggle_27pxThumbWithShadow()
    {
        var theme = new AppleTheme();
        var t = theme.Toggle;

        await Assert.That(t.ThumbSize).IsEqualTo(27f);
        await Assert.That(t.ThumbColor).IsEqualTo(new ColorValue("#FFFFFF"));
        await Assert.That(t.ThumbShadow).IsEqualTo(AppleTokens.ThumbShadow);
    }

    [Test]
    public async Task AppleTheme_Toggle_ThumbOffsets()
    {
        var theme = new AppleTheme();
        var t = theme.Toggle;

        await Assert.That(t.ThumbOffsetOn).IsEqualTo(22f);
        await Assert.That(t.ThumbOffsetOff).IsEqualTo(2f);
    }

    // ── Tooltip ─────────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_Tooltip_DarkBackground()
    {
        var theme = new AppleTheme();
        var tip = theme.Tooltip;

        await Assert.That(tip.Background).IsEqualTo(new ColorValue("#1C1C1E"));
        await Assert.That(tip.TextColor).IsEqualTo(new ColorValue("#FFFFFF"));
        await Assert.That(tip.ArrowSize).IsEqualTo(6f);
        await Assert.That(tip.ArrowHeight).IsEqualTo(5f);
        await Assert.That(tip.MaxWidth).IsEqualTo(240f);
    }

    // ── NavBar ──────────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_NavBar_52dpTranslucent()
    {
        var theme = new AppleTheme();
        var nav = theme.NavBar;

        await Assert.That(nav.Height).IsEqualTo(52f);
        await Assert.That(nav.BackgroundOpacity).IsEqualTo(0.94f);
        await Assert.That(nav.BlurRadius).IsEqualTo(20f);
        await Assert.That(nav.BorderWidth).IsEqualTo(0.5f);
    }

    [Test]
    public async Task AppleTheme_NavBar_TitleAndAction()
    {
        var theme = new AppleTheme();
        var nav = theme.NavBar;

        await Assert.That(nav.TitleStyle).IsEqualTo(theme.Typography.Heading3);
        await Assert.That(nav.ActionColor).IsEqualTo(theme.Colors.Primary);
    }

    // ── Dialog ──────────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_Dialog_LgRadius400MaxWidth()
    {
        var theme = new AppleTheme();
        var d = theme.Dialog;

        await Assert.That(d.Radius).IsEqualTo(AppleTokens.Radius.Lg);
        await Assert.That(d.MaxWidth).IsEqualTo(400f);
        await Assert.That(d.EnterScale).IsEqualTo(0.94f);
        await Assert.That(d.BackdropBlur).IsEqualTo(0f);
    }

    [Test]
    public async Task AppleTheme_Dialog_BackdropColor40Percent()
    {
        var theme = new AppleTheme();
        var expected = new ColorValue("#000000").Opacity(0.40f);
        await Assert.That(theme.Dialog.BackdropColor).IsEqualTo(expected);
        await Assert.That(theme.Dialog.TitleStyle).IsEqualTo(theme.Typography.Heading3);
    }

    // ── Scroll ──────────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_Scroll_OverlayThumbs()
    {
        var theme = new AppleTheme();
        var s = theme.Scroll;

        await Assert.That(s.OverlayRadius).IsEqualTo(3.5f);
        await Assert.That(s.TrackThumbRadius).IsEqualTo(0f);
        await Assert.That(s.OverlayWidth).IsEqualTo(7f);
    }

    [Test]
    public async Task AppleTheme_Scroll_DarkModeThumbColors()
    {
        var theme = new AppleTheme(ThemeMode.Dark);
        var s = theme.Scroll;

        await Assert.That(s.OverlayThumbColor).IsEqualTo(AppleTokens.DarkText35);
        await Assert.That(s.OverlayThumbHoverColor).IsEqualTo(AppleTokens.DarkText55);
    }

    [Test]
    public async Task AppleTheme_Scroll_LightModeThumbColors()
    {
        var theme = new AppleTheme(ThemeMode.Light);
        var s = theme.Scroll;

        await Assert.That(s.OverlayThumbColor).IsEqualTo(AppleTokens.LightText35);
        await Assert.That(s.OverlayThumbHoverColor).IsEqualTo(AppleTokens.LightText55);
    }

    [Test]
    public async Task AppleTheme_Scroll_RubberBandPhysics()
    {
        var theme = new AppleTheme();
        var s = theme.Scroll;

        await Assert.That(s.RubberBandMaxStretch).IsEqualTo(80f);
        await Assert.That(s.RubberBandResistance).IsEqualTo(220f);
    }

    [Test]
    public async Task AppleTheme_Scroll_SquareTrackThumb()
    {
        var theme = new AppleTheme();
        await Assert.That(theme.Scroll.TrackThumbRadius).IsEqualTo(0f);
        await Assert.That(theme.Scroll.TrackThumbMinHeight).IsEqualTo(32f);
        await Assert.That(theme.Scroll.OverlayPadding).IsEqualTo(2f);
        await Assert.That(theme.Scroll.MouseWheelStepPx).IsEqualTo(48f);
        await Assert.That(theme.Scroll.TrackpadDecelerationRate).IsEqualTo(0.998f);
        await Assert.That(theme.Scroll.FadeEdgeLength).IsEqualTo(32f);
    }

    // ── IconAnimation ───────────────────────────────────────────

    [Test]
    public async Task AppleTheme_IconAnimation_MorphWithSpringStandard()
    {
        var theme = new AppleTheme();
        var icon = theme.IconAnimation;

        await Assert.That(icon.DefaultTransition).IsEqualTo(IconTransition.Morph);
        await Assert.That(icon.TransitionModel).IsEqualTo(AnimationModel.Spring.Standard);
        await Assert.That(icon.AttentionModel).IsEqualTo(AnimationModel.Spring.Snappy);
        await Assert.That(icon.AttentionIntensity).IsEqualTo(0.9f);
    }

    // ── Caret ───────────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_Caret_TextColorSmoothBlink()
    {
        var theme = new AppleTheme();
        var c = theme.Caret;

        await Assert.That(c.Color).IsEqualTo(theme.Colors.Text);
        await Assert.That(c.Width).IsEqualTo(2f);
        await Assert.That(c.SmoothBlink).IsTrue();
        await Assert.That(c.BlinkInterval).IsEqualTo(Duration.Ms(1060));
    }

    // ── Password ────────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_Password_StrengthIndicatorEnabled()
    {
        var theme = new AppleTheme();
        var p = theme.Password;

        await Assert.That(p.ShowStrengthIndicator).IsTrue();
        await Assert.That(p.ShowRevealToggle).IsTrue();
        await Assert.That(p.StrengthBarHeight).IsEqualTo(3f);
    }

    // ── Chart ───────────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_Chart_UsesThemeColors()
    {
        var theme = new AppleTheme();
        var chart = theme.Chart;

        await Assert.That(chart.GridLine).IsEqualTo(theme.Colors.Border);
        await Assert.That(chart.Axis).IsEqualTo(theme.Colors.TextMuted);
        await Assert.That(chart.BarRadius).IsEqualTo(AppleTokens.Radius.Sm);
    }

    // ── Spinner ─────────────────────────────────────────────────

    [Test]
    public async Task AppleTheme_Spinner_ArcStyle()
    {
        var spinner = AppleTokens.Spinner;
        await Assert.That(spinner.Thickness).IsEqualTo(2.5f);
    }

    // ── High contrast ───────────────────────────────────────────

    [Test]
    public async Task AppleTheme_HighContrastLight_MatchesSpec()
    {
        var theme = new AppleTheme();
        var hc = theme.HighContrastLightColors;

        await Assert.That(hc).IsNotNull();
        await Assert.That(hc!.Primary).IsEqualTo(new ColorValue("#0000EE"));
        await Assert.That(hc.Text).IsEqualTo(new ColorValue("#000000"));
        await Assert.That(hc.Border).IsEqualTo(new ColorValue("#000000"));
        await Assert.That(hc.TextMuted).IsEqualTo(new ColorValue("#3D3D3D"));
        await Assert.That(hc.Danger).IsEqualTo(new ColorValue("#C00000"));
        await Assert.That(hc.Success).IsEqualTo(new ColorValue("#007000"));
    }

    [Test]
    public async Task AppleTheme_HighContrastDark_IsNotNull()
    {
        var theme = new AppleTheme();
        await Assert.That(theme.HighContrastDarkColors).IsNotNull();
        await Assert.That(theme.HighContrastDarkColors!.Text).IsEqualTo(new ColorValue("#FFFFFF"));
        await Assert.That(theme.HighContrastDarkColors!.Border).IsEqualTo(new ColorValue("#FFFFFF"));
    }

    // ── Cross-mode consistency ──────────────────────────────────

    [Test]
    public async Task AppleTheme_BothModes_ShareTypography()
    {
        var light = new AppleTheme(ThemeMode.Light);
        var dark = new AppleTheme(ThemeMode.Dark);

        await Assert.That(light.Typography.Scale.Body.Size).IsEqualTo(dark.Typography.Scale.Body.Size);
    }

    [Test]
    public async Task AppleTheme_BothModes_ShareRadiusAndSpacing()
    {
        var light = new AppleTheme(ThemeMode.Light);
        var dark = new AppleTheme(ThemeMode.Dark);

        await Assert.That(light.Radius.Full).IsEqualTo(dark.Radius.Full);
        await Assert.That(light.Spacing.Base).IsEqualTo(dark.Spacing.Base);
    }

    [Test]
    public async Task AppleTheme_LightAndDark_HaveDifferentPrimary()
    {
        var light = new AppleTheme(ThemeMode.Light);
        var dark = new AppleTheme(ThemeMode.Dark);

        await Assert.That(light.Colors.Primary).IsNotEqualTo(dark.Colors.Primary);
    }

    // ── All control themes present ──────────────────────────────

    [Test]
    public async Task AppleTheme_AllControlThemes_AreNotNull()
    {
        var theme = new AppleTheme();
        await Assert.That(theme.Button).IsNotNull();
        await Assert.That(theme.TextInput).IsNotNull();
        await Assert.That(theme.Toggle).IsNotNull();
        await Assert.That(theme.Checkbox).IsNotNull();
        await Assert.That(theme.Slider).IsNotNull();
        await Assert.That(theme.Select).IsNotNull();
        await Assert.That(theme.Tabs).IsNotNull();
        await Assert.That(theme.Progress).IsNotNull();
        await Assert.That(theme.Badge).IsNotNull();
        await Assert.That(theme.Card).IsNotNull();
        await Assert.That(theme.Tooltip).IsNotNull();
        await Assert.That(theme.Dialog).IsNotNull();
        await Assert.That(theme.NavBar).IsNotNull();
        await Assert.That(theme.Scroll).IsNotNull();
        await Assert.That(theme.IconAnimation).IsNotNull();
        await Assert.That(theme.Caret).IsNotNull();
        await Assert.That(theme.Password).IsNotNull();
        await Assert.That(theme.Chart).IsNotNull();
    }

    // ── Apple-specific behavioral tests ─────────────────────────

    [Test]
    public async Task AppleTheme_Slider_22x20ThumbWithGradient()
    {
        var theme = new AppleTheme();
        var s = theme.Slider;

        await Assert.That(s.ThumbWidth).IsEqualTo(22f);
        await Assert.That(s.ThumbHeight).IsEqualTo(20f);
        await Assert.That(s.ThumbFill!.Kind).IsEqualTo(BrushKind.LinearGradient);
        await Assert.That(s.ThumbDisabled!.Opacity).IsEqualTo(0.35f);
        await Assert.That(s.ShowTicks).IsFalse();
    }

    [Test]
    public async Task AppleTheme_Slider_HoverWithProximityEffect()
    {
        var theme = new AppleTheme();
        var hover = theme.Slider.ThumbHover!;

        await Assert.That(hover.ScaleX).IsEqualTo(1.05f);
        await Assert.That(hover.ScaleY).IsEqualTo(1.05f);
        await Assert.That(hover.Proximity).IsNotNull();
        await Assert.That(hover.Proximity!.Radius).IsEqualTo(48f);
        await Assert.That(hover.Proximity.Property).IsEqualTo(ProximityProperty.ShadowSpread);
    }

    [Test]
    public async Task AppleTheme_Slider_PressedScaleDown097()
    {
        var theme = new AppleTheme();
        var pressed = theme.Slider.ThumbPressed!;

        await Assert.That(pressed.ScaleX).IsEqualTo(0.97f);
        await Assert.That(pressed.ScaleY).IsEqualTo(0.97f);
    }

    [Test]
    public async Task AppleTheme_Select_36pxWithDropdown()
    {
        var theme = new AppleTheme();
        var s = theme.Select;

        await Assert.That(s.Height).IsEqualTo(36f);
        await Assert.That(s.Radius).IsEqualTo(AppleTokens.Radius.Base);
        await Assert.That(s.Background).IsEqualTo(theme.Colors.Surface);
        await Assert.That(s.FocusRingWidth).IsEqualTo(3f);
    }

    [Test]
    public async Task AppleTheme_Tabs_44pxWithBorderIndicator()
    {
        var theme = new AppleTheme();
        var t = theme.Tabs;

        await Assert.That(t.Height).IsEqualTo(44f);
        await Assert.That(t.IndicatorColor).IsEqualTo(theme.Colors.Primary);
        await Assert.That(t.FocusRingWidth).IsEqualTo(3f);
    }

    [Test]
    public async Task AppleTheme_Progress_4pxBarRounded()
    {
        var theme = new AppleTheme();
        var p = theme.Progress;

        await Assert.That(p.BarHeight).IsEqualTo(4f);
        await Assert.That(p.BarRadius).IsEqualTo(AppleTokens.Radius.Full);
        await Assert.That(p.FillColor).IsEqualTo(theme.Colors.Primary);
    }

    [Test]
    public async Task AppleTheme_Badge_DangerPill()
    {
        var theme = new AppleTheme();
        var b = theme.Badge;

        await Assert.That(b.Radius).IsEqualTo(AppleTokens.Radius.Full);
        await Assert.That(b.Background).IsEqualTo(theme.Colors.Danger);
    }

    [Test]
    public async Task AppleTheme_Card_SurfaceBgWithShadow()
    {
        var theme = new AppleTheme();
        var c = theme.Card;

        await Assert.That(c.Background).IsEqualTo(theme.Colors.Surface);
        await Assert.That(c.Radius).IsEqualTo(AppleTokens.Radius.Md);
    }

    [Test]
    public async Task AppleTheme_Toggle_UsesGreen_NotPrimary()
    {
        var theme = new AppleTheme();
        await Assert.That(theme.Toggle.TrackOnColor).IsEqualTo(theme.Colors.Success);
        await Assert.That(theme.Toggle.TrackOnColor).IsNotEqualTo(theme.Colors.Primary);
    }

    [Test]
    public async Task AppleTheme_Button_PressedHasScale096()
    {
        // Pressed scale tuned to 0.96 by the author in WP-3201 ("theme tuning");
        // 0.97 was never the shipped value. The theme is the source of truth.
        var theme = new AppleTheme();
        await Assert.That(theme.Button.Pressed.Scale).IsEqualTo(0.96f);

        var outline = theme.Button.Variants["outline"];
        await Assert.That(outline.Pressed.Scale).IsEqualTo(0.96f);

        var ghost = theme.Button.Variants["ghost"];
        await Assert.That(ghost.Pressed.Scale).IsEqualTo(0.96f);
    }

    [Test]
    public async Task AppleTheme_AllFocusRings_Are3px()
    {
        var theme = new AppleTheme();
        await Assert.That(theme.Button.Focused.OutlineWidth).IsEqualTo(3f);
        await Assert.That(theme.TextInput.FocusRingWidth).IsEqualTo(3f);
        await Assert.That(theme.Toggle.FocusRingWidth).IsEqualTo(3f);
        await Assert.That(theme.Checkbox.FocusRingWidth).IsEqualTo(3f);
        await Assert.That(theme.Select.FocusRingWidth).IsEqualTo(3f);
        await Assert.That(theme.Tabs.FocusRingWidth).IsEqualTo(3f);
    }
}
