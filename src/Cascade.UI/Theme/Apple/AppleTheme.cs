using System.Collections.Immutable;

namespace Cascade.UI;

/// <summary>
/// Apple Human Interface Guidelines theme. Implements Apple's design language with
/// a warm, organic feel: spring-based motion, subtle shadows, large touch-friendly
/// controls (17pt body text), and translucent materials. Cross-platform — uses
/// San Francisco on macOS and the system default on other platforms.
/// </summary>
public class AppleTheme : CascadeTheme
{
    private readonly ThemeMode mode;

    /// <summary>
    /// Creates a new Apple theme instance.
    /// </summary>
    /// <param name="mode">Light, Dark, or System mode.</param>
    public AppleTheme(ThemeMode mode = ThemeMode.Light)
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
    public override ColorSet LightColors => AppleTokens.LightColors;

    /// <inheritdoc />
    public override ColorSet DarkColors => AppleTokens.DarkColors;

    /// <inheritdoc />
    public override ColorSet? HighContrastLightColors => AppleTokens.HighContrastLightColors;

    /// <inheritdoc />
    public override ColorSet? HighContrastDarkColors => AppleTokens.HighContrastDarkColors;

    /// <inheritdoc />
    public override TypographySet Typography => AppleTokens.Typography;

    /// <inheritdoc />
    public override SpacingScale Spacing => AppleTokens.Spacing;

    /// <inheritdoc />
    public override RadiusScale Radius => AppleTokens.Radius;

    /// <inheritdoc />
    public override ShadowSet Shadows => AppleTokens.Shadows;

    /// <inheritdoc />
    public override MotionSet Motion => AppleTokens.Motion;

    /// <inheritdoc />
    public override MotionPersonality Personality => AppleTokens.Personality;

    /// <inheritdoc />
    public override NamedColorPalette Palette => mode == ThemeMode.Dark
        ? AppleTokens.DarkPalette
        : AppleTokens.LightPalette;

    /// <inheritdoc />
    public override SpinnerStyle Spinner => AppleTokens.Spinner;

    // ── Control themes ────────────────────────────────────────────────

