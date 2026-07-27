using System.Collections.Immutable;

namespace Cascade.UI;

/// <summary>
/// Material Design 3 (Material You) theme. Implements Google's design language with
/// rounded shapes, tonal surface hierarchy, state layers instead of background shifts,
/// and expressive motion. Baseline seed color is #6750A4. Cross-platform — bundles
/// Roboto for consistent rendering on all platforms.
/// </summary>
public class Material3Theme : CascadeTheme
{
    private readonly ThemeMode mode;

    /// <summary>
    /// Creates a new Material 3 theme instance.
    /// </summary>
    /// <param name="mode">Light, Dark, or System mode.</param>
    public Material3Theme(ThemeMode mode = ThemeMode.Light)
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
    public override ColorSet LightColors => MaterialTokens.LightColors;

    /// <inheritdoc />
    public override ColorSet DarkColors => MaterialTokens.DarkColors;

    /// <inheritdoc />
    public override ColorSet? HighContrastLightColors => new()
    {
        Primary       = new("#4500B5"),
        PrimaryText   = new("#4500B5"),
        TextOnPrimary = new("#FFFFFF"),
        Background    = new("#FFFFFF"),
        Surface       = new("#FFFFFF"),
        SurfaceAlt    = new("#F5F5F5"),
        Border        = new("#000000"),
        Text          = new("#000000"),
        TextMuted     = new("#3D3D3D"),
        Danger        = new("#8C0009"),
        DangerSubtle  = new("#FFF0F0"),
        Success       = new("#1B5E00"),
        SuccessSubtle = new("#F0FFF0"),
        Warning       = new("#5C4000"),
        WarningSubtle = new("#FFFBE6"),
        Focus         = new("#4500B5"),
    };

    /// <inheritdoc />
    public override ColorSet? HighContrastDarkColors => new()
    {
        Primary       = new("#E8DEFF"),
        PrimaryText   = new("#E8DEFF"),
        TextOnPrimary = new("#000000"),
        Background    = new("#000000"),
        Surface       = new("#000000"),
        SurfaceAlt    = new("#1A1A1A"),
        Border        = new("#FFFFFF"),
        Text          = new("#FFFFFF"),
        TextMuted     = new("#C8C8C8"),
        Danger        = new("#FFB4AB"),
        DangerSubtle  = new("#3D0000"),
        Success       = new("#B5DFAA"),
        SuccessSubtle = new("#003D00"),
        Warning       = new("#FFE082"),
        WarningSubtle = new("#3D2E00"),
        Focus         = new("#E8DEFF"),
    };

    /// <inheritdoc />
    public override TypographySet Typography => MaterialTokens.Typography;

    /// <inheritdoc />
    public override SpacingScale Spacing => MaterialTokens.Spacing;

    /// <inheritdoc />
    public override RadiusScale Radius => MaterialTokens.Radius;

    /// <inheritdoc />
    public override ShadowSet Shadows => MaterialTokens.Shadows;

    /// <inheritdoc />
    public override MotionSet Motion => MaterialTokens.Motion;

    /// <inheritdoc />
    public override MotionPersonality Personality => MaterialTokens.Personality;

    /// <inheritdoc />
    public override NamedColorPalette Palette => mode == ThemeMode.Dark
        ? MaterialTokens.DarkPalette
        : MaterialTokens.LightPalette;

    /// <inheritdoc />
    public override SpinnerStyle Spinner => MaterialTokens.Spinner;

    // ── Control themes ────────────────────────────────────────────────

