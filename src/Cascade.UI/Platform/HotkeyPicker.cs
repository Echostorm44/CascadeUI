namespace Cascade.UI;

/// <summary>
/// A control for settings UIs that allows the user to record a hotkey
/// combination by pressing keys. Displays the current hotkey and enters
/// recording mode on focus/click.
/// </summary>
public class HotkeyPicker : Node
{
    /// <summary>
    /// The label displayed above or beside the picker control.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// The currently assigned hotkey. Null if no hotkey is set.
    /// </summary>
    public Hotkey? Current { get; init; }

    /// <summary>
    /// Callback invoked when the user records a new hotkey combination.
    /// </summary>
    public Action<Hotkey>? OnChange { get; init; }
}
