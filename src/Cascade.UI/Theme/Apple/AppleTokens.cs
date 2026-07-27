namespace Cascade.UI;

/// <summary>
/// Apple Human Interface Guidelines tokens — shared constants for AppleTheme.
/// Colors use Apple's named system colors (systemBlue, systemRed, etc.)
/// mapped to Cascade's <see cref="ColorSet"/>.
/// </summary>
public static class AppleTokens
{
    // ── Core colors (Light) ────────────────────────────────────────────

    public static readonly ColorValue LightPrimary = new("#007AFF");
    public static readonly ColorValue LightPrimaryText = new("#007AFF");
    public static readonly ColorValue LightTextOnPrimary = new("#FFFFFF");
    public static readonly ColorValue LightBackground = new("#F2F2F7");
    public static readonly ColorValue LightSurface = new("#FFFFFF");
    public static readonly ColorValue LightSurfaceAlt = new("#E5E5EA");
    public static readonly ColorValue LightBorder = new("#C6C6C8");
    public static readonly ColorValue LightText = new("#000000");
    public static readonly ColorValue LightTextMuted = new("#8E8E93");
    public static readonly ColorValue LightDanger = new("#FF3B30");
    public static readonly ColorValue LightDangerSubtle = LightDanger.Opacity(0.12f);
    public static readonly ColorValue LightSuccess = new("#34C759");
    public static readonly ColorValue LightSuccessSubtle = LightSuccess.Opacity(0.12f);
    public static readonly ColorValue LightWarning = new("#FF9500");
    public static readonly ColorValue LightWarningSubtle = LightWarning.Opacity(0.12f);
    public static readonly ColorValue LightFocus = new("#007AFF");

    // ── Core colors (Dark) ─────────────────────────────────────────────

    public static readonly ColorValue DarkPrimary = new("#0A84FF");
    public static readonly ColorValue DarkPrimaryText = new("#0A84FF");
    public static readonly ColorValue DarkTextOnPrimary = new("#FFFFFF");
    public static readonly ColorValue DarkBackground = new("#000000");
    public static readonly ColorValue DarkSurface = new("#1C1C1E");
    public static readonly ColorValue DarkSurfaceAlt = new("#2C2C2E");
    public static readonly ColorValue DarkBorder = new("#38383A");
    public static readonly ColorValue DarkText = new("#FFFFFF");
    public static readonly ColorValue DarkTextMuted = new("#8E8E93");
    public static readonly ColorValue DarkDanger = new("#FF453A");
    public static readonly ColorValue DarkDangerSubtle = DarkDanger.Opacity(0.15f);
    public static readonly ColorValue DarkSuccess = new("#30D158");
    public static readonly ColorValue DarkSuccessSubtle = DarkSuccess.Opacity(0.15f);
    public static readonly ColorValue DarkWarning = new("#FF9F0A");
    public static readonly ColorValue DarkWarningSubtle = DarkWarning.Opacity(0.15f);
    public static readonly ColorValue DarkFocus = new("#0A84FF");

    // ── Opacity variants ───────────────────────────────────────────────

    public static readonly ColorValue LightText30 = LightText.Opacity(0.30f);
    public static readonly ColorValue DarkText30 = DarkText.Opacity(0.30f);
    public static readonly ColorValue LightText35 = LightText.Opacity(0.35f);
    public static readonly ColorValue DarkText35 = DarkText.Opacity(0.35f);
    public static readonly ColorValue LightText50 = LightText.Opacity(0.50f);
    public static readonly ColorValue DarkText50 = DarkText.Opacity(0.50f);
    public static readonly ColorValue LightText55 = LightText.Opacity(0.55f);
    public static readonly ColorValue DarkText55 = DarkText.Opacity(0.55f);
    public static readonly ColorValue LightText60 = LightText.Opacity(0.60f);
    public static readonly ColorValue DarkText60 = DarkText.Opacity(0.60f);
    public static readonly ColorValue LightPrimary15 = LightPrimary.Opacity(0.15f);
    public static readonly ColorValue DarkPrimary15 = DarkPrimary.Opacity(0.15f);

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

    // ── High contrast color sets ───────────────────────────────────────

    public static readonly ColorSet HighContrastLightColors = new()
    {
        Primary       = new("#0000EE"),
        PrimaryText   = new("#0000EE"),
        TextOnPrimary = new("#FFFFFF"),
        Background    = new("#FFFFFF"),
        Surface       = new("#FFFFFF"),
        SurfaceAlt    = new("#F5F5F5"),
        Border        = new("#000000"),
        Text          = new("#000000"),
        TextMuted     = new("#3D3D3D"),
        Danger        = new("#C00000"),
        DangerSubtle  = new("#FFF0F0"),
        Success       = new("#007000"),
        SuccessSubtle = new("#F0FFF0"),
        Warning       = new("#7D4700"),
        WarningSubtle = new("#FFFBE6"),
        Focus         = new("#0000EE"),
    };

