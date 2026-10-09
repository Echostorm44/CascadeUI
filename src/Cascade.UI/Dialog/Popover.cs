namespace Cascade.UI;

/// <summary>
/// Shows popovers: arbitrary content in a floating panel anchored to a node or a window point.
/// Popovers are not async — the content communicates through its own properties and closes
/// itself with <c>DialogContext.Close()</c> (or <see cref="Dialog.Dismiss"/>).
/// </summary>
/// <remarks>
/// <para>
/// The panel goes on <see cref="PopoverOptions.PreferredSide"/> of the anchor, centered on it,
/// flips to the opposite side when there is more room there, and is kept inside the window. It
/// follows the anchor as the page re-lays out and closes when the anchor leaves the tree.
/// </para>
/// <para>
/// Light dismiss: a press outside the panel or Escape closes it (when
/// <see cref="PopoverOptions.Dismissable"/>), as does the window losing activation. A
/// non-modal popover lets that outside press through to what is underneath. Focus moves to the
/// popover's first focusable control (or its <c>AutoFocus</c> node) and Tab stays inside it;
/// with nothing focusable, focus stays where it was and only Escape is taken.
/// </para>
/// <para>Callable from any thread once the window is running.</para>
/// </remarks>
public static class Popover
{
    /// <summary>Whether a popover is open in the running window.</summary>
    public static bool IsOpen => InputDispatcher.Active?.Overlays?.TopmostPopover() is not null;

    /// <summary>
    /// Shows a new <typeparamref name="TComponent"/> in a popover anchored to
    /// <paramref name="anchor"/>, which must be in the rendered tree (the page, or an open
    /// dialog).
    /// </summary>
    /// <typeparam name="TComponent">The popover content component type.</typeparam>
    /// <param name="anchor">The node to anchor the popover to.</param>
    /// <param name="options">Popover configuration options.</param>
    /// <exception cref="InvalidOperationException">No window is running, or the anchor is not in the rendered tree (when called on the UI thread).</exception>
    public static void Show<TComponent>(
        Node anchor,
        PopoverOptions? options = null)
        where TComponent : Component, new()
    {
        ArgumentNullException.ThrowIfNull(anchor);

        var resolved = options ?? new PopoverOptions();
        OpenCore(() => new TComponent(), anchor, null, resolved);
    }

    /// <summary>
    /// Shows a new <typeparamref name="TComponent"/> in a popover at a window position (logical
    /// pixels), as if anchored to a zero-size node there.
    /// </summary>
    /// <typeparam name="TComponent">The popover content component type.</typeparam>
    /// <param name="position">The window position to anchor the popover to.</param>
    /// <param name="options">Popover configuration options.</param>
    /// <exception cref="InvalidOperationException">No window is running (when called on the UI thread).</exception>
    public static void Show<TComponent>(
        Point position,
        PopoverOptions? options = null)
        where TComponent : Component, new()
    {
        var resolved = options ?? new PopoverOptions();
        OpenCore(() => new TComponent(), null, position, resolved);
    }

    /// <summary>
    /// Shows <paramref name="content"/> in a popover anchored to <paramref name="anchor"/>. Use
    /// this overload to hand the content its inputs and callbacks. Pass a new instance each time.
    /// </summary>
    /// <param name="content">The popover's content component.</param>
    /// <param name="anchor">The node to anchor the popover to.</param>
    /// <param name="options">Popover configuration options.</param>
    /// <exception cref="InvalidOperationException">No window is running, or the anchor is not in the rendered tree (when called on the UI thread).</exception>
    public static void Show(Component content, Node anchor, PopoverOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(anchor);

        var resolved = options ?? new PopoverOptions();
        OpenCore(() => content, anchor, null, resolved);
    }

    /// <summary>
    /// Shows <paramref name="content"/> in a popover at a window position (logical pixels).
    /// Pass a new instance each time.
    /// </summary>
    /// <param name="content">The popover's content component.</param>
    /// <param name="position">The window position to anchor the popover to.</param>
    /// <param name="options">Popover configuration options.</param>
    /// <exception cref="InvalidOperationException">No window is running (when called on the UI thread).</exception>
    public static void Show(Component content, Point position, PopoverOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var resolved = options ?? new PopoverOptions();
        OpenCore(() => content, null, position, resolved);
    }

    /// <summary>Closes the topmost open popover, if any.</summary>
    public static void Close()
    {
        OverlayThreading.RunOnUiThread(() =>
        {
            if (InputDispatcher.Active?.Overlays is { } manager && manager.TopmostPopover() is { } popover)
            {
                manager.Close(popover, null);
            }
        });
    }

    private static void OpenCore(Func<Component> createContent, Node? anchor, Point? position, PopoverOptions options)
    {
        OverlayThreading.RunOnUiThread(() =>
        {
            var manager = OverlayThreading.RequireManager("Popover.Show");
            var content = createContent();
            Dialog.ClaimContent(content);
            manager.Open(new OverlayEntry(manager, OverlayKind.Popover, content, new OverlayResultSource<object>())
            {
                AccessibleLabel = options.AccessibleLabel,
                Role = AccessibleRole.Dialog,
                Dismissable = options.Dismissable,
                ShowBackdrop = options.ShowBackdrop,
                BlocksInput = options.Modal || options.ShowBackdrop,
                Anchor = anchor,
                AnchorPoint = position,
                PreferredSide = options.PreferredSide,
                OffsetX = options.OffsetX,
                OffsetY = options.OffsetY,
                Animation = DialogAnimationKind.Scale,
                EnterModel = PopoverEnter,
                ExitModel = PopoverExit,
            });
        });
    }

    // Popovers are small and close to the pointer: quicker than a dialog.
    private static readonly AnimationModel PopoverEnter = AnimationModel.EaseOut(Duration.Ms(140));
    private static readonly AnimationModel PopoverExit = AnimationModel.EaseIn(Duration.Ms(100));
}