    /// <inheritdoc />
    protected override ButtonTheme CreateButton() => new()
    {
        Height     = 36,
        PaddingH   = 16,
        Radius     = Radius.Base,

        Background  = Brush.Solid(Colors.Primary),
        TextColor   = Colors.TextOnPrimary,
        Border      = null,
        BorderWidth = 0,
        Shadow      = new DropShadow { Blur = 1, OffsetY = 0.5f, Color = new ColorValue("#000000").Opacity(0.15f) },
        TextStyle   = Typography.Body,

        Hover = new ButtonStateStyle
        {
            BackgroundOpacity = 0.82f,
            Scale  = 1.01f,
            Shadow = new DropShadow { Blur = 6, OffsetY = 2, Spread = 1, Color = new ColorValue("#000000").Opacity(0.25f) },
        },
        Pressed = new ButtonStateStyle
        {
            BackgroundOpacity = 0.75f,
            Scale  = 0.96f,
            Shadow = new DropShadow { Blur = 0.5f, OffsetY = 0.5f, Color = new ColorValue("#000000").Opacity(0.1f) },
        },
        Focused = new ButtonStateStyle
        {
            OutlineColor = Colors.Focus,
            OutlineWidth = 3,
            OutlineOffset = 2,
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
                Height      = 36,
                PaddingH    = 16,
                Radius      = Radius.Base,
                Background  = Brush.Solid(ColorValue.Transparent),
                TextColor   = Colors.Primary,
                Border      = Brush.Solid(Colors.Primary),
                BorderWidth = 1.5f,
                Shadow      = ShadowSpec.None,
                TextStyle   = Typography.Body,
                Hover    = new ButtonStateStyle { BackgroundColor = Colors.Primary.Opacity(0.06f), Scale = 1.01f },
                Pressed  = new ButtonStateStyle { BackgroundColor = Colors.Primary.Opacity(0.12f), Scale = 0.96f },
                Focused  = new ButtonStateStyle
                {
                    OutlineColor = Colors.Focus,
                    OutlineWidth = 3,
                    OutlineOffset = 2,
                },
                Disabled = new ButtonStateStyle { BackgroundOpacity = 0.38f, TextOpacity = 0.38f },
                Loading  = new ButtonStateStyle(),
                Transition = Motion.Subtle,
            },
            ["ghost"] = new()
            {
                Height      = 36,
                PaddingH    = 16,
                Radius      = Radius.Base,
                Background  = Brush.Solid(ColorValue.Transparent),
                TextColor   = Colors.Primary,
                Border      = null,
                BorderWidth = 0,
                Shadow      = ShadowSpec.None,
                TextStyle   = Typography.Body,
                Hover    = new ButtonStateStyle { BackgroundColor = Colors.Primary.Opacity(0.06f), Scale = 1.01f },
                Pressed  = new ButtonStateStyle { BackgroundColor = Colors.Primary.Opacity(0.12f), Scale = 0.96f },
                Focused  = new ButtonStateStyle
                {
                    OutlineColor = Colors.Focus,
                    OutlineWidth = 3,
                    OutlineOffset = 2,
                },
                Disabled = new ButtonStateStyle { BackgroundOpacity = 0.38f, TextOpacity = 0.38f },
                Loading  = new ButtonStateStyle(),
                Transition = Motion.Subtle,
            },
            ["subtle"] = new()
            {
                Height      = 36,
                PaddingH    = 16,
                Radius      = Radius.Base,
                Background  = Brush.Solid(Colors.SurfaceAlt),
                TextColor   = Colors.Text,
                Border      = null,
                BorderWidth = 0,
                Shadow      = ShadowSpec.None,
                TextStyle   = Typography.Body,
                Hover    = new ButtonStateStyle { BackgroundColor = Colors.Border, Scale = 1.01f },
                Pressed  = new ButtonStateStyle { Scale = 0.96f },
                Focused  = new ButtonStateStyle
                {
                    OutlineColor = Colors.Focus,
                    OutlineWidth = 3,
                    OutlineOffset = 2,
                },
                Disabled = new ButtonStateStyle { BackgroundOpacity = 0.38f, TextOpacity = 0.38f },
                Loading  = new ButtonStateStyle(),
                Transition = Motion.Subtle,
            },
            ["destructive"] = new()
            {
                Height      = 36,
                PaddingH    = 16,
                Radius      = Radius.Base,
                Background  = Brush.Solid(Colors.Danger),
                TextColor   = Colors.TextOnPrimary,
                Border      = null,
                BorderWidth = 0,
                Shadow      = ShadowSpec.None,
                TextStyle   = Typography.Body,
                Hover    = new ButtonStateStyle { BackgroundOpacity = 0.88f, Scale = 1.01f },
                Pressed  = new ButtonStateStyle { BackgroundOpacity = 0.75f, Scale = 0.96f },
                Focused  = new ButtonStateStyle
                {
                    OutlineColor = Colors.Focus,
                    OutlineWidth = 3,
                    OutlineOffset = 2,
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
        Height           = 36,
        PaddingH         = 10,
        Radius           = Radius.Base,

        Background       = Colors.Surface,
        TextColor        = Colors.Text,
        PlaceholderColor = Colors.TextMuted,
        BorderColor      = Colors.Border,
        BorderWidth      = 1,
        Shadow           = ShadowSpec.None,
        TextStyle        = Typography.Body,

        FocusBorderColor = Colors.Primary,
        FocusBorderWidth = 1,
        FocusRingColor   = Colors.Focus.Opacity(0.25f),
        FocusRingWidth   = 3,

        ErrorBorderColor = Colors.Danger,
        ErrorRingColor   = Colors.Danger.Opacity(0.20f),

        DisabledBackground  = Colors.SurfaceAlt,
        DisabledTextColor   = Colors.TextMuted,
        DisabledBorderColor = Colors.Border.Opacity(0.5f),

        PrefixColor = Colors.TextMuted,
        SuffixColor = Colors.TextMuted,
        PrefixGap   = 8,
        SuffixGap   = 8,

        Transition  = Motion.Subtle,
    };

    /// <inheritdoc />
    protected override CheckboxTheme CreateCheckbox() => new()
    {
        Size        = 20,
        Radius      = 4,
        BorderWidth = 1.5f,

        // In dark mode Colors.Border (#38383A) is invisible against dark surfaces.
        // Use a lighter border that provides clear visibility for unchecked controls.
        BorderColor        = mode == ThemeMode.Dark ? new ColorValue("#8E8E93") : Colors.Border,
        Background         = Colors.Surface,
        CheckedBg          = Colors.Primary,
        CheckColor         = Colors.TextOnPrimary,
        IndeterminateBg    = Colors.Primary,
        IndeterminateColor = Colors.TextOnPrimary,

        LabelStyle  = Typography.Body,
        LabelGap    = 8,
        CheckAnimation = CheckAnimation.PathDraw(Duration.Ms(150)),

        FocusRingColor  = Colors.Focus.Opacity(0.25f),
        FocusRingWidth  = 3,
        DisabledOpacity = 0.38f,

        Transition = Motion.Subtle,
    };

    /// <inheritdoc />
    protected override RadioTheme CreateRadio() => new()
    {
        Size        = 20,
        BorderWidth = 1.5f,

        BorderColor   = mode == ThemeMode.Dark ? new ColorValue("#8E8E93") : Colors.Border,
        Background    = Colors.Surface,
        SelectedColor = Colors.Primary,
        DotSize       = 8,
        DotColor      = Colors.TextOnPrimary,

        LabelStyle = Typography.Body,
        LabelGap   = 8,

        FocusRingColor  = Colors.Focus.Opacity(0.25f),
        FocusRingWidth  = 3,
        DisabledOpacity = 0.38f,

        Transition = Motion.Subtle,
    };

    /// <inheritdoc />
    protected override ToggleTheme CreateToggle() => new()
    {
        // Apple UISwitch: 51×31 track, green when on
        TrackWidth  = 51,
        TrackHeight = 31,
        TrackRadius = Radius.Full,
        TrackOnColor  = Colors.Success,
        TrackOffColor = Colors.Border,

        ThumbSize   = 27,
        ThumbRadius = Radius.Full,
        ThumbColor  = new ColorValue("#FFFFFF"),
        ThumbShadow = AppleTokens.ThumbShadow,

        ThumbOffsetOn  = 22,
        ThumbOffsetOff = 2,
        ThumbTransition = new Transition(AnimationModel.SpringModel(450, 30)),
        TrackTransition = new Transition(AnimationModel.Ease(Duration.Ms(120))),

        LabelStyle    = Typography.Body,
        LabelGap      = 10,
        LabelPosition = ToggleLabelPosition.Left,

        FocusRingColor  = Colors.Focus.Opacity(0.25f),
        FocusRingWidth  = 3,
        DisabledOpacity = 0.38f,
    };

    /// <inheritdoc />
    protected override SliderTheme CreateSlider() => new()
    {
        TrackHeight = 4,
        TrackRadius = Radius.Full,
        TrackFill   = Brush.Solid(Colors.Primary),
        TrackEmpty  = Brush.Solid(Colors.Border),

        ThumbWidth  = 22,
        ThumbHeight = 20,
        ThumbRadius = Radius.Full,
        ThumbFill   = Brush.Linear(Gradient.Linear(
            Angle.Degrees(180),
            new GradientStop(0.0f, new ColorValue("#FFFFFF")),
            new GradientStop(1.0f, new ColorValue("#E5E5E5")))),
        ThumbShadow = ShadowSpec.FromDrop(new DropShadow
        {
            Blur = 4, Spread = 0, OffsetX = 0, OffsetY = 2,
            Color = new ColorValue("#000000").Opacity(0.3f),
        }),

        ThumbHover = new ThumbStateStyle
        {
            ScaleX = 1.05f,
            ScaleY = 1.05f,
            Proximity = new CursorProximityEffect(
                radius:   48f,
                property: ProximityProperty.ShadowSpread,
                range:    new FloatRange(0f, 5f)),
        },
        ThumbPressed = ThumbStateStyle.SpringGroup(
            new Transition(AnimationModel.SpringModel(400, 26)),
            new ThumbStateStyle { ScaleX = 0.97f, ScaleY = 0.97f },
            new ThumbStateStyle
            {
                Shadow = ShadowSpec.FromDrop(new DropShadow
                {
                    Blur = 2, Spread = 0, OffsetX = 0, OffsetY = 1,
                    Color = new ColorValue("#000000").Opacity(0.15f),
                }),
            }
        ) with
        {
            EnterTransition = new Transition(AnimationModel.Cubic(Duration.Ms(80), 0.4f, 0f, 1f, 1f)),
            ExitTransition  = new Transition(AnimationModel.SpringModel(400, 28)),
        },
        ThumbFocused = new ThumbStateStyle
        {
            OutlineColor = Colors.Focus,
            OutlineWidth = 3,
        },
        ThumbDisabled = new ThumbStateStyle { Opacity = 0.35f },

        ThumbTransition     = new Transition(AnimationModel.SpringModel(400, 28)),
        TrackFillTransition = new Transition(AnimationModel.Ease(Duration.Ms(80))),

        ShowTicks = false,
        TickSize  = 3,
        TickFill  = Brush.Solid(Colors.TextMuted),
    };

    /// <inheritdoc />
    protected override SelectTheme CreateSelect() => new()
    {
        Height      = 36,
        PaddingH    = 10,
        Radius      = Radius.Base,
        Background  = Colors.Surface,
        TextColor   = Colors.Text,
        PlaceholderColor = Colors.TextMuted,
        BorderColor = Colors.Border,
        BorderWidth = 1,
        TextStyle   = Typography.Body,

        ChevronColor = Colors.TextMuted,
        ChevronSize  = 10,

        // Apple combo box: accent rounded square with a single white down chevron.
        ChevronStyle        = SelectChevronStyle.ComboBox,
        ChevronBoxColor     = Colors.Primary,
        ChevronBoxTextColor = Colors.TextOnPrimary,

        DropdownBackground = Colors.Surface,
        DropdownRadius     = Radius.Md,
        DropdownShadow     = Shadows.Lg,
        DropdownMaxHeight  = 320,

        ItemHeight           = 36,
        ItemPaddingH         = 10,
        ItemHoverBackground  = Colors.SurfaceAlt,
        ItemSelectedBackground = Colors.Primary,

        FocusRingColor  = Colors.Focus.Opacity(0.25f),
        FocusRingWidth  = 3,
        DisabledOpacity = 0.38f,

        Transition = Motion.Subtle,
    };

    /// <inheritdoc />
    protected override TabTheme CreateTabs() => new()
    {
        Height          = 44,
        ItemPaddingH    = Spacing.Md,
        ItemGap         = 0,
        Background      = Colors.Surface,
        ActiveTextColor   = Colors.Primary,
        InactiveTextColor = Colors.TextMuted,
        TextStyle       = Typography.Body,

        IndicatorHeight     = 2,
        IndicatorColor      = Colors.Primary,
        IndicatorRadius     = Radius.Sm,
        IndicatorTransition = Motion.Default,

        HoverBackground = Colors.SurfaceAlt,
        FocusRingColor  = Colors.Focus.Opacity(0.25f),
        FocusRingWidth  = 3,
        DisabledOpacity = 0.38f,
        BorderColor     = Colors.Border,
        BorderWidth     = 0.5f,

        Transition = Motion.Default,
    };

    /// <inheritdoc />
    protected override ProgressTheme CreateProgress() => new()
    {
        BarHeight      = 4,
        BarRadius      = Radius.Full,
        TrackColor     = Colors.SurfaceAlt,
        FillColor      = Colors.Primary,

        RingStrokeWidth = 2.5f,
        RingTrackColor  = Colors.SurfaceAlt,
        RingFillColor   = Colors.Primary,

        IndeterminateAnimation = AnimationModel.AppleStandard,

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
        FilledColor  = Colors.Warning,
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
        Background = new ColorValue("#1C1C1E"),
        TextColor  = new ColorValue("#FFFFFF"),
        TextStyle  = Typography.Caption,
        Padding    = EdgeInsets.Symmetric(horizontal: 8, vertical: 5),
        Radius     = Radius.Sm,
        Shadow     = Shadows.Md,
        ArrowSize  = 6,
        ArrowHeight = 5,
        ShowDelay  = Duration.Ms(400),
        HideDelay  = Duration.Ms(50),
        MaxWidth   = 240,
        Transition = new Transition(AnimationModel.EaseOut(Duration.Ms(120))),
    };

    /// <inheritdoc />
    protected override DialogTheme CreateDialog() => new()
    {
        BackdropColor = new ColorValue("#000000").Opacity(0.40f),
        BackdropBlur  = 0,

        Background = Colors.Surface,
        Radius     = Radius.Lg,
        Shadow     = Shadows.Xl,
        MaxWidth   = 400,
        PaddingH   = 20,
        PaddingV   = 20,

        TitleStyle = Typography.Heading3,
        TitleColor = Colors.Text,
        BodyStyle  = Typography.Body,
        BodyColor  = Colors.Text,

        EnterTransition = Motion.Enter,
        ExitTransition  = new Transition(AnimationModel.EaseIn(Duration.Ms(150))),
        EnterScale      = 0.94f,
    };

    /// <inheritdoc />
    protected override NavBarTheme CreateNavBar() => new()
    {
        // Apple navigation bar: translucent with blur
        Height            = 52,
        Background        = Colors.Surface,
        BackgroundOpacity = 0.94f,
        BlurRadius        = 20,

        TitleStyle = Typography.Heading3,
        TitleColor = Colors.Text,

        BorderBottom = Colors.Border,
        BorderWidth  = 0.5f,

        ActionStyle = Typography.Body,
        ActionColor = Colors.Primary,

        Transition = Motion.Default,
    };

    /// <inheritdoc />
    protected override ScrollTheme CreateScroll() => new()
    {
        OverlayWidth           = 7,
        OverlayWidthHover      = 11,
        OverlayRadius          = 3.5f,
        OverlayThumbColor      = mode == ThemeMode.Dark
            ? AppleTokens.DarkText35
            : AppleTokens.LightText35,
        OverlayThumbHoverColor = mode == ThemeMode.Dark
            ? AppleTokens.DarkText55
            : AppleTokens.LightText55,
        OverlayFadeDelay       = Duration.Ms(1200),
        OverlayFadeDuration    = Duration.Ms(250),
        OverlayPadding         = 2,

        TrackWidth           = 14,
        TrackColor           = Colors.SurfaceAlt,
        TrackThumbColor      = mode == ThemeMode.Dark
            ? AppleTokens.DarkText30
            : AppleTokens.LightText30,
        TrackThumbHoverColor = mode == ThemeMode.Dark
            ? AppleTokens.DarkText50
            : AppleTokens.LightText50,
        TrackThumbDragColor  = mode == ThemeMode.Dark
            ? AppleTokens.DarkText60
            : AppleTokens.LightText60,
        TrackThumbRadius     = 0,
        TrackThumbMinHeight  = 32,

        OverscrollGlowColor  = mode == ThemeMode.Dark
            ? AppleTokens.DarkPrimary15
            : AppleTokens.LightPrimary15,
        RubberBandMaxStretch  = 80,
        RubberBandResistance  = 220,
        RubberBandReturnModel = AnimationModel.SpringModel(400, 28),

        MouseWheelStepPx         = 48,
        TrackpadDecelerationRate = 0.998f,
        TrackpadStopThreshold    = 0.1f,

        SnapModel          = AnimationModel.SpringModel(400, 30),
        ProximityThreshold = 0.3f,

        FadeEdgeLength = 32,
        FadeEdgeColor  = Colors.Surface,
    };

    /// <inheritdoc />
    protected override IconAnimationTheme CreateIconAnimation() => new()
    {
        DefaultTransition     = IconTransition.Morph,
        TransitionModel       = AnimationModel.Spring.Standard,
        AttentionModel        = AnimationModel.Spring.Snappy,
        AttentionIntensity    = 0.9f,
        ContinuousSpeedFactor = 1.0f,
    };

    /// <inheritdoc />
    protected override CaretTheme CreateCaret() => new()
    {
        Width         = 2f,
        Color         = Colors.Text,
        BlinkInterval = Duration.Ms(1060),
        SmoothBlink   = true,
        MoveAnimation = AnimationModel.AppleStandard,
    };

    /// <inheritdoc />
    protected override PasswordTheme CreatePassword() => new()
    {
        MaskCharacter       = '●',
        ShowRevealToggle    = true,
        RevealToggleColor   = Colors.TextMuted,
        RevealToggleSize    = 18,
        RevealAnimation     = AnimationModel.AppleStandard,
        ShowStrengthIndicator = true,
        StrengthBarHeight   = 3,
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
    public override WindowMaterial WindowMaterial => WindowMaterial.Vibrancy;
}
