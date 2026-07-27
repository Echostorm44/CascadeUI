using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Fcitx input method adapter for Linux. Fcitx (Flexible Input Method Framework)
/// is common in Chinese-locale systems and supports multiple input methods
/// concurrently (Pinyin, Wubi, Hangul, Mozc, etc.).
///
/// Like IBus, Fcitx communicates over D-Bus. The adapter connects to the
/// Fcitx InputContext interface for composition handling.
///
/// Fcitx5 D-Bus interface: org.fcitx.Fcitx.InputContext1
/// Signals: CommitString, UpdateFormattedPreedit, ForwardKey
/// </summary>
internal sealed class FcitxAdapter : IPlatformTextInput, IDisposable
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

    internal FcitxAdapter()
    {
        currentLocale = DetectCurrentLocale();
    }

    /// <summary>
    /// Called when Fcitx reports an input method change via D-Bus signal.
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
    /// Checks whether Fcitx is available on this system.
    /// </summary>
    internal static bool IsAvailable()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        // Check for Fcitx-specific environment variables
        string? imModule = Environment.GetEnvironmentVariable("GTK_IM_MODULE");
        if (imModule is "fcitx" or "fcitx5")
        {
            return true;
        }

        string? inputMethod = Environment.GetEnvironmentVariable("INPUT_METHOD");
        return inputMethod is "fcitx" or "fcitx5";
    }

    public void SetCompositionRect(Rect screenRect)
    {
        if (!contextActive)
        {
            return;
        }

        // Fcitx5: org.fcitx.Fcitx.InputContext1.SetCursorRect(x, y, w, h)
        SetCursorRect((int)screenRect.X, (int)screenRect.Y,
            (int)screenRect.Width, (int)screenRect.Height);
    }

    public void ActivateInputContext()
    {
        if (contextActive)
        {
            return;
        }

        // Connect to Fcitx D-Bus service:
        // 1. Call org.fcitx.Fcitx.InputMethod1.CreateInputContext("CascadeUI")
        // 2. Subscribe to CommitString, UpdateFormattedPreedit signals
        // 3. Call FocusIn
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

        // Call FocusOut on the Fcitx input context
        contextActive = false;
    }

    /// <summary>
    /// Handles UpdateFormattedPreedit signal from Fcitx.
    /// Fcitx sends preedit text with formatting attributes (segment info).
    /// </summary>
    internal void HandleFormattedPreeditUpdate(string text, int cursorPos,
        IReadOnlyList<CompositionSegment> segments)
    {
        if (string.IsNullOrEmpty(text))
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
        activeComposition = new TextComposition(text, cursorPos, segments);
        CompositionUpdated?.Invoke(activeComposition.Value);
    }

    /// <summary>
    /// Handles CommitString signal from Fcitx.
    /// </summary>
    internal void HandleCommitString(string text)
    {
        composing = false;
        activeComposition = null;

        if (!string.IsNullOrEmpty(text))
        {
            CompositionCommitted?.Invoke(text);
        }
    }

    /// <summary>Gets the current composition state.</summary>
    internal TextComposition? ActiveComposition => activeComposition;

    /// <summary>Whether a composition is in progress.</summary>
    internal bool IsComposing => composing;

    /// <summary>Cancels the active composition.</summary>
    internal void CancelComposition()
    {
        if (!composing)
        {
            return;
        }

        // org.fcitx.Fcitx.InputContext1.Reset()
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
        string? lang = Environment.GetEnvironmentVariable("LANG")
                    ?? Environment.GetEnvironmentVariable("LC_ALL");

        if (lang is not null)
        {
            string identifier = lang.Split('.')[0].Replace("_", "-", StringComparison.Ordinal);
            TextDirection direction = identifier.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
                || identifier.StartsWith("he", StringComparison.OrdinalIgnoreCase)
                ? TextDirection.RightToLeft
                : TextDirection.LeftToRight;
            return new InputLocale(identifier, direction);
        }

        return new InputLocale("en-US", TextDirection.LeftToRight);
    }

    private static void SetCursorRect(int x, int y, int w, int h)
    {
        // D-Bus call: org.fcitx.Fcitx.InputContext1.SetCursorRect(x, y, w, h)
        _ = x; _ = y; _ = w; _ = h;
    }
}
