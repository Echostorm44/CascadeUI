namespace Cascade.UI;

/// <summary>
/// Shows bottom sheets: modal panels against the bottom edge of the window that slide up over
/// a backdrop. Built on the same overlay layer as <see cref="Dialog"/> (same stack, focus trap
/// and threading rules), with a drag handle drawn at the top.
/// </summary>
/// <remarks>
/// A sheet is dismissed by Escape, a click on the backdrop, or dragging it down by its handle
/// past a threshold — all only when <see cref="DialogOptions.Dismissable"/> (the default); a
/// drag that stops short snaps back. Its width follows <see cref="DialogOptions.Size"/>
/// (content-sized sheets are at most 640px wide); its height follows the content, up to the
/// window less a gap at the top.
/// </remarks>
public static class BottomSheet
{
    /// <summary>
    /// Shows a new <typeparamref name="TComponent"/> as a bottom sheet and awaits a result.
    /// </summary>
    /// <typeparam name="TComponent">The sheet component type.</typeparam>
    /// <typeparam name="TResult">The result type returned by the sheet.</typeparam>
    /// <param name="options">Optional dialog options (size, title, dismissable, backdrop).</param>
    public static Task<TResult?> ShowAsync<TComponent, TResult>(
        DialogOptions? options = null)
        where TComponent : Component, new()
    {
        var resolved = ApplyDefaults(options);
        return Dialog.Open<TResult>(
            "BottomSheet.ShowAsync",
            (manager, completion) => Dialog.Custom(manager, OverlayKind.Sheet, new TComponent(), completion, resolved));
    }

    /// <summary>
    /// Shows a new <typeparamref name="TComponent"/> as a bottom sheet with no return value.
    /// </summary>
    /// <typeparam name="TComponent">The sheet component type.</typeparam>
    /// <param name="options">Optional dialog options (size, title, dismissable, backdrop).</param>
    public static Task ShowAsync<TComponent>(
        DialogOptions? options = null)
        where TComponent : Component, new()
    {
        var resolved = ApplyDefaults(options);
        return Dialog.Open<object>(
            "BottomSheet.ShowAsync",
            (manager, completion) => Dialog.Custom(manager, OverlayKind.Sheet, new TComponent(), completion, resolved));
    }

    /// <summary>
    /// Shows <paramref name="content"/> as a bottom sheet and awaits a result. Pass a new
    /// instance each time.
    /// </summary>
    /// <typeparam name="TResult">The result type returned by the sheet.</typeparam>
    /// <param name="content">The sheet's content component.</param>
    /// <param name="options">Optional dialog options (size, title, dismissable, backdrop).</param>
    public static Task<TResult?> ShowAsync<TResult>(Component content, DialogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var resolved = ApplyDefaults(options);
        return Dialog.Open<TResult>(
            "BottomSheet.ShowAsync",
            (manager, completion) => Dialog.Custom(manager, OverlayKind.Sheet, content, completion, resolved));
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

        var options = ApplyDefaults(null);
        return Dialog.Open<string>(
            "BottomSheet.ShowActionsAsync",
            (manager, completion) => Dialog.Custom(manager, OverlayKind.Sheet, new ActionSheetView(title, actions, cancel), completion, options, accessibleLabel: title));
    }

    /// <summary>Sheet defaults: bottom position and slide-up animation; the caller's other options are kept.</summary>
    internal static DialogOptions ApplyDefaults(DialogOptions? options)
    {
        var resolved = options ?? new DialogOptions();
        return new DialogOptions
        {
            Size = resolved.Size,
            Position = DialogPosition.Bottom,
            Dismissable = resolved.Dismissable,
            ShowBackdrop = resolved.ShowBackdrop,
            BackdropOpacity = resolved.BackdropOpacity,
            Animation = resolved.Animation == DialogAnimation.None ? DialogAnimation.None : DialogAnimation.SlideUp,
            Title = resolved.Title,
            Style = resolved.Style,
        };
    }
}
