namespace Cascade.UI;

/// <summary>
/// The window's overlay layer: every open dialog, bottom sheet and popover, bottom to top in
/// the order they opened. One instance per window (owned by <see cref="FrameOrchestrator"/>,
/// reached through <see cref="InputDispatcher.Overlays"/>).
/// </summary>
/// <remarks>
/// <para>
/// Each overlay's content is a real component tree, mounted under an <see cref="OverlayChrome"/>
/// with its own <see cref="ComponentHost"/> on the window's <see cref="RenderScheduler"/>, so
/// state, <c>Invalidate()</c>, focus, key bindings and every control work in it exactly as in
/// the page. The frame lays the page out first and then each overlay (<see cref="Layout"/>),
/// paints them in the same order after the page (backdrop, surface, content, then the
/// content's own popups), and only then the context menu and toasts, which stay on top of
/// everything.
/// </para>
/// <para>
/// Input reaches an overlay through <see cref="InputDispatcher"/>, which runs its normal
/// dispatch with the overlay's tree as the root: a modal overlay (dialogs, sheets, popovers
/// with a backdrop or <see cref="PopoverOptions.Modal"/>) keeps the pointer and the keyboard
/// from everything beneath it, which also confines Tab to it. A non-modal popover dismisses on
/// an outside press and lets that press through.
/// </para>
/// <para>
/// Open/close animate one <see cref="AnimationChannel"/> per overlay, advanced by
/// <see cref="Advance"/> once per frame; <see cref="IsAnimating"/> keeps the frame loop alive
/// only while one is moving. Nothing here runs while the overlays are idle.
/// </para>
/// </remarks>
internal sealed class OverlayManager
{
    /// <summary>Space kept between a dialog and the window edge.</summary>
    internal const float DialogMargin = 16f;

    /// <summary>Space kept between a popover and the window edge.</summary>
    internal const float PopoverMargin = 8f;

    /// <summary>Gap between a popover and its anchor.</summary>
    internal const float PopoverGap = 6f;

    /// <summary>Widest a content-sized bottom sheet gets.</summary>
    internal const float SheetMaxWidth = 640f;

    /// <summary>Room always left above a bottom sheet.</summary>
    internal const float SheetTopGap = 48f;

    /// <summary>How far a centered dialog travels for the slide animations.</summary>
    internal const float SlideDistance = 32f;

    /// <summary>Dragging a sheet down by this fraction of its height (or flinging it) dismisses it.</summary>
    internal const float SheetDismissFraction = 0.3f;

    private readonly List<OverlayEntry> entries = [];
    private readonly RenderScheduler scheduler;
    private readonly InputDispatcher input;

    internal OverlayManager(RenderScheduler scheduler, InputDispatcher input)
    {
        this.scheduler = scheduler;
        this.input = input;
    }

    /// <summary>Every overlay still on screen (closing ones too, until their exit animation ends), bottom first.</summary>
    internal IReadOnlyList<OverlayEntry> Entries => entries;

    /// <summary>Whether anything is on screen.</summary>
    internal bool HasEntries => entries.Count > 0;

    /// <summary>The overlay whose tree the current input event is being dispatched to, if any.</summary>
    internal OverlayEntry? DispatchingEntry { get; set; }

    /// <summary>An open or close animation (or a sheet snapping back) is in flight.</summary>
    internal bool IsAnimating { get; private set; }

    /// <summary>The window size the overlays were last laid out against.</summary>
    internal Size Viewport { get; private set; }

    /// <summary>The topmost overlay that has not been closed, or null.</summary>
    internal OverlayEntry? Topmost
    {
        get
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (!entries[i].IsClosing)
                {
                    return entries[i];
                }
            }

