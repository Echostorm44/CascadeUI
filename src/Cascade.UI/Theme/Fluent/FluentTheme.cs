using System.Collections.Immutable;

namespace Cascade.UI;

/// <summary>
/// Fluent Design System 2 theme (Windows 11). Implements Microsoft's design language with
/// high information density, curve-based motion, compound focus rings, and a cool-tinted
/// neutral palette. Cross-platform — runs on macOS and Linux with solid surface fallbacks
/// where Mica/Acrylic are unavailable.
/// </summary>
public class FluentTheme : CascadeTheme
{
    private readonly ThemeMode mode;

    /// <summary>
    /// Creates a new Fluent theme instance.
    /// </summary>
    /// <param name="mode">Light, Dark, or System mode.</param>
    public FluentTheme(ThemeMode mode = ThemeMode.Light)
    {
        this.mode = mode;
    }

    // ── Global tokens ─────────────────────────────────────────────────

    /// <inheritdoc />
    public override ThemeMode Mode => mode;

    /// <inheritdoc />
    public override ColorSet Colors => mode == ThemeMode.Dark ? DarkColors : LightColors;
    // ThemeMode.System resolves to Light until platform preference detection is available.
    // The platform layer will provide system mode resolution via PlatformServices.

    /// <inheritdoc />
    public override ColorSet LightColors => FluentTokens.LightColors;

    /// <inheritdoc />
    public override ColorSet DarkColors => FluentTokens.DarkColors;

    /// <inheritdoc />
    public override ColorSet? HighContrastLightColors => new()
    {
        Primary       = new("#003CA4"),
        PrimaryText   = new("#003CA4"),
        TextOnPrimary = new("#FFFFFF"),
        Background    = new("#FFFFFF"),
        Surface       = new("#FFFFFF"),
        SurfaceAlt    = new("#F5F5F5"),
        Border        = new("#000000"),
        Text          = new("#000000"),
        TextMuted     = new("#3D3D3D"),
        Danger        = new("#C50F1F"),
        DangerSubtle  = new("#FFF0F0"),
        Success       = new("#0B6A0B"),
        SuccessSubtle = new("#F0FFF0"),
        Warning       = new("#6D4D00"),
        WarningSubtle = new("#FFFBE6"),
        Focus         = new("#003CA4"),
    };

    /// <inheritdoc />
    public override ColorSet? HighContrastDarkColors => new()
    {
        Primary       = new("#6CC5FF"),
        PrimaryText   = new("#6CC5FF"),
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
        Focus         = new("#6CC5FF"),
    };

    /// <inheritdoc />
    public override TypographySet Typography => FluentTokens.Typography;

    /// <inheritdoc />
    public override SpacingScale Spacing => FluentTokens.Spacing;

    /// <inheritdoc />
    public override RadiusScale Radius => FluentTokens.Radius;

    /// <inheritdoc />
    public override ShadowSet Shadows => FluentTokens.Shadows;

    /// <inheritdoc />
    public override MotionSet Motion => FluentTokens.Motion;

    /// <inheritdoc />
    public override MotionPersonality Personality => FluentTokens.Personality;

    /// <inheritdoc />
    public override NamedColorPalette Palette => mode == ThemeMode.Dark
        ? FluentTokens.DarkPalette
        : FluentTokens.LightPalette;

    /// <inheritdoc />
    public override SpinnerStyle Spinner => FluentTokens.Spinner;

    // ── Control themes ────────────────────────────────────────────────

