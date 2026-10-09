namespace Cascade.UI;

/// <summary>
/// Dialogs, bottom sheets and popovers (<see cref="OverlayManager"/>): routing input to them.
/// </summary>
/// <remarks>
/// <para>
/// An overlay's content is an ordinary node tree, so input reaches it through the ordinary
/// dispatch (<see cref="DispatchMouse"/>, <see cref="DispatchScroll"/>,
/// <see cref="DispatchKey"/>) with <see cref="rootNode"/> temporarily set to the overlay's
/// tree. Every control, hit test, key binding and Tab traversal then works inside it as it does
/// in the page — and cannot see the page: that is what makes a modal overlay modal and keeps
/// Tab inside it.
/// </para>
/// <para>
/// Pointer: overlays are tested top-down. Inside a panel, the event goes to that overlay (and a
/// press keeps the pointer there until its release). Outside a modal overlay, nothing beneath
/// reacts; a press dismisses it when it is dismissable (and is swallowed, release included).
/// Outside a non-modal popover, a press dismisses it and carries on to whatever is beneath.
/// Keyboard: the topmost modal overlay, or the overlay holding focus, gets every key; Escape
/// dismisses a popover that does not hold focus. The open context menu still comes first,
/// and toasts stay clickable above everything.
/// </para>
/// </remarks>
internal sealed partial class InputDispatcher
{
    // The page's tree, as set by SetRoot (rootNode is swapped while an overlay is dispatched to).
    private Node? mainRoot;

    // The overlay a press began in: it keeps the pointer until the release.
    private OverlayEntry? overlayPointerCapture;

    // The bottom sheet being dragged by its handle.
    private OverlayEntry? draggingSheet;

    /// <summary>The window's overlay layer; null for a dispatcher created without a window (tests).</summary>
    internal OverlayManager? Overlays { get; set; }

    /// <summary>The page's laid-out tree (never an overlay's).</summary>
    internal Node? MainRoot => mainRoot;

    /// <summary>The first keyboard tab stop under <paramref name="root"/> in document order, or null.</summary>
    internal static Node? FirstTabStop(Node root)
    {
        var stops = new List<Node>();
        CollectTabStops(root, stops);
        return stops.Count > 0 ? stops[0] : null;
    }

    /// <summary>
    /// An overlay is opening: close the context menu and any control dropdown, and forget the
    /// press and hover in progress, so nothing beneath stays pressed or lit under the backdrop.
    /// </summary>
    internal void PrepareForOverlay()
    {
        CloseMenu();
        DismissControlPopups();
        ResetPressState();
        overlayPointerCapture = null;
        if (hoveredNode is not null)
        {
            hoveredNode.IsHovered = false;
            InvokePointerLeave(hoveredNode);
            hoveredNode = null;
        }
    }

    /// <summary>
    /// The window lost activation or was hidden: the context menu and popovers close (as native
    /// light-dismiss UI does); dialogs and sheets stay. A sheet drag in progress is abandoned.
    /// </summary>
    internal void HandleWindowDeactivation()
    {
        HandleWindowDeactivated();

        if (draggingSheet is { } sheet)
        {
            draggingSheet = null;
            sheet.IsDragging = false;
            sheet.DragOffset.SnapTo(0f);
        }

        overlayPointerCapture = null;
        Overlays?.DismissPopovers();
    }

    // ── Pointer ───────────────────────────────────────────────────────

    /// <summary>Routes a pointer event to an overlay. Returns true when an overlay took (or swallowed) it.</summary>
    private bool RouteOverlayMouse(NativeMouseEvent evt)
    {
        var overlays = Overlays;
        if (overlays is null || !overlays.HasEntries)
        {
            overlayPointerCapture = null;
            draggingSheet = null;
            return false;
        }

        var point = new Point(evt.X, evt.Y);

        if (draggingSheet is { } sheet)
        {
            HandleSheetDrag(overlays, sheet, evt, point);
            return true;
        }

        if (overlayPointerCapture is { } captured)
        {
            if (captured.IsClosing || captured.Tree is null)
            {
                overlayPointerCapture = null;
            }
            else
            {
                if (evt.Type == NativeMouseEventType.MouseUp)
                {
                    overlayPointerCapture = null;
                }

                DispatchMouseIn(overlays, captured, evt);
                return true;
            }
        }

        if (overlays.Topmost is not { } top)
        {
            return false;
        }

        if (evt.Type == NativeMouseEventType.MouseDown && IsOverToast(point))
        {
            return false;
        }

        // A control's dropdown (a Select's list, a date picker's calendar) belongs to the top
        // overlay and may reach past its panel.
        if (HasOpenControlPopup() && top.Tree is not null)
        {
            if (evt.Type == NativeMouseEventType.MouseDown)
            {
                overlayPointerCapture = top;
            }

            DispatchMouseIn(overlays, top, evt);
            return true;
        }

        var entries = overlays.Entries;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry.IsClosing || entry.Tree is null)
            {
                continue;
            }

