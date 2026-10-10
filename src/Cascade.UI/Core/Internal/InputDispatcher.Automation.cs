namespace Cascade.UI;

/// <summary>
/// Actions requested by assistive technology (UI Automation patterns): the same effect as the
/// pointer or keyboard gesture they stand for, through the same code paths, so a screen reader
/// user's "invoke", "toggle" or "select" behaves exactly like a click or a key press.
/// All of these run on the UI thread.
/// </summary>
internal sealed partial class InputDispatcher
{
    /// <summary>Invokes (or toggles, expands, selects…) <paramref name="node"/> as a tap on it would.</summary>
    internal void AutomationInvoke(Node node)
    {
        current = this;
        InvokeTap(node);
        if (rootNode is not null && IsInteractiveNode(node))
        {
            MarkScrollViewLayersDirty(rootNode, node);
        }
        RequestRepaint?.Invoke();
    }

    /// <summary>Moves keyboard focus to <paramref name="node"/>, as Tab would.</summary>
    internal void AutomationFocus(Node node)
    {
        current = this;
        FocusManager.RequestFocus(node);
        FocusManager.LastFocusWasKeyboard = true;
        RequestRepaint?.Invoke();
    }

    /// <summary>
    /// Replaces the text of a text control, as if the user selected all and typed
    /// <paramref name="text"/>: the binding and <c>OnChange</c> see one change, and a focused
    /// field's edit buffer and caret follow.
    /// </summary>
    internal bool AutomationSetText(Node node, string text)
    {
        current = this;
        bool focused = ReferenceEquals(FocusManager.FocusedElement, node);
        switch (node)
        {
            case TextInput input when !input.IsDisabled && !input.IsReadOnly:
                if (input.MaxLengthValue is { } max && text.Length > max)
                {
                    text = text[..max];
                }
                if (focused)
                {
                    textInputBuffer = text;
                    ActiveEditBuffer = text;
                    TextInputCaretIndex = text.Length;
                    TextInputSelectionAnchor = text.Length;
                }
                input.Value.OnChange(text);
                input.OnChangeHandler?.Invoke(text);
                break;

            case TextArea area when !area.IsDisabled && !area.IsReadOnly:
                if (focused)
                {
                    TextAreaEditBuffer = text;
                }
                area.Value.OnChange(text);
                area.OnChangeHandler?.Invoke(text);
                break;

            case PasswordInput password when !password.IsDisabled:
                if (focused)
                {
                    PasswordEditBuffer = text;
                }
                password.Value.OnChange(text);
                break;

            default:
                return false;
        }

        if (rootNode is not null)
        {
            MarkScrollViewLayersDirty(rootNode, node);
        }
        RequestRepaint?.Invoke();
        return true;
    }

    /// <summary>Selects row <paramref name="index"/> of a list and scrolls it into view, as the arrow keys do.</summary>
    internal void AutomationSelectRow(IListViewNode list, int index)
    {
        current = this;
        if (index < 0 || index >= list.ItemCount)
        {
            return;
        }

        if (list.SelectedIndex != index)
        {
            list.SelectIndex(index);
        }
        AutomationScrollRowIntoView(list, index);
    }

    /// <summary>Scrolls row <paramref name="index"/> of a list into view.</summary>
    internal void AutomationScrollRowIntoView(IListViewNode list, int index)
    {
        if (list.RowExtent(index) is { } extent && list.ViewportHeight > 0f)
        {
            list.OffsetY = Math.Clamp(LayoutSolver.ScrollToReveal(list.OffsetY, extent.Top, extent.Height, list.ViewportHeight), 0f, list.MaxY);
        }
        if (rootNode is not null && list is Node listNode)
        {
            MarkScrollViewLayersDirty(rootNode, listNode);
        }
        RequestRepaint?.Invoke();
    }

    /// <summary>Activates a list row (what Enter or a double-click does).</summary>
    internal void AutomationActivateRow(IListViewNode list, int index)
    {
        current = this;
        list.ActivateIndex(index);
        RequestRepaint?.Invoke();
    }

    /// <summary>Runs (or, for a submenu, opens) item <paramref name="item"/> of open menu panel <paramref name="level"/>.</summary>
    internal void AutomationActivateMenuItem(int level, int item)
    {
        if (!isMenuHost)
        {
            current = this;
        }
        ActivateMenuItem(level, item, fromKeyboard: true);
    }

    /// <summary>Highlights a menu item, as moving to it with the arrow keys does.</summary>
    internal void AutomationHighlightMenuItem(int level, int item)
    {
        if (menu is not { IsOpen: true } open || level < 0 || level >= open.Levels.Count)
        {
            return;
        }

        open.CloseLevelsAbove(level);
        open.Levels[level].Highlighted = item;
        LastInputWasKeyboard = true;
        RequestRepaint?.Invoke();
    }

    /// <summary>Closes the submenu opened from item <paramref name="item"/> of panel <paramref name="level"/>.</summary>
    internal void AutomationCollapseSubmenu(int level, int item)
    {
        if (menu is not { IsOpen: true } open || level + 1 >= open.Levels.Count || open.Levels[level + 1].ParentIndex != item)
        {
            return;
        }

        open.CloseLevelsAbove(level);
        RequestRepaint?.Invoke();
    }

    /// <summary>Sets a slider's value, snapped to its step and clamped to its range.</summary>
    internal void AutomationSetSliderValue(Slider slider, float value)
    {
        float clamped = Math.Clamp(value, slider.Min, slider.Max);
        if (slider.Step is { } step && step > 0f)
        {
            clamped = Math.Clamp(slider.Min + (MathF.Round((clamped - slider.Min) / step) * step), slider.Min, slider.Max);
        }

        slider.Bind.OnChange(clamped);
        if (rootNode is not null)
        {
            MarkScrollViewLayersDirty(rootNode, slider);
        }
        RequestRepaint?.Invoke();
    }
}
