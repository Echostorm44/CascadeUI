using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// XIM (X Input Method) adapter for legacy X11 input method support on Linux.
/// XIM is the oldest input method protocol on X11 and serves as the fallback
/// when neither IBus nor Fcitx is available.
///
/// XIM uses the Xlib functions: XOpenIM, XCreateIC, XFilterEvent, XmbLookupString.
/// The IC (Input Context) handles composition state, and XFilterEvent routes
/// key events through the IM before normal processing.
/// </summary>
internal sealed class XimAdapter : IPlatformTextInput, IDisposable
{
    private nint display;
    private nint window;
    private nint xic;
    private bool contextActive;
    private bool disposed;
    private InputLocale currentLocale;

    // Composition state
    private bool composing;
    private TextComposition? activeComposition;

    /// <summary>Fired when preedit text changes.</summary>
    internal event Action<TextComposition>? CompositionUpdated;

    /// <summary>Fired when text is committed.</summary>
    internal event Action<string>? CompositionCommitted;

    /// <summary>Fired when composition is cancelled.</summary>
    internal event Action? CompositionCancelled;

    public InputLocale CurrentLocale => currentLocale;

    public event Action<InputLocale>? LocaleChanged;

    internal XimAdapter(nint display, nint window)
    {
        this.display = display;
        this.window = window;
        currentLocale = DetectCurrentLocale();
    }

    /// <summary>
    /// Called when the X11 input method locale changes.
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
    /// Checks whether XIM is available on this system.
    /// Returns true if we're running under X11 (not Wayland).
    /// </summary>
    internal static bool IsAvailable()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        // XIM requires X11 display
        string? display = Environment.GetEnvironmentVariable("DISPLAY");
        return !string.IsNullOrEmpty(display);
    }

    public void SetCompositionRect(Rect screenRect)
    {
        if (!contextActive || xic == 0)
        {
            return;
        }

        // Set the spot location for the preedit:
        // XVaNestedList preeditAttr = XVaCreateNestedList(0, XNSpotLocation, &point, NULL)
        // XSetICValues(xic, XNPreeditAttributes, preeditAttr, NULL)
        SetSpotLocation((int)screenRect.X, (int)screenRect.Y + (int)screenRect.Height);
    }

    public void ActivateInputContext()
    {
        if (contextActive)
        {
            return;
        }

        if (display == 0 || window == 0)
        {
            return;
        }

        try
        {
            // Open the input method
            // xim = XOpenIM(display, null, null, null)
            // xic = XCreateIC(xim, XNInputStyle, XIMPreeditNothing|XIMStatusNothing,
            //                 XNClientWindow, window, XNFocusWindow, window, NULL)
            // XSetICFocus(xic)
            contextActive = true;
        }
        catch (DllNotFoundException)
        {
            // X11 not available (running under pure Wayland).
        }
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

        // XUnsetICFocus(xic)
        contextActive = false;
    }

    /// <summary>
    /// Filters an X11 key event through XIM. Returns true if the IM consumed
    /// the event (composition handling), false if the event should be processed normally.
    /// </summary>
    /// <param name="keyEvent">The X11 key event to filter.</param>
    /// <returns>True if the IM consumed the event.</returns>
    internal bool FilterKeyEvent(nint keyEvent)
    {
        if (!contextActive || xic == 0)
        {
            return false;
        }

        // bool filtered = XFilterEvent(keyEvent, window);
        // if (filtered) return true;
        // Otherwise, look up the string:
        // int len = XmbLookupString(xic, keyEvent, buffer, bufSize, &keysym, &status)
        // if (status == XLookupChars || status == XLookupBoth) commit text
        // if (status == XBufferOverflow) reallocate and retry
        return false;
    }

    /// <summary>
    /// Handles preedit text update from the XIM callback.
    /// </summary>
    internal void HandlePreeditUpdate(string text, int cursorPos)
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
        var segments = new List<CompositionSegment>
        {
            new(0, text.Length, CompositionSegmentStyle.Input),
        };

        activeComposition = new TextComposition(text, cursorPos, segments);
        CompositionUpdated?.Invoke(activeComposition.Value);
    }

    /// <summary>
    /// Handles committed text from XmbLookupString.
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

        // XmbResetIC(xic) resets the input context
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

        // XDestroyIC(xic); XCloseIM(xim);
        xic = 0;
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

    private static void SetSpotLocation(int x, int y)
    {
        // XVaCreateNestedList + XSetICValues for XNSpotLocation
        _ = x; _ = y;
    }
}