    /// <inheritdoc />
    protected override ButtonTheme CreateButton() => new()
    {
        // Fluent standard height — more compact than the default 36
        Height    = 32,
        PaddingH  = 12,
        Radius    = Radius.Base,

        Background = Brush.Solid(Colors.Primary),
        TextColor  = Colors.TextOnPrimary,
        Border     = null,
        BorderWidth = 0,
        Shadow     = ShadowSpec.None,
        TextStyle  = new TextStyle(14, FontWeight.SemiBold, 1.5f),

        // Fluent hover: overlay darkens in light mode, lightens in dark mode.
        // Encoded as an overlay color — the renderer composites it over the background.
        Hover = new ButtonStateStyle
        {
            OverlayColor = FluentTokens.OverlayBlack06,
        },
        Pressed = new ButtonStateStyle
        {
            OverlayColor = FluentTokens.OverlayBlack12,
        },
        // Fluent's compound focus ring: 2px white inner, 2px primary outer.
        Focused = new ButtonStateStyle
        {
            OutlineColor  = Colors.Focus,
            OutlineWidth  = 2,
            OutlineOffset = 2,
            InnerRingColor = Colors.Surface,
            InnerRingWidth = 2,
        },
        Disabled = new ButtonStateStyle
        {
            BackgroundOpacity = 0.38f,
            TextOpacity       = 0.38f,
        },
        Loading = new ButtonStateStyle(),

        Transition = Motion.Subtle,

        Variants = new Dictionary<string, ButtonTheme>
        {
            ["outline"] = new()
            {
                Height      = 32,
                PaddingH    = 12,
                Radius      = Radius.Base,
                Background  = Brush.Solid(ColorValue.Transparent),
                TextColor   = Colors.PrimaryText,
                Border      = Brush.Solid(Colors.Border),
                BorderWidth = 1,
                Shadow      = ShadowSpec.None,
                TextStyle   = Typography.Body,
                Hover    = new ButtonStateStyle { BackgroundColor = Colors.SurfaceAlt },
                Pressed  = new ButtonStateStyle { BackgroundColor = Colors.Border },
                Focused  = new ButtonStateStyle
                {
                    OutlineColor   = Colors.Focus,
                    OutlineWidth   = 2,
                    OutlineOffset  = 2,
                    InnerRingColor = Colors.Surface,
                    InnerRingWidth = 2,
                },
                Disabled = new ButtonStateStyle { BackgroundOpacity = 0.38f, TextOpacity = 0.38f },
                Loading  = new ButtonStateStyle(),
                Transition = Motion.Subtle,
            },
            ["ghost"] = new()
            {
                Height      = 32,
                PaddingH    = 12,
                Radius      = Radius.Base,
                Background  = Brush.Solid(ColorValue.Transparent),
                TextColor   = Colors.Text,
                Border      = null,
                BorderWidth = 0,
                Shadow      = ShadowSpec.None,
                TextStyle   = Typography.Body,
                Hover    = new ButtonStateStyle { BackgroundColor = Colors.SurfaceAlt },
                Pressed  = new ButtonStateStyle { BackgroundColor = Colors.Border },
                Focused  = new ButtonStateStyle
                {
                    OutlineColor   = Colors.Focus,
                    OutlineWidth   = 2,
                    OutlineOffset  = 2,
                    InnerRingColor = Colors.Surface,
                    InnerRingWidth = 2,
                },
                Disabled = new ButtonStateStyle { BackgroundOpacity = 0.38f, TextOpacity = 0.38f },
                Loading  = new ButtonStateStyle(),
                Transition = Motion.Subtle,
            },
            ["subtle"] = new()
            {
                Height      = 32,
                PaddingH    = 12,
                Radius      = Radius.Base,
                Background  = Brush.Solid(Colors.SurfaceAlt),
                TextColor   = Colors.Text,
                Border      = null,
                BorderWidth = 0,
                Shadow      = ShadowSpec.None,
                TextStyle   = Typography.Body,
                Hover    = new ButtonStateStyle { BackgroundColor = Colors.Border },
                Pressed  = new ButtonStateStyle { OverlayColor = FluentTokens.OverlayBlack08 },
                Focused  = new ButtonStateStyle
                {
                    OutlineColor   = Colors.Focus,
                    OutlineWidth   = 2,
                    OutlineOffset  = 2,
                    InnerRingColor = Colors.Surface,
                    InnerRingWidth = 2,
                },
                Disabled = new ButtonStateStyle { BackgroundOpacity = 0.38f, TextOpacity = 0.38f },
                Loading  = new ButtonStateStyle(),
                Transition = Motion.Subtle,
            },
            ["destructive"] = new()
            {
                Height      = 32,
                PaddingH    = 12,
                Radius      = Radius.Base,
                Background  = Brush.Solid(Colors.Danger),
                TextColor   = Colors.TextOnPrimary,
                Border      = null,
                BorderWidth = 0,
                Shadow      = ShadowSpec.None,
                TextStyle   = Typography.Body,
                Hover    = new ButtonStateStyle { OverlayColor = FluentTokens.OverlayBlack06 },
                Pressed  = new ButtonStateStyle { OverlayColor = FluentTokens.OverlayBlack12 },
                Focused  = new ButtonStateStyle
                {
                    OutlineColor   = Colors.Focus,
                    OutlineWidth   = 2,
                    OutlineOffset  = 2,
                    InnerRingColor = Colors.Surface,
                    InnerRingWidth = 2,
                },
                Disabled = new ButtonStateStyle { BackgroundOpacity = 0.38f, TextOpacity = 0.38f },
                Loading  = new ButtonStateStyle(),
                Transition = Motion.Subtle,
            },
        }.ToImmutableDictionary(),
    };