    /// <inheritdoc />
    protected override ButtonTheme CreateButton() => new()
    {
        // M3 FilledButton: 40dp tall, pill-shaped (Full radius)
        Height    = 40,
        PaddingH  = 24,
        Radius    = Radius.Full,

        Background = Brush.Solid(Colors.Primary),
        TextColor  = Colors.TextOnPrimary,
        Border     = null,
        BorderWidth = 0,
        Shadow     = Shadows.Sm,
        TextStyle  = Typography.Heading3,

        // M3 state layers: translucent overlay driven by StateLayerOpacity
        Hover = new ButtonStateStyle
        {
            StateLayerOpacity = 0.08f,
        },
        Pressed = new ButtonStateStyle
        {
            StateLayerOpacity = 0.12f,
        },
        Focused = new ButtonStateStyle
        {
            StateLayerOpacity = 0.12f,
            OutlineColor = Colors.Focus,
            OutlineWidth = 3,
            OutlineOffset = 2,
        },
        Disabled = new ButtonStateStyle
        {
            BackgroundOpacity = 0.12f,
            TextOpacity       = 0.38f,
        },
        Loading = new ButtonStateStyle(),

        Transition = Motion.Default,

        Variants = new Dictionary<string, ButtonTheme>
        {
            ["outline"] = new()
            {
                Height      = 40,
                PaddingH    = 24,
                Radius      = Radius.Full,
                Background  = Brush.Solid(ColorValue.Transparent),
                TextColor   = Colors.Primary,
                Border      = Brush.Solid(Colors.Border),
                BorderWidth = 1,
                Shadow      = ShadowSpec.None,
                TextStyle   = Typography.Heading3,
                Hover    = new ButtonStateStyle { StateLayerOpacity = 0.08f, StateLayerColor = Colors.Primary },
                Pressed  = new ButtonStateStyle { StateLayerOpacity = 0.12f, StateLayerColor = Colors.Primary },
                Focused  = new ButtonStateStyle
                {
                    StateLayerOpacity = 0.12f,
                    OutlineColor = Colors.Focus,
                    OutlineWidth = 3,
                    OutlineOffset = 2,
                },
                Disabled = new ButtonStateStyle { BackgroundOpacity = 0.12f, TextOpacity = 0.38f },
                Loading  = new ButtonStateStyle(),
                Transition = Motion.Default,
            },
            ["ghost"] = new()
            {
                Height      = 40,
                PaddingH    = 12,
                Radius      = Radius.Full,
                Background  = Brush.Solid(ColorValue.Transparent),
                TextColor   = Colors.Primary,
                Border      = null,
                BorderWidth = 0,
                Shadow      = ShadowSpec.None,
                TextStyle   = Typography.Heading3,
                Hover    = new ButtonStateStyle { StateLayerOpacity = 0.08f, StateLayerColor = Colors.Primary },
                Pressed  = new ButtonStateStyle { StateLayerOpacity = 0.12f, StateLayerColor = Colors.Primary },
                Focused  = new ButtonStateStyle
                {
                    StateLayerOpacity = 0.12f,
                    OutlineColor = Colors.Focus,
                    OutlineWidth = 3,
                    OutlineOffset = 2,
                },
                Disabled = new ButtonStateStyle { BackgroundOpacity = 0.12f, TextOpacity = 0.38f },
                Loading  = new ButtonStateStyle(),
                Transition = Motion.Default,
            },
            ["subtle"] = new()
            {
                Height      = 40,
                PaddingH    = 24,
                Radius      = Radius.Full,
                Background  = Brush.Solid(mode == ThemeMode.Dark
                    ? Colors.SurfaceAlt
                    : MaterialTokens.SecondaryContainerLight),
                TextColor   = mode == ThemeMode.Dark
                    ? Colors.Text
                    : MaterialTokens.OnSecondaryContainerLight,
                Border      = null,
                BorderWidth = 0,
                Shadow      = Shadows.Sm,
                TextStyle   = Typography.Heading3,
                Hover    = new ButtonStateStyle { StateLayerOpacity = 0.08f },
                Pressed  = new ButtonStateStyle { StateLayerOpacity = 0.12f },
                Focused  = new ButtonStateStyle
                {
                    StateLayerOpacity = 0.12f,
                    OutlineColor = Colors.Focus,
                    OutlineWidth = 3,
                    OutlineOffset = 2,
                },
                Disabled = new ButtonStateStyle { BackgroundOpacity = 0.12f, TextOpacity = 0.38f },
                Loading  = new ButtonStateStyle(),
                Transition = Motion.Default,
            },
            ["destructive"] = new()
            {
                Height      = 40,
                PaddingH    = 24,
                Radius      = Radius.Full,
                Background  = Brush.Solid(Colors.Danger),
                TextColor   = Colors.TextOnPrimary,
                Border      = null,
                BorderWidth = 0,
                Shadow      = Shadows.Sm,
                TextStyle   = Typography.Heading3,
                Hover    = new ButtonStateStyle { StateLayerOpacity = 0.08f },
                Pressed  = new ButtonStateStyle { StateLayerOpacity = 0.12f },
                Focused  = new ButtonStateStyle
                {
                    StateLayerOpacity = 0.12f,
                    OutlineColor = Colors.Focus,
                    OutlineWidth = 3,
                    OutlineOffset = 2,
                },
                Disabled = new ButtonStateStyle { BackgroundOpacity = 0.12f, TextOpacity = 0.38f },
                Loading  = new ButtonStateStyle(),
                Transition = Motion.Default,
            },
        }.ToImmutableDictionary(),
    };

