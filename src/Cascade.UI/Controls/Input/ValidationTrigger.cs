namespace Cascade.UI;

/// <summary>
/// Determines when validation fires on an input control.
/// </summary>
public enum ValidationTrigger
{
    /// <summary>Fires on every change. Use for character-count limits and format restrictions.</summary>
    Immediate,

    /// <summary>Fires after the debounce delay settles. Default when <c>.Debounce()</c> is applied.</summary>
    Debounced,

    /// <summary>
    /// Fires when the control loses focus. Recommended default for most form fields.
    /// </summary>
    Blur,

    /// <summary>
    /// Fires only when the enclosing <see cref="FormValidator"/> requests submission.
    /// </summary>
    Submit,

    /// <summary>
    /// Never fires automatically. The developer calls Validate() on the control ref explicitly.
    /// </summary>
    Manual
}
