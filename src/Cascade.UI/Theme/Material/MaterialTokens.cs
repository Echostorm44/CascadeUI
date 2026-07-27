namespace Cascade.UI;

/// <summary>
/// Material Design 3 (Material You) tokens — shared constants for Material3Theme.
/// Baseline seed color is #6750A4 (Google's reference purple). Color roles follow
/// M3 tonal palette naming mapped to Cascade's <see cref="ColorSet"/>.
/// </summary>
public static class MaterialTokens
{
    // ── Core colors (Light) ────────────────────────────────────────────

    public static readonly ColorValue LightPrimary = new("#6750A4");
    public static readonly ColorValue LightPrimaryText = new("#6750A4");
    public static readonly ColorValue LightTextOnPrimary = new("#FFFFFF");
    public static readonly ColorValue LightBackground = new("#FFFBFE");
    public static readonly ColorValue LightSurface = new("#FFFBFE");
    public static readonly ColorValue LightSurfaceAlt = new("#F4EFF4");
    public static readonly ColorValue LightBorder = new("#79747E");
    public static readonly ColorValue LightText = new("#1C1B1F");
    public static readonly ColorValue LightTextMuted = new("#49454F");
    public static readonly ColorValue LightDanger = new("#B3261E");
    public static readonly ColorValue LightDangerSubtle = LightDanger.Opacity(0.12f);
    public static readonly ColorValue LightSuccess = new("#386A20");
    public static readonly ColorValue LightSuccessSubtle = LightSuccess.Opacity(0.12f);
    public static readonly ColorValue LightWarning = new("#7D5700");
    public static readonly ColorValue LightWarningSubtle = LightWarning.Opacity(0.12f);
    public static readonly ColorValue LightFocus = new("#6750A4");

    // ── Core colors (Dark) ─────────────────────────────────────────────

    public static readonly ColorValue DarkPrimary = new("#D0BCFF");
    public static readonly ColorValue DarkPrimaryText = new("#D0BCFF");
    public static readonly ColorValue DarkTextOnPrimary = new("#381E72");
    public static readonly ColorValue DarkBackground = new("#1C1B1F");
    public static readonly ColorValue DarkSurface = new("#1C1B1F");
    public static readonly ColorValue DarkSurfaceAlt = new("#2B2930");
    public static readonly ColorValue DarkBorder = new("#938F99");
    public static readonly ColorValue DarkText = new("#E6E1E5");
    public static readonly ColorValue DarkTextMuted = new("#CAC4D0");
    public static readonly ColorValue DarkDanger = new("#F2B8B5");
    public static readonly ColorValue DarkDangerSubtle = DarkDanger.Opacity(0.12f);
    public static readonly ColorValue DarkSuccess = new("#A5D6A7");
    public static readonly ColorValue DarkSuccessSubtle = DarkSuccess.Opacity(0.12f);
    public static readonly ColorValue DarkWarning = new("#FFD54F");
    public static readonly ColorValue DarkWarningSubtle = DarkWarning.Opacity(0.12f);
    public static readonly ColorValue DarkFocus = new("#D0BCFF");

    // ── M3 role colors (used by control theme variants) ────────────────

    public static readonly ColorValue SecondaryContainerLight = new("#E8DEF8");
    public static readonly ColorValue OnSecondaryContainerLight = new("#21005D");

    // ── Opacity variants ──────────────────────────────────────────────

    public static readonly ColorValue LightText38 = LightText.Opacity(0.38f);
    public static readonly ColorValue DarkText38 = DarkText.Opacity(0.38f);
    public static readonly ColorValue LightText28 = LightText.Opacity(0.28f);
    public static readonly ColorValue DarkText28 = DarkText.Opacity(0.28f);
    public static readonly ColorValue LightText48 = LightText.Opacity(0.48f);
    public static readonly ColorValue DarkText48 = DarkText.Opacity(0.48f);
    public static readonly ColorValue LightText56 = LightText.Opacity(0.56f);
    public static readonly ColorValue DarkText56 = DarkText.Opacity(0.56f);
    public static readonly ColorValue LightText58 = LightText.Opacity(0.58f);
    public static readonly ColorValue DarkText58 = DarkText.Opacity(0.58f);
    public static readonly ColorValue LightPrimary20 = LightPrimary.Opacity(0.20f);
    public static readonly ColorValue DarkPrimary20 = DarkPrimary.Opacity(0.20f);

    // ── Shadow colors ──────────────────────────────────────────────────

