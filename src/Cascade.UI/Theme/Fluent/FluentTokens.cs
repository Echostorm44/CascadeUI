namespace Cascade.UI;

/// <summary>
/// Fluent Design System tokens (Windows 11) — shared constants for FluentTheme.
/// </summary>
public static class FluentTokens
{
    // ── Core colors (Light) ────────────────────────────────────────────

    public static readonly ColorValue LightPrimary = new("#0078D4");
    public static readonly ColorValue LightPrimaryText = new("#0078D4");
    public static readonly ColorValue LightTextOnPrimary = new("#FFFFFF");
    public static readonly ColorValue LightBackground = new("#F3F3F3");
    public static readonly ColorValue LightSurface = new("#FFFFFF");
    public static readonly ColorValue LightSurfaceAlt = new("#F0F0F0");
    public static readonly ColorValue LightBorder = new("#D1D1D1");
    public static readonly ColorValue LightText = new("#242424");
    public static readonly ColorValue LightTextMuted = new("#616161");
    public static readonly ColorValue LightDanger = new("#BC2F32");
    public static readonly ColorValue LightDangerSubtle = new("#BC2F321A");
    public static readonly ColorValue LightSuccess = new("#107C10");
    public static readonly ColorValue LightSuccessSubtle = new("#107C101A");
    public static readonly ColorValue LightWarning = new("#D47300");
    public static readonly ColorValue LightWarningSubtle = new("#D473001A");
    public static readonly ColorValue LightFocus = new("#0078D4");

    // ── Core colors (Dark) ─────────────────────────────────────────────
    // Values sourced from Fluent 2 Design System (https://fluent2.microsoft.design/color-tokens/)
    // grey[N] ≈ round(N/100 × 255) per channel

    public static readonly ColorValue DarkPrimary = new("#0F6CBD");       // brand[80] — colorBrandBackground1 (dark)
    public static readonly ColorValue DarkPrimaryText = new("#479EF5");   // brand[100] — colorBrandForeground1 (dark)
    public static readonly ColorValue DarkTextOnPrimary = new("#FFFFFF"); // white text on primary backgrounds
    public static readonly ColorValue DarkBackground = new("#292929");    // grey[16] — colorNeutralBackground1
    public static readonly ColorValue DarkSurface = new("#333333");       // grey[20] — colorNeutralCardBackground
    public static readonly ColorValue DarkSurfaceAlt = new("#383838");    // grey[22] — colorSubtleBackground:Hover
    public static readonly ColorValue DarkBorder = new("#666666");        // grey[40] — colorNeutralStroke1
    public static readonly ColorValue DarkText = new("#FFFFFF");
    public static readonly ColorValue DarkTextMuted = new("#ADADAD");
    public static readonly ColorValue DarkDanger = new("#F1707B");
    public static readonly ColorValue DarkDangerSubtle = new("#F1707B1F");
    public static readonly ColorValue DarkSuccess = new("#54B054");
    public static readonly ColorValue DarkSuccessSubtle = new("#54B0541F");
    public static readonly ColorValue DarkWarning = new("#FCE100");
    public static readonly ColorValue DarkWarningSubtle = new("#FCE1001F");
    public static readonly ColorValue DarkFocus = new("#479EF5");

    // ── Opacity variants (precomputed, no runtime color math) ──────────

    public static readonly ColorValue OverlayBlack06 = new("#0000000F");
    public static readonly ColorValue OverlayBlack08 = new("#00000014");
    public static readonly ColorValue OverlayBlack12 = new("#0000001F");

    public static readonly ColorValue LightBorder50 = new("#D1D1D180");
    public static readonly ColorValue DarkBorder50 = new("#66666680");

    public static readonly ColorValue LightText40 = new("#24242466");
    public static readonly ColorValue LightText60 = new("#24242499");
    public static readonly ColorValue LightText30 = new("#2424244C");
    public static readonly ColorValue LightText45 = new("#24242473");
    public static readonly ColorValue LightText55 = new("#2424248C");

    public static readonly ColorValue DarkText40 = new("#FFFFFF66");
    public static readonly ColorValue DarkText60 = new("#FFFFFF99");
    public static readonly ColorValue DarkText30 = new("#FFFFFF4C");
    public static readonly ColorValue DarkText45 = new("#FFFFFF73");
    public static readonly ColorValue DarkText55 = new("#FFFFFF8C");

    public static readonly ColorValue LightPrimary20 = new("#0078D433");
    public static readonly ColorValue DarkPrimary20 = new("#0F6CBD33");

    // ── Color sets ─────────────────────────────────────────────────────

    public static readonly ColorSet LightColors = new()
    {
        Primary = LightPrimary,
        PrimaryText = LightPrimaryText,
        TextOnPrimary = LightTextOnPrimary,
        Background = LightBackground,
        Surface = LightSurface,
        SurfaceAlt = LightSurfaceAlt,
        Border = LightBorder,
        Text = LightText,
        TextMuted = LightTextMuted,
        Danger = LightDanger,
        DangerSubtle = LightDangerSubtle,
        Success = LightSuccess,
        SuccessSubtle = LightSuccessSubtle,
        Warning = LightWarning,
        WarningSubtle = LightWarningSubtle,
        Focus = LightFocus,
    };

    public static readonly ColorSet DarkColors = new()
    {
        Primary = DarkPrimary,
        PrimaryText = DarkPrimaryText,
        TextOnPrimary = DarkTextOnPrimary,
        Background = DarkBackground,
        Surface = DarkSurface,
        SurfaceAlt = DarkSurfaceAlt,
        Border = DarkBorder,
        Text = DarkText,
        TextMuted = DarkTextMuted,
        Danger = DarkDanger,
        DangerSubtle = DarkDangerSubtle,
        Success = DarkSuccess,
        SuccessSubtle = DarkSuccessSubtle,
        Warning = DarkWarning,
        WarningSubtle = DarkWarningSubtle,
        Focus = DarkFocus,
    };

