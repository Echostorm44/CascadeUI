namespace Cascade.UI;

/// <summary>
/// Abstract base class for all Cascade UI themes. A theme is a complete, composable
/// visual specification covering every control's colors, typography, spacing, shadows,
/// motion, and state transitions.
/// </summary>
/// <remarks>
/// <para>
/// Themes that support both light and dark mode override <see cref="LightColors"/> and
/// <see cref="DarkColors"/>; <see cref="Colors"/> returns the resolved set for the
/// current <see cref="ThemeMode"/>. Single-mode themes override only <see cref="Colors"/>.
/// </para>
/// <para>
/// Control theme properties are virtual with defaults derived from global tokens.
/// Custom themes only need to override controls where the derived defaults are
/// insufficient — all other controls work automatically.
/// </para>
/// </remarks>
public abstract class CascadeTheme
{
    // ── Global tokens (abstract — every theme must implement) ──────────

    /// <summary>Semantic color tokens for the current mode.</summary>
    public abstract ColorSet Colors { get; }

    /// <summary>Typography: font families and type scale.</summary>
    public abstract TypographySet Typography { get; }

    /// <summary>Spacing scale based on a base unit.</summary>
    public abstract SpacingScale Spacing { get; }

    /// <summary>Corner radius scale.</summary>
    public abstract RadiusScale Radius { get; }

    /// <summary>Shadow elevation presets.</summary>
#pragma warning disable CA1716 // Shadows is the spec-defined name for this property
    public abstract ShadowSet Shadows { get; }
#pragma warning restore CA1716

    /// <summary>Motion transition presets.</summary>
    public abstract MotionSet Motion { get; }

    /// <summary>Motion personality — global feel of all animations.</summary>
    public abstract MotionPersonality Personality { get; }

    /// <summary>
    /// Named decorative colors — curated for this theme and mode. Use for avatars,
    /// chart series, category indicators, and similar non-semantic color needs.
    /// </summary>
    public abstract NamedColorPalette Palette { get; }

    /// <summary>Spinner visual style.</summary>
    public abstract SpinnerStyle Spinner { get; }

    // ── Theme mode ────────────────────────────────────────────────────

    /// <summary>
    /// The theme mode this theme was created with. Override to return the
    /// mode passed at construction time so <see cref="ThemeSwitcher"/> can
    /// stay in sync.
    /// </summary>
    public virtual ThemeMode Mode => ThemeMode.Light;

    // ── Dual-mode color support ───────────────────────────────────────

    /// <summary>
    /// Light mode color set. Override this and <see cref="DarkColors"/> for dual-mode themes.
    /// Returns <see cref="Colors"/> by default for single-mode themes.
    /// </summary>
    public virtual ColorSet LightColors => Colors;

    /// <summary>
    /// Dark mode color set. Override this and <see cref="LightColors"/> for dual-mode themes.
    /// Returns <see cref="Colors"/> by default for single-mode themes.
    /// </summary>
    public virtual ColorSet DarkColors => Colors;

    // ── High contrast ─────────────────────────────────────────────────

    /// <summary>
    /// Light mode high contrast color set. Override for custom high contrast support.
    /// Returns null by default — the framework applies automatic luminance adjustments.
    /// </summary>
    public virtual ColorSet? HighContrastLightColors => null;

    /// <summary>
    /// Dark mode high contrast color set. Override for custom high contrast support.
    /// Returns null by default — the framework applies automatic luminance adjustments.
    /// </summary>
    public virtual ColorSet? HighContrastDarkColors => null;

    // ── Window material ─────────────────────────────────────────────────

    /// <summary>
    /// Material effect applied to the main application window background.
    /// Themes override this to request platform-specific effects (Mica, Vibrancy).
    /// The framework checks <see cref="PlatformMaterials.IsAvailable"/> at runtime
    /// and falls back to a solid background when the effect is unavailable.
    /// </summary>
    public virtual WindowMaterial WindowMaterial => WindowMaterial.None;

    // ── Control themes (cached factory pattern — defaults derive from global tokens) ──
    //
    // Each control-theme accessor is a non-virtual cached getter that delegates
    // to a protected virtual CreateXxx() factory. Subclasses override the factory,
    // never the public property — this guarantees one allocation per theme instance
    // across the entire framework, no matter how many subclass overrides exist.
    //
    // Theme defaults build nontrivial object graphs (Brush, ButtonStateStyle,
    // TextStyle, gradient stops, etc.). Accessing Theme.Button in hot layout or
    // paint paths without caching allocated ~9.6 KB/frame at ~40 fps (400 KB/s of
    // GC pressure) with AppleTheme. Factory pattern drops that to zero after
    // first access. See Diagnostics regression gate.
    //
    // Call <see cref="RefreshThemeCaches"/> after mutating theme state to
    // invalidate all cached sub-themes.