    /// <inheritdoc />
    protected override TextInputTheme CreateTextInput() => new()
    {
        // M3 OutlinedTextField: 56dp tall, transparent background, outlined border
        Height           = 56,
        PaddingH         = 16,
        Radius           = Radius.Base,

        Background       = ColorValue.Transparent,
        TextColor        = Colors.Text,
        PlaceholderColor = Colors.TextMuted,
        BorderColor      = Colors.Border,
        BorderWidth      = 1,
        Shadow           = ShadowSpec.None,
        TextStyle        = Typography.Body,

        // M3: no focus ring — just thicker border
        FocusBorderColor = Colors.Primary,
        FocusBorderWidth = 2,
        FocusRingColor   = ColorValue.Transparent,
        FocusRingWidth   = 0,

        ErrorBorderColor = Colors.Danger,
        ErrorRingColor   = ColorValue.Transparent,

        DisabledBackground  = Colors.Surface.Opacity(0.04f),
        DisabledTextColor   = Colors.TextMuted,
        DisabledBorderColor = Colors.Border.Opacity(0.38f),

        PrefixColor = Colors.TextMuted,
        SuffixColor = Colors.TextMuted,
        PrefixGap   = 12,
        SuffixGap   = 12,

        Transition  = Motion.Default,
    };

    /// <inheritdoc />
    protected override CheckboxTheme CreateCheckbox() => new()
    {
        Size        = 18,
        Radius      = Radius.Sm,
        BorderWidth = 2,

        BorderColor        = Colors.Border,
        Background         = ColorValue.Transparent,
        CheckedBg          = Colors.Primary,
        CheckColor         = Colors.TextOnPrimary,
        IndeterminateBg    = Colors.Primary,
        IndeterminateColor = Colors.TextOnPrimary,

        LabelStyle  = Typography.Body,
        LabelGap    = 12,
        CheckAnimation = CheckAnimation.ScaleIn(Duration.Ms(200)),

        FocusRingColor  = Colors.Focus,
        FocusRingWidth  = 3,
        DisabledOpacity = 0.38f,

        Transition = Motion.Default,
    };

    /// <inheritdoc />
    protected override ToggleTheme CreateToggle() => new()
    {
        // M3 Switch: larger track and thumb, outlined in off-state
        TrackWidth  = 52,
        TrackHeight = 32,
        TrackRadius = Radius.Full,
        TrackOnColor  = Colors.Primary,
        TrackOffColor = Colors.SurfaceAlt,
        TrackBorderColor = Colors.Border,
        TrackBorderWidth = 2,

        ThumbSize   = 24,
        ThumbRadius = Radius.Full,
        ThumbColor  = new ColorValue("#FFFBFE"),
        ThumbShadow = Shadows.Sm,

        ThumbOffsetOn  = 24,
        ThumbOffsetOff = 4,
        ThumbTransition = Motion.Default,
        TrackTransition = Motion.Default,

        LabelStyle    = Typography.Body,
        LabelGap      = 12,
        LabelPosition = ToggleLabelPosition.Left,

        FocusRingColor  = Colors.Focus,
        FocusRingWidth  = 3,
        DisabledOpacity = 0.38f,
    };

