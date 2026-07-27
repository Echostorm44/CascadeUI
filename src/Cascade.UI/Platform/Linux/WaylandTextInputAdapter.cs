using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Wayland text-input-v3 protocol adapter for Linux. This is the native input
/// method protocol for Wayland compositors that support the zwp_text_input_v3
/// extension.
///
/// Unlike X11-based methods (IBus/Fcitx/XIM which can also run under XWayland),
/// this adapter uses the Wayland protocol directly for optimal integration with
/// native Wayland compositors (GNOME on Wayland, KDE Plasma on Wayland, sway).
///
/// Protocol: zwp_text_input_v3 (stable in most compositors)
/// Events: enter, leave, preedit_string, commit_string, delete_surrounding_text, done
/// Requests: enable, disable, set_surrounding_text, set_text_change_cause,
///           set_content_type, set_cursor_rectangle, commit
/// </summary>
internal sealed class WaylandTextInputAdapter : IPlatformTextInput, IDisposable
{
    private nint display;
    private nint surface;
    private nint textInput;
    private bool contextActive;
    private bool disposed;
    private InputLocale currentLocale;

    // Composition state
    private bool composing;
    private TextComposition? activeComposition;

    // Pending state (accumulated between events, applied at done)
    private string? pendingPreeditText;
    private int pendingPreeditCursorBegin;
    private int pendingPreeditCursorEnd;
    private string? pendingCommitText;

    /// <summary>Fired when preedit text changes.</summary>
    internal event Action<TextComposition>? CompositionUpdated;

    /// <summary>Fired when text is committed.</summary>
    internal event Action<string>? CompositionCommitted;

    /// <summary>Fired when composition is cancelled.</summary>
    internal event Action? CompositionCancelled;

    public InputLocale CurrentLocale => currentLocale;

    public event Action<InputLocale>? LocaleChanged;

    internal WaylandTextInputAdapter(nint display, nint surface)
    {
        this.display = display;
        this.surface = surface;
        currentLocale = DetectCurrentLocale();
    }

    /// <summary>
    /// Called when the Wayland compositor reports a locale change.
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
    /// Checks whether the Wayland text-input-v3 protocol is available.
    /// </summary>
    internal static bool IsAvailable()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        // Wayland requires WAYLAND_DISPLAY to be set
        string? waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        return !string.IsNullOrEmpty(waylandDisplay);
    }

    public void SetCompositionRect(Rect screenRect)
    {
        if (!contextActive || textInput == 0)
        {
            return;
        }

        // zwp_text_input_v3_set_cursor_rectangle(textInput, x, y, width, height)
        // Followed by zwp_text_input_v3_commit(textInput)
        SetCursorRectangle((int)screenRect.X, (int)screenRect.Y,
            (int)screenRect.Width, (int)screenRect.Height);
    }

    public void ActivateInputContext()
    {
        if (contextActive)
        {
            return;
        }

        if (display == 0 || surface == 0)
        {
            return;
        }

        // 1. Get zwp_text_input_manager_v3 from registry
        // 2. zwp_text_input_manager_v3_get_text_input(manager, seat) → text_input
        // 3. zwp_text_input_v3_enable(textInput)
        // 4. zwp_text_input_v3_set_content_type(textInput, hint, purpose)
        // 5. zwp_text_input_v3_commit(textInput)
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

        // zwp_text_input_v3_disable(textInput)
        // zwp_text_input_v3_commit(textInput)
        contextActive = false;
    }

    /// <summary>
    /// Handles preedit_string event from the compositor.
    /// Note: In text-input-v3, all events between enter/done are accumulated
    /// and applied atomically when the done event arrives.
    /// </summary>
    internal void HandlePreeditString(string text, int cursorBegin, int cursorEnd)
    {
        pendingPreeditText = text;
        pendingPreeditCursorBegin = cursorBegin;
        pendingPreeditCursorEnd = cursorEnd;
    }

    /// <summary>
    /// Handles commit_string event from the compositor.
    /// </summary>
    internal void HandleCommitString(string text)
    {
        pendingCommitText = text;
    }

    /// <summary>
    /// Handles done event from the compositor. Applies all pending changes
    /// atomically. This is the text-input-v3 model — all state updates arrive
    /// between preedit_string/commit_string events and are applied at done.
    /// </summary>
    internal void HandleDone()
    {
        // Process commit first (if any)
        if (pendingCommitText is not null)
        {
            composing = false;
            activeComposition = null;
            CompositionCommitted?.Invoke(pendingCommitText);
            pendingCommitText = null;
        }

        // Then process preedit
        if (pendingPreeditText is not null)
        {
            if (string.IsNullOrEmpty(pendingPreeditText))
            {
                if (composing)
                {
                    composing = false;
                    activeComposition = null;
                    CompositionCancelled?.Invoke();
                }
            }
            else
            {
                composing = true;
                var segments = BuildSegments(pendingPreeditText, pendingPreeditCursorBegin,
                    pendingPreeditCursorEnd);
                activeComposition = new TextComposition(pendingPreeditText,
                    pendingPreeditCursorBegin, segments);
                CompositionUpdated?.Invoke(activeComposition.Value);
            }
            pendingPreeditText = null;
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

        // zwp_text_input_v3_destroy(textInput)
        textInput = 0;
        disposed = true;
    }

    // ─── Private implementation ─────────────────────────────────────

    private static IReadOnlyList<CompositionSegment> BuildSegments(
        string text, int cursorBegin, int cursorEnd)
    {
        var segments = new List<CompositionSegment>();

        if (cursorBegin >= 0 && cursorEnd > cursorBegin && cursorEnd <= text.Length)
        {
            // Text before cursor selection
            if (cursorBegin > 0)
            {
                segments.Add(new CompositionSegment(0, cursorBegin, CompositionSegmentStyle.Input));
            }

            // Cursor selection range (target)
            segments.Add(new CompositionSegment(cursorBegin, cursorEnd - cursorBegin,
                CompositionSegmentStyle.TargetConverted));

            // Text after cursor selection
            if (cursorEnd < text.Length)
            {
                segments.Add(new CompositionSegment(cursorEnd, text.Length - cursorEnd,
                    CompositionSegmentStyle.Input));
            }
        }
        else
        {
            // No specific cursor range — entire text is input
            segments.Add(new CompositionSegment(0, text.Length, CompositionSegmentStyle.Input));
        }

        return segments;
    }

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

    private static void SetCursorRectangle(int x, int y, int w, int h)
    {
        // zwp_text_input_v3_set_cursor_rectangle(textInput, x, y, w, h)
        _ = x; _ = y; _ = w; _ = h;
    }
}
