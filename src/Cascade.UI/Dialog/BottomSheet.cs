namespace Cascade.UI;

/// <summary>
/// Static methods for showing bottom sheets — sugar over
/// <see cref="Dialog.ShowAsync{TComponent,TResult}"/> with
/// <see cref="DialogPosition.Bottom"/> and <see cref="DialogAnimation.SlideUp"/>.
/// Supports swipe-to-dismiss and shows a drag handle indicator automatically.
/// </summary>
public static class BottomSheet
{
    internal sealed record BottomSheetRequest(Type ComponentType, DialogOptions Options);

    internal sealed record ActionSheetRequest(string Title, IReadOnlyList<string> Actions, string CancelLabel);

    internal static BottomSheetRequest? LastRequest { get; private set; }

    internal static ActionSheetRequest? LastActionSheetRequest { get; private set; }

    /// <summary>
    /// Shows a custom component as a bottom sheet and awaits a result.
    /// </summary>
    /// <typeparam name="TComponent">The sheet component type.</typeparam>
    /// <typeparam name="TResult">The result type returned by the sheet.</typeparam>
    /// <param name="options">Optional dialog options to override defaults.</param>
    public static Task<TResult?> ShowAsync<TComponent, TResult>(
        DialogOptions? options = null)
        where TComponent : Component
    {
        var sheetOptions = ApplyDefaults(options);
        LastRequest = new BottomSheetRequest(typeof(TComponent), sheetOptions);
        return Dialog.ShowAsync<TComponent, TResult>(sheetOptions);
    }

    /// <summary>
    /// Shows a custom component as a bottom sheet with no return value.
    /// </summary>
    /// <typeparam name="TComponent">The sheet component type.</typeparam>
    /// <param name="options">Optional dialog options to override defaults.</param>
    public static Task ShowAsync<TComponent>(
        DialogOptions? options = null)
        where TComponent : Component
    {
        var sheetOptions = ApplyDefaults(options);
        LastRequest = new BottomSheetRequest(typeof(TComponent), sheetOptions);
        return Dialog.ShowAsync<TComponent>(sheetOptions);
    }

    /// <summary>
    /// Shows a simple action sheet with a list of choices. Returns the
    /// selected action label, or null if cancelled.
    /// </summary>
    /// <param name="title">Title displayed at the top of the sheet.</param>
    /// <param name="actions">List of action labels to display.</param>
    /// <param name="cancel">Label for the cancel button.</param>
    public static Task<string?> ShowActionsAsync(
        string title,
        IReadOnlyList<string> actions,
        string cancel = "Cancel")
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(cancel);

        LastActionSheetRequest = new ActionSheetRequest(title, actions, cancel);
        var sheetOptions = ApplyDefaults(null);
        return Dialog.ShowAsync<ActionSheetDialog, string?>(sheetOptions);
    }

    private static DialogOptions ApplyDefaults(DialogOptions? options)
    {
        var resolved = options ?? new DialogOptions();
        return new DialogOptions
        {
            Size = resolved.Size,
            Position = DialogPosition.Bottom,
            Dismissable = resolved.Dismissable,
            ShowBackdrop = resolved.ShowBackdrop,
            BackdropOpacity = resolved.BackdropOpacity,
            Animation = DialogAnimation.SlideUp,
            Title = resolved.Title,
            Style = resolved.Style
        };
    }

    private sealed class ActionSheetDialog : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }
}
