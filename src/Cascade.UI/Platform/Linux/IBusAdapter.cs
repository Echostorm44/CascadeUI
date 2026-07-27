using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// IBus (Intelligent Input Bus) adapter for Linux. IBus is the most common
/// input method framework on modern Linux distributions (Ubuntu, Fedora, etc.).
///
/// IBus communicates over D-Bus with the ibus-daemon. The adapter connects
/// to the IBus InputContext interface to handle composition lifecycle:
///   - CommitText signal → committed text
///   - UpdatePreeditText signal → composition update
///   - ShowPreeditText / HidePreeditText → composition visibility
///   - ForwardKeyEvent signal → keys the IM didn't consume
/// </summary>
internal sealed class IBusAdapter : IPlatformTextInput, IDisposable
{
    private bool contextActive;
    private bool disposed;
    private InputLocale currentLocale;

    // Composition state
    private bool composing;
    private TextComposition? activeComposition;

    /// <summary>Fired when preedit text is updated.</summary>
    internal event Action<TextComposition>? CompositionUpdated;

    /// <summary>Fired when text is committed.</summary>
    internal event Action<string>? CompositionCommitted;

    /// <summary>Fired when composition is cancelled.</summary>
    internal event Action? CompositionCancelled;

    public InputLocale CurrentLocale => currentLocale;

    public event Action<InputLocale>? LocaleChanged;

    internal IBusAdapter()
    {
        currentLocale = DetectCurrentLocale();
    }

    /// <summary>
    /// Called when IBus reports an input source change via D-Bus signal.
    /// </summary>
    internal void HandleLocaleChanged(string localeIdentifier)
    {
        TextDirection direction = localeIdentifier.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
            || localeIdentifier.StartsWith("he", StringComparison.OrdinalIgnoreCase)
            ? TextDirection.RightToLeft
            : TextDirection.LeftToRight;
        currentLocale = new InputLocale(localeIdentifier, direction);
        LocaleChanged?.Invoke(currentLocale);
    }

    /// <summary>
    /// Checks whether IBus is available on this system.
    /// </summary>
    internal static bool IsAvailable()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        // Check for IBUS_ADDRESS environment variable or ibus-daemon socket
        string? ibusAddress = Environment.GetEnvironmentVariable("IBUS_ADDRESS");
        if (!string.IsNullOrEmpty(ibusAddress))
        {
            return true;
        }

        // Check for the standard IBus socket path
        string? xdgRuntime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (xdgRuntime is not null)
        {
            string socketPath = System.IO.Path.Combine(xdgRuntime, "ibus", "bus");
            return System.IO.Directory.Exists(socketPath);
        }

        return false;
    }

    public void SetCompositionRect(Rect screenRect)
    {
        if (!contextActive)
        {
            return;
        }

        // Send SetCursorLocation to the IBus input context via D-Bus:
        // org.freedesktop.IBus.InputContext.SetCursorLocation(x, y, w, h)
        SetCursorLocation((int)screenRect.X, (int)screenRect.Y,
            (int)screenRect.Width, (int)screenRect.Height);
    }

    public void ActivateInputContext()
    {
        if (contextActive)
        {
            return;
        }

        // Connect to IBus daemon and create input context:
        // 1. Connect to D-Bus session bus
        // 2. Call org.freedesktop.IBus.CreateInputContext("CascadeUI")
        // 3. Subscribe to CommitText, UpdatePreeditText, etc. signals
        // 4. Call FocusIn on the input context
        contextActive = true;
    }

    public void DeactivateInputContext()
    {
        if (!contextActive)
        {
            return;
        }

        if (composing)
        {
            CancelComposition();
        }

        // Call FocusOut on the IBus input context
        contextActive = false;
    }

    /// <summary>
    /// Handles UpdatePreeditText signal from IBus.
    /// </summary>
    internal void HandlePreeditUpdate(string text, int cursorPos, bool visible)
    {
        if (!visible || string.IsNullOrEmpty(text))
        {
            if (composing)
            {
                composing = false;
                activeComposition = null;
                CompositionCancelled?.Invoke();
            }
            return;
        }

        composing = true;
        var segments = new List<CompositionSegment>
        {
            new(0, text.Length, CompositionSegmentStyle.Input),
        };

        activeComposition = new TextComposition(text, cursorPos, segments);
        CompositionUpdated?.Invoke(activeComposition.Value);
    }

    /// <summary>
    /// Handles CommitText signal from IBus.
    /// </summary>
    internal void HandleCommitText(string text)
    {
        composing = false;
        activeComposition = null;

        if (!string.IsNullOrEmpty(text))
        {
            CompositionCommitted?.Invoke(text);
        }
    }

    /// <summary>
    /// Gets the current composition state.
    /// </summary>
    internal TextComposition? ActiveComposition => activeComposition;

    /// <summary>
    /// Whether a composition is currently in progress.
    /// </summary>
    internal bool IsComposing => composing;

    /// <summary>
    /// Cancels the active composition by sending Reset to IBus.
    /// </summary>
    internal void CancelComposition()
    {
        if (!composing)
        {
            return;
        }

        // org.freedesktop.IBus.InputContext.Reset()
        composing = false;
        activeComposition = null;
        CompositionCancelled?.Invoke();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        DeactivateInputContext();
        disposed = true;
    }

    // ─── Private implementation ─────────────────────────────────────

    private static InputLocale DetectCurrentLocale()
    {
        // Read from LANG or LC_ALL environment variables
        string? lang = Environment.GetEnvironmentVariable("LANG")
                    ?? Environment.GetEnvironmentVariable("LC_ALL");

        if (lang is not null)
        {
            // Parse "en_US.UTF-8" → "en-US"
            string identifier = lang.Split('.')[0].Replace("_", "-", StringComparison.Ordinal);
            TextDirection direction = identifier.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
                || identifier.StartsWith("he", StringComparison.OrdinalIgnoreCase)
                ? TextDirection.RightToLeft
                : TextDirection.LeftToRight;
            return new InputLocale(identifier, direction);
        }

        return new InputLocale("en-US", TextDirection.LeftToRight);
    }

    private static void SetCursorLocation(int x, int y, int w, int h)
    {
        // D-Bus call: org.freedesktop.IBus.InputContext.SetCursorLocation(x, y, w, h)
        _ = x; _ = y; _ = w; _ = h;
    }
}