    private static readonly ColorValue Black = new("#000000");
    private static readonly ColorValue Shadow15 = Black.Opacity(0.15f);
    private static readonly ColorValue Shadow20 = Black.Opacity(0.20f);
    private static readonly ColorValue Shadow25 = Black.Opacity(0.25f);
    private static readonly ColorValue Shadow30 = Black.Opacity(0.30f);

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
        FontFamily = FontFamily.Bundled("Roboto"),
        MonoFamily = FontFamily.ForCategory(SystemFont.Monospace),
        Scale = new TypeScale
        {
            Display   = new TextStyle(45, FontWeight.Regular,  1.16f),
            H1        = new TextStyle(32, FontWeight.Regular,  1.25f),
            H2        = new TextStyle(24, FontWeight.Regular,  1.33f),
            H3        = new TextStyle(16, FontWeight.Medium,   1.5f),
            Body      = new TextStyle(14, FontWeight.Regular,  1.43f),
            BodySmall = new TextStyle(12, FontWeight.Regular,  1.33f),
            Caption   = new TextStyle(11, FontWeight.Regular,  1.45f),
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
        Sm = 4,
        Base = 8,
        Md = 12,
        Lg = 16,
        Xl = 28,
        Full = 9999,
    };

    // ── Shadows ────────────────────────────────────────────────────────

    public static readonly ShadowSpec ShadowSm =
        ShadowSpec.FromDrop(new DropShadow { Blur = 2, Spread = 0, OffsetX = 0, OffsetY = 1, Color = Shadow15 });

    public static readonly ShadowSpec ShadowMd =
        ShadowSpec.FromDrop(new DropShadow { Blur = 6, Spread = 0, OffsetX = 0, OffsetY = 3, Color = Shadow20 });

    public static readonly ShadowSpec ShadowLg =
        ShadowSpec.FromDrop(new DropShadow { Blur = 12, Spread = 0, OffsetX = 0, OffsetY = 6, Color = Shadow25 });

    public static readonly ShadowSpec ShadowXl =
        ShadowSpec.FromDrop(new DropShadow { Blur = 20, Spread = 0, OffsetX = 0, OffsetY = 10, Color = Shadow30 });

    public static readonly ShadowSet Shadows = new()
    {
        Sm = ShadowSm,
        Md = ShadowMd,
        Lg = ShadowLg,
        Xl = ShadowXl,
    };

    // ── Motion ─────────────────────────────────────────────────────────

    public static readonly Transition MotionDefault = new(AnimationModel.MaterialStandard);
    public static readonly Transition MotionEmphasis =
        new(AnimationModel.Cubic(Duration.Ms(500), 0.2f, 0.0f, 0.0f, 1.0f));
    public static readonly Transition MotionSubtle = new(AnimationModel.EaseOut(Duration.Ms(200)));
    public static readonly Transition MotionEnter =
        new(AnimationModel.Cubic(Duration.Ms(300), 0.05f, 0.7f, 0.1f, 1.0f));
    public static readonly Transition MotionExit =
        new(AnimationModel.Cubic(Duration.Ms(200), 0.3f, 0.0f, 1.0f, 1.0f));

    public static readonly MotionSet Motion = new()
    {
        Default = MotionDefault,
        Emphasis = MotionEmphasis,
        Subtle = MotionSubtle,
        Enter = MotionEnter,
        Exit = MotionExit,
        Stagger = StaggerSpec.Standard,
    };

    public static readonly MotionPersonality Personality = MotionPersonality.Standard;

    // ── Named color palette (Material 3 tonal variants) ──────────────

    // Light mode: deeper/saturated tones on light surfaces
    public static readonly NamedColorPalette LightPalette = new()
    {
        Red    = new("#B3261E"),
        Orange = new("#C54B00"),
        Yellow = new("#7D5700"),
        Green  = new("#386A20"),
        Mint   = new("#006B5E"),
        Teal   = new("#00696C"),
        Cyan   = new("#00658E"),
        Blue   = new("#1565C0"),
        Indigo = new("#3949AB"),
        Purple = new("#6750A4"),
        Pink   = new("#B3261E"),
        Brown  = new("#6D4C41"),
    };

    // Dark mode: lighter/desaturated tones characteristic of Material You
    public static readonly NamedColorPalette DarkPalette = new()
    {
        Red    = new("#F2B8B5"),
        Orange = new("#FFB68C"),
        Yellow = new("#E5C48B"),
        Green  = new("#A5D6A7"),
        Mint   = new("#80CBC4"),
        Teal   = new("#80CBC4"),
        Cyan   = new("#80DEEA"),
        Blue   = new("#90CAF9"),
        Indigo = new("#9FA8DA"),
        Purple = new("#D0BCFF"),
        Pink   = new("#FFB1C1"),
        Brown  = new("#BCAAA4"),
    };

    // ── Spinner ────────────────────────────────────────────────────────

    public static readonly SpinnerStyle Spinner = SpinnerStyle.Arc(thickness: 3, speed: Duration.Ms(1333));
}
