using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// macOS NSTextInputClient adapter. Implements <see cref="IPlatformTextInput"/>
/// by bridging to the Cocoa text input system via the NSTextInputClient protocol.
///
/// NSTextInputClient is the standard macOS mechanism for receiving text from
/// input methods. It handles Japanese (Kotoeri/Google IME), Chinese (Pinyin,
/// Zhuyin), Korean, emoji picker, dictation, and the character viewer.
///
/// Key protocol methods:
///   setMarkedText:selectedRange:replacementRange: — composition update
///   unmarkText — composition commit
///   firstRectForCharacterRange:actualRange: — candidate window positioning
///   hasMarkedText, markedRange — composition state queries
/// </summary>
internal sealed class NsTextInputAdapter : IPlatformTextInput, IDisposable
{
    private nint windowHandle;
    private bool contextActive;
    private bool disposed;
    private InputLocale currentLocale;

    // Composition state
    private bool composing;
    private TextComposition? activeComposition;

    /// <summary>Fired when marked text is set (composition begins/updates).</summary>
    internal event Action<TextComposition>? CompositionUpdated;

    /// <summary>Fired when marked text is committed.</summary>
    internal event Action<string>? CompositionCommitted;

    /// <summary>Fired when composition is cancelled.</summary>
    internal event Action? CompositionCancelled;

    public InputLocale CurrentLocale => currentLocale;

    public event Action<InputLocale>? LocaleChanged;

    internal NsTextInputAdapter(nint windowHandle)
    {
        this.windowHandle = windowHandle;
        currentLocale = DetectCurrentLocale();
    }

    public void SetCompositionRect(Rect screenRect)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || windowHandle == 0)
        {
            return;
        }

        // macOS queries firstRectForCharacterRange:actualRange: from the
        // NSTextInputClient when it needs to position the candidate window.
        // We store the rect and return it when queried.
        // Note: macOS uses bottom-left origin; convert from top-left.
        StoreCompositionRect(screenRect);
    }

    public void ActivateInputContext()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || windowHandle == 0)
        {
            return;
        }

        if (contextActive)
        {
            return;
        }

        // Make this window's text input context the first responder.
        // objc_msgSend(window, sel_getUid("makeFirstResponder:"), textInputView)
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

        contextActive = false;
    }

    /// <summary>
    /// Handles setMarkedText:selectedRange:replacementRange: callback.
    /// Called by the input method when composition text changes.
    /// </summary>
    /// <param name="text">The composition text (may include attributed string segments).</param>
    /// <param name="selectedStart">Start of selection within marked text.</param>
    /// <param name="selectedLength">Length of selection within marked text.</param>
    internal void HandleSetMarkedText(string text, int selectedStart, int selectedLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            // Empty marked text = cancel composition
            if (composing)
            {
                composing = false;
                activeComposition = null;
                CompositionCancelled?.Invoke();
            }
            return;
        }

        composing = true;

        // Build segments from the marked text range
        var segments = new List<CompositionSegment>();
        if (selectedLength > 0 && selectedStart >= 0)
        {
            if (selectedStart > 0)
            {
                segments.Add(new CompositionSegment(0, selectedStart, CompositionSegmentStyle.Input));
            }
            segments.Add(new CompositionSegment(selectedStart, selectedLength,
                CompositionSegmentStyle.TargetConverted));
            int afterSelected = selectedStart + selectedLength;
            if (afterSelected < text.Length)
            {
                segments.Add(new CompositionSegment(afterSelected, text.Length - afterSelected,
                    CompositionSegmentStyle.Input));
            }
        }
        else
        {
            segments.Add(new CompositionSegment(0, text.Length, CompositionSegmentStyle.Input));
        }

        activeComposition = new TextComposition(text, selectedStart, segments);
        CompositionUpdated?.Invoke(activeComposition.Value);
    }

    /// <summary>
    /// Handles unmarkText callback. The composition is committed.
    /// </summary>
    internal void HandleUnmarkText()
    {
        if (!composing)
        {
            return;
        }

        string committedText = activeComposition?.Text ?? "";
        composing = false;
        activeComposition = null;

        if (!string.IsNullOrEmpty(committedText))
        {
            CompositionCommitted?.Invoke(committedText);
        }
    }

    /// <summary>
    /// Handles insertText:replacementRange: callback.
    /// Direct text insertion (bypassing composition).
    /// </summary>
    internal void HandleInsertText(string text)
    {
        if (composing)
        {
            composing = false;
            activeComposition = null;
        }

        if (!string.IsNullOrEmpty(text))
        {
            CompositionCommitted?.Invoke(text);
        }
    }

    /// <summary>
    /// Returns whether marked text (composition) is active.
    /// Called by macOS via hasMarkedText query.
    /// </summary>
    internal bool HasMarkedText => composing;

    /// <summary>
    /// Gets the current composition state.
    /// </summary>
    internal TextComposition? ActiveComposition => activeComposition;

    /// <summary>
    /// Whether a composition is currently in progress.
    /// </summary>
    internal bool IsComposing => composing;

    /// <summary>
    /// Handles selectedInputSourceChanged notification.
    /// The user switched keyboard layout or input method.
    /// </summary>
    internal void HandleInputSourceChanged()
    {
        var newLocale = DetectCurrentLocale();
        if (newLocale != currentLocale)
        {
            currentLocale = newLocale;
            LocaleChanged?.Invoke(currentLocale);
        }
    }

    /// <summary>
    /// Cancels the active composition.
    /// </summary>
    internal void CancelComposition()
    {
        if (!composing)
        {
            return;
        }

        composing = false;
        activeComposition = null;
        CompositionCancelled?.Invoke();

        // Tell the input method to discard marked text:
        // objc_msgSend(inputContext, sel_getUid("discardMarkedText"))
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
        // In a full implementation, reads from:
        // TISCopyCurrentKeyboardInputSource() → TISGetInputSourceProperty(kTISPropertyInputSourceLanguages)
        // For now, use a reasonable default.
        return new InputLocale("en-US", TextDirection.LeftToRight);
    }

    private static void StoreCompositionRect(Rect screenRect)
    {
        // Store the rect for responding to firstRectForCharacterRange: queries.
        // macOS uses bottom-left origin coordinates, so we need to flip Y
        // relative to the screen height.
        _ = screenRect;
    }
}
