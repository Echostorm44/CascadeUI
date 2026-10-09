namespace Cascade.UI;

/// <summary>
/// DataGrid cell-level keyboard: the current cell moves with the arrows, Home/End, Ctrl+Home/End
/// and Page Up/Down in the order the grid is painted (sorted, filtered, grouped — rows through
/// <see cref="TabularNavigation"/>, columns through <see cref="TabularCellNavigation"/>); Shift
/// extends the selection; Tab walks the editable cells; Enter/F2/typing edit; Delete clears;
/// Ctrl+A, Shift+Space and Ctrl+Space select. Keys arrive here only while the grid has focus and is
/// not editing, filtering or showing its select/date popup.
/// </summary>
internal sealed partial class InputDispatcher
{
    /// <summary>
    /// A key for a grid that is not editing. Returns false for keys it does not handle (the table's
    /// row handling and the rest of the dispatcher then see them) — including Tab past the last
    /// editable cell, so focus moves on, and Right past the last column, so the row's inline
    /// actions take over.
    /// </summary>
    private bool HandleGridCellKey(ITabularDataNode tdn, ITabularCellGrid cells, NativeKeyEvent evt)
    {
        var mods = evt.Modifiers;
        bool ctrl = mods.HasFlag(ModifierKeys.Ctrl);
        bool shift = mods.HasFlag(ModifierKeys.Shift);
        bool alt = mods.HasFlag(ModifierKeys.Alt) || mods.HasFlag(ModifierKeys.Meta);
        if (alt)
        {
            return false;
        }

        if (evt.Key == Key.None)
        {
            return evt.Character is { } ch && !ctrl && HandleGridTyping(tdn, cells, ch);
        }

        if (ctrl && !shift && evt.Key == Key.A)
        {
            cells.SelectAllCells();
            RequestRepaint?.Invoke();
            return true;
        }

        if (!cells.CellNavigationEnabled)
        {
            return HandleGridRowRangeKey(tdn, evt.Key, ctrl, shift);
        }

        int row = tdn.SelectedRowIndex;
        int col = TabularCellNavigation.Clamp(tdn, cells.CurrentColumn);
        var kind = shift ? CellSelectKind.Extend : CellSelectKind.Replace;

        switch (evt.Key)
        {
            case Key.Up:
            case Key.Down:
            {
                int delta = evt.Key == Key.Up ? -1 : 1;
                int target = ctrl
                    ? (delta < 0 ? TabularNavigation.First(tdn) : TabularNavigation.Last(tdn))
                    : TabularNavigation.Step(tdn, row, delta);
                return MoveCurrentCell(tdn, cells, target, col, kind);
            }

            case Key.PageUp:
            case Key.PageDown:
            {
                int page = TabularNavigation.PageSize(tdn);
                int target = TabularNavigation.Step(tdn, row, evt.Key == Key.PageUp ? -page : page);
                return MoveCurrentCell(tdn, cells, target, col, kind);
            }

            case Key.Left:
            case Key.Right:
            {
                int direction = evt.Key == Key.Left ? -1 : 1;
                if (row < 0)
                {
                    return MoveCurrentCell(tdn, cells, TabularNavigation.Step(tdn, -1, 1), col, CellSelectKind.Replace);
                }

                int target = ctrl
                    ? (direction < 0 ? TabularCellNavigation.FirstColumn(tdn) : TabularCellNavigation.LastColumn(tdn))
                    : TabularCellNavigation.Next(tdn, col, direction);
                if (target < 0)
                {
                    // Past the last column, Right belongs to the row's inline actions; Left at the
                    // first column stays put.
                    return direction < 0 || shift || tdn.RowActionStrip is null;
                }

                return MoveCurrentCell(tdn, cells, row, target, kind);
            }

            case Key.Home:
            case Key.End:
            {
                bool end = evt.Key == Key.End;
                int targetCol = end ? TabularCellNavigation.LastColumn(tdn) : TabularCellNavigation.FirstColumn(tdn);
                int targetRow = ctrl
                    ? (end ? TabularNavigation.Last(tdn) : TabularNavigation.First(tdn))
                    : (row >= 0 ? row : TabularNavigation.First(tdn));
                return MoveCurrentCell(tdn, cells, targetRow, targetCol, kind);
            }

            case Key.Tab:
            {
                if (ctrl || !TabularCellNavigation.TryNextEditableCell(tdn, row, col, shift ? -1 : 1, out int nextRow, out int nextCol))
                {
                    // Past the last (or before the first) editable cell, Tab leaves the grid.
                    return false;
                }

                return MoveCurrentCell(tdn, cells, nextRow, nextCol, CellSelectKind.Replace);
            }

            case Key.Enter:
            case Key.NumPadEnter:
            {
                if (ctrl || row < 0)
                {
                    return false;
                }

                if (shift)
                {
                    return MoveCurrentCell(tdn, cells, TabularNavigation.Step(tdn, row, -1), col, CellSelectKind.Replace);
                }

                if (TabularCellNavigation.IsEditableCell(tdn, col))
                {
                    ActivateGridCell(tdn, row, col);
                    return true;
                }

                // A read-only cell: Enter moves down, as in a spreadsheet.
                return MoveCurrentCell(tdn, cells, TabularNavigation.Step(tdn, row, 1), col, CellSelectKind.Replace);
            }

            case Key.F2:
                if (row >= 0 && TabularCellNavigation.IsEditableCell(tdn, col) && !tdn.IsBoolColumn(col))
                {
                    BeginGridCellEdit(tdn, row, col);
                }

                return true;

            case Key.Space:
                if (shift && !ctrl)
                {
                    cells.SelectEntireRows();
                    RequestRepaint?.Invoke();
                    return true;
                }

                if (ctrl && !shift)
                {
                    if (cells.SelectEntireColumns())
                    {
                        RequestRepaint?.Invoke();
                    }

                    return true;
                }

                if (row >= 0 && tdn.IsBoolColumn(col) && TabularCellNavigation.IsEditableCell(tdn, col))
                {
                    ToggleGridBool(tdn, row, col);
                    return true;
                }

                // Space on a text cell types a space (the character event starts the edit).
                return false;

            case Key.Delete:
                if (!ctrl && !shift && cells.ClearSelectedCells())
                {
                    RequestRepaint?.Invoke();
                }

                return !ctrl && !shift;

            case Key.Backspace:
                if (!ctrl && !shift && row >= 0 && cells.BeginEditWithText(row, col, ""))
                {
                    RequestRepaint?.Invoke();
                    return true;
                }

                return false;

            default:
                return false;
        }
    }