    public static readonly ColorSet HighContrastDarkColors = new()
    {
        Primary       = new("#66BFFF"),
        PrimaryText   = new("#66BFFF"),
        TextOnPrimary = new("#000000"),
        Background    = new("#000000"),
        Surface       = new("#000000"),
        SurfaceAlt    = new("#1A1A1A"),
        Border        = new("#FFFFFF"),
        Text          = new("#FFFFFF"),
        TextMuted     = new("#C8C8C8"),
        Danger        = new("#FF9999"),
        DangerSubtle  = new("#3D0000"),
        Success       = new("#99FF99"),
        SuccessSubtle = new("#003D00"),
        Warning       = new("#FFD966"),
        WarningSubtle = new("#3D2E00"),
        Focus         = new("#66BFFF"),
    };

    // ── Typography ─────────────────────────────────────────────────────

    public static readonly TypographySet Typography = new()
    {
        FontFamily = FontFamily.Bundled("Inter"),
        MonoFamily = FontFamily.ForCategory(SystemFont.Monospace),
        Scale = new TypeScale
        {
            Display   = new TextStyle(34, FontWeight.Bold,     1.2f),
            H1        = new TextStyle(28, FontWeight.Bold,     1.2f),
            H2        = new TextStyle(22, FontWeight.SemiBold, 1.3f),
            H3        = new TextStyle(17, FontWeight.SemiBold, 1.4f),
            Body      = new TextStyle(17, FontWeight.Regular,  1.5f),
            BodySmall = new TextStyle(15, FontWeight.Regular,  1.5f),
            Caption   = new TextStyle(12, FontWeight.Regular,  1.4f),
            Code      = new TextStyle(14, FontWeight.Regular,  1.6f),
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
        Base = 6,
        Md = 10,
        Lg = 14,
        Xl = 20,
        Full = 9999,
    };

    // ── Shadows ────────────────────────────────────────────────────────

    private static readonly ColorValue Black = new("#000000");

    public static readonly ShadowSpec ShadowSm =
        ShadowSpec.FromDrop(new DropShadow { Blur = 4, Spread = 0, OffsetX = 0, OffsetY = 1, Color = Black.Opacity(0.08f) });

    public static readonly ShadowSpec ShadowMd =
        ShadowSpec.FromDrop(new DropShadow { Blur = 8, Spread = 0, OffsetX = 0, OffsetY = 2, Color = Black.Opacity(0.10f) });

    public static readonly ShadowSpec ShadowLg =
        ShadowSpec.FromDrop(new DropShadow { Blur = 16, Spread = 0, OffsetX = 0, OffsetY = 4, Color = Black.Opacity(0.12f) });

    public static readonly ShadowSpec ShadowXl =
        ShadowSpec.FromDrop(new DropShadow { Blur = 24, Spread = 0, OffsetX = 0, OffsetY = 8, Color = Black.Opacity(0.15f) });

    public static readonly ShadowSet Shadows = new()
    {
        Sm = ShadowSm,
        Md = ShadowMd,
        Lg = ShadowLg,
        Xl = ShadowXl,
    };

    // ── Motion ─────────────────────────────────────────────────────────

    public static readonly Transition MotionDefault =
        new(AnimationModel.SpringModel(400, 28));
    public static readonly Transition MotionEmphasis =
        new(AnimationModel.SpringModel(320, 22));
    public static readonly Transition MotionSubtle =
        new(AnimationModel.Ease(Duration.Ms(150)));
    public static readonly Transition MotionEnter =
        new(AnimationModel.SpringModel(380, 26));
    public static readonly Transition MotionExit =
        new(AnimationModel.EaseIn(Duration.Ms(180)));

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

    // ── Named color palette (iOS system colors) ──────────────────────

    // Light mode: iOS systemRed through systemBrown
    public static readonly NamedColorPalette LightPalette = new()
    {
        Red    = new("#FF3B30"),
        Orange = new("#FF9500"),
        Yellow = new("#FFCC00"),
        Green  = new("#34C759"),
        Mint   = new("#00C7BE"),
        Teal   = new("#30B0C7"),
        Cyan   = new("#32ADE6"),
        Blue   = new("#007AFF"),
        Indigo = new("#5856D6"),
        Purple = new("#AF52DE"),
        Pink   = new("#FF2D55"),
        Brown  = new("#A2845E"),
    };

    // Dark mode: iOS systemRed through systemBrown (dark variants)
    public static readonly NamedColorPalette DarkPalette = new()
    {
        Red    = new("#FF453A"),
        Orange = new("#FF9F0A"),
        Yellow = new("#FFD60A"),
        Green  = new("#30D158"),
        Mint   = new("#63E6E2"),
        Teal   = new("#40CBE0"),
        Cyan   = new("#64D2FF"),
        Blue   = new("#0A84FF"),
        Indigo = new("#5E5CE6"),
        Purple = new("#BF5AF2"),
        Pink   = new("#FF375F"),
        Brown  = new("#AC8E68"),
    };

    // ── Spinner ────────────────────────────────────────────────────────

    public static readonly SpinnerStyle Spinner = SpinnerStyle.Arc(thickness: 2.5f, speed: Duration.Ms(700));

    // ── Toggle thumb shadow ────────────────────────────────────────────

    public static readonly ShadowSpec ThumbShadow =
        ShadowSpec.FromDrop(new DropShadow { Blur = 4, Spread = 0, OffsetX = 0, OffsetY = 2, Color = Black.Opacity(0.25f) });
}
