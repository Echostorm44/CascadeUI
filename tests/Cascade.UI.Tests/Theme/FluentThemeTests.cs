using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class FluentThemeTests
{
    // ── Construction ─────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_DefaultsToLightMode()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Colors.Primary).IsEqualTo(FluentTokens.LightPrimary);
    }

    [Test]
    public async Task FluentTheme_LightMode_UsesLightColors()
    {
        var theme = new FluentTheme(ThemeMode.Light);
        await Assert.That(theme.Colors.Primary).IsEqualTo(FluentTokens.LightPrimary);
        await Assert.That(theme.Colors.Background).IsEqualTo(FluentTokens.LightBackground);
        await Assert.That(theme.Colors.Text).IsEqualTo(FluentTokens.LightText);
    }

    [Test]
    public async Task FluentTheme_DarkMode_UsesDarkColors()
    {
        var theme = new FluentTheme(ThemeMode.Dark);
        await Assert.That(theme.Colors.Primary).IsEqualTo(FluentTokens.DarkPrimary);
        await Assert.That(theme.Colors.Background).IsEqualTo(FluentTokens.DarkBackground);
        await Assert.That(theme.Colors.Text).IsEqualTo(FluentTokens.DarkText);
    }

    [Test]
    public async Task FluentTheme_LightColors_MatchSpec()
    {
        var theme = new FluentTheme(ThemeMode.Light);
        var c = theme.LightColors;

        await Assert.That(c.Primary).IsEqualTo(new ColorValue("#0078D4"));
        await Assert.That(c.PrimaryText).IsEqualTo(new ColorValue("#0078D4"));
        await Assert.That(c.TextOnPrimary).IsEqualTo(new ColorValue("#FFFFFF"));
        await Assert.That(c.Background).IsEqualTo(new ColorValue("#F3F3F3"));
        await Assert.That(c.Surface).IsEqualTo(new ColorValue("#FFFFFF"));
        await Assert.That(c.SurfaceAlt).IsEqualTo(new ColorValue("#F0F0F0"));
        await Assert.That(c.Border).IsEqualTo(new ColorValue("#D1D1D1"));
        await Assert.That(c.Text).IsEqualTo(new ColorValue("#242424"));
        await Assert.That(c.TextMuted).IsEqualTo(new ColorValue("#616161"));
        await Assert.That(c.Danger).IsEqualTo(new ColorValue("#BC2F32"));
        await Assert.That(c.Success).IsEqualTo(new ColorValue("#107C10"));
        await Assert.That(c.Warning).IsEqualTo(new ColorValue("#D47300"));
        await Assert.That(c.Focus).IsEqualTo(new ColorValue("#0078D4"));
    }

    [Test]
    public async Task FluentTheme_DarkColors_MatchSpec()
    {
        var theme = new FluentTheme(ThemeMode.Dark);
        var c = theme.DarkColors;

        await Assert.That(c.Primary).IsEqualTo(new ColorValue("#0F6CBD"));
        await Assert.That(c.PrimaryText).IsEqualTo(new ColorValue("#479EF5"));
        await Assert.That(c.TextOnPrimary).IsEqualTo(new ColorValue("#FFFFFF"));
        await Assert.That(c.Background).IsEqualTo(new ColorValue("#292929"));
        await Assert.That(c.Surface).IsEqualTo(new ColorValue("#333333"));
        await Assert.That(c.SurfaceAlt).IsEqualTo(new ColorValue("#383838"));
        await Assert.That(c.Border).IsEqualTo(new ColorValue("#666666"));
        await Assert.That(c.Text).IsEqualTo(new ColorValue("#FFFFFF"));
        await Assert.That(c.TextMuted).IsEqualTo(new ColorValue("#ADADAD"));
        await Assert.That(c.Danger).IsEqualTo(new ColorValue("#F1707B"));
        await Assert.That(c.Success).IsEqualTo(new ColorValue("#54B054"));
        await Assert.That(c.Warning).IsEqualTo(new ColorValue("#FCE100"));
        await Assert.That(c.Focus).IsEqualTo(new ColorValue("#479EF5"));
    }

    // ── Typography ──────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_Typography_BodyIs14px()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Typography.Body.Size).IsEqualTo(14);
        await Assert.That(theme.Typography.Body.Weight).IsEqualTo(FontWeight.Regular);
    }

    [Test]
    public async Task FluentTheme_Typography_DisplayIs40px()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Typography.Display.Size).IsEqualTo(40);
        await Assert.That(theme.Typography.Display.Weight).IsEqualTo(FontWeight.SemiBold);
    }

    [Test]
    public async Task FluentTheme_Typography_HeadingScaleMatchesSpec()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Typography.Heading1.Size).IsEqualTo(28);
        await Assert.That(theme.Typography.Heading2.Size).IsEqualTo(20);
        await Assert.That(theme.Typography.Heading3.Size).IsEqualTo(16);
        await Assert.That(theme.Typography.Caption.Size).IsEqualTo(11);
        await Assert.That(theme.Typography.Code.Size).IsEqualTo(13);
    }

    [Test]
    public async Task FluentTheme_Typography_FontFamilyIsInter()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Typography.FontFamily.Kind).IsEqualTo(FontFamilyKind.Bundled);
        await Assert.That(theme.Typography.FontFamily.BundledName).IsEqualTo("Inter");
    }

    // ── Spacing ─────────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_SpacingBase_Is4()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Spacing.Base).IsEqualTo(4f);
    }

    [Test]
    public async Task FluentTheme_SpacingDerived_MatchMultiples()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Spacing.Xs).IsEqualTo(4f);
        await Assert.That(theme.Spacing.Sm).IsEqualTo(8f);
        await Assert.That(theme.Spacing.Md).IsEqualTo(16f);
        await Assert.That(theme.Spacing.Lg).IsEqualTo(24f);
        await Assert.That(theme.Spacing.Xl).IsEqualTo(32f);
    }

    // ── Radius ──────────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_Radius_MatchSpec()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Radius.None).IsEqualTo(0f);
        await Assert.That(theme.Radius.Sm).IsEqualTo(2f);
        await Assert.That(theme.Radius.Base).IsEqualTo(4f);
        await Assert.That(theme.Radius.Md).IsEqualTo(6f);
        await Assert.That(theme.Radius.Lg).IsEqualTo(8f);
        await Assert.That(theme.Radius.Xl).IsEqualTo(12f);
        await Assert.That(theme.Radius.Full).IsEqualTo(9999f);
    }

    // ── Motion ──────────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_Personality_IsSnappy()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Personality).IsEqualTo(MotionPersonality.Snappy);
    }

    // ── Button ──────────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_Button_Height32()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Button.Height).IsEqualTo(32f);
    }

    [Test]
    public async Task FluentTheme_Button_Radius4()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Button.Radius).IsEqualTo(4f);
    }

    [Test]
    public async Task FluentTheme_Button_NoShadow()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Button.Shadow).IsEqualTo(ShadowSpec.None);
    }

    [Test]
    public async Task FluentTheme_Button_Focused_CompoundRing()
    {
        var theme = new FluentTheme();
        var focused = theme.Button.Focused;

        await Assert.That(focused.OutlineColor).IsEqualTo(theme.Colors.Focus);
        await Assert.That(focused.OutlineWidth).IsEqualTo(2f);
        await Assert.That(focused.OutlineOffset).IsEqualTo(2f);
        await Assert.That(focused.InnerRingColor).IsEqualTo(theme.Colors.Surface);
        await Assert.That(focused.InnerRingWidth).IsEqualTo(2f);
    }

    [Test]
    public async Task FluentTheme_Button_Disabled_Opacity038()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Button.Disabled.BackgroundOpacity).IsEqualTo(0.38f);
        await Assert.That(theme.Button.Disabled.TextOpacity).IsEqualTo(0.38f);
    }

    [Test]
    public async Task FluentTheme_Button_HasFourVariants()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Button.Variants.Count).IsEqualTo(4);
        await Assert.That(theme.Button.Variants.ContainsKey("outline")).IsTrue();
        await Assert.That(theme.Button.Variants.ContainsKey("ghost")).IsTrue();
        await Assert.That(theme.Button.Variants.ContainsKey("subtle")).IsTrue();
        await Assert.That(theme.Button.Variants.ContainsKey("destructive")).IsTrue();
    }

    [Test]
    public async Task FluentTheme_Button_OutlineVariant_TransparentBackground()
    {
        var theme = new FluentTheme();
        var outline = theme.Button.Variants["outline"];
        await Assert.That(outline.TextColor).IsEqualTo(theme.Colors.Primary);
        await Assert.That(outline.BorderWidth).IsEqualTo(1f);
    }

    [Test]
    public async Task FluentTheme_Button_GhostVariant_NoBorder()
    {
        var theme = new FluentTheme();
        var ghost = theme.Button.Variants["ghost"];
        await Assert.That(ghost.TextColor).IsEqualTo(theme.Colors.Text);
        await Assert.That(ghost.Border).IsNull();
        await Assert.That(ghost.BorderWidth).IsEqualTo(0f);
    }

    [Test]
    public async Task FluentTheme_Button_DestructiveVariant_UsesDanger()
    {
        var theme = new FluentTheme();
        var destructive = theme.Button.Variants["destructive"];
        await Assert.That(destructive.TextColor).IsEqualTo(theme.Colors.TextOnPrimary);
    }

    // ── TextInput ───────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_TextInput_Height32()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.TextInput.Height).IsEqualTo(32f);
    }

    [Test]
    public async Task FluentTheme_TextInput_CompoundFocusRing()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.TextInput.FocusRingWidth).IsEqualTo(2f);
        await Assert.That(theme.TextInput.InnerRingColor).IsEqualTo(theme.Colors.Surface);
        await Assert.That(theme.TextInput.InnerRingWidth).IsEqualTo(2f);
    }

    [Test]
    public async Task FluentTheme_TextInput_ErrorState()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.TextInput.ErrorBorderColor).IsEqualTo(theme.Colors.Danger);
        await Assert.That(theme.TextInput.ErrorRingColor).IsEqualTo(theme.Colors.Danger);
    }

    [Test]
    public async Task FluentTheme_TextInput_DisabledState()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.TextInput.DisabledBackground).IsEqualTo(theme.Colors.SurfaceAlt);
        await Assert.That(theme.TextInput.DisabledTextColor).IsEqualTo(theme.Colors.TextMuted);
    }

    // ── Toggle ──────────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_Toggle_Dimensions()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Toggle.TrackWidth).IsEqualTo(40f);
        await Assert.That(theme.Toggle.TrackHeight).IsEqualTo(20f);
        await Assert.That(theme.Toggle.ThumbSize).IsEqualTo(14f);
    }

    [Test]
    public async Task FluentTheme_Toggle_BlueWhenOn()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Toggle.TrackOnColor).IsEqualTo(theme.Colors.Primary);
    }

    [Test]
    public async Task FluentTheme_Toggle_NoThumbShadow()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Toggle.ThumbShadow).IsEqualTo(ShadowSpec.None);
    }

    [Test]
    public async Task FluentTheme_Toggle_ThumbOffsets()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Toggle.ThumbOffsetOn).IsEqualTo(22f);
        await Assert.That(theme.Toggle.ThumbOffsetOff).IsEqualTo(4f);
    }

    [Test]
    public async Task FluentTheme_Toggle_LabelOnLeft()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Toggle.LabelPosition).IsEqualTo(ToggleLabelPosition.Left);
    }

    [Test]
    public async Task FluentTheme_Toggle_DisabledOpacity038()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Toggle.DisabledOpacity).IsEqualTo(0.38f);
    }

    // ── NavBar ──────────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_NavBar_Height48()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.NavBar.Height).IsEqualTo(48f);
    }

    [Test]
    public async Task FluentTheme_NavBar_NoBorder()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.NavBar.BorderBottom).IsEqualTo(ColorValue.Transparent);
        await Assert.That(theme.NavBar.BorderWidth).IsEqualTo(0f);
    }

    [Test]
    public async Task FluentTheme_NavBar_SolidBackground()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.NavBar.BackgroundOpacity).IsEqualTo(1.0f);
    }

    // ── Scroll ──────────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_Scroll_OverlayWidth6()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Scroll.OverlayWidth).IsEqualTo(6f);
        await Assert.That(theme.Scroll.OverlayWidthHover).IsEqualTo(10f);
    }

    [Test]
    public async Task FluentTheme_Scroll_SquareTrackThumbs()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Scroll.TrackThumbRadius).IsEqualTo(0f);
    }

    [Test]
    public async Task FluentTheme_Scroll_RubberBand60()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Scroll.RubberBandMaxStretch).IsEqualTo(60f);
        await Assert.That(theme.Scroll.RubberBandResistance).IsEqualTo(250f);
    }

    [Test]
    public async Task FluentTheme_Scroll_TrackWidth14()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Scroll.TrackWidth).IsEqualTo(14f);
    }

    // ── IconAnimation ───────────────────────────────────────────

    [Test]
    public async Task FluentTheme_IconAnimation_Crossfade()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.IconAnimation.DefaultTransition).IsEqualTo(IconTransition.Crossfade);
    }

    [Test]
    public async Task FluentTheme_IconAnimation_AttentionIntensity1()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.IconAnimation.AttentionIntensity).IsEqualTo(1.0f);
    }

    // ── Card ────────────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_Card_HasBorder()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Card.BorderColor).IsEqualTo(theme.Colors.Border);
        await Assert.That(theme.Card.BorderWidth).IsEqualTo(1f);
    }

    [Test]
    public async Task FluentTheme_Card_SmallShadow()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Card.Shadow).IsEqualTo(FluentTokens.ShadowSm);
    }

    [Test]
    public async Task FluentTheme_Card_NoHoverScale()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Card.HoverScale).IsNull();
    }

    // ── Dialog ──────────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_Dialog_MaxWidth480()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Dialog.MaxWidth).IsEqualTo(480f);
    }

    [Test]
    public async Task FluentTheme_Dialog_EnterScale096()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Dialog.EnterScale).IsEqualTo(0.96f);
    }

    // ── Checkbox ────────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_Checkbox_Size18()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Checkbox.Size).IsEqualTo(18f);
    }

    [Test]
    public async Task FluentTheme_Checkbox_FocusRing2px()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Checkbox.FocusRingWidth).IsEqualTo(2f);
    }

    [Test]
    public async Task FluentTheme_Checkbox_DisabledOpacity038()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Checkbox.DisabledOpacity).IsEqualTo(0.38f);
    }

    // ── Select ──────────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_Select_Height32()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Select.Height).IsEqualTo(32f);
    }

    [Test]
    public async Task FluentTheme_Select_DropdownMaxHeight360()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Select.DropdownMaxHeight).IsEqualTo(360f);
    }

    // ── Progress ────────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_Progress_ThinBar()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Progress.BarHeight).IsEqualTo(4f);
    }

    [Test]
    public async Task FluentTheme_Progress_ThinRing()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Progress.RingStrokeWidth).IsEqualTo(2f);
    }

    // ── Caret ───────────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_Caret_UsesTextColor()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Caret.Color).IsEqualTo(theme.Colors.Text);
    }

    [Test]
    public async Task FluentTheme_Caret_SharpBlink()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Caret.SmoothBlink).IsFalse();
    }

    // ── Password ────────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_Password_BulletMask()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Password.MaskCharacter).IsEqualTo('●');
    }

    [Test]
    public async Task FluentTheme_Password_ShowsRevealToggle()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Password.ShowRevealToggle).IsTrue();
    }

    // ── Spinner ─────────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_Spinner_IsArc()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Spinner).IsNotNull();
    }

    // ── Cross-cutting ───────────────────────────────────────────

    [Test]
    public async Task FluentTheme_InheritsFromCascadeTheme()
    {
        var theme = new FluentTheme();
        await Assert.That(theme is CascadeTheme).IsTrue();
    }

    [Test]
    public async Task FluentTheme_LightAndDarkColors_AreDifferent()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.LightColors.Primary).IsNotEqualTo(theme.DarkColors.Primary);
        await Assert.That(theme.LightColors.Background).IsNotEqualTo(theme.DarkColors.Background);
    }

    [Test]
    public async Task FluentTheme_AllControlThemes_AreNotNull()
    {
        var theme = new FluentTheme();
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

    [Test]
    public async Task FluentTheme_DarkMode_AllControlThemes_AreNotNull()
    {
        var theme = new FluentTheme(ThemeMode.Dark);
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

    // ── Fluent-specific behavioral tests ────────────────────────

    [Test]
    public async Task FluentTheme_Button_HoverUsesOverlay_NotScale()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Button.Hover.OverlayColor).IsNotNull();
        await Assert.That(theme.Button.Hover.Scale).IsNull();
    }

    [Test]
    public async Task FluentTheme_AllFocusRings_Are2px()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Button.Focused.OutlineWidth).IsEqualTo(2f);
        await Assert.That(theme.TextInput.FocusRingWidth).IsEqualTo(2f);
        await Assert.That(theme.Toggle.FocusRingWidth).IsEqualTo(2f);
        await Assert.That(theme.Checkbox.FocusRingWidth).IsEqualTo(2f);
        await Assert.That(theme.Select.FocusRingWidth).IsEqualTo(2f);
        await Assert.That(theme.Tabs.FocusRingWidth).IsEqualTo(2f);
    }

    [Test]
    public async Task FluentTheme_AllDisabledOpacities_Are038()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Toggle.DisabledOpacity).IsEqualTo(0.38f);
        await Assert.That(theme.Checkbox.DisabledOpacity).IsEqualTo(0.38f);
        await Assert.That(theme.Select.DisabledOpacity).IsEqualTo(0.38f);
        await Assert.That(theme.Tabs.DisabledOpacity).IsEqualTo(0.38f);
    }

    [Test]
    public async Task FluentTheme_CompactControlHeights_32px()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Button.Height).IsEqualTo(32f);
        await Assert.That(theme.TextInput.Height).IsEqualTo(32f);
        await Assert.That(theme.Select.Height).IsEqualTo(32f);
    }

    // ── Overlay color exact values ──────────────────────────────

    [Test]
    public async Task FluentTheme_Button_HoverOverlayColor_MatchesSpec()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Button.Hover.OverlayColor).IsEqualTo(FluentTokens.OverlayBlack06);
        await Assert.That(theme.Button.Pressed.OverlayColor).IsEqualTo(FluentTokens.OverlayBlack12);
    }

    // ── Dark mode specific values ───────────────────────────────

    [Test]
    public async Task FluentTheme_DarkMode_ScrollThumbColors()
    {
        var theme = new FluentTheme(ThemeMode.Dark);
        var s = theme.Scroll;

        await Assert.That(s.OverlayThumbColor).IsEqualTo(FluentTokens.DarkText40);
        await Assert.That(s.OverlayThumbHoverColor).IsEqualTo(FluentTokens.DarkText60);
    }

    [Test]
    public async Task FluentTheme_LightMode_ScrollThumbColors()
    {
        var theme = new FluentTheme(ThemeMode.Light);
        var s = theme.Scroll;

        await Assert.That(s.OverlayThumbColor).IsEqualTo(FluentTokens.LightText40);
        await Assert.That(s.OverlayThumbHoverColor).IsEqualTo(FluentTokens.LightText60);
    }

    // ── TextInput spec values ───────────────────────────────────

    [Test]
    public async Task FluentTheme_TextInput_SpecValues()
    {
        var theme = new FluentTheme();
        var ti = theme.TextInput;

        await Assert.That(ti.PaddingH).IsEqualTo(8f);
        await Assert.That(ti.Background).IsEqualTo(theme.Colors.Surface);
        await Assert.That(ti.BorderColor).IsEqualTo(theme.Colors.Border);
        await Assert.That(ti.BorderWidth).IsEqualTo(1f);
        await Assert.That(ti.FocusBorderColor).IsEqualTo(theme.Colors.Primary);
        await Assert.That(ti.FocusBorderWidth).IsEqualTo(1f);
    }

    // ── Toggle spec values ──────────────────────────────────────

    [Test]
    public async Task FluentTheme_Toggle_TrackAndThumbColors()
    {
        var theme = new FluentTheme();
        var t = theme.Toggle;

        await Assert.That(t.TrackOffColor).IsEqualTo(theme.Colors.Border);
        await Assert.That(t.ThumbColor).IsEqualTo(new ColorValue("#FFFFFF"));
    }

    // ── NavBar spec values ──────────────────────────────────────

    [Test]
    public async Task FluentTheme_NavBar_TitleAndAction()
    {
        var theme = new FluentTheme();
        var nav = theme.NavBar;

        await Assert.That(nav.TitleStyle).IsEqualTo(theme.Typography.Heading3);
        await Assert.That(nav.ActionColor).IsEqualTo(theme.Colors.Primary);
    }

    // ── IconAnimation ───────────────────────────────────────────

    [Test]
    public async Task FluentTheme_IconAnimation_CrossfadeWithFluentMotion()
    {
        var theme = new FluentTheme();
        var icon = theme.IconAnimation;

        await Assert.That(icon.DefaultTransition).IsEqualTo(IconTransition.Crossfade);
        await Assert.That(icon.TransitionModel).IsEqualTo(AnimationModel.FluentFast);
    }

    // ── High contrast ───────────────────────────────────────────

    [Test]
    public async Task FluentTheme_HighContrastLight_IsNotNull()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.HighContrastLightColors).IsNotNull();
        await Assert.That(theme.HighContrastLightColors!.Text).IsEqualTo(new ColorValue("#000000"));
        await Assert.That(theme.HighContrastLightColors!.Border).IsEqualTo(new ColorValue("#000000"));
    }

    [Test]
    public async Task FluentTheme_HighContrastDark_IsNotNull()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.HighContrastDarkColors).IsNotNull();
        await Assert.That(theme.HighContrastDarkColors!.Text).IsEqualTo(new ColorValue("#FFFFFF"));
        await Assert.That(theme.HighContrastDarkColors!.Border).IsEqualTo(new ColorValue("#FFFFFF"));
    }

    // ── ThemeMode.System fallback ───────────────────────────────

    [Test]
    public async Task FluentTheme_SystemMode_FallsBackToLight()
    {
        var system = new FluentTheme(ThemeMode.System);
        var light = new FluentTheme(ThemeMode.Light);

        await Assert.That(system.Colors.Primary).IsEqualTo(light.Colors.Primary);
    }
}