    /// <summary>A grid with cell navigation off (<see cref="CellSelectionMode.RowOnly"/>): Shift extends the row selection.</summary>
    private bool HandleGridRowRangeKey(ITabularDataNode tdn, Key key, bool ctrl, bool shift)
    {
        if (!shift || ctrl || tdn is not ITabularCellGrid cells)
        {
            return false;
        }

        int row = tdn.SelectedRowIndex;
        int target = key switch
        {
            Key.Up => TabularNavigation.Step(tdn, row, -1),
            Key.Down => TabularNavigation.Step(tdn, row, 1),
            Key.PageUp => TabularNavigation.Step(tdn, row, -TabularNavigation.PageSize(tdn)),
            Key.PageDown => TabularNavigation.Step(tdn, row, TabularNavigation.PageSize(tdn)),
            Key.Home => TabularNavigation.First(tdn),
            Key.End => TabularNavigation.Last(tdn),
            _ => -2,
        };
        if (target == -2)
        {
            return false;
        }

        return MoveCurrentCell(tdn, cells, target, -1, CellSelectKind.Extend);
    }

    /// <summary>
    /// Moves the current cell (and the selection) to (<paramref name="row"/>, <paramref name="col"/>),
    /// scrolls the row into view and repaints. A move that changes nothing does nothing — and does
    /// not fire <c>OnSelect</c> again. Always consumes the key (a key that cannot move still
    /// belongs to the grid).
    /// </summary>
    private bool MoveCurrentCell(ITabularDataNode tdn, ITabularCellGrid cells, int row, int col, CellSelectKind kind)
    {
        if (row < 0)
        {
            return true;
        }

        bool sameCell = row == tdn.SelectedRowIndex && col == cells.CurrentColumn;
        bool singleSelection = tdn.SelectedRowCount <= 1 && !(cells.HasCellBlocks && cells.AnchorColumn != cells.CurrentColumn);
        if (sameCell && kind == CellSelectKind.Replace && singleSelection)
        {
            return true;
        }

        cells.SelectCell(row, col, kind);
        AfterTabularNavigation(tdn);
        return true;
    }

    /// <summary>Enter on an editable cell: toggles a boolean, otherwise starts editing (or opens the cell's dropdown/calendar).</summary>
    private void ActivateGridCell(ITabularDataNode tdn, int row, int col)
    {
        if (tdn.IsBoolColumn(col))
        {
            ToggleGridBool(tdn, row, col);
            return;
        }

        BeginGridCellEdit(tdn, row, col);
    }

    private void ToggleGridBool(ITabularDataNode tdn, int row, int col)
    {
        tdn.ToggleBool(row, col);
        RequestRepaint?.Invoke();
    }