    /// <inheritdoc />
    protected override TextInputTheme CreateTextInput() => new()
    {
        Height           = 32,
        PaddingH         = 8,
        Radius           = Radius.Base,

        Background       = Colors.Surface,
        TextColor        = Colors.Text,
        PlaceholderColor = Colors.TextMuted,
        BorderColor      = Colors.Border,
        BorderWidth      = 1,
        Shadow           = ShadowSpec.None,
        TextStyle        = Typography.Body,

        // Focus: full border brightens + compound focus ring
        FocusBorderColor = Colors.Primary,
        FocusBorderWidth = 1,
        FocusRingColor   = Colors.Focus,
        FocusRingWidth   = 2,
        // Fluent compound ring: 2px white inner, 2px primary outer
        InnerRingColor   = Colors.Surface,
        InnerRingWidth   = 2,

        ErrorBorderColor = Colors.Danger,
        ErrorRingColor   = Colors.Danger,

        DisabledBackground  = Colors.SurfaceAlt,
        DisabledTextColor   = Colors.TextMuted,
        DisabledBorderColor = mode == ThemeMode.Dark
            ? FluentTokens.DarkBorder50
            : FluentTokens.LightBorder50,

        PrefixColor = Colors.TextMuted,
        SuffixColor = Colors.TextMuted,
        PrefixGap   = 6,
        SuffixGap   = 6,

        Transition  = Motion.Subtle,
    };

    /// <inheritdoc />
    protected override CheckboxTheme CreateCheckbox() => new()
    {
        Size        = 18,
        Radius      = Radius.Sm,
        BorderWidth = 1.5f,

        BorderColor       = Colors.Border,
        Background        = Colors.Surface,
        CheckedBg         = Colors.Primary,
        CheckColor        = Colors.TextOnPrimary,
        IndeterminateBg   = Colors.Primary,
        IndeterminateColor = Colors.TextOnPrimary,

        LabelStyle  = Typography.Body,
        LabelGap    = 8,
        CheckAnimation = CheckAnimation.ScaleIn(Duration.Ms(120)),

        FocusRingColor  = Colors.Focus,
        FocusRingWidth  = 2,
        DisabledOpacity = 0.38f,

        Transition = Motion.Subtle,
    };

    /// <inheritdoc />
    protected override RadioTheme CreateRadio() => new()
    {
        Size        = 22,
        BorderWidth = 2f,

        BorderColor   = Colors.Border,
        Background    = Colors.Surface,
        SelectedColor = Colors.Primary,
        DotSize       = 11,
        DotColor      = Colors.TextOnPrimary,

        LabelStyle  = Typography.Body,
        LabelGap    = 8,

        FocusRingColor  = Colors.Focus,
        FocusRingWidth  = 2,
        DisabledOpacity = 0.38f,

        Transition = Motion.Subtle,
    };

