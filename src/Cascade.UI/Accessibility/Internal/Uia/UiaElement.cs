using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Cascade.UI;

/// <summary>What a <see cref="UiaElement"/> speaks for.</summary>
internal enum UiaElementKind : byte
{
    /// <summary>A node (control or semantic container).</summary>
    Node,

    /// <summary>A row of a flat <see cref="ListView{T}"/>, by item index.</summary>
    Row,

    /// <summary>A context-menu panel.</summary>
    Menu,

    /// <summary>A context-menu item.</summary>
    MenuItem,
}

/// <summary>
/// One UI Automation element below the window. A node element follows its node across
/// re-renders (<see cref="Rebind"/>); list rows and menu items are addressed by index. Every
/// property is read live from the control when UIA asks — nothing is cached per frame — and every
/// action goes through <see cref="InputDispatcher"/> so it behaves like the gesture it stands for.
/// </summary>
[GeneratedComClass]
internal sealed partial class UiaElement : UiaFragment,
    IRawElementProviderSimple,
    IRawElementProviderFragment,
    IInvokeProvider,
    IValueProvider,
    IRangeValueProvider,
    IToggleProvider,
    ISelectionProvider,
    ISelectionItemProvider,
    IExpandCollapseProvider,
    IScrollItemProvider
{
    private readonly UiaElementKind kind;
    private readonly UiaElement? list;
    private readonly int index;
    private readonly MenuLevel? menuLevel;
    private readonly int menuItem;
    private readonly int runtimeId;
    private Node? node;
    private Dictionary<int, UiaElement>? rows;
    private IListViewNode? rowNameSource;
    private string? rowName;

    private UiaElement(UiaContext context, UiaElementKind kind, Node? node, UiaElement? list, int index, MenuLevel? menuLevel, int menuItem)
        : base(context)
    {
        this.kind = kind;
        this.node = node;
        this.list = list;
        this.index = index;
        this.menuLevel = menuLevel;
        this.menuItem = menuItem;
        runtimeId = context.NewRuntimeId();
    }

    internal static UiaElement ForNode(UiaContext context, Node node)
    {
        return new UiaElement(context, UiaElementKind.Node, node, null, -1, null, -1);
    }

    internal static UiaElement ForMenu(UiaContext context, MenuLevel level)
    {
        return new UiaElement(context, UiaElementKind.Menu, null, null, -1, level, -1);
    }

    internal static UiaElement ForMenuItem(UiaContext context, MenuLevel level, int item)
    {
        return new UiaElement(context, UiaElementKind.MenuItem, null, null, -1, level, item);
    }

    internal UiaElementKind Kind => kind;

    /// <summary>The node this element speaks for (the list, for a row).</summary>
    internal Node? Node => kind == UiaElementKind.Row ? list!.node : node;

    /// <summary>Row index for a row element.</summary>
    internal int RowIndex => index;

    internal int RuntimeId => runtimeId;

    /// <summary>The element for row <paramref name="row"/> of this list element.</summary>
    internal UiaElement Row(int row)
    {
        rows ??= [];
        if (!rows.TryGetValue(row, out var element))
        {
            element = new UiaElement(Context, UiaElementKind.Row, null, this, row, null, -1);
            rows[row] = element;
        }
        return element;
    }

    /// <summary>A re-render replaced the node: speak for the new one.</summary>
    internal void Rebind(Node replacement)
    {
        node = replacement;
    }

    internal override int Resolve(AccessibleTree tree)
    {
        switch (kind)
        {
            case UiaElementKind.Node:
                return node is null ? AccessibleTree.None : tree.IndexOf(node);

            case UiaElementKind.Row:
                int listIndex = list!.Resolve(tree);
                return listIndex != AccessibleTree.None && list.node is IListViewNode { SectionCount: 0 } rowsOf && index < rowsOf.ItemCount
                    ? listIndex
                    : AccessibleTree.None;

            case UiaElementKind.Menu:
                return tree.IndexOfMenu(menuLevel!, -1);

            default:
                return tree.IndexOfMenu(menuLevel!, menuItem);
        }
    }

    private int ResolveOrThrow(out AccessibleTree tree)
    {
        tree = Context.Tree;
        int resolved = Resolve(tree);
        if (resolved == AccessibleTree.None)
        {
            throw new COMException("The element is no longer in the tree.", UiaIds.UIA_E_ELEMENTNOTAVAILABLE);
        }
        return resolved;
    }

    private IListViewNode ListNode => (IListViewNode)list!.node!;

    private InputDispatcher Input => Context.Input
        ?? throw new COMException("No input dispatcher.", UiaIds.UIA_E_ELEMENTNOTAVAILABLE);

    // ── Navigation and geometry ───────────────────────────────────────

    internal override UiaFragment? Navigate(int direction)
    {
        int self = ResolveOrThrow(out var tree);
        if (kind == UiaElementKind.Row)
        {
            int count = ListNode.ItemCount;
            return direction switch
            {
                UiaIds.NavigateDirection_Parent => list,
                UiaIds.NavigateDirection_NextSibling => index + 1 < count ? list!.Row(index + 1) : null,
                UiaIds.NavigateDirection_PreviousSibling => index > 0 ? list!.Row(index - 1) : null,
                _ => null,
            };
        }

        if (node is IListViewNode { SectionCount: 0 } rowsOf
            && direction is UiaIds.NavigateDirection_FirstChild or UiaIds.NavigateDirection_LastChild)
        {
            if (rowsOf.ItemCount == 0)
            {
                return null;
            }
            return Row(direction == UiaIds.NavigateDirection_FirstChild ? 0 : rowsOf.ItemCount - 1);
        }

        return EntryNeighbour(Context, tree, self, direction);
    }

    internal override Rect LogicalBounds()
    {
        int self = ResolveOrThrow(out var tree);
        if (kind != UiaElementKind.Row)
        {
            return tree[self].Bounds;
        }

        var listBounds = tree[self].Bounds;
        if (ListNode.RowExtent(index) is not { } extent)
        {
            return default;
        }

        float top = listBounds.Y + list!.ContentInsetTop() + extent.Top - ListNode.OffsetY;
        return new Rect(listBounds.X, top, listBounds.Width, extent.Height);
    }

    /// <summary>For a list element: the gap between its top edge and where its rows start (its padding).</summary>
    internal float ContentInsetTop()
    {
        return node?.LayoutData.Padding.Top ?? 0f;
    }

    private Rect VisibleBounds()
    {
        int self = ResolveOrThrow(out var tree);
        if (kind != UiaElementKind.Row)
        {
            return tree[self].Visible;
        }

        var row = LogicalBounds();
        var visible = tree[self].Visible;
        var clipped = visible.Intersect(row);
        return clipped.Width > 0 && clipped.Height > 0 ? clipped : default;
    }

    // ── Semantics ─────────────────────────────────────────────────────

    internal AccessibleRole Role()
    {
        int self = ResolveOrThrow(out var tree);
        return kind switch
        {
            UiaElementKind.Row => AccessibleRole.ListItem,
            _ => tree[self].Role,
        };
    }

    internal int ControlType()
    {
        int self = ResolveOrThrow(out var tree);
        return kind switch
        {
            UiaElementKind.Row => UiaIds.ListItemControl,
            UiaElementKind.Menu => UiaIds.MenuControl,
            UiaElementKind.MenuItem => UiaIds.MenuItemControl,
            _ => UiaProvider.MapRoleToUiaControlType(tree[self].Role),
        };
    }

    internal string? Name()
    {
        int self = ResolveOrThrow(out var tree);
        switch (kind)
        {
            case UiaElementKind.Row:
                // Rendering a row template for its text is not free: remember it for this list instance.
                if (!ReferenceEquals(rowNameSource, ListNode) || rowName is null)
                {
                    rowNameSource = ListNode;
                    rowName = ListNode.GetItemAccessibleName(index);
                }
                return rowName;

            case UiaElementKind.Menu:
                return MenuName(tree[self].Level);

            case UiaElementKind.MenuItem:
                return menuLevel!.Items[menuItem].Label;

            default:
                ref readonly var entry = ref tree[self];
                return AccessibilityTreeBuilder.ResolveName(node!) ?? entry.Overlay?.Title ?? entry.Overlay?.AccessibleLabel;
        }
    }

    private string MenuName(int level)
    {
        var menu = Input.Menu!;
        if (level > 0)
        {
            int parent = menu.Levels[level].ParentIndex;
            return menu.Levels[level - 1].Items[parent].Label ?? "Submenu";
        }

        return menu.Owner is SplitButton owner ? owner.Label.Resolve() : "Context menu";
    }

    internal bool IsEnabled()
    {
        ResolveOrThrow(out _);
        return kind switch
        {
            UiaElementKind.Row => !AccessibilityTreeBuilder.IsDisabled(list!.node!),
            UiaElementKind.Menu => true,
            UiaElementKind.MenuItem => !menuLevel!.Items[menuItem].Disabled,
            _ => !AccessibilityTreeBuilder.IsDisabled(node!),
        };
    }

    internal bool IsKeyboardFocusable()
    {
        if (!IsEnabled())
        {
            return false;
        }

        switch (kind)
        {
            case UiaElementKind.Row:
                return ListNode.IsSelectable;
            case UiaElementKind.Menu:
                return false;
            case UiaElementKind.MenuItem:
                return true;
        }

        var data = node!.LayoutData;
        if (data.A11yFocusable || data.FocusData is not null)
        {
            return true;
        }

        return Role() is AccessibleRole.Button or AccessibleRole.Checkbox or AccessibleRole.TextBox
            or AccessibleRole.Slider or AccessibleRole.Switch or AccessibleRole.Link or AccessibleRole.Radio
            or AccessibleRole.ComboBox or AccessibleRole.Tab or AccessibleRole.List or AccessibleRole.Table
            or AccessibleRole.Tree;
    }

    internal bool HasKeyboardFocus()
    {
        ResolveOrThrow(out _);
        return ReferenceEquals(Context.FocusedElement(), this);
    }

    internal bool IsOffscreen()
    {
        var visible = VisibleBounds();
        return visible.Width <= 0 || visible.Height <= 0;
    }

    private bool IsListRowSelected()
    {
        var rowsOf = ListNode;
        return rowsOf.SelectionModeValue == SelectionMode.Single
            ? rowsOf.SelectedIndex == index
            : rowsOf.IsItemSelected(index);
    }

    private bool HasSubmenu => kind == UiaElementKind.MenuItem && menuLevel!.Items[menuItem].Items is not null;

    private int MenuLevelIndex()
    {
        var menu = Input.Menu;
        if (menu is null)
        {
            return -1;
        }

        for (int i = 0; i < menu.Levels.Count; i++)
        {
            if (ReferenceEquals(menu.Levels[i], menuLevel))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Whether this element supports the given UIA pattern.</summary>
    internal bool SupportsPattern(int patternId)
    {
        ResolveOrThrow(out _);
        switch (kind)
        {
            case UiaElementKind.Row:
                return patternId switch
                {
                    UiaIds.SelectionItemPattern => ListNode.IsSelectable,
                    UiaIds.ScrollItemPattern => true,
                    UiaIds.InvokePattern => ListNode.CanActivate,
                    _ => false,
                };

            case UiaElementKind.Menu:
                return false;

            case UiaElementKind.MenuItem:
                return patternId switch
                {
                    UiaIds.InvokePattern => !HasSubmenu,
                    UiaIds.ExpandCollapsePattern => HasSubmenu,
                    _ => false,
                };
        }

        var target = node!;
        return patternId switch
        {
            UiaIds.InvokePattern => target is Button or IconButton or LinkButton or SplitButton
                || (Role() is AccessibleRole.Button or AccessibleRole.Link && target is not (Checkbox or Cascade.UI.Toggle or Expander)),
            UiaIds.TogglePattern => target is Checkbox or Cascade.UI.Toggle,
            UiaIds.ValuePattern => target is TextInput or TextArea or PasswordInput or ISelectNode or IComboboxNode or INumberInput,
            UiaIds.RangeValuePattern => target is Slider || target is ProgressBar { Mode: ProgressMode.Determinate },
            UiaIds.SelectionPattern => target is IListViewNode { SectionCount: 0, IsSelectable: true },
            UiaIds.SelectionItemPattern => target is IRadioButton,
            UiaIds.ExpandCollapsePattern => target is ISelectNode or IComboboxNode or IMultiSelectNode or Expander,
            _ => false,
        };
    }

    // ── Property values ───────────────────────────────────────────────

    /// <summary>The value of a UIA property, or VT_EMPTY when this element does not have it.</summary>
    internal UiaVariant Property(int propertyId)
    {
        int self = ResolveOrThrow(out var tree);
        switch (propertyId)
        {
            case UiaIds.ControlTypeProperty:
                return UiaVariant.From(ControlType());
            case UiaIds.NameProperty:
                return Name() is { Length: > 0 } name ? UiaVariant.From(name) : default;
            case UiaIds.FrameworkIdProperty:
                return UiaVariant.From("Cascade");
            case UiaIds.ProcessIdProperty:
                return UiaVariant.From(Environment.ProcessId);
            case UiaIds.ClassNameProperty:
                return UiaVariant.From(ClassName());
            case UiaIds.AutomationIdProperty:
                return kind == UiaElementKind.Node && node!.ReconciliationKey is { Length: > 0 } key ? UiaVariant.From(key) : default;
            case UiaIds.HasKeyboardFocusProperty:
                return UiaVariant.From(HasKeyboardFocus());
            case UiaIds.IsKeyboardFocusableProperty:
                return UiaVariant.From(IsKeyboardFocusable());
            case UiaIds.IsEnabledProperty:
                return UiaVariant.From(IsEnabled());
            case UiaIds.IsControlElementProperty:
            case UiaIds.IsContentElementProperty:
                return UiaVariant.From(true);
            case UiaIds.IsOffscreenProperty:
                return UiaVariant.From(IsOffscreen());
            case UiaIds.IsPasswordProperty:
                return UiaVariant.From(kind == UiaElementKind.Node && node is PasswordInput);
            case UiaIds.HelpTextProperty:
                return kind == UiaElementKind.Node && node!.LayoutData.A11yDescription is { Length: > 0 } help ? UiaVariant.From(help) : default;
            case UiaIds.AcceleratorKeyProperty:
                return kind == UiaElementKind.MenuItem && menuLevel!.Items[menuItem].Shortcut is { Length: > 0 } shortcut ? UiaVariant.From(shortcut) : default;
            case UiaIds.PositionInSetProperty:
                return SetPosition() is { } position ? UiaVariant.From(position.Index) : default;
            case UiaIds.SizeOfSetProperty:
                return SetPosition() is { } size ? UiaVariant.From(size.Count) : default;
            case UiaIds.IsDialogProperty:
                return UiaVariant.From(tree[self].Role is AccessibleRole.Dialog or AccessibleRole.AlertDialog && kind == UiaElementKind.Node);
            case UiaIds.HeadingLevelProperty:
                return kind == UiaElementKind.Node && tree[self].Role == AccessibleRole.Heading ? UiaVariant.From(HeadingLevel()) : default;
            case UiaIds.LiveSettingProperty:
                return kind == UiaElementKind.Node && node!.LayoutData.A11yLiveRegion != LiveRegionMode.Off
                    ? UiaVariant.From(node.LayoutData.A11yLiveRegion == LiveRegionMode.Assertive ? UiaIds.LiveSetting_Assertive : UiaIds.LiveSetting_Polite)
                    : default;
            case UiaIds.ValueValueProperty:
                return SupportsPattern(UiaIds.ValuePattern) ? UiaVariant.From(ValueText()) : default;
            case UiaIds.ValueIsReadOnlyProperty:
                return SupportsPattern(UiaIds.ValuePattern) ? UiaVariant.From(IsValueReadOnly()) : default;
            case UiaIds.ToggleStateProperty:
                return SupportsPattern(UiaIds.TogglePattern) ? UiaVariant.From(ToggleState()) : default;
            case UiaIds.ExpandCollapseStateProperty:
                return SupportsPattern(UiaIds.ExpandCollapsePattern) ? UiaVariant.From(ExpandState()) : default;
            case UiaIds.SelectionItemIsSelectedProperty:
                return SupportsPattern(UiaIds.SelectionItemPattern) ? UiaVariant.From(IsSelected()) : default;
            case UiaIds.RangeValueValueProperty:
                return SupportsPattern(UiaIds.RangeValuePattern) ? UiaVariant.From((double)RangeValue().Value) : default;
            default:
                return default;
        }
    }

    private string ClassName()
    {
        string name = kind switch
        {
            UiaElementKind.Row => "ListViewItem",
            UiaElementKind.Menu => "ContextMenu",
            UiaElementKind.MenuItem => "ContextMenuItem",
            _ => node!.GetType().Name,
        };
        int tick = name.IndexOf('`', StringComparison.Ordinal);
        return tick >= 0 ? name[..tick] : name;
    }

    private (int Index, int Count)? SetPosition()
    {
        if (kind == UiaElementKind.Row)
        {
            return (index + 1, ListNode.ItemCount);
        }

        if (kind != UiaElementKind.MenuItem)
        {
            return null;
        }

        // Separators are not items.
        int position = 0;
        int count = 0;
        var items = menuLevel!.Items;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].Label is null)
            {
                continue;
            }
            count++;
            if (i <= menuItem)
            {
                position++;
            }
        }
        return (position, count);
    }

    private int HeadingLevel()
    {
        if (node!.LayoutData.A11yState is { } state && state.TryGetValue("level", out var text) && int.TryParse(text, out int level) && level is >= 1 and <= 9)
        {
            return UiaIds.HeadingLevel1 + level - 1;
        }
        return UiaIds.HeadingLevel1;
    }

    // ── State readers shared with the event diff in UiaProvider (no tree needed) ──

    /// <summary>The Value-pattern text of a node, or null when it has no value.</summary>
    internal static string? ValueOf(Node node)
    {
        bool focused = ReferenceEquals(FocusManager.FocusedElement, node);
        return node switch
        {
            TextInput input => (focused ? InputDispatcher.ActiveEditBuffer : null) ?? input.Value.Value ?? "",
            TextArea area => (focused ? InputDispatcher.TextAreaEditBuffer : null) ?? area.Value.Value ?? "",
            PasswordInput => "",
            ISelectNode select => select.SelectedDisplayText ?? "",
            IComboboxNode combo => combo.DisplayText ?? "",
            INumberInput number => number.DisplayValue,
            _ => null,
        };
    }

    /// <summary>The ToggleState of a checkbox or switch.</summary>
    internal static int ToggleStateOf(Node node)
    {
        return node switch
        {
            Checkbox { BoolValue: { } bound } => bound.Value ? UiaIds.ToggleState_On : UiaIds.ToggleState_Off,
            Checkbox { ThreeStateValue: { } three } => three switch
            {
                CheckboxValue.Checked => UiaIds.ToggleState_On,
                CheckboxValue.Unchecked => UiaIds.ToggleState_Off,
                _ => UiaIds.ToggleState_Indeterminate,
            },
            Cascade.UI.Toggle toggle => toggle.Value.Value ? UiaIds.ToggleState_On : UiaIds.ToggleState_Off,
            _ => UiaIds.ToggleState_Off,
        };
    }

    /// <summary>The ExpandCollapseState of a dropdown or expander, or -1 when the node does not expand.</summary>
    internal static int ExpandStateOf(Node node)
    {
        bool? expanded = node switch
        {
            ISelectNode select => select.IsOpen,
            IComboboxNode combo => combo.IsOpen,
            IMultiSelectNode multi => multi.IsOpen,
            Expander expander => expander.IsExpanded,
            _ => null,
        };
        return expanded switch
        {
            true => UiaIds.ExpandCollapseState_Expanded,
            false => UiaIds.ExpandCollapseState_Collapsed,
            null => -1,
        };
    }

    // ── Pattern behaviour (UI thread) ─────────────────────────────────

    internal void InvokeCore()
    {
        if (!SupportsPattern(UiaIds.InvokePattern))
        {
            throw new COMException("Invoke is not supported.", UiaIds.UIA_E_INVALIDOPERATION);
        }
        if (!IsEnabled())
        {
            throw new COMException("The element is disabled.", UiaIds.UIA_E_ELEMENTNOTENABLED);
        }

        switch (kind)
        {
            case UiaElementKind.Row:
                Input.AutomationActivateRow(ListNode, index);
                break;
            case UiaElementKind.MenuItem:
                Input.AutomationActivateMenuItem(MenuLevelIndex(), menuItem);
                break;
            default:
                Input.AutomationInvoke(node!);
                break;
        }

        // UIA asks providers to raise Invoked whenever the control is invoked.
        RaiseEvent(UiaIds.InvokedEvent);
    }

    internal string ValueText()
    {
        return ValueOf(node!) ?? "";
    }

    internal bool IsValueReadOnly()
    {
        return node switch
        {
            TextInput input => input.IsReadOnly || input.IsDisabled,
            TextArea area => area.IsReadOnly || area.IsDisabled,
            PasswordInput password => password.IsDisabled,
            _ => true,
        };
    }

    internal void SetValueCore(string value)
    {
        ResolveOrThrow(out _);
        if (!SupportsPattern(UiaIds.ValuePattern) || IsValueReadOnly())
        {
            throw new COMException("The value is read-only.", UiaIds.UIA_E_INVALIDOPERATION);
        }

        if (!Input.AutomationSetText(node!, value))
        {
            throw new COMException("The value cannot be set.", UiaIds.UIA_E_INVALIDOPERATION);
        }
    }

    internal int ToggleState()
    {
        return ToggleStateOf(node!);
    }

    internal int ExpandState()
    {
        if (kind == UiaElementKind.MenuItem)
        {
            var menu = Input.Menu;
            int level = MenuLevelIndex();
            bool open = menu is not null && level >= 0 && level + 1 < menu.Levels.Count && menu.Levels[level + 1].ParentIndex == menuItem;
            return open ? UiaIds.ExpandCollapseState_Expanded : UiaIds.ExpandCollapseState_Collapsed;
        }

        return ExpandStateOf(node!);
    }

    internal void SetExpanded(bool expand)
    {
        if (!SupportsPattern(UiaIds.ExpandCollapsePattern))
        {
            throw new COMException("ExpandCollapse is not supported.", UiaIds.UIA_E_INVALIDOPERATION);
        }

        bool expanded = ExpandState() == UiaIds.ExpandCollapseState_Expanded;
        if (expanded == expand)
        {
            return;
        }

        if (kind == UiaElementKind.MenuItem)
        {
            if (expand)
            {
                Input.AutomationActivateMenuItem(MenuLevelIndex(), menuItem);
            }
            else
            {
                Input.AutomationCollapseSubmenu(MenuLevelIndex(), menuItem);
            }
            return;
        }

        // Opening and closing these is what a tap on them does.
        Input.AutomationInvoke(node!);
    }

    internal bool IsSelected()
    {
        return kind == UiaElementKind.Row ? IsListRowSelected() : node is IRadioButton { IsSelected: true };
    }

    internal void SelectCore()
    {
        if (!SupportsPattern(UiaIds.SelectionItemPattern))
        {
            throw new COMException("SelectionItem is not supported.", UiaIds.UIA_E_INVALIDOPERATION);
        }

        if (kind == UiaElementKind.Row)
        {
            Input.AutomationSelectRow(ListNode, index);
            return;
        }

        if (!IsSelected())
        {
            Input.AutomationInvoke(node!);
        }
    }

    internal (float Value, float Min, float Max, float Small, float Large, bool ReadOnly) RangeValue()
    {
        return node switch
        {
            Slider slider => (slider.Bind.Value, slider.Min, slider.Max,
                slider.Step ?? ((slider.Max - slider.Min) / 100f),
                (slider.Max - slider.Min) / 10f,
                slider.IsReadOnly || slider.IsDisabled),
            ProgressBar bar => (bar.Value * 100f, 0f, 100f, 1f, 10f, true),
            _ => (0f, 0f, 0f, 0f, 0f, true),
        };
    }

    internal UiaFragment? SelectionContainer()
    {
        if (kind == UiaElementKind.Row)
        {
            return list;
        }

        return Navigate(UiaIds.NavigateDirection_Parent);
    }

    internal List<object> Selection()
    {
        var result = new List<object>();
        if (node is not IListViewNode { SectionCount: 0 } rowsOf)
        {
            return result;
        }

        if (rowsOf.SelectionModeValue == SelectionMode.Single)
        {
            if (rowsOf.SelectedIndex >= 0 && rowsOf.SelectedIndex < rowsOf.ItemCount)
            {
                result.Add(Row(rowsOf.SelectedIndex));
            }
            return result;
        }

        for (int i = 0; i < rowsOf.ItemCount; i++)
        {
            if (rowsOf.IsItemSelected(i))
            {
                result.Add(Row(i));
            }
        }
        return result;
    }

    internal void FocusCore()
    {
        ResolveOrThrow(out _);
        switch (kind)
        {
            case UiaElementKind.Row:
                if (!ReferenceEquals(FocusManager.FocusedElement, list!.node))
                {
                    Input.AutomationFocus(list.node!);
                }
                if (ListNode.IsSelectable)
                {
                    Input.AutomationSelectRow(ListNode, index);
                }
                return;

            case UiaElementKind.MenuItem:
                Input.AutomationHighlightMenuItem(MenuLevelIndex(), menuItem);
                return;

            case UiaElementKind.Menu:
                return;
        }

        if (!IsKeyboardFocusable())
        {
            throw new COMException("The element cannot take focus.", UiaIds.UIA_E_INVALIDOPERATION);
        }
        Input.AutomationFocus(node!);
    }

    /// <summary>Raises a UIA event on this element when a client listens.</summary>
    internal void RaiseEvent(int eventId)
    {
        if (Context.Host.Handle == 0 || !UiaNative.UiaClientsAreListening())
        {
            return;
        }

        nint simple = UiaComObjects.Simple(this);
        try
        {
            _ = UiaNative.UiaRaiseAutomationEvent(simple, eventId);
        }
        finally
        {
            Marshal.Release(simple);
        }
    }

    // ── COM: IRawElementProviderSimple ────────────────────────────────

    public int GetProviderOptions(out int options)
    {
        options = UiaIds.ProviderOptions_ServerSideProvider;
        return UiaIds.S_OK;
    }

    public int GetPatternProvider(int patternId, out nint provider)
    {
        nint result = 0;
        int hr = UiaContext.Run(() =>
        {
            result = SupportsPattern(patternId) ? UiaComObjects.Unknown(this) : 0;
            return UiaIds.S_OK;
        });
        provider = result;
        return hr;
    }

    public int GetPropertyValue(int propertyId, out UiaVariant value)
    {
        UiaVariant result = default;
        int hr = UiaContext.Run(() =>
        {
            result = Property(propertyId);
            return UiaIds.S_OK;
        });
        value = result;
        return hr;
    }

    public int GetHostRawElementProvider(out nint provider)
    {
        provider = 0;
        return UiaIds.S_OK;
    }

    // ── COM: IRawElementProviderFragment ──────────────────────────────

    public int Navigate(int direction, out nint fragment)
    {
        nint result = 0;
        int hr = UiaContext.Run(() =>
        {
            result = UiaComObjects.Fragment(Navigate(direction));
            return UiaIds.S_OK;
        });
        fragment = result;
        return hr;
    }

    public int GetRuntimeId(out nint safeArray)
    {
        safeArray = UiaComObjects.IntArray([UiaIds.UiaAppendRuntimeId, runtimeId]);
        return safeArray == 0 ? UiaIds.E_FAIL : UiaIds.S_OK;
    }

    public int GetBoundingRectangle(out UiaRect rect)
    {
        UiaRect result = default;
        int hr = UiaContext.Run(() =>
        {
            var visible = LogicalBounds();
            result = visible.Width > 0 && visible.Height > 0 ? Context.Host.ToScreen(visible) : default;
            return UiaIds.S_OK;
        });
        rect = result;
        return hr;
    }

    public int GetEmbeddedFragmentRoots(out nint safeArray)
    {
        safeArray = 0;
        return UiaIds.S_OK;
    }

    public int SetFocus()
    {
        return UiaContext.Run(() =>
        {
            FocusCore();
            return UiaIds.S_OK;
        });
    }

    public int GetFragmentRoot(out nint root)
    {
        root = UiaComObjects.FragmentRoot(Context.Root);
        return UiaIds.S_OK;
    }

    // ── COM: patterns ─────────────────────────────────────────────────

    public int Invoke()
    {
        return UiaContext.Run(() =>
        {
            InvokeCore();
            return UiaIds.S_OK;
        });
    }

    int IValueProvider.SetValue(nint value)
    {
        string text = Marshal.PtrToStringUni(value) ?? "";
        return UiaContext.Run(() =>
        {
            SetValueCore(text);
            return UiaIds.S_OK;
        });
    }

    int IValueProvider.GetValue(out nint bstr)
    {
        string text = "";
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            text = ValueText();
            return UiaIds.S_OK;
        });
        bstr = hr >= 0 ? UiaNative.SysAllocString(text) : 0;
        return hr;
    }

    int IValueProvider.GetIsReadOnly(out int readOnly)
    {
        bool result = true;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            result = IsValueReadOnly();
            return UiaIds.S_OK;
        });
        readOnly = result ? 1 : 0;
        return hr;
    }

    int IRangeValueProvider.SetValue(double value)
    {
        return UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            if (node is not Slider slider || RangeValue().ReadOnly)
            {
                throw new COMException("The value is read-only.", UiaIds.UIA_E_INVALIDOPERATION);
            }
            if (value < slider.Min || value > slider.Max)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            Input.AutomationSetSliderValue(slider, (float)value);
            return UiaIds.S_OK;
        });
    }

    int IRangeValueProvider.GetValue(out double value)
    {
        return ReadRange(static range => range.Value, out value);
    }

    int IRangeValueProvider.GetIsReadOnly(out int readOnly)
    {
        int hr = ReadRange(static range => range.ReadOnly ? 1 : 0, out double result);
        readOnly = (int)result;
        return hr;
    }

    int IRangeValueProvider.GetMaximum(out double value)
    {
        return ReadRange(static range => range.Max, out value);
    }

    int IRangeValueProvider.GetMinimum(out double value)
    {
        return ReadRange(static range => range.Min, out value);
    }

    int IRangeValueProvider.GetLargeChange(out double value)
    {
        return ReadRange(static range => range.Large, out value);
    }

    int IRangeValueProvider.GetSmallChange(out double value)
    {
        return ReadRange(static range => range.Small, out value);
    }

    private int ReadRange(Func<(float Value, float Min, float Max, float Small, float Large, bool ReadOnly), double> read, out double value)
    {
        double result = 0;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            result = read(RangeValue());
            return UiaIds.S_OK;
        });
        value = result;
        return hr;
    }

    int IToggleProvider.Toggle()
    {
        return UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            if (!IsEnabled())
            {
                throw new COMException("The element is disabled.", UiaIds.UIA_E_ELEMENTNOTENABLED);
            }
            Input.AutomationInvoke(node!);
            return UiaIds.S_OK;
        });
    }

    int IToggleProvider.GetToggleState(out int state)
    {
        int result = UiaIds.ToggleState_Off;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            result = ToggleState();
            return UiaIds.S_OK;
        });
        state = result;
        return hr;
    }

    public int GetSelection(out nint safeArray)
    {
        nint result = 0;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            result = UiaComObjects.ProviderArray(Selection());
            return UiaIds.S_OK;
        });
        safeArray = result;
        return hr;
    }

    public int GetCanSelectMultiple(out int value)
    {
        bool result = false;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            result = node is IListViewNode { SelectionModeValue: SelectionMode.Multi or SelectionMode.MultiRange };
            return UiaIds.S_OK;
        });
        value = result ? 1 : 0;
        return hr;
    }

    public int GetIsSelectionRequired(out int value)
    {
        value = 0;
        return UiaIds.S_OK;
    }

    public int Select()
    {
        return UiaContext.Run(() =>
        {
            SelectCore();
            return UiaIds.S_OK;
        });
    }

    public int AddToSelection()
    {
        return UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            if (IsSelected())
            {
                return UiaIds.S_OK;
            }
            if (kind == UiaElementKind.Row && ListNode.SelectionModeValue != SelectionMode.Single)
            {
                // Multi-selection lists select one row at a time through the binding today.
                throw new COMException("Adding to the selection is not supported.", UiaIds.UIA_E_INVALIDOPERATION);
            }
            SelectCore();
            return UiaIds.S_OK;
        });
    }

    public int RemoveFromSelection()
    {
        return UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            return IsSelected()
                ? throw new COMException("A selected item cannot be deselected.", UiaIds.UIA_E_INVALIDOPERATION)
                : UiaIds.S_OK;
        });
    }

    public int GetIsSelected(out int value)
    {
        bool result = false;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            result = IsSelected();
            return UiaIds.S_OK;
        });
        value = result ? 1 : 0;
        return hr;
    }

    public int GetSelectionContainer(out nint provider)
    {
        nint result = 0;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            result = UiaComObjects.Simple(SelectionContainer());
            return UiaIds.S_OK;
        });
        provider = result;
        return hr;
    }

    public int Expand()
    {
        return UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            SetExpanded(true);
            return UiaIds.S_OK;
        });
    }

    public int Collapse()
    {
        return UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            SetExpanded(false);
            return UiaIds.S_OK;
        });
    }

    public int GetExpandCollapseState(out int state)
    {
        int result = UiaIds.ExpandCollapseState_LeafNode;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            result = ExpandState();
            return UiaIds.S_OK;
        });
        state = result;
        return hr;
    }

    public int ScrollIntoView()
    {
        return UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            if (kind != UiaElementKind.Row)
            {
                throw new COMException("ScrollItem is not supported.", UiaIds.UIA_E_INVALIDOPERATION);
            }
            Input.AutomationScrollRowIntoView(ListNode, index);
            return UiaIds.S_OK;
        });
    }
}
