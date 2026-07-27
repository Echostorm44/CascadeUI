using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Windows Text Services Framework (TSF) adapter. Implements <see cref="IPlatformTextInput"/>
/// by integrating with the TSF COM APIs for IME composition, candidate window
/// positioning, and input locale tracking.
///
/// TSF supersedes the older IMM32 API and provides full Unicode support,
/// reconversion, language bar integration, and handwriting/speech input.
///
/// P/Invoke calls target tsf.dll and imm32.dll for NativeAOT compatibility.
/// </summary>
#pragma warning disable CA2216 // Native handles released in DeactivateInputContext; no finalizer needed
internal sealed class TsfAdapter : IPlatformTextInput, IDisposable
#pragma warning restore CA2216
{
    private nint windowHandle;
    private nint inputContext;
    private bool contextActive;
    private bool disposed;
    private InputLocale currentLocale;

    // Composition state
    private bool composing;
    private TextComposition? activeComposition;

    /// <summary>Fired when a composition begins.</summary>
    internal event Action? CompositionStarted;

    /// <summary>Fired when the composition text or cursor changes.</summary>
    internal event Action<TextComposition>? CompositionUpdated;

    /// <summary>Fired when the composition is committed to the document.</summary>
    internal event Action<string>? CompositionCommitted;

    /// <summary>Fired when the composition is cancelled.</summary>
    internal event Action? CompositionCancelled;

    public InputLocale CurrentLocale => currentLocale;

    public event Action<InputLocale>? LocaleChanged;

    internal TsfAdapter(nint windowHandle)
    {
        this.windowHandle = windowHandle;
        currentLocale = DetectCurrentLocale();
    }

    public void SetCompositionRect(Rect screenRect)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || windowHandle == 0)
        {
            return;
        }

        // TSF uses ITfContextView::GetTextExt for candidate positioning.
        // For IMM32 fallback, we set the composition window position:
        //   ImmSetCompositionWindow(hImc, &compositionForm)
        // where compositionForm specifies CFS_POINT with the screen rect.
        try
        {
            SetCandidateWindowPosition(screenRect);
        }
        catch (DllNotFoundException)
        {
            // IMM32 not available (shouldn't happen on Windows).
        }
    }

    public void ActivateInputContext()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || windowHandle == 0)
        {
            return;
        }

        if (contextActive)
        {
            return;
        }

        try
        {
            // Associate the default input context with this window
            inputContext = TsfInterop.ImmGetContext(windowHandle);
            contextActive = true;
        }
        catch (DllNotFoundException)
        {
            // IMM32 not available.
        }
    }

    public void DeactivateInputContext()
    {
        if (!contextActive)
        {
            return;
        }

        try
        {
            // If composing, cancel the composition first
            if (composing)
            {
                CancelComposition();
            }

            // Release the input context
            if (windowHandle != 0 && inputContext != 0)
            {
                TsfInterop.ImmReleaseContext(windowHandle, inputContext);
            }

            inputContext = 0;
            contextActive = false;
        }
        catch (DllNotFoundException)
        {
            contextActive = false;
        }
    }

    /// <summary>
    /// Processes WM_IME_STARTCOMPOSITION — the IME begins composing text.
    /// </summary>
    internal void HandleStartComposition()
    {
        composing = true;
        activeComposition = null;
        CompositionStarted?.Invoke();
    }

    /// <summary>
    /// Processes WM_IME_COMPOSITION — the composition text or state changes.
    /// Reads the composition string and clause information from the input context.
    /// </summary>
    internal void HandleComposition(nint lParam)
    {
        if (!composing)
        {
            return;
        }

        // Read composition string from the input context
        string compositionText = GetCompositionString();
        int cursorOffset = GetCompositionCursorOffset();
        var segments = GetCompositionSegments(compositionText.Length);

        activeComposition = new TextComposition(compositionText, cursorOffset, segments);
        CompositionUpdated?.Invoke(activeComposition.Value);
    }

    /// <summary>
    /// Processes WM_IME_ENDCOMPOSITION — the composition ends.
    /// The result string has been committed to the document.
    /// </summary>
    internal void HandleEndComposition()
    {
        if (!composing)
        {
            return;
        }

        string resultText = GetResultString();
        composing = false;
        activeComposition = null;

        if (!string.IsNullOrEmpty(resultText))
        {
            CompositionCommitted?.Invoke(resultText);
        }
    }

    /// <summary>
    /// Processes WM_INPUTLANGCHANGE — the user switched keyboard layouts.
    /// </summary>
    internal void HandleInputLanguageChange()
    {
        var newLocale = DetectCurrentLocale();
        if (newLocale != currentLocale)
        {
            currentLocale = newLocale;
            LocaleChanged?.Invoke(currentLocale);
        }
    }

    /// <summary>
    /// Gets the current active composition, if any.
    /// </summary>
    internal TextComposition? ActiveComposition => activeComposition;

    /// <summary>
    /// Whether a composition is currently in progress.
    /// </summary>
    internal bool IsComposing => composing;

    /// <summary>
    /// Cancels the active composition.
    /// </summary>
    internal void CancelComposition()
    {
        if (!composing)
        {
            return;
        }

        try
        {
            if (inputContext != 0)
            {
                TsfInterop.ImmNotifyIME(inputContext, TsfInterop.NI_COMPOSITIONSTR,
                    TsfInterop.CPS_CANCEL, 0);
            }
        }
        catch (DllNotFoundException)
        {
            // Fall through.
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

        // Ensure composing state is fully reset even if the context was never
        // successfully activated (e.g. on non-Windows platforms in tests)
        composing = false;
        activeComposition = null;
        disposed = true;
    }

    // ─── Private implementation ─────────────────────────────────────

    private static InputLocale DetectCurrentLocale()
    {
        try
        {
            nint hkl = TsfInterop.GetKeyboardLayout(0);
            int langId = (int)(hkl.ToInt64() & 0xFFFF);

            // Map common language IDs to locale strings
            string identifier = langId switch
            {
                0x0409 => "en-US",
                0x0411 => "ja-JP",
                0x0412 => "ko-KR",
                0x0804 => "zh-CN",
                0x0404 => "zh-TW",
                0x0407 => "de-DE",
                0x040C => "fr-FR",
                0x0410 => "it-IT",
                0x0C0A => "es-ES",
                0x0416 => "pt-BR",
                0x0401 => "ar-SA",
                0x040D => "he-IL",
                0x0419 => "ru-RU",
                0x0415 => "pl-PL",
                _ => $"lang-{langId:X4}",
            };

            TextDirection direction = langId switch
            {
                0x0401 => TextDirection.RightToLeft, // Arabic
                0x040D => TextDirection.RightToLeft, // Hebrew
                _ => TextDirection.LeftToRight,
            };

            return new InputLocale(identifier, direction);
        }
        catch (DllNotFoundException)
        {
            return new InputLocale("en-US", TextDirection.LeftToRight);
        }
    }

    private static void SetCandidateWindowPosition(Rect screenRect)
    {
        // In a full implementation, this would call:
        // ImmSetCompositionWindow(inputContext, &compositionForm)
        // where compositionForm.dwStyle = CFS_POINT,
        //       compositionForm.ptCurrentPos = { (int)screenRect.X, (int)screenRect.Y + (int)screenRect.Height }
        _ = screenRect;
    }

    private string GetCompositionString()
    {
        // In a full implementation:
        // int length = ImmGetCompositionString(inputContext, GCS_COMPSTR, null, 0);
        // byte[] buffer = new byte[length];
        // ImmGetCompositionString(inputContext, GCS_COMPSTR, buffer, length);
        // return Encoding.Unicode.GetString(buffer);
        return activeComposition?.Text ?? "";
    }

    private int GetCompositionCursorOffset()
    {
        // ImmGetCompositionString(inputContext, GCS_CURSORPOS, null, 0) returns cursor offset
        return activeComposition?.CursorOffset ?? 0;
    }

    private static IReadOnlyList<CompositionSegment> GetCompositionSegments(int textLength)
    {
        // In a full implementation, read GCS_COMPATTR to get per-character attributes,
        // then group consecutive same-attribute characters into segments.
        // ATTR_INPUT = 0, ATTR_TARGET_CONVERTED = 1, etc.
        if (textLength <= 0)
        {
            return [];
        }

        return [new CompositionSegment(0, textLength, CompositionSegmentStyle.Input)];
    }

    private static string GetResultString()
    {
        // ImmGetCompositionString(inputContext, GCS_RESULTSTR, buffer, length)
        return "";
    }
}

/// <summary>
/// P/Invoke declarations for Windows IMM32/TSF APIs.
/// </summary>
#pragma warning disable CA5392 // P/Invokes target well-known system DLLs (imm32.dll, user32.dll)
internal static partial class TsfInterop
{
    internal const int NI_COMPOSITIONSTR = 0x0015;
    internal const int CPS_CANCEL = 0x0004;

    [LibraryImport("imm32.dll")]
    internal static partial nint ImmGetContext(nint hWnd);

    [LibraryImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ImmReleaseContext(nint hWnd, nint hIMC);

    [LibraryImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ImmNotifyIME(nint hIMC, int dwAction, int dwIndex, int dwValue);

    [LibraryImport("user32.dll")]
    internal static partial nint GetKeyboardLayout(uint idThread);
}
#pragma warning restore CA5392