    /// <summary>Starts editing a cell (a select or date column opens its popup instead), scrolled into view.</summary>
    private void BeginGridCellEdit(ITabularDataNode tdn, int row, int col)
    {
        if (openGridOverlay is not null && !ReferenceEquals(openGridOverlay, tdn))
        {
            openGridOverlay.CloseOverlay();
            openGridOverlay = null;
        }

        tdn.ScrollIntoView(row);
        if (!tdn.BeginEdit(row, col))
        {
            return;
        }

        if (tdn.IsSelectDropdownOpen || tdn.IsDatePopupOpen)
        {
            openGridOverlay = tdn;
        }

        RequestRepaint?.Invoke();
    }

    /// <summary>A printable character on a cell that is not being edited: start editing with it.</summary>
    private bool HandleGridTyping(ITabularDataNode tdn, ITabularCellGrid cells, char ch)
    {
        if (char.IsControl(ch) || !cells.CellNavigationEnabled || tdn.EditModeValue == GridEditMode.AlwaysEditing)
        {
            return false;
        }

        int row = tdn.SelectedRowIndex;
        int col = cells.CurrentColumn;
        if (row < 0 || !TabularCellNavigation.IsEditableCell(tdn, col))
        {
            return false;
        }

        tdn.ScrollIntoView(row);
        if (!cells.BeginEditWithText(row, col, ch.ToString()))
        {
            return false;
        }

        RequestRepaint?.Invoke();
        return true;
    }

    /// <summary>
    /// Keys that end an edit and move on, in a grid with cell navigation: Enter / Shift+Enter
    /// commit and move down / up, Up / Down commit and move, Tab / Shift+Tab commit and edit the
    /// next / previous editable cell (past the last one the edit is committed and focus leaves the
    /// grid). Returns null for keys that are not one of these.
    /// </summary>
    private bool? HandleGridEditNavigationKey(ITabularDataNode tdn, ITabularCellGrid cells, NativeKeyEvent evt)
    {
        if (!cells.CellNavigationEnabled)
        {
            return null;
        }

        bool shift = evt.Modifiers.HasFlag(ModifierKeys.Shift);
        bool ctrl = evt.Modifiers.HasFlag(ModifierKeys.Ctrl);
        int row = tdn.EditingRow;
        int col = tdn.EditingCol;

        switch (evt.Key)
        {
            case Key.Enter:
            case Key.NumPadEnter:
            case Key.Up:
            case Key.Down:
            {
                if (ctrl)
                {
                    return null;
                }

                bool up = evt.Key == Key.Up || (shift && evt.Key is Key.Enter or Key.NumPadEnter);
                tdn.CommitEdit();
                MoveCurrentCell(tdn, cells, TabularNavigation.Step(tdn, row, up ? -1 : 1), col, CellSelectKind.Replace);
                RequestRepaint?.Invoke();
                return true;
            }

            case Key.Tab:
            {
                if (ctrl)
                {
                    return null;
                }

                tdn.CommitEdit();
                if (!TabularCellNavigation.TryNextEditableCell(tdn, row, col, shift ? -1 : 1, out int nextRow, out int nextCol))
                {
                    RequestRepaint?.Invoke();
                    return false;
                }

                MoveCurrentCell(tdn, cells, nextRow, nextCol, CellSelectKind.Replace);
                if (!tdn.IsBoolColumn(nextCol))
                {
                    BeginGridCellEdit(tdn, nextRow, nextCol);
                }

                RequestRepaint?.Invoke();
                return true;
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// A click on a grid cell: the clicked column becomes the current column. In a range mode the
    /// cell is selected through the cell blocks (Shift extends, Ctrl adds in MultiRange); otherwise
    /// the row selection works as before. Returns false when the grid has no cell navigation.
    /// </summary>
    private static bool SelectClickedGridCell(ITabularDataNode tdn, int row, int col, bool ctrl, bool shift)
    {
        if (tdn is not ITabularCellGrid { CellNavigationEnabled: true } cells || col < 0)
        {
            return false;
        }

        if (cells.RangeSelectionEnabled)
        {
            var kind = (ctrl, shift) switch
            {
                (true, true) => CellSelectKind.AddExtend,
                (true, false) => CellSelectKind.Add,
                (false, true) => CellSelectKind.Extend,
                _ => CellSelectKind.Replace,
            };
            int previousRow = tdn.SelectedRowIndex;
            cells.SelectCell(row, col, kind);
            if (previousRow == row && kind == CellSelectKind.Replace)
            {
                // A click always reports the row, as a row click did before.
                tdn.SelectRow(row, false, false);
            }

            return true;
        }

        tdn.SelectRow(row, ctrl, shift);
        if (!ctrl || shift)
        {
            cells.SetCurrentColumn(col);
        }

        return true;
    }
}