            return null;
        }
    }

    /// <summary>Whether any overlay is open (not closing).</summary>
    internal bool HasLiveEntries => Topmost is not null;

    /// <summary>Whether <paramref name="entry"/> is the topmost open overlay.</summary>
    internal bool IsTopmost(OverlayEntry entry)
    {
        return ReferenceEquals(Topmost, entry);
    }

    /// <summary>
    /// The overlay a position-less <see cref="Dialog.Return{TResult}"/> / <see cref="Dialog.Dismiss"/>
    /// means: the one handling the current input event, else the topmost open one.
    /// </summary>
    internal OverlayEntry? CurrentTarget()
    {
        if (DispatchingEntry is { IsClosing: false } dispatching)
        {
            return dispatching;
        }

        return Topmost;
    }

    /// <summary>The topmost open popover, or null.</summary>
    internal OverlayEntry? TopmostPopover()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] is { IsClosing: false, Kind: OverlayKind.Popover } popover)
            {
                return popover;
            }
        }

        return null;
    }

    /// <summary>
    /// Opens <paramref name="entry"/> above everything else: mounts its content, starts its
    /// enter animation and closes what an overlay replaces (an open context menu, a control's
    /// dropdown, and — for a dialog or sheet — light-dismiss popovers that would be stranded
    /// underneath it). Focus moves into it after its first layout.
    /// </summary>
    /// <exception cref="InvalidOperationException">A popover's anchor node is not in the rendered tree.</exception>
    internal void Open(OverlayEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Anchor is { } anchor && !TryFindBounds(anchor, entries.Count, out _))
        {
            throw new InvalidOperationException(
                $"The anchor {anchor.GetType().Name} is not part of the rendered tree. "
                + "Anchor to a node from the current render (or one inside an open dialog).");
        }

        if (entry.Kind != OverlayKind.Popover)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i] is { IsClosing: false, Kind: OverlayKind.Popover } popover)
                {
                    Close(popover, null);
                }
            }
        }

        input.PrepareForOverlay();

        entry.PreviousFocus = FocusManager.FocusedElement;
        entry.PreviousFocusWasKeyboard = FocusManager.LastFocusWasKeyboard;
        entries.Add(entry);

        entry.Host = new ComponentHost(entry.Chrome, scheduler, treeDepth: 0);
        entry.Host.Mount();
        entry.Host.CompleteMountAsync();
        entry.IsOpened = true;

        entry.Presence.SnapTo(0f);
        var enter = ResolveModel(entry, entering: true);
        if (enter.IsNoneModel)
        {
            entry.Presence.SnapTo(1f);
        }
        else
        {
            entry.Presence.SetTarget(1f, enter);
            IsAnimating = true;
        }

        if (entry.AccessibleLabel is { Length: > 0 } label && entry.Kind != OverlayKind.Popover)
        {
            Accessibility.Announce(label, entry.Role == AccessibleRole.AlertDialog ? AnnouncePriority.High : AnnouncePriority.Normal);
        }

        input.RequestRepaint?.Invoke();
    }

    /// <summary>
    /// Closes <paramref name="entry"/> (and anything opened above it): delivers
    /// <paramref name="result"/> to the awaiting caller, returns focus to where it was when the
    /// overlay opened and starts the exit animation. The overlay stops taking input at once;
    /// it is unmounted when the animation ends. Does nothing for an overlay already closed.
    /// </summary>
    internal void Close(OverlayEntry entry, object? result)
    {
        ArgumentNullException.ThrowIfNull(entry);

        int index = entries.IndexOf(entry);
        if (index < 0 || entry.IsClosing)
        {
            return;
        }

        for (int i = entries.Count - 1; i > index; i--)
        {
            if (!entries[i].IsClosing)
            {
                Close(entries[i], null);
            }
        }

        entry.IsClosing = true;
        entry.IsDragging = false;
        RestoreFocus(entry);

        var exit = ResolveModel(entry, entering: false);
        if (exit.IsNoneModel || !entry.IsLaidOut)
        {
            entry.Presence.SnapTo(0f);
        }
        else
        {
            entry.Presence.SetTarget(0f, exit);
            IsAnimating = true;
        }

        input.RequestRepaint?.Invoke();
        entry.Completion.TryComplete(result);
    }

    /// <summary>Closes every open popover (light dismiss: the window lost activation or was hidden).</summary>
    internal void DismissPopovers()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] is { IsClosing: false, Kind: OverlayKind.Popover } popover)
            {
                Close(popover, null);
            }
        }
    }

    /// <summary>
    /// Advances every overlay's animation by <paramref name="deltaTime"/> seconds and unmounts
    /// overlays whose exit animation has finished. Sets <see cref="IsAnimating"/>.
    /// </summary>
    internal void Advance(float deltaTime)
    {
        bool animating = false;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            entry.Presence.Advance(deltaTime);
            entry.DragOffset.Advance(deltaTime);

            if (entry.IsClosing && !entry.Presence.IsAnimating)
            {
                Remove(i);
                continue;
            }

            if (entry.Presence.IsAnimating || entry.DragOffset.IsAnimating)
            {
                animating = true;
            }
        }

        IsAnimating = animating;
    }

    /// <summary>
    /// Lays out every overlay against the window (after the page, so anchors have their final
    /// positions): measures the panel under its size rules and places it — centered / top /
    /// bottom for dialogs, against the bottom edge for sheets, against the anchor (flipped and
    /// clamped) for popovers and anchored dialogs. A popover whose anchor has left the tree is
    /// dismissed.
    /// </summary>
    internal void Layout(Size viewport, float pixelRatio)
    {
        Viewport = viewport;
        float scale = pixelRatio > 0f ? pixelRatio : 1f;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var tree = entry.Tree;
            if (tree is null)
            {
                continue;
            }

            Rect? anchorRect = null;
            bool anchorLost = false;
            if (entry.Anchor is { } anchor)
            {
                if (TryFindBounds(anchor, i, out var bounds))
                {
                    anchorRect = bounds;
                }
                else
                {
                    anchorLost = true;
                    Close(entry, null);
                }
            }
            else if (entry.AnchorPoint is { } point)
            {
                anchorRect = new Rect(point.X, point.Y, 0f, 0f);
            }

            var size = MeasurePanel(entry, tree, viewport);

            Rect panel;
            if (anchorRect is { } a)
            {
                var side = entry.Kind == OverlayKind.Popover ? entry.PreferredSide : PopoverSide.Auto;
                (panel, var resolved) = PlaceAgainst(size, a, side, entry.OffsetX, entry.OffsetY, viewport, scale);
                entry.ResolvedSide = resolved;
            }
            else if (anchorLost && entry.IsLaidOut)
            {
                panel = new Rect(entry.PanelBounds.X, entry.PanelBounds.Y, size.Width, size.Height);
            }
            else
            {
                var position = entry.Kind == OverlayKind.Sheet ? DialogPositionKind.Bottom : entry.Position;
                panel = PlaceDialog(size, position, viewport, scale);
            }

            tree.LayoutData.Bounds = panel;
            entry.PanelBounds = panel;
            entry.IsLaidOut = true;
        }
    }

    /// <summary>
    /// After layout (and after <see cref="FocusManager.ApplyMountFocus"/>): moves focus into
    /// overlays laid out for the first time — their <c>AutoFocus</c> node if they have one,
    /// else their first tab stop — and keeps focus inside the topmost modal overlay.
    /// </summary>
    internal void ApplyFocus()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.IsClosing || !entry.IsLaidOut || entry.FocusInitialized || entry.Tree is not { } tree)
            {
                continue;
            }

            entry.FocusInitialized = true;
            if (FocusManager.FocusedElement is { } focused && ContainsNode(tree, focused))
            {
                continue;
            }

            MoveFocusInto(entry);
        }

        int modal = TopmostModalIndex();
        if (modal < 0 || !entries[modal].FocusInitialized)
        {
            return;
        }

        if (FocusManager.FocusedElement is { } current && !IsInEntriesFrom(modal, current))
        {
            MoveFocusInto(entries[modal]);
        }
    }

    /// <summary>Whether <paramref name="node"/> is in <paramref name="entry"/>'s tree.</summary>
    internal static bool Contains(OverlayEntry entry, Node node)
    {
        return entry.Tree is { } tree && ContainsNode(tree, node);
    }

    /// <summary>The overlay whose panel tree is rooted at <paramref name="tree"/>, if any.</summary>
    internal OverlayEntry? FindByTree(Node tree)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (ReferenceEquals(entries[i].Tree, tree))
            {
                return entries[i];
            }
        }

        return null;
    }

    /// <summary>
    /// A node was replaced by a re-render: keep anchors and the focus to restore pointing at the
    /// live instance.
    /// </summary>
    internal void NotifyNodeReplaced(Node oldNode, Node newNode)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (ReferenceEquals(entry.Anchor, oldNode))
            {
                entry.Anchor = newNode;
            }

            if (ReferenceEquals(entry.PreviousFocus, oldNode))
            {
                entry.PreviousFocus = newNode;
            }
        }
    }

    /// <summary>Marks the retained layers of every ScrollView inside the overlays for recapture.</summary>
    internal void MarkScrollLayersDirty()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Tree is { } tree)
            {
                FrameOrchestrator.MarkAllScrollLayersDirty(tree);
            }
        }
    }

    /// <summary>Closes everything at once (the window is going away): callers receive null.</summary>
    internal void CloseAll()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (!entries[i].IsClosing)
            {
                entries[i].IsClosing = true;
                entries[i].Completion.TryComplete(null);
            }
        }

        while (entries.Count > 0)
        {
            Remove(entries.Count - 1);
        }
    }

    // ── Geometry ──────────────────────────────────────────────────────

    /// <summary>
    /// Measures a panel. A content-sized one (popovers, <see cref="DialogSize.Auto"/> dialogs)
    /// first measures at its natural width — without a width limit, so a column that stretches
    /// its children does not simply take the whole window — and is then laid out at that width,
    /// capped by the window; every other size is measured once under its constraints.
    /// </summary>
    internal static Size MeasurePanel(OverlayEntry entry, Node tree, Size viewport)
    {
        var constraints = ResolveConstraints(entry, viewport);
        if (constraints.MinWidth >= constraints.MaxWidth)
        {
            return LayoutSolver.Measure(tree, constraints);
        }

        var natural = LayoutSolver.Measure(
            tree,
            new LayoutConstraints(0f, float.PositiveInfinity, constraints.MinHeight, constraints.MaxHeight));
        float width = float.IsFinite(natural.Width) && natural.Width > 0f
            ? Math.Min(MathF.Ceiling(natural.Width), constraints.MaxWidth)
            : constraints.MaxWidth;
        return LayoutSolver.Measure(
            tree,
            new LayoutConstraints(width, width, constraints.MinHeight, constraints.MaxHeight));
    }

    /// <summary>The constraints an overlay's panel is measured under.</summary>
    internal static LayoutConstraints ResolveConstraints(OverlayEntry entry, Size viewport)
    {
        float w = Math.Max(0f, viewport.Width);
        float h = Math.Max(0f, viewport.Height);
        var size = entry.Size;

        if (entry.Kind == OverlayKind.Popover)
        {
            return LayoutConstraints.Loose(new Size(Math.Max(0f, w - (2f * PopoverMargin)), Math.Max(0f, h - (2f * PopoverMargin))));
        }

        if (size.Kind == DialogSizeKind.FullScreen)
        {
            return LayoutConstraints.Tight(new Size(w, h));
        }

        if (entry.Kind == OverlayKind.Sheet)
        {
            float sheetWidth = size.Kind == DialogSizeKind.Fixed && size.Width is { } fixedWidth
                ? Math.Min(fixedWidth, w)
                : Math.Min(SheetMaxWidth, w);
            float maxSheetHeight = Math.Max(0f, h - SheetTopGap);
            if (size.Height is { } sheetHeight)
            {
                float tight = Math.Min(sheetHeight, maxSheetHeight);
                return new LayoutConstraints(sheetWidth, sheetWidth, tight, tight);
            }

            return new LayoutConstraints(sheetWidth, sheetWidth, 0f, maxSheetHeight);
        }

        float maxWidth = Math.Max(0f, w - (2f * DialogMargin));
        float maxHeight = Math.Max(0f, h - (2f * DialogMargin));
        if (size.Kind == DialogSizeKind.Fixed && size.Width is { } width)
        {
            float tightWidth = Math.Min(width, maxWidth);
            if (size.Height is { } height)
            {
                float tightHeight = Math.Min(height, maxHeight);
                return new LayoutConstraints(tightWidth, tightWidth, tightHeight, tightHeight);
            }

            return new LayoutConstraints(tightWidth, tightWidth, 0f, maxHeight);
        }

        return LayoutConstraints.Loose(new Size(maxWidth, maxHeight));
    }

    /// <summary>
    /// Places a dialog panel of <paramref name="panel"/> size: centered, near the top, or flush
    /// with the bottom edge (horizontally centered in every case), snapped to device pixels.
    /// </summary>
    internal static Rect PlaceDialog(Size panel, DialogPositionKind position, Size viewport, float pixelRatio = 1f)
    {
        float x = (viewport.Width - panel.Width) / 2f;
        float y = position switch
        {
            DialogPositionKind.Top => DialogMargin,
            DialogPositionKind.Bottom => viewport.Height - panel.Height,
            _ => (viewport.Height - panel.Height) / 2f,
        };

        var snapped = Snap(new Rect(Math.Max(0f, x), Math.Max(0f, y), panel.Width, panel.Height), pixelRatio);
        if (position == DialogPositionKind.Bottom && y > 0f)
        {
            // Flush with the bottom edge: round down the screen (the panel may hang past the edge by
            // under a pixel), never up, so no sliver of the page shows below it.
            float s = pixelRatio > 0f ? pixelRatio : 1f;
            snapped = snapped with { Y = MathF.Ceiling(y * s) / s };
        }

        return snapped;
    }

    /// <summary>
    /// Places a panel against <paramref name="anchor"/> (window-logical; a zero-size rect for a
    /// point) on <paramref name="preferred"/>, centered on the anchor along the other axis.
    /// <see cref="PopoverSide.Auto"/> means below when the panel fits there, else whichever of
    /// below/above has more room. A side the panel does not fit on flips to the opposite side
    /// when that has more room; a panel too wide for either side of the anchor goes below (or
    /// above) it instead. The offset is applied, then the panel is clamped inside the
    /// window margin and snapped to device pixels. Returns the panel and the side used.
    /// </summary>
    internal static (Rect Bounds, PopoverSide Side) PlaceAgainst(
        Size panel,
        Rect anchor,
        PopoverSide preferred,
        float offsetX,
        float offsetY,
        Size viewport,
        float pixelRatio = 1f)
    {
        float w = panel.Width;
        float h = panel.Height;
        float vw = viewport.Width;
        float vh = viewport.Height;
        float below = vh - PopoverMargin - (anchor.Bottom + PopoverGap);
        float above = anchor.Y - PopoverGap - PopoverMargin;
        float right = vw - PopoverMargin - (anchor.Right + PopoverGap);
        float left = anchor.X - PopoverGap - PopoverMargin;

        var side = preferred;
        if (side == PopoverSide.Auto)
        {
            side = h <= below || below >= above ? PopoverSide.Bottom : PopoverSide.Top;
        }
        else if (side == PopoverSide.Bottom && h > below && above > below)
        {
            side = PopoverSide.Top;
        }
        else if (side == PopoverSide.Top && h > above && below > above)
        {
            side = PopoverSide.Bottom;
        }
        else if (side == PopoverSide.Right && w > right && left > right)
        {
            side = PopoverSide.Left;
        }
        else if (side == PopoverSide.Left && w > left && right > left)
        {
            side = PopoverSide.Right;
        }

        // Too wide for either side of the anchor: below it (or above), where a wide panel fits.
        if ((side == PopoverSide.Right && w > right) || (side == PopoverSide.Left && w > left))
        {
            side = h <= below || below >= above ? PopoverSide.Bottom : PopoverSide.Top;
        }

        float centerX = anchor.X + (anchor.Width / 2f);
        float centerY = anchor.Y + (anchor.Height / 2f);
        (float x, float y) = side switch
        {
            PopoverSide.Top => (centerX - (w / 2f), anchor.Y - PopoverGap - h),
            PopoverSide.Left => (anchor.X - PopoverGap - w, centerY - (h / 2f)),
            PopoverSide.Right => (anchor.Right + PopoverGap, centerY - (h / 2f)),
            _ => (centerX - (w / 2f), anchor.Bottom + PopoverGap),
        };

        x += offsetX;
        y += offsetY;
        if (vw > 0f)
        {
            x = Math.Max(PopoverMargin, Math.Min(x, vw - PopoverMargin - w));
        }

        if (vh > 0f)
        {
            y = Math.Max(PopoverMargin, Math.Min(y, vh - PopoverMargin - h));
        }

        return (Snap(new Rect(x, y, w, h), pixelRatio), side);
    }

    /// <summary>The model an overlay animates in (<paramref name="entering"/>) or out with.</summary>
    internal static AnimationModel ResolveModel(OverlayEntry entry, bool entering)
    {
        if (entry.Animation == DialogAnimationKind.None || ControlStateAnimator.ReducedMotion || ThemeSwitcher.Current.Motion.ReducedMotion)
        {
            return AnimationModel.None;
        }

        var custom = entering ? entry.EnterModel : entry.ExitModel;
        if (custom is not null)
        {
            return custom;
        }

        var dialog = ThemeSwitcher.Current.Dialog;
        return (entering ? dialog.EnterTransition : dialog.ExitTransition).Model;
    }

    // ── Internals ─────────────────────────────────────────────────────

    private static Rect Snap(Rect r, float pixelRatio)
    {
        float s = pixelRatio > 0f ? pixelRatio : 1f;
        return new Rect(MathF.Round(r.X * s) / s, MathF.Round(r.Y * s) / s, r.Width, r.Height);
    }

    private void Remove(int index)
    {
        var entry = entries[index];
        entries.RemoveAt(index);
        if (ReferenceEquals(DispatchingEntry, entry))
        {
            DispatchingEntry = null;
        }

        if (entry.Host is { } host)
        {
            scheduler.RemoveDirty(host);
            host.Unmount();
        }

        entry.Host = null;
    }

    /// <summary>
    /// Finds <paramref name="node"/>'s window bounds in the page or in an overlay below
    /// <paramref name="belowIndex"/> (a popover may be anchored inside a dialog).
    /// </summary>
    private bool TryFindBounds(Node node, int belowIndex, out Rect bounds)
    {
        if (input.MainRoot is { } root && HitTester.TryGetAbsoluteBounds(root, node, out bounds))
        {
            return true;
        }

        for (int i = Math.Min(belowIndex, entries.Count) - 1; i >= 0; i--)
        {
            if (entries[i].Tree is { } tree && HitTester.TryGetAbsoluteBounds(tree, node, out bounds))
            {
                return true;
            }
        }

        bounds = default;
        return false;
    }

    private int TopmostModalIndex()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] is { IsClosing: false, BlocksInput: true })
            {
                return i;
            }
        }

        return -1;
    }

    // In the overlay at `from` or one above it (a popover opened from a dialog sits above it).
    private bool IsInEntriesFrom(int from, Node node)
    {
        for (int i = from; i < entries.Count; i++)
        {
            if (!entries[i].IsClosing && Contains(entries[i], node))
            {
                return true;
            }
        }

        return false;
    }

    private void MoveFocusInto(OverlayEntry entry)
    {
        if (entry.Tree is not { } tree)
        {
            return;
        }

        var stop = InputDispatcher.FirstTabStop(tree);
        if (stop is not null)
        {
            bool keyboard = input.LastInputWasKeyboard;
            FocusManager.RequestFocus(stop);
            FocusManager.LastFocusWasKeyboard = keyboard;
            return;
        }

        if (entry.BlocksInput)
        {
            FocusManager.ClearFocus();
        }
    }

    /// <summary>
    /// Returns focus to where it was when <paramref name="entry"/> opened — when focus is still
    /// inside the closing overlay (or nowhere), and the old target is still in a live tree.
    /// </summary>
    private void RestoreFocus(OverlayEntry entry)
    {
        var focused = FocusManager.FocusedElement;
        if (focused is not null && !Contains(entry, focused))
        {
            return;
        }

        if (entry.PreviousFocus is { } previous && IsLive(previous, entry))
        {
            FocusManager.RequestFocus(previous);
            FocusManager.LastFocusWasKeyboard = entry.PreviousFocusWasKeyboard;
            return;
        }

        if (focused is not null)
        {
            FocusManager.ClearFocus();
        }
    }

    private bool IsLive(Node node, OverlayEntry closing)
    {
        if (input.MainRoot is { } root && HitTester.TryGetAbsoluteBounds(root, node, out _))
        {
            return true;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var other = entries[i];
            if (!ReferenceEquals(other, closing) && !other.IsClosing && Contains(other, node))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsNode(Node root, Node target)
    {
        if (ReferenceEquals(root, target))
        {
            return true;
        }

        foreach (var child in NodeDiffer.GetChildren(root))
        {
            if (ContainsNode(child, target))
            {
                return true;
            }
        }

        return false;
    }
}