    /// <inheritdoc />
    protected override ToggleTheme CreateToggle() => new()
    {
        // Fluent toggle: slightly smaller than default, blue when on
        TrackWidth  = 40,
        TrackHeight = 20,
        TrackRadius = Radius.Full,
        TrackOnColor  = Colors.Primary,
        TrackOffColor = Colors.Border,
        TrackBorderColor = null,
        TrackBorderWidth = 0,

        ThumbSize   = 14,
        ThumbRadius = Radius.Full,
        ThumbColor  = new ColorValue("#FFFFFF"),
        ThumbShadow = ShadowSpec.None,

        ThumbOffsetOn  = 22,
        ThumbOffsetOff = 4,
        ThumbTransition = Motion.Subtle,
        TrackTransition = Motion.Subtle,

        LabelStyle    = Typography.Body,
        LabelGap      = 8,
        LabelPosition = ToggleLabelPosition.Left,

        FocusRingColor  = Colors.Focus,
        FocusRingWidth  = 2,
        DisabledOpacity = 0.38f,
    };

    /// <inheritdoc />
    protected override SliderTheme CreateSlider() => new()
    {
        TrackHeight = 4,
        TrackRadius = Radius.Full,
        TrackFill   = Brush.Solid(Colors.Primary),
        TrackEmpty  = Brush.Solid(Colors.Border),

        ThumbWidth  = 20,
        ThumbHeight = 20,
        ThumbRadius = Radius.Full,
        ThumbFill   = Brush.Solid(new ColorValue("#FFFFFF")),
        ThumbShadow = Shadows.Sm,

        ThumbHover   = new ThumbStateStyle { ScaleX = 1.1f, ScaleY = 1.1f },
        ThumbPressed = new ThumbStateStyle { ScaleX = 0.9f, ScaleY = 0.9f },
        ThumbFocused = new ThumbStateStyle
        {
            OutlineColor = Colors.Focus,
            OutlineWidth = 2,
        },
        ThumbDisabled = new ThumbStateStyle { Opacity = 0.38f },

        ThumbTransition     = Motion.Subtle,
        TrackFillTransition = Motion.Subtle,

        ShowTicks = false,
        TickSize  = 2,
        TickFill  = Brush.Solid(Colors.Border),
    };

    /// <inheritdoc />
    protected override SelectTheme CreateSelect() => new()
    {
        Height      = 32,
        PaddingH    = 8,
        Radius      = Radius.Base,
        Background  = Colors.Surface,
        TextColor   = Colors.Text,
        PlaceholderColor = Colors.TextMuted,
        BorderColor = Colors.Border,
        BorderWidth = 1,
        TextStyle   = Typography.Body,

        ChevronColor = Colors.TextMuted,
        ChevronSize  = 12,

        DropdownBackground = Colors.Background,
        DropdownRadius     = Radius.Md,
        DropdownShadow     = Shadows.Lg,
        DropdownMaxHeight  = 360,

        ItemHeight           = 32,
        ItemPaddingH         = 8,
        ItemHoverBackground  = Colors.Surface,
        ItemSelectedBackground = Colors.Primary,

        FocusRingColor  = Colors.Focus,
        FocusRingWidth  = 2,
        DisabledOpacity = 0.38f,

        Transition = Motion.Subtle,
    };