            if (!entry.IsLaidOut)
            {
                // Opened this frame and not laid out yet: a modal one already owns the pointer.
                if (entry.BlocksInput)
                {
                    return true;
                }

                continue;
            }

            if (entry.PanelBounds.Contains(point))
            {
                if (evt.Type == NativeMouseEventType.MouseDown)
                {
                    if (IsSheetHandlePress(entry, evt, point))
                    {
                        BeginSheetDrag(entry, point);
                        return true;
                    }

                    overlayPointerCapture = entry;
                }

                DispatchMouseIn(overlays, entry, evt);
                return true;
            }

            if (evt.Type == NativeMouseEventType.MouseDown && entry.Dismissable)
            {
                overlays.Close(entry, null);
                if (entry.BlocksInput)
                {
                    swallowNextMouseUp = true;
                    ResetPressState();
                    return true;
                }

                // A non-modal popover lets the press through to what is beneath it.
                continue;
            }

            if (entry.BlocksInput)
            {
                if (evt.Type is NativeMouseEventType.MouseMove or NativeMouseEventType.MouseEnter or NativeMouseEventType.MouseLeave)
                {
                    // Hover still has to leave the overlay's own controls.
                    DispatchMouseIn(overlays, entry, evt);
                }
                else if (evt.Type == NativeMouseEventType.MouseDown)
                {
                    swallowNextMouseUp = true;
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>Routes a wheel event to an overlay. Returns true when an overlay took (or swallowed) it.</summary>
    private bool RouteOverlayScroll(NativeScrollEvent evt)
    {
        var overlays = Overlays;
        if (overlays?.Topmost is not { } top)
        {
            return false;
        }

        if (HasOpenControlPopup() && top.Tree is not null)
        {
            DispatchScrollIn(overlays, top, evt);
            return true;
        }

        var point = new Point(evt.X, evt.Y);
        var entries = overlays.Entries;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry.IsClosing || entry.Tree is null)
            {
                continue;
            }

            if (entry.IsLaidOut && entry.PanelBounds.Contains(point))
            {
                DispatchScrollIn(overlays, entry, evt);
                return true;
            }

            if (entry.BlocksInput)
            {
                return true;
            }
        }

        return false;
    }

    private void DispatchMouseIn(OverlayManager overlays, OverlayEntry entry, NativeMouseEvent evt)
    {
        var tree = entry.Tree;
        if (tree is null)
        {
            return;
        }

        var savedRoot = rootNode;
        var savedEntry = overlays.DispatchingEntry;
        rootNode = tree;
        overlays.DispatchingEntry = entry;
        try
        {
            DispatchMouse(evt);
        }
        finally
        {
            rootNode = savedRoot;
            overlays.DispatchingEntry = savedEntry;
        }
    }

    private void DispatchScrollIn(OverlayManager overlays, OverlayEntry entry, NativeScrollEvent evt)
    {
        var tree = entry.Tree;
        if (tree is null)
        {
            return;
        }

        var savedRoot = rootNode;
        var savedEntry = overlays.DispatchingEntry;
        rootNode = tree;
        overlays.DispatchingEntry = entry;
        try
        {
            DispatchScroll(evt);
        }
        finally
        {
            rootNode = savedRoot;
            overlays.DispatchingEntry = savedEntry;
        }
    }

    // ── Bottom sheet drag ─────────────────────────────────────────────

    private static bool IsSheetHandlePress(OverlayEntry entry, NativeMouseEvent evt, Point point)
    {
        return entry is { Kind: OverlayKind.Sheet, Dismissable: true }
            && evt.Button == NativeMouseButton.Left
            && point.Y < entry.PanelBounds.Y + OverlayChrome.SheetHandleArea;
    }

    private void BeginSheetDrag(OverlayEntry entry, Point point)
    {
        ResetPressState();
        draggingSheet = entry;
        entry.IsDragging = true;
        // Grabbing a sheet that is still snapping back continues from where it is.
        entry.DragStartY = point.Y - Math.Max(0f, entry.DragOffset.Current);
        entry.DragOffset.SnapTo(Math.Max(0f, entry.DragOffset.Current));
    }