    /// <inheritdoc />
    protected override SliderTheme CreateSlider() => new()
    {
        TrackHeight = 4,
        TrackRadius = Radius.Full,
        TrackFill   = Brush.Solid(Colors.Primary),
        TrackEmpty  = Brush.Solid(Colors.SurfaceAlt),

        // M3 thumb: primary-colored circle, no shadow
        ThumbWidth  = 20,
        ThumbHeight = 20,
        ThumbRadius = Radius.Full,
        ThumbFill   = Brush.Solid(Colors.Primary),
        ThumbShadow = ShadowSpec.None,

        ThumbHover   = new ThumbStateStyle { StateLayerOpacity = 0.08f, StateLayerRadius = 20 },
        ThumbPressed = new ThumbStateStyle { StateLayerOpacity = 0.12f, StateLayerRadius = 20 },
        ThumbFocused = new ThumbStateStyle
        {
            OutlineColor = Colors.Focus,
            OutlineWidth = 3,
        },
        ThumbDisabled = new ThumbStateStyle { Opacity = 0.38f },

        ThumbTransition     = Motion.Default,
        TrackFillTransition = Motion.Default,

        ShowTicks = false,
        TickSize  = 3,
        TickFill  = Brush.Solid(Colors.TextMuted),
    };

    /// <inheritdoc />
    protected override SelectTheme CreateSelect() => new()
    {
        Height      = 56,
        PaddingH    = 16,
        Radius      = Radius.Base,
        Background  = ColorValue.Transparent,
        TextColor   = Colors.Text,
        PlaceholderColor = Colors.TextMuted,
        BorderColor = Colors.Border,
        BorderWidth = 1,
        TextStyle   = Typography.Body,

        ChevronColor = Colors.TextMuted,
        ChevronSize  = 12,

        DropdownBackground = Colors.Surface,
        DropdownRadius     = Radius.Md,
        DropdownShadow     = Shadows.Lg,
        DropdownMaxHeight  = 360,

        ItemHeight           = 48,
        ItemPaddingH         = 16,
        ItemHoverBackground  = Colors.SurfaceAlt,
        ItemSelectedBackground = Colors.Primary,

        FocusRingColor  = Colors.Focus,
        FocusRingWidth  = 3,
        DisabledOpacity = 0.38f,

        Transition = Motion.Default,
    };

    /// <inheritdoc />
    protected override TabTheme CreateTabs() => new()
    {
        Height          = 48,
        ItemPaddingH    = Spacing.Md,
        ItemGap         = 0,
        Background      = Colors.Surface,
        ActiveTextColor   = Colors.Primary,
        InactiveTextColor = Colors.TextMuted,
        TextStyle       = Typography.Heading3,

        IndicatorHeight     = 3,
        IndicatorColor      = Colors.Primary,
        IndicatorRadius     = Radius.Sm,
        IndicatorTransition = Motion.Default,

        HoverBackground = Colors.SurfaceAlt,
        FocusRingColor  = Colors.Focus,
        FocusRingWidth  = 3,
        DisabledOpacity = 0.38f,
        BorderColor     = Colors.Border,
        BorderWidth     = 1,

        Transition = Motion.Default,
    };

    /// <inheritdoc />
    protected override ProgressTheme CreateProgress() => new()
    {
        BarHeight      = 4,
        BarRadius      = Radius.Sm,
        TrackColor     = Colors.SurfaceAlt,
        FillColor      = Colors.Primary,

        RingStrokeWidth = 3,
        RingTrackColor  = Colors.SurfaceAlt,
        RingFillColor   = Colors.Primary,

        IndeterminateAnimation = AnimationModel.MaterialStandard,

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
        DotSize    = 6,
        DotColor   = Colors.Danger,
        Transition = Motion.Default,
    };

    /// <inheritdoc />
    protected override RatingTheme CreateRating() => new()
    {
        IconSize     = 24,
        Gap          = 4,
        FilledColor  = new("#FFC107"),
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

        BorderColor = null,
        BorderWidth = 0,

        HoverShadow = Shadows.Md,
        HoverScale  = null,

        Transition = Motion.Default,
    };

    /// <inheritdoc />
    protected override TooltipTheme CreateTooltip() => new()
    {
        Background = Colors.Text,
        TextColor  = Colors.Surface,
        TextStyle  = Typography.Caption,
        Padding    = EdgeInsets.Symmetric(horizontal: 12, vertical: 6),
        Radius     = Radius.Sm,
        Shadow     = Shadows.Md,
        ArrowSize  = 0,
        ArrowHeight = 0,
        ShowDelay  = Duration.Ms(500),
        HideDelay  = Duration.Ms(150),
        MaxWidth   = 320,
        Transition = Motion.Subtle,
    };