    /// <inheritdoc />
    protected override TabTheme CreateTabs() => new()
    {
        Height          = 40,
        ItemPaddingH    = Spacing.Md,
        ItemGap         = Spacing.Sm,
        Background      = Colors.Surface,
        ActiveTextColor   = Colors.Text,
        InactiveTextColor = Colors.TextMuted,
        TextStyle       = Typography.Body,

        IndicatorHeight     = 3,
        IndicatorColor      = Colors.Primary,
        IndicatorRadius     = Radius.Sm,
        IndicatorTransition = Motion.Default,

        HoverBackground = Colors.SurfaceAlt,
        FocusRingColor  = Colors.Focus,
        FocusRingWidth  = 2,
        DisabledOpacity = 0.38f,
        BorderColor     = Colors.Border,
        BorderWidth     = 1,

        Transition = Motion.Subtle,
    };

    /// <inheritdoc />
    protected override ProgressTheme CreateProgress() => new()
    {
        BarHeight      = 4,
        BarRadius      = Radius.Sm,
        TrackColor     = Colors.SurfaceAlt,
        FillColor      = Colors.Primary,

        RingStrokeWidth = 2,
        RingTrackColor  = Colors.Border,
        RingFillColor   = Colors.Primary,

        IndeterminateAnimation = AnimationModel.FluentFast,

        Transition = Motion.Default,
    };

    /// <inheritdoc />
    protected override BadgeTheme CreateBadge() => new()
    {
        Height    = 20,
        PaddingH  = Spacing.Sm,
        Radius    = Radius.Full,
        Background = Colors.Danger,
        TextColor  = Colors.TextOnPrimary,
        TextStyle  = Typography.Caption,
        BorderColor = null,
        BorderWidth = 0,
        DotSize    = 8,
        DotColor   = Colors.Danger,
        Transition = Motion.Subtle,
    };

    /// <inheritdoc />
    protected override RatingTheme CreateRating() => new()
    {
        IconSize     = 24,
        Gap          = 4,
        FilledColor  = new("#FFB900"),
        EmptyColor   = Colors.Border,
        DisabledColor = Colors.Border,
    };

    /// <inheritdoc />
    protected override CardTheme CreateCard() => new()
    {
        Background = Colors.Surface,
        Radius     = Radius.Md,
        Shadow     = Shadows.Sm,
        Padding    = EdgeInsets.All(Spacing.Md),

        BorderColor = Colors.Border,
        BorderWidth = 1,

        HoverShadow = Shadows.Md,
        HoverScale  = null,

        Transition = Motion.Subtle,
    };

    /// <inheritdoc />
    protected override TooltipTheme CreateTooltip() => new()
    {
        Background = Colors.Text,
        TextColor  = Colors.Surface,
        TextStyle  = Typography.Caption,
        Padding    = EdgeInsets.Symmetric(horizontal: 8, vertical: 4),
        Radius     = Radius.Sm,
        Shadow     = Shadows.Md,
        ArrowSize  = 6,
        ArrowHeight = 4,
        ShowDelay  = Duration.Ms(400),
        HideDelay  = Duration.Ms(120),
        MaxWidth   = 320,
        Transition = Motion.Subtle,
    };

    /// <inheritdoc />
    protected override DialogTheme CreateDialog() => new()
    {
        BackdropColor = new ColorValue("#00000066"),
        BackdropBlur  = 0,

        Background = Colors.Surface,
        Radius     = Radius.Lg,
        Shadow     = Shadows.Xl,
        MaxWidth   = 480,
        PaddingH   = 24,
        PaddingV   = 20,

        TitleStyle = Typography.Heading2,
        TitleColor = Colors.Text,
        BodyStyle  = Typography.Body,
        BodyColor  = Colors.Text,

        EnterTransition = Motion.Enter,
        ExitTransition  = Motion.Exit,
        EnterScale      = 0.96f,
    };

    /// <inheritdoc />
    protected override NavBarTheme CreateNavBar() => new()
    {
        // Fluent nav bar: flat, borderless. Mica sits behind the entire window chrome.
        Height            = 48,
        Background        = Colors.Surface,
        BackgroundOpacity = 1.0f,

        TitleStyle = Typography.Heading3,
        TitleColor = Colors.Text,

        BorderBottom = ColorValue.Transparent,
        BorderWidth  = 0,

        ActionStyle = Typography.Body,
        ActionColor = Colors.PrimaryText,

        Transition = Motion.Default,
    };

