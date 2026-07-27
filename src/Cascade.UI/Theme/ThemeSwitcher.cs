namespace Cascade.UI;

/// <summary>
/// Manages the active theme and provides reactive notification when the
/// theme changes at runtime. Components that read <see cref="Current"/>
/// in their <c>Render()</c> method automatically re-render when the theme
/// switches.
/// </summary>
/// <remarks>
/// <para>
/// Theme switching is explicit — the framework does not automatically
/// select a theme based on the operating system. Developers who want
/// platform-adaptive themes write that logic themselves:
/// </para>
/// <code>
/// ThemeSwitcher.Apply(OperatingSystem.IsMacOS()
///     ? new AppleTheme()
///     : new FluentTheme());
/// </code>
/// </remarks>
public static class ThemeSwitcher
{
    private static readonly object syncLock = new();
    private static CascadeTheme currentTheme = new FluentTheme();
    private static bool isDarkMode;
    private static bool useHighContrast;
    private static volatile int version;
    private static readonly List<Action> listeners = [];

    /// <summary>
    /// Monotonic counter bumped whenever the active theme, dark-mode, or
    /// high-contrast setting changes. Retained render caches (e.g. a
    /// <see cref="ScrollView"/>'s composited layer texture) compare against this
    /// to detect appearance-only changes — a theme switch recolours and restyles
    /// content without altering its structure or size, so the layout-based
    /// recapture heuristics never fire on their own. Read without locking; a
    /// stale read at worst defers a recapture by one frame.
    /// </summary>
    internal static int Version => version;

    /// <summary>
    /// The currently active theme. Reading this in <c>Render()</c> creates
    /// a reactive dependency — the component re-renders when the theme changes.
    /// </summary>
    public static CascadeTheme Current
    {
        get
        {
            lock (syncLock)
            {
                return currentTheme;
            }
        }
    }

    /// <summary>
    /// Whether dark mode is active. When true, themes should use their
    /// <c>DarkColors</c> palette.
    /// </summary>
    public static bool IsDarkMode
    {
        get
        {
            lock (syncLock)
            {
                return isDarkMode;
            }
        }
    }

    /// <summary>
    /// Whether high-contrast mode is active. When true, themes should use
    /// their <c>HighContrastLightColors</c> or <c>HighContrastDarkColors</c>.
    /// </summary>
    public static bool UseHighContrast
    {
        get
        {
            lock (syncLock)
            {
                return useHighContrast;
            }
        }
    }

    /// <summary>
    /// Applies a new theme, notifying all listeners to re-render.
    /// </summary>
    public static void Apply(CascadeTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        List<Action> snapshot;
        lock (syncLock)
        {
            currentTheme = theme;
            version++;
            snapshot = [.. listeners];
        }
        NotifyListeners(snapshot);
    }

    /// <summary>
    /// Switches between light and dark mode, notifying all listeners.
    /// </summary>
    public static void SetDarkMode(bool dark)
    {
        List<Action> snapshot;
        lock (syncLock)
        {
            if (isDarkMode == dark)
            {
                return;
            }
            isDarkMode = dark;
            version++;
            snapshot = [.. listeners];
        }
        NotifyListeners(snapshot);
    }

    /// <summary>
    /// Toggles dark mode on or off.
    /// </summary>
    public static void ToggleDarkMode()
    {
        SetDarkMode(!IsDarkMode);
    }

    /// <summary>
    /// Enables or disables high-contrast mode, notifying all listeners.
    /// </summary>
    public static void SetHighContrast(bool highContrast)
    {
        List<Action> snapshot;
        lock (syncLock)
        {
            if (useHighContrast == highContrast)
            {
                return;
            }
            useHighContrast = highContrast;
            version++;
            snapshot = [.. listeners];
        }
        NotifyListeners(snapshot);
    }

    /// <summary>
    /// Returns the active <see cref="ColorSet"/> for the current theme,
    /// taking into account dark mode and high-contrast settings.
    /// </summary>
    public static ColorSet ActiveColors
    {
        get
        {
            lock (syncLock)
            {
                if (useHighContrast)
                {
                    var hc = isDarkMode
                        ? currentTheme.HighContrastDarkColors
                        : currentTheme.HighContrastLightColors;
                    if (hc != null)
                    {
                        return hc;
                    }
                }
                return isDarkMode ? currentTheme.DarkColors : currentTheme.LightColors;
            }
        }
    }

    /// <summary>
    /// Registers a callback invoked when the theme, dark mode, or high-contrast
    /// settings change. Returns a disposable that removes the listener.
    /// </summary>
    public static IDisposable Subscribe(Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(onChanged);
        lock (syncLock)
        {
            listeners.Add(onChanged);
        }
        return new ThemeSubscription(onChanged);
    }

    private static void NotifyListeners(List<Action> snapshot)
    {
        foreach (var listener in snapshot)
        {
            listener();
        }
    }

    /// <summary>Resets to defaults. For testing only.</summary>
    internal static void Reset()
    {
        lock (syncLock)
        {
            currentTheme = new FluentTheme();
            isDarkMode = false;
            useHighContrast = false;
            version++;
            listeners.Clear();
        }
    }

    private sealed class ThemeSubscription : IDisposable
    {
        private readonly Action callback;

        public ThemeSubscription(Action callback)
        {
            this.callback = callback;
        }

        public void Dispose()
        {
            lock (syncLock)
            {
                listeners.Remove(callback);
            }
        }
    }
}
