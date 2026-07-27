namespace Cascade.UI;

/// <summary>
/// Configuration options for custom dialogs opened via
/// <see cref="Dialog.ShowAsync{TComponent,TResult}"/>.
/// </summary>
public class DialogOptions
{
    /// <summary>Dialog width sizing. Default: <see cref="DialogSize.Auto"/>.</summary>
    public DialogSize Size { get; init; } = DialogSize.Auto;

    /// <summary>Where the dialog appears. Default: <see cref="DialogPosition.Center"/>.</summary>
    public DialogPosition Position { get; init; } = DialogPosition.Center;

    /// <summary>
    /// Whether tapping the backdrop or pressing Escape dismisses the dialog.
    /// Default: true.
    /// </summary>
    public bool Dismissable { get; init; } = true;

    /// <summary>Whether to show a backdrop behind the dialog. Default: true.</summary>
    public bool ShowBackdrop { get; init; } = true;

    /// <summary>Opacity of the backdrop layer (0.0–1.0). Default: 0.5.</summary>
    public float BackdropOpacity { get; init; } = 0.5f;

    /// <summary>Enter/exit animation. Default: <see cref="DialogAnimation.Fade"/>.</summary>
    public DialogAnimation Animation { get; init; } = DialogAnimation.Fade;

    /// <summary>Optional chrome title displayed above the dialog content.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// Visual style hint. <see cref="DialogStyle.Sheet"/> renders as an
    /// attached sheet on macOS, a standard modal elsewhere.
    /// </summary>
    public DialogStyle Style { get; init; } = DialogStyle.Normal;
}

/// <summary>
/// Visual style for a dialog.
/// </summary>
public enum DialogStyle
{
    /// <summary>Standard dialog appearance.</summary>
    Normal,

    /// <summary>
    /// Destructive action styling — the confirm button renders in the
    /// danger color.
    /// </summary>
    Destructive,

    /// <summary>
    /// macOS-style attached sheet. On macOS, renders as a sheet attached
    /// to the title bar. On other platforms, degrades gracefully to a
    /// standard centered modal.
    /// </summary>
    Sheet
}

/// <summary>
/// Controls which button receives default activation (Enter/Space) in
/// confirmation dialogs.
/// </summary>
public enum DialogDefault
{
    /// <summary>The confirm button is the default action.</summary>
    Confirm,

    /// <summary>The cancel button is the default action.</summary>
    Cancel
}