    // ── Typography ─────────────────────────────────────────────────────

    public static readonly TypographySet Typography = new()
    {
        FontFamily = FontFamily.Bundled("Inter"),
        MonoFamily = FontFamily.ForCategory(SystemFont.Monospace),
        Scale = new TypeScale
        {
            Display   = new TextStyle(40, FontWeight.SemiBold, 1.2f),
            H1        = new TextStyle(28, FontWeight.SemiBold, 1.25f),
            H2        = new TextStyle(20, FontWeight.SemiBold, 1.3f),
            H3        = new TextStyle(16, FontWeight.SemiBold, 1.4f),
            Body      = new TextStyle(14, FontWeight.Regular,  1.5f),
            BodySmall = new TextStyle(12, FontWeight.Regular,  1.5f),
            Caption   = new TextStyle(11, FontWeight.Regular,  1.4f),
            Code      = new TextStyle(13, FontWeight.Regular,  1.6f),
        }
    };

    // ── Spacing ────────────────────────────────────────────────────────

    public const float SpacingBase = 4f;

    public static readonly SpacingScale Spacing = new()
    {
        Base = SpacingBase,
    };

    // ── Radius ─────────────────────────────────────────────────────────

    public static readonly RadiusScale Radius = new()
    {
        None = 0,
        Sm = 2,
        Base = 4,
        Md = 6,
        Lg = 8,
        Xl = 12,
        Full = 9999,
    };

    // ── Shadows ────────────────────────────────────────────────────────

    private static readonly ColorValue Shadow10 = new("#0000001A");
    private static readonly ColorValue Shadow14 = new("#00000024");
    private static readonly ColorValue Shadow18 = new("#0000002E");
    private static readonly ColorValue Shadow22 = new("#00000038");

    public static readonly ShadowSpec ShadowSm =
        ShadowSpec.FromDrop(new DropShadow { Blur = 4, Spread = 0, OffsetX = 0, OffsetY = 2, Color = Shadow10 });

    public static readonly ShadowSpec ShadowMd =
        ShadowSpec.FromDrop(new DropShadow { Blur = 8, Spread = 0, OffsetX = 0, OffsetY = 4, Color = Shadow14 });

    public static readonly ShadowSpec ShadowLg =
        ShadowSpec.FromDrop(new DropShadow { Blur = 16, Spread = 0, OffsetX = 0, OffsetY = 8, Color = Shadow18 });

    public static readonly ShadowSpec ShadowXl =
        ShadowSpec.FromDrop(new DropShadow { Blur = 28, Spread = 0, OffsetX = 0, OffsetY = 14, Color = Shadow22 });

    public static readonly ShadowSet Shadows = new()
    {
        Sm = ShadowSm,
        Md = ShadowMd,
        Lg = ShadowLg,
        Xl = ShadowXl,
    };

    // ── Motion ─────────────────────────────────────────────────────────

    public static readonly Transition MotionDefault = new(AnimationModel.FluentFast);
    public static readonly Transition MotionEmphasis =
        new(AnimationModel.Cubic(Duration.Ms(350), 0.0f, 0.0f, 0.0f, 1.0f));
    public static readonly Transition MotionSubtle = new(AnimationModel.EaseOut(Duration.Ms(100)));
    public static readonly Transition MotionEnter =
        new(AnimationModel.Cubic(Duration.Ms(200), 0.0f, 0.0f, 0.0f, 1.0f));
    public static readonly Transition MotionExit = new(AnimationModel.FluentFast);

    public static readonly MotionSet Motion = new()
    {
        Default = MotionDefault,
        Emphasis = MotionEmphasis,
        Subtle = MotionSubtle,
        Enter = MotionEnter,
        Exit = MotionExit,
        Stagger = StaggerSpec.Standard,
    };

    public static readonly MotionPersonality Personality = MotionPersonality.Snappy;

    // ── Named color palette (Windows 11 communication colors) ────────

    // Light mode: curated for readability on light Fluent surfaces
    public static readonly NamedColorPalette LightPalette = new()
    {
        Red    = new("#C50F1F"),
        Orange = new("#DA3B01"),
        Yellow = new("#EAA300"),
        Green  = new("#107C10"),
        Mint   = new("#00A79D"),
        Teal   = new("#038387"),
        Cyan   = new("#0099BC"),
        Blue   = new("#0078D4"),
        Indigo = new("#4F52B2"),
        Purple = new("#5C2D91"),
        Pink   = new("#E3008C"),
        Brown  = new("#8E562E"),
    };

    // Dark mode: lighter tones for contrast against dark Fluent surfaces
    public static readonly NamedColorPalette DarkPalette = new()
    {
        Red    = new("#FF99A4"),
        Orange = new("#F7894A"),
        Yellow = new("#FECD71"),
        Green  = new("#6CCB5F"),
        Mint   = new("#63D6D1"),
        Teal   = new("#4CD3D9"),
        Cyan   = new("#61D6D6"),
        Blue   = new("#479EF5"),
        Indigo = new("#9B8FFF"),
        Purple = new("#C28BFF"),
        Pink   = new("#FF6EC1"),
        Brown  = new("#C49B6E"),
    };

    // ── Spinner ────────────────────────────────────────────────────────

    public static readonly SpinnerStyle Spinner = SpinnerStyle.Arc(thickness: 2, speed: Duration.Ms(750));
}