    private ButtonTheme? cachedButton;
    private SliderTheme? cachedSlider;
    private TextInputTheme? cachedTextInput;
    private CheckboxTheme? cachedCheckbox;
    private RadioTheme? cachedRadio;
    private ToggleTheme? cachedToggle;
    private TooltipTheme? cachedTooltip;
    private DialogTheme? cachedDialog;
    private SelectTheme? cachedSelect;
    private TabTheme? cachedTabs;
    private ProgressTheme? cachedProgress;
    private BadgeTheme? cachedBadge;
    private RatingTheme? cachedRating;
    private CardTheme? cachedCard;
    private NavBarTheme? cachedNavBar;
    private ScrollTheme? cachedScroll;
    private IconAnimationTheme? cachedIconAnimation;
    private CaretTheme? cachedCaret;
    private PasswordTheme? cachedPassword;
    private ChartTheme? cachedChart;
    private ColorPickerTheme? cachedColorPicker;
    private EmojiPickerTheme? cachedEmojiPicker;
    private NotificationBellTheme? cachedNotificationBell;
    private ToastTheme? cachedToast;
    private AiSurfaceTheme? cachedAiSurface;

    /// <summary>Button theme tokens.</summary>
    public ButtonTheme Button => cachedButton ??= CreateButton();
    /// <summary>Slider theme tokens.</summary>
    public SliderTheme Slider => cachedSlider ??= CreateSlider();
    /// <summary>Text input theme tokens.</summary>
    public TextInputTheme TextInput => cachedTextInput ??= CreateTextInput();
    /// <summary>Checkbox theme tokens.</summary>
    public CheckboxTheme Checkbox => cachedCheckbox ??= CreateCheckbox();
    /// <summary>Radio button theme tokens.</summary>
    public RadioTheme Radio => cachedRadio ??= CreateRadio();
    /// <summary>Toggle (switch) theme tokens.</summary>
    public ToggleTheme Toggle => cachedToggle ??= CreateToggle();
    /// <summary>Tooltip theme tokens.</summary>
    public TooltipTheme Tooltip => cachedTooltip ??= CreateTooltip();
    /// <summary>Dialog theme tokens.</summary>
    public DialogTheme Dialog => cachedDialog ??= CreateDialog();
    /// <summary>Select (dropdown) theme tokens.</summary>
#pragma warning disable CA1716 // Select is the spec-defined name for this property
    public SelectTheme Select => cachedSelect ??= CreateSelect();
#pragma warning restore CA1716
    /// <summary>Tab theme tokens.</summary>
    public TabTheme Tabs => cachedTabs ??= CreateTabs();
    /// <summary>Progress bar/ring theme tokens.</summary>
    public ProgressTheme Progress => cachedProgress ??= CreateProgress();
    /// <summary>Badge theme tokens.</summary>
    public BadgeTheme Badge => cachedBadge ??= CreateBadge();
    /// <summary>Rating theme tokens.</summary>
    public RatingTheme Rating => cachedRating ??= CreateRating();
    /// <summary>Card theme tokens.</summary>
    public CardTheme Card => cachedCard ??= CreateCard();
    /// <summary>Navigation bar theme tokens.</summary>
    public NavBarTheme NavBar => cachedNavBar ??= CreateNavBar();
    /// <summary>Scrollbar and scroll physics theme tokens.</summary>
    public ScrollTheme Scroll => cachedScroll ??= CreateScroll();
    /// <summary>Icon animation theme tokens.</summary>
    public IconAnimationTheme IconAnimation => cachedIconAnimation ??= CreateIconAnimation();
    /// <summary>Text caret theme tokens.</summary>
    public CaretTheme Caret => cachedCaret ??= CreateCaret();
    /// <summary>Password input theme tokens.</summary>
    public PasswordTheme Password => cachedPassword ??= CreatePassword();
    /// <summary>Chart theme tokens.</summary>
    public ChartTheme Chart => cachedChart ??= CreateChart();
    /// <summary>Control theme for ColorPicker.</summary>
    public ColorPickerTheme ColorPicker => cachedColorPicker ??= CreateColorPicker();
    /// <summary>Control theme for EmojiPicker.</summary>
    public EmojiPickerTheme EmojiPicker => cachedEmojiPicker ??= CreateEmojiPicker();
    /// <summary>Control theme for NotificationBell.</summary>
    public NotificationBellTheme NotificationBell => cachedNotificationBell ??= CreateNotificationBell();
    /// <summary>Toast notification theme tokens.</summary>
    public ToastTheme Toast => cachedToast ??= CreateToast();
    /// <summary>AI surface theme: confirmation dialogs, settings panel, status colors.</summary>
    public AiSurfaceTheme AiSurface => cachedAiSurface ??= CreateAiSurface();