    /// <inheritdoc />
    protected override ScrollTheme CreateScroll() => new()
    {
        OverlayWidth           = 6,
        OverlayWidthHover      = 10,
        OverlayRadius          = 3,
        OverlayThumbColor      = mode == ThemeMode.Dark
            ? FluentTokens.DarkText40
            : FluentTokens.LightText40,
        OverlayThumbHoverColor = mode == ThemeMode.Dark
            ? FluentTokens.DarkText60
            : FluentTokens.LightText60,
        OverlayFadeDelay       = Duration.Ms(1500),
        OverlayFadeDuration    = Duration.Ms(200),
        OverlayPadding         = 1,

        TrackWidth           = 14,
        TrackColor           = Colors.SurfaceAlt,
        TrackThumbColor      = mode == ThemeMode.Dark
            ? FluentTokens.DarkText30
            : FluentTokens.LightText30,
        TrackThumbHoverColor = mode == ThemeMode.Dark
            ? FluentTokens.DarkText45
            : FluentTokens.LightText45,
        TrackThumbDragColor  = mode == ThemeMode.Dark
            ? FluentTokens.DarkText55
            : FluentTokens.LightText55,
        TrackThumbRadius     = 0,
        TrackThumbMinHeight  = 28,

        OverscrollGlowColor  = mode == ThemeMode.Dark
            ? FluentTokens.DarkPrimary20
            : FluentTokens.LightPrimary20,
        RubberBandMaxStretch  = 60,
        RubberBandResistance  = 250,
        RubberBandReturnModel = AnimationModel.FluentFast,

        MouseWheelStepPx         = 48,
        TrackpadDecelerationRate = 0.997f,
        TrackpadStopThreshold    = 0.1f,

        SnapModel          = AnimationModel.FluentFast,
        ProximityThreshold = 0.3f,

        FadeEdgeLength = 28,
        FadeEdgeColor  = Colors.Surface,
    };

    /// <inheritdoc />
    protected override IconAnimationTheme CreateIconAnimation() => new()
    {
        DefaultTransition     = IconTransition.Crossfade,
        TransitionModel       = AnimationModel.FluentFast,
        AttentionModel        = AnimationModel.Spring.Snappy,
        AttentionIntensity    = 1.0f,
        ContinuousSpeedFactor = 1.0f,
    };

    /// <inheritdoc />
    protected override CaretTheme CreateCaret() => new()
    {
        Width         = 1.5f,
        Color         = Colors.Text,
        BlinkInterval = Duration.Ms(1060),
        SmoothBlink   = false,
        MoveAnimation = AnimationModel.FluentFast,
    };

    /// <inheritdoc />
    protected override PasswordTheme CreatePassword() => new()
    {
        MaskCharacter       = '●',
        ShowRevealToggle    = true,
        RevealToggleColor   = Colors.TextMuted,
        RevealToggleSize    = 16,
        RevealAnimation     = AnimationModel.FluentFast,
        ShowStrengthIndicator = true,
        StrengthBarHeight   = 4,
        StrengthWeakColor   = Colors.Danger,
        StrengthFairColor   = Colors.Warning,
        StrengthStrongColor = Colors.Success,
    };

    /// <inheritdoc />
    protected override ChartTheme CreateChart() => new()
    {
        Palette       = ColorPalette.FromTheme(this),
        AxisLabel     = Typography.Caption,
        Legend        = Typography.BodySmall,
        Tooltip       = Typography.Caption,
        GridLine      = Colors.Border,
        Axis          = Colors.TextMuted,
        GridLineWidth = 1,
        BarRadius     = Radius.Sm,
        DataTransition = Motion.Default,
        TooltipShadow  = Shadows.Md,
        TooltipRadius  = Radius.Sm,
    };

    // ── Window material ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public override WindowMaterial WindowMaterial => WindowMaterial.Mica;
}
