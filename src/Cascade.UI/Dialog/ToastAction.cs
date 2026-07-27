namespace Cascade.UI;

/// <summary>
/// An action button displayed on a toast notification. Allows the user
/// to perform an action (e.g., "Undo") before the toast auto-dismisses.
/// </summary>
public class ToastAction
{
    /// <summary>
    /// Creates a toast action button.
    /// </summary>
    /// <param name="label">The button label text.</param>
    /// <param name="onClick">Handler invoked when the button is clicked.</param>
    public ToastAction(string label, Action onClick)
    {
        Label = label;
        OnClick = onClick;
    }

    /// <summary>The button label text.</summary>
    public string Label { get; }

    /// <summary>Handler invoked when the button is clicked.</summary>
    public Action OnClick { get; }
}