    /// <summary>Override to customize button theme. Called once per theme instance; result is cached.</summary>
    protected virtual ButtonTheme CreateButton() => ButtonTheme.Default(this);
    /// <summary>Override to customize slider theme. Called once per theme instance; result is cached.</summary>
    protected virtual SliderTheme CreateSlider() => SliderTheme.Default(this);
    /// <summary>Override to customize text input theme. Called once per theme instance; result is cached.</summary>
    protected virtual TextInputTheme CreateTextInput() => TextInputTheme.Default(this);
    /// <summary>Override to customize checkbox theme. Called once per theme instance; result is cached.</summary>
    protected virtual CheckboxTheme CreateCheckbox() => CheckboxTheme.Default(this);
    /// <summary>Override to customize radio button theme. Called once per theme instance; result is cached.</summary>
    protected virtual RadioTheme CreateRadio() => RadioTheme.Default(this);
    /// <summary>Override to customize toggle theme. Called once per theme instance; result is cached.</summary>
    protected virtual ToggleTheme CreateToggle() => ToggleTheme.Default(this);
    /// <summary>Override to customize tooltip theme. Called once per theme instance; result is cached.</summary>
    protected virtual TooltipTheme CreateTooltip() => TooltipTheme.Default(this);
    /// <summary>Override to customize dialog theme. Called once per theme instance; result is cached.</summary>
    protected virtual DialogTheme CreateDialog() => DialogTheme.Default(this);
    /// <summary>Override to customize select theme. Called once per theme instance; result is cached.</summary>
    protected virtual SelectTheme CreateSelect() => SelectTheme.Default(this);
    /// <summary>Override to customize tabs theme. Called once per theme instance; result is cached.</summary>
    protected virtual TabTheme CreateTabs() => TabTheme.Default(this);
    /// <summary>Override to customize progress theme. Called once per theme instance; result is cached.</summary>
    protected virtual ProgressTheme CreateProgress() => ProgressTheme.Default(this);
    /// <summary>Override to customize badge theme. Called once per theme instance; result is cached.</summary>
    protected virtual BadgeTheme CreateBadge() => BadgeTheme.Default(this);
    /// <summary>Override to customize rating theme. Called once per theme instance; result is cached.</summary>
    protected virtual RatingTheme CreateRating() => RatingTheme.Default(this);
    /// <summary>Override to customize card theme. Called once per theme instance; result is cached.</summary>
    protected virtual CardTheme CreateCard() => CardTheme.Default(this);
    /// <summary>Override to customize navbar theme. Called once per theme instance; result is cached.</summary>
    protected virtual NavBarTheme CreateNavBar() => NavBarTheme.Default(this);
    /// <summary>Override to customize scroll theme. Called once per theme instance; result is cached.</summary>
    protected virtual ScrollTheme CreateScroll() => ScrollTheme.Default(this);
    /// <summary>Override to customize icon animation theme. Called once per theme instance; result is cached.</summary>
    protected virtual IconAnimationTheme CreateIconAnimation() => IconAnimationTheme.Default(this);
    /// <summary>Override to customize caret theme. Called once per theme instance; result is cached.</summary>
    protected virtual CaretTheme CreateCaret() => CaretTheme.Default(this);
    /// <summary>Override to customize password theme. Called once per theme instance; result is cached.</summary>
    protected virtual PasswordTheme CreatePassword() => PasswordTheme.Default(this);
    /// <summary>Override to customize chart theme. Called once per theme instance; result is cached.</summary>
    protected virtual ChartTheme CreateChart() => ChartTheme.Default(this);
    /// <summary>Override to customize color picker theme. Called once per theme instance; result is cached.</summary>
    protected virtual ColorPickerTheme CreateColorPicker() => ColorPickerTheme.Default(this);
    /// <summary>Override to customize emoji picker theme. Called once per theme instance; result is cached.</summary>
    protected virtual EmojiPickerTheme CreateEmojiPicker() => EmojiPickerTheme.Default(this);
    /// <summary>Override to customize notification bell theme. Called once per theme instance; result is cached.</summary>
    protected virtual NotificationBellTheme CreateNotificationBell() => NotificationBellTheme.Default(this);
    /// <summary>Override to customize toast theme. Called once per theme instance; result is cached.</summary>
    protected virtual ToastTheme CreateToast() => ToastTheme.Default(this);
    /// <summary>Override to customize AI surface theme. Called once per theme instance; result is cached.</summary>
    protected virtual AiSurfaceTheme CreateAiSurface() => AiSurfaceTheme.Default(this);

    /// <summary>
    /// Clears all cached sub-themes. Call after mutating theme state (e.g., changing
    /// mode or palette) to force all accessors to re-invoke their Create factories.
    /// </summary>
    public void RefreshThemeCaches()
    {
        cachedButton = null;
        cachedSlider = null;
        cachedTextInput = null;
        cachedCheckbox = null;
        cachedRadio = null;
        cachedToggle = null;
        cachedTooltip = null;
        cachedDialog = null;
        cachedSelect = null;
        cachedTabs = null;
        cachedProgress = null;
        cachedBadge = null;
        cachedRating = null;
        cachedCard = null;
        cachedNavBar = null;
        cachedScroll = null;
        cachedIconAnimation = null;
        cachedCaret = null;
        cachedPassword = null;
        cachedChart = null;
        cachedColorPicker = null;
        cachedEmojiPicker = null;
        cachedNotificationBell = null;
        cachedToast = null;
        cachedAiSurface = null;
    }
}
