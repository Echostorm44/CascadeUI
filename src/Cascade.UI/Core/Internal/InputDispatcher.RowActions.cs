namespace Cascade.UI;

/// <summary>
/// Inline row actions of <see cref="DataTable{T}"/> and <see cref="DataGrid{T}"/>
/// (<c>RowActions(row => [...])</c>): the action buttons are painted inside the table, which is a
/// single hit-test node, so the dispatcher finds the action under the pointer itself (through
/// <see cref="TabularRowGeometry.TryHitRowAction"/>) and drives the action nodes' hover and press
/// state, their activation, and keyboard focus within the selected row.
/// </summary>
internal sealed partial class InputDispatcher
{
    /// <summary>
    /// A left click on a row action: commits any cell edit, closes the grid's own popups and runs
    /// the action's control. The selection is left alone — an action on another row (often
    /// "delete this") must not first select that row and fire <c>OnSelect</c> for it.
    /// </summary>
    private bool TryClickRowAction(ITabularDataNode tdn, float relX, float relY, float tableHeight)
    {
        if (!TabularRowGeometry.TryHitRowAction(tdn, relX, relY, tableHeight, out _, out var target))
        {
            return false;
        }

        PrepareForRowAction(tdn);
        InvokeTap(target);
        RequestRepaint?.Invoke();
        return true;
    }

    private void PrepareForRowAction(ITabularDataNode tdn)
    {
        if (tdn.IsEditing)
        {
            tdn.CommitEdit();
        }

        if (openGridOverlay != null)
        {
            openGridOverlay.CloseOverlay();
            openGridOverlay = null;
        }
    }

    /// <summary>Marks the action control under a left press as pressed (cleared on release).</summary>
    private static void PressRowAction(ITabularDataNode tdn, float x, float y)
    {
        var bounds = tdn.AbsoluteBounds;
        if (tdn.RowActionStrip is not { } actions
            || !TabularRowGeometry.TryHitRowAction(tdn, x - bounds.X, y - bounds.Y, bounds.Height, out _, out var target))
        {
            return;
        }

        target.IsPressed = true;
        actions.PressedTarget = target;
    }

    private static void ReleaseRowAction(Node? previousPressed)
    {
        if (previousPressed is not ITabularDataNode { RowActionStrip: { PressedTarget: { } pressed } actions })
        {
            return;
        }

        pressed.IsPressed = false;
        actions.PressedTarget = null;
    }

    /// <summary>
    /// Moves the hovered state between action controls as the pointer moves over a table, and
    /// clears it when the pointer leaves the strip (or the table: <paramref name="hitNode"/> is
    /// then something else and <paramref name="previous"/> the table).
    /// </summary>
    private void UpdateRowActionHover(Node? hitNode, Node? previous, float x, float y)
    {
        if (previous is ITabularDataNode { RowActionStrip: { } previousActions } && !ReferenceEquals(previous, hitNode))
        {
            SetHoveredRowAction(previousActions, null);
        }

        if (hitNode is not ITabularDataNode { RowActionStrip: { } actions } tdn)
        {
            return;
        }

        var bounds = tdn.AbsoluteBounds;
        Node? target = TabularRowGeometry.TryHitRowAction(tdn, x - bounds.X, y - bounds.Y, bounds.Height, out _, out var hit)
            ? hit
            : null;
        SetHoveredRowAction(actions, target);
    }

    /// <summary>
    /// Re-reads the hovered row, cell and row action after the table scrolled under a still
    /// pointer. Without it the highlight (and a hovered button with its tooltip) stayed on the
    /// row that had scrolled away until the mouse moved.
    /// </summary>
    private void RefreshTabularHover(ITabularDataNode tdn, float x, float y)
    {
        if (tdn.IsHoverHighlightEnabled)
        {
            int row = HitTestTabularRow(tdn, x, y);
            float relX = x - tdn.AbsoluteBounds.X;
            float dataRelX = tdn.HasRowDetail ? relX - ExpandIndicatorWidth : relX;
            tdn.HoveredRowIndex = row;
            tdn.HoveredColIndex = row >= 0 ? HitTestTabularColumn(tdn, dataRelX, tdn.AbsoluteBounds.Width) : -1;
        }

        if (tdn is Node node)
        {
            UpdateRowActionHover(node, node, x, y);
        }
    }

    private void SetHoveredRowAction(TabularRowActions actions, Node? target)
    {
        if (ReferenceEquals(actions.HoveredTarget, target))
        {
            return;
        }

        if (actions.HoveredTarget is { } old)
        {
            old.IsHovered = false;
        }

        if (target is not null)
        {
            target.IsHovered = true;
        }

        actions.HoveredTarget = target;
        RequestRepaint?.Invoke();
    }

    /// <summary>
    /// Keyboard access to the selected row's actions: Right/Left move focus through them (Left from
    /// the first returns to the row), Enter/Space run the focused one, Escape returns to the row.
    /// Returns false for any other key, or when there is nothing to act on, so the table's own
    /// handling continues.
    /// </summary>
    private bool HandleRowActionKey(ITabularDataNode tdn, NativeKeyEvent evt)
    {
        if (tdn.RowActionStrip is not { } actions || evt.Modifiers != ModifierKeys.None)
        {
            return false;
        }

        int row = tdn.SelectedRowIndex;
        if (row < 0)
        {
            return false;
        }

        var nodes = actions.ForDisplayRow(row);
        if (nodes.Count == 0)
        {
            actions.FocusedIndex = -1;
            return false;
        }

        switch (evt.Key)
        {
            case Key.Right:
                if (actions.FocusedIndex < nodes.Count - 1)
                {
                    actions.FocusedIndex++;
                    tdn.ScrollIntoView(row);
                    RequestRepaint?.Invoke();
                }

                return true;

            case Key.Left:
                if (actions.FocusedIndex < 0)
                {
                    return false;
                }

                actions.FocusedIndex--;
                RequestRepaint?.Invoke();
                return true;

            case Key.Escape:
                if (actions.FocusedIndex < 0)
                {
                    return false;
                }

                actions.FocusedIndex = -1;
                RequestRepaint?.Invoke();
                return true;

            case Key.Enter:
            case Key.Space:
            {
                if (actions.FocusedIndex < 0 || actions.FocusedIndex >= nodes.Count)
                {
                    return false;
                }

                if (TabularRowActions.FindActivatable(nodes[actions.FocusedIndex]) is not { } target)
                {
                    return true;
                }

                PrepareForRowAction(tdn);
                InvokeTap(target);
                RequestRepaint?.Invoke();
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>
    /// After the selection moved, keeps the focused action inside the new row's actions (a row
    /// with fewer actions pulls it back to its last one; a row with none returns focus to the row).
    /// </summary>
    private static void ClampRowActionFocus(ITabularDataNode tdn)
    {
        if (tdn.RowActionStrip is not { FocusedIndex: >= 0 } actions)
        {
            return;
        }

        int count = tdn.SelectedRowIndex >= 0 ? actions.ForDisplayRow(tdn.SelectedRowIndex).Count : 0;
        actions.FocusedIndex = Math.Min(actions.FocusedIndex, count - 1);
    }
}