    private void HandleSheetDrag(OverlayManager overlays, OverlayEntry entry, NativeMouseEvent evt, Point point)
    {
        if (entry.IsClosing)
        {
            draggingSheet = null;
            entry.IsDragging = false;
            return;
        }

        switch (evt.Type)
        {
            case NativeMouseEventType.MouseMove:
            {
                // Only downwards: a sheet is not pulled above its resting place.
                float offset = Math.Max(0f, point.Y - entry.DragStartY);
                if (offset != entry.DragOffset.Current)
                {
                    entry.DragOffset.SnapTo(offset);
                    RequestRepaint?.Invoke();
                }

                return;
            }

            case NativeMouseEventType.MouseUp:
            {
                draggingSheet = null;
                entry.IsDragging = false;
                if (entry.DragOffset.Current > entry.PanelBounds.Height * OverlayManager.SheetDismissFraction)
                {
                    overlays.Close(entry, null);
                }
                else
                {
                    entry.DragOffset.SetTarget(0f, AnimationModel.Spring.Snappy);
                }

                RequestRepaint?.Invoke();
                return;
            }
        }
    }

    // ── Keyboard ──────────────────────────────────────────────────────

    /// <summary>
    /// Routes a key-down (or typed character) to the overlay that owns the keyboard: the topmost
    /// modal one, or one holding focus above it. Escape dismisses a non-modal popover that does
    /// not hold focus. Returns true when an overlay took the key.
    /// </summary>
    private bool RouteOverlayKey(NativeKeyEvent evt)
    {
        var overlays = Overlays;
        if (overlays is null || !overlays.HasLiveEntries)
        {
            return false;
        }

        var entries = overlays.Entries;
        var focused = FocusManager.FocusedElement;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry.IsClosing || entry.Tree is null)
            {
                continue;
            }

            if (entry.BlocksInput || (focused is not null && OverlayManager.Contains(entry, focused)))
            {
                DispatchKeyIn(overlays, entry, evt);
                return true;
            }

            if (evt.Key == Key.Escape && entry.Dismissable)
            {
                overlays.Close(entry, null);
                suppressNextCharacter = true;
                RequestRepaint?.Invoke();
                return true;
            }
        }

        return false;
    }

    /// <summary>Key-ups reach the same overlay key-downs do.</summary>
    private void RouteKeyUp(NativeKeyEvent evt)
    {
        var overlays = Overlays;
        if (overlays is not null && overlays.HasLiveEntries)
        {
            var entries = overlays.Entries;
            var focused = FocusManager.FocusedElement;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry.IsClosing || entry.Tree is not { } tree)
                {
                    continue;
                }

                if (entry.BlocksInput || (focused is not null && OverlayManager.Contains(entry, focused)))
                {
                    var savedRoot = rootNode;
                    rootNode = tree;
                    try
                    {
                        DispatchKeyUp(evt);
                    }
                    finally
                    {
                        rootNode = savedRoot;
                    }

                    return;
                }
            }
        }

        DispatchKeyUp(evt);
    }

    private void DispatchKeyIn(OverlayManager overlays, OverlayEntry entry, NativeKeyEvent evt)
    {
        var tree = entry.Tree;
        if (tree is null)
        {
            return;
        }

        var savedRoot = rootNode;
        var savedEntry = overlays.DispatchingEntry;
        rootNode = tree;
        overlays.DispatchingEntry = entry;
        try
        {
            DispatchKey(evt);
        }
        finally
        {
            rootNode = savedRoot;
            overlays.DispatchingEntry = savedEntry;
        }
    }

    /// <summary>
    /// Escape that nothing inside the overlay handled (no open dropdown, no app binding):
    /// dismisses the overlay being dispatched to when it is dismissable. Either way the key is
    /// consumed, so Escape never clears focus out of an open overlay.
    /// </summary>
    private bool HandleOverlayEscape()
    {
        var overlays = Overlays;
        if (overlays?.DispatchingEntry is not { IsClosing: false } entry)
        {
            return false;
        }

        if (entry.Dismissable)
        {
            overlays.Close(entry, null);
        }

        suppressNextCharacter = true;
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private bool HasOpenControlPopup()
    {
        return openSelect is not null
            || openMultiSelect is not null
            || openCombobox is not null
            || openDatePicker is not null
            || openDateTimePicker is not null
            || openDateRangePicker is not null
            || openTimePicker is not null
            || openMonthPicker is not null
            || openMenuBar is not null
            || openNotificationBell is not null
            || openGridOverlay is { IsSelectDropdownOpen: true } or { IsDatePopupOpen: true } or { IsColumnChooserOpen: true };
    }

    private static bool IsOverToast(Point point)
    {
        var zones = Toast.HitZones;
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i].Bounds.Contains(point))
            {
                return true;
            }
        }

        return false;
    }
}