    /// <inheritdoc />
    protected override DialogTheme CreateDialog() => new()
    {
        // M3 scrim: 32% black
        BackdropColor = new ColorValue("#000000").Opacity(0.32f),
        BackdropBlur  = 0,

        Background = Colors.SurfaceAlt,
        Radius     = Radius.Xl,
        Shadow     = Shadows.Xl,
        MaxWidth   = 560,
        PaddingH   = 24,
        PaddingV   = 24,

        TitleStyle = Typography.Heading2,
        TitleColor = Colors.Text,
        BodyStyle  = Typography.Body,
        BodyColor  = Colors.Text,

        EnterTransition = Motion.Enter,
        ExitTransition  = Motion.Exit,
        EnterScale      = 0.87f,
    };

    /// <inheritdoc />
    protected override NavBarTheme CreateNavBar() => new()
    {
        // M3 TopAppBar: tonal surface, 64dp
        Height            = 64,
        Background        = Colors.Surface,
        BackgroundOpacity = 1.0f,

        TitleStyle = Typography.Heading2,
        TitleColor = Colors.Text,

        BorderBottom = ColorValue.Transparent,
        BorderWidth  = 0,

        ActionStyle = Typography.Body,
        ActionColor = Colors.TextMuted,

        Transition = Motion.Default,
    };

    /// <inheritdoc />
    protected override ScrollTheme CreateScroll() => new()
    {
        OverlayWidth           = 6,
        OverlayWidthHover      = 10,
        OverlayRadius          = 3,
        OverlayThumbColor      = mode == ThemeMode.Dark
            ? MaterialTokens.DarkText38
            : MaterialTokens.LightText38,
        OverlayThumbHoverColor = mode == ThemeMode.Dark
            ? MaterialTokens.DarkText56
            : MaterialTokens.LightText56,
        OverlayFadeDelay       = Duration.Ms(1500),
        OverlayFadeDuration    = Duration.Ms(300),
        OverlayPadding         = 2,

        TrackWidth           = 14,
        TrackColor           = Colors.SurfaceAlt,
        TrackThumbColor      = mode == ThemeMode.Dark
            ? MaterialTokens.DarkText28
            : MaterialTokens.LightText28,
        TrackThumbHoverColor = mode == ThemeMode.Dark
            ? MaterialTokens.DarkText48
            : MaterialTokens.LightText48,
        TrackThumbDragColor  = mode == ThemeMode.Dark
            ? MaterialTokens.DarkText58
            : MaterialTokens.LightText58,
        TrackThumbRadius     = 7,
        TrackThumbMinHeight  = 36,

        OverscrollGlowColor  = mode == ThemeMode.Dark
            ? MaterialTokens.DarkPrimary20
            : MaterialTokens.LightPrimary20,
        RubberBandMaxStretch  = 100,
        RubberBandResistance  = 200,
        RubberBandReturnModel = AnimationModel.SpringModel(400, 28),

        MouseWheelStepPx         = 48,
        TrackpadDecelerationRate = 0.998f,
        TrackpadStopThreshold    = 0.1f,

        SnapModel          = AnimationModel.MaterialStandard,
        ProximityThreshold = 0.3f,

        FadeEdgeLength = 32,
        FadeEdgeColor  = Colors.Surface,
    };

    /// <inheritdoc />
    protected override IconAnimationTheme CreateIconAnimation() => new()
    {
        DefaultTransition     = IconTransition.Morph,
        TransitionModel       = AnimationModel.MaterialStandard,
        AttentionModel        = AnimationModel.Spring.Standard,
        AttentionIntensity    = 1.1f,
        ContinuousSpeedFactor = 1.0f,
    };

    /// <inheritdoc />
    protected override CaretTheme CreateCaret() => new()
    {
        Width         = 2f,
        Color         = Colors.Text,
        BlinkInterval = Duration.Ms(1060),
        SmoothBlink   = true,
        MoveAnimation = AnimationModel.MaterialStandard,
    };

    /// <inheritdoc />
    protected override PasswordTheme CreatePassword() => new()
    {
        MaskCharacter       = '●',
        ShowRevealToggle    = true,
        RevealToggleColor   = Colors.TextMuted,
        RevealToggleSize    = 18,
        RevealAnimation     = AnimationModel.MaterialStandard,
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
}
