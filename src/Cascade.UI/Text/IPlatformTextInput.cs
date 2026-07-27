namespace Cascade.UI;

/// <summary>
/// Platform adapter interface for OS text input services (IME, dead keys,
/// input context activation). Each platform provides an implementation
/// wrapping its native text services.
/// </summary>
public interface IPlatformTextInput
{
    /// <summary>
    /// Informs the OS where the composition text is so the candidate window
    /// positions correctly.
    /// </summary>
    void SetCompositionRect(Rect screenRect);

    /// <summary>Activates the OS input context when the control gains focus.</summary>
    void ActivateInputContext();

    /// <summary>Deactivates the OS input context when the control loses focus.</summary>
    void DeactivateInputContext();

    /// <summary>The current input locale (affects word-boundary detection, keyboard layout).</summary>
    InputLocale CurrentLocale { get; }

    /// <summary>Raised when the input locale changes (e.g., user switches keyboard layout).</summary>
    event Action<InputLocale> LocaleChanged;
}
