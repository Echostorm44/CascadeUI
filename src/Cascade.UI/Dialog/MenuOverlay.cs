using System.Diagnostics;

namespace Cascade.UI;

/// <summary>
/// Where a menu's root panel goes: at a point (a right-click, the pointer) or against an anchor
/// rectangle (a row or control opened from the keyboard, a split button's arrow). Window-logical
/// coordinates.
/// </summary>
internal readonly record struct MenuPlacement(Rect Anchor, bool IsPoint)
{
    /// <summary>Top-left corner at <paramref name="point"/>, flipped left/up when it does not fit.</summary>
    internal static MenuPlacement AtPoint(Point point)
    {
        return new MenuPlacement(new Rect(point.X, point.Y, 0f, 0f), IsPoint: true);
    }

    /// <summary>Below <paramref name="anchor"/>, left-aligned with it; flipped above when it does not fit.</summary>
    internal static MenuPlacement Below(Rect anchor)
    {
        return new MenuPlacement(anchor, IsPoint: false);
    }
}

/// <summary>
/// The measurements a menu panel is laid out with. Colours and the item height come from the
/// active theme's dropdown tokens (<see cref="SelectTheme"/>), so a menu matches the Select and
/// SplitButton dropdowns of the same theme.
/// </summary>
internal readonly record struct MenuMetrics(
    float ItemHeight,
    float ItemPaddingH,
    float FontSize,
    float ShortcutFontSize,
    float SeparatorHeight,
    float PaddingV,
    float InsetH,
    float IconColumn,
    float ShortcutGap,
    float SubmenuArrowWidth,
    float MinWidth,
    float HeaderHeight = 24f,
    float HeaderFontSize = 12f,
    float CheckColumn = 22f)
{
    internal static MenuMetrics FromTheme(CascadeTheme theme)
    {
        var select = theme.Select;
        return new MenuMetrics(
            ItemHeight:        select.ItemHeight,
            ItemPaddingH:      select.ItemPaddingH,
            FontSize:          theme.Typography.Body.Size,
            ShortcutFontSize:  12f,
            SeparatorHeight:   9f,
            PaddingV:          6f,
            InsetH:            6f,
            IconColumn:        24f,
            ShortcutGap:       24f,
            SubmenuArrowWidth: 16f,
            MinWidth:          160f,
            HeaderHeight:      Math.Max(20f, MathF.Round(select.ItemHeight * 0.75f)),
            HeaderFontSize:    12f,
            CheckColumn:       22f);
    }

    /// <summary>Space between the panel edge and an item's text (highlight inset + item padding).</summary>
    internal float TextInset => InsetH + ItemPaddingH;
}

/// <summary>
/// One panel of an open menu: the root, or a submenu opened from an item of the panel before it.
/// Geometry is computed once when the panel opens; painting and hit-testing only read it.
/// </summary>
internal sealed class MenuLevel
{
    internal MenuLevel(ContextMenuItem[] items, int parentIndex)
    {
        Items = items;
        ParentIndex = parentIndex;
        ItemTops = new float[items.Length];
        ItemHeights = new float[items.Length];
    }

    /// <summary>The items shown, in order. Separators have a null label.</summary>
    internal ContextMenuItem[] Items { get; }

    /// <summary>Index of the item in the previous level that opened this one; -1 for the root.</summary>
    internal int ParentIndex { get; }

    /// <summary>Each item's top, relative to the first item (content coordinates, before scrolling).</summary>
    internal float[] ItemTops { get; }

    /// <summary>Each item's height (item height, or separator height).</summary>
    internal float[] ItemHeights { get; }

    /// <summary>The panel, window-logical.</summary>
    internal Rect Bounds { get; set; }

    /// <summary>Height of all items together (no panel padding).</summary>
    internal float ContentHeight { get; set; }

    /// <summary>How far the items are scrolled when the panel is shorter than its content.</summary>
    internal float ScrollOffset { get; set; }

    /// <summary>Largest valid <see cref="ScrollOffset"/>; 0 when everything fits.</summary>
    internal float MaxScroll { get; set; }

    /// <summary>Vertical padding above the first item and below the last.</summary>
    internal float PaddingV { get; set; }

    /// <summary>Whether any item has an icon (the panel then reserves an icon column).</summary>
    internal bool HasIcons { get; set; }

    /// <summary>Whether any item is a toggle or a radio choice (the panel then reserves a check column).</summary>
    internal bool HasChecks { get; set; }

    /// <summary>Set by the painter once custom content rows have been laid out at the panel width.</summary>
    internal bool ContentLaidOut { get; set; }

    /// <summary>Whether any item opens a submenu (the panel then reserves an arrow column).</summary>
    internal bool HasSubmenus { get; set; }

    /// <summary>Width of the widest shortcut hint, or 0 when no item has one.</summary>
    internal float ShortcutWidth { get; set; }

    /// <summary>The highlighted item (pointer or keyboard), or -1.</summary>
    internal int Highlighted { get; set; } = -1;

    /// <summary>When the panel opened (Stopwatch timestamp), for its entrance animation.</summary>
    internal long OpenedAt { get; set; }

    /// <summary>Set by the painter once the item icons have been laid out.</summary>
    internal bool IconsLaidOut { get; set; }

    /// <summary>Height available to the items inside the panel.</summary>
    internal float ViewportHeight => Bounds.Height - PaddingV * 2f;

    /// <summary>An item's top edge, window-logical, after scrolling.</summary>
    internal float ItemTop(int index)
    {
        return Bounds.Y + PaddingV + ItemTops[index] - ScrollOffset;
    }

    /// <summary>
    /// The item under <paramref name="point"/>: -1 when the point is outside the panel or in its
    /// padding, otherwise the item index (which may be a separator or a disabled item).
    /// </summary>
    internal int ItemAt(Point point)
    {
        if (!Bounds.Contains(point))
        {
            return -1;
        }

        float contentTop = Bounds.Y + PaddingV;
        if (point.Y < contentTop || point.Y >= Bounds.Bottom - PaddingV)
        {
            return -1;
        }

        float y = point.Y - contentTop + ScrollOffset;
        for (int i = 0; i < Items.Length; i++)
        {
            if (y >= ItemTops[i] && y < ItemTops[i] + ItemHeights[i])
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Scrolls the least amount that brings <paramref name="index"/> fully into view.</summary>
    internal void EnsureVisible(int index)
    {
        if (MaxScroll <= 0f || index < 0 || index >= Items.Length)
        {
            return;
        }

        float top = ItemTops[index];
        float bottom = top + ItemHeights[index];
        float view = ViewportHeight;
        if (top < ScrollOffset)
        {
            ScrollOffset = top;
        }
        else if (bottom > ScrollOffset + view)
        {
            ScrollOffset = bottom - view;
        }

        ScrollOffset = Math.Clamp(ScrollOffset, 0f, MaxScroll);
    }
}

/// <summary>
/// The open menu of a window: a stack of panels (the root and any open submenus) with their
/// geometry and highlight state. One instance per <see cref="InputDispatcher"/>; it is the shared
/// popup that <see cref="ContextMenu"/>, <see cref="ListView{T}.ItemContextMenu"/>, TreeView rows
/// and <see cref="SplitButton"/> all open. The painter draws it after the whole tree, in window
/// coordinates, so it is never clipped by a parent and never captured into a ScrollView's
/// retained layer.
/// </summary>
/// <remarks>
/// Everything here is pure state and geometry: no input routing, no painting. The dispatcher
/// owns the policy (which events open, move, activate or dismiss it), which keeps this class
/// directly unit-testable.
/// </remarks>
internal sealed class MenuOverlay
{
    /// <summary>Space kept between a panel and the window edge.</summary>
    internal const float EdgeMargin = 4f;

    /// <summary>Gap between an anchor rectangle and the panel placed against it.</summary>
    internal const float AnchorGap = 2f;

    /// <summary>How far a submenu overlaps its parent panel horizontally.</summary>
    internal const float SubmenuOverlap = 2f;

    /// <summary>
    /// Pointer travel (logical px) after which releasing the button over an item activates it,
    /// so press–drag–release picks an item while the release of the opening click does not.
    /// </summary>
    internal const float DragActivateThreshold = 4f;

    /// <summary>Extra width given to the label column beyond the widest measured label.</summary>
    internal const float LabelSlack = 4f;

    private readonly List<MenuLevel> levels = new(capacity: 2);
    private Func<string, float, float> measure = MeasureText;
    private Func<Node, float, Size> measureNode = MeasureNode;

    // Lays out custom content rows to measure them (only when a menu with one opens).
    private static LayoutEngine? contentLayout;

    /// <summary>Replaces the custom-content measurer (tests use a deterministic one).</summary>
    internal void SetNodeMeasurer(Func<Node, float, Size> measurer)
    {
        measureNode = measurer;
    }

    /// <summary>Default custom-content measurer: lays the node out loosely within <paramref name="maxWidth"/>.</summary>
    internal static Size MeasureNode(Node node, float maxWidth)
    {
        contentLayout ??= new LayoutEngine();
        contentLayout.Layout(node, LayoutConstraints.Loose(new Size(maxWidth, float.PositiveInfinity)));
        return node.LayoutData.Bounds.Size;
    }

    /// <summary>
    /// The custom-content row under <paramref name="point"/>: its panel, item and the point
    /// relative to the row's top-left corner. False when the point is not over a custom row.
    /// </summary>
    internal bool TryHitCustom(Point point, out int levelIndex, out int itemIndex, out Point local)
    {
        local = default;
        (levelIndex, itemIndex) = HitTest(point);
        if (levelIndex < 0 || itemIndex < 0)
        {
            return false;
        }

        var level = levels[levelIndex];
        if (level.Items[itemIndex].Kind != MenuItemKind.Custom)
        {
            return false;
        }

        local = new Point(point.X - (level.Bounds.X + Metrics.InsetH), point.Y - level.ItemTop(itemIndex));
        return true;
    }

    /// <summary>The open panels, root first. Empty when the menu is closed.</summary>
    internal IReadOnlyList<MenuLevel> Levels => levels;

    /// <summary>Whether a menu is showing.</summary>
    internal bool IsOpen => levels.Count > 0;

    /// <summary>The control the menu belongs to (a <see cref="SplitButton"/>), or null for a context menu.</summary>
    internal Node? Owner { get; set; }

    /// <summary>Metrics the open menu was laid out with.</summary>
    internal MenuMetrics Metrics { get; private set; }

    /// <summary>Window size (logical px) the panels are kept inside; empty when unknown (no clamping).</summary>
    internal Size Viewport { get; private set; }

    /// <summary>Device pixels per logical pixel, for snapping panel edges to whole device pixels.</summary>
    internal float PixelRatio { get; private set; } = 1f;

    /// <summary>Pointer position when the menu opened.</summary>
    internal Point PointerAtOpen { get; private set; }

    /// <summary>The pointer has moved more than <see cref="DragActivateThreshold"/> since the menu opened.</summary>
    internal bool PointerTravelled { get; private set; }

    /// <summary>The current mouse press began inside a panel (its release may activate an item).</summary>
    internal bool PressStartedInside { get; set; }

    /// <summary>
    /// Replaces the text measurer (tests use a deterministic one). The default shapes with the
    /// window's default font and falls back to an estimate when no font is loaded.
    /// </summary>
    internal void SetMeasurer(Func<string, float, float> measurer)
    {
        measure = measurer;
    }

    /// <summary>
    /// Opens a menu with <paramref name="items"/>, replacing any menu already open. Returns false
    /// (and leaves the menu closed) when there is nothing to show — only separators.
    /// </summary>
    internal bool Open(
        IReadOnlyList<ContextMenuItem> items,
        MenuPlacement placement,
        MenuMetrics metrics,
        Size viewport,
        float pixelRatio,
        Point pointer,
        bool highlightFirst,
        Node? owner)
    {
        ArgumentNullException.ThrowIfNull(items);

        levels.Clear();
        Owner = null;
        PressStartedInside = false;
        PointerTravelled = false;

        if (!HasVisibleItem(items))
        {
            return false;
        }

        Metrics = metrics;
        Viewport = viewport;
        PixelRatio = pixelRatio > 0f ? pixelRatio : 1f;
        PointerAtOpen = pointer;
        Owner = owner;

        var level = CreateLevel(ToArray(items), parentIndex: -1);
        var size = level.Bounds.Size;
        float height = CapHeight(size.Height);
        level.Bounds = PlaceRoot(new Size(size.Width, height), placement, viewport, PixelRatio);
        FinishLevel(level);
        levels.Add(level);

        if (highlightFirst)
        {
            level.Highlighted = NextActionable(level.Items, -1, +1);
        }

        return true;
    }

    /// <summary>Closes every panel.</summary>
    internal void Close()
    {
        levels.Clear();
        Owner = null;
        PressStartedInside = false;
        PointerTravelled = false;
    }

    /// <summary>Closes the panels above <paramref name="levelIndex"/>.</summary>
    internal void CloseLevelsAbove(int levelIndex)
    {
        while (levels.Count > levelIndex + 1)
        {
            levels.RemoveAt(levels.Count - 1);
        }
    }

    /// <summary>
    /// Opens the submenu of item <paramref name="itemIndex"/> in panel <paramref name="levelIndex"/>,
    /// closing any deeper panels. Returns false when the item has no (non-empty) submenu.
    /// </summary>
    internal bool OpenSubmenu(int levelIndex, int itemIndex, bool highlightFirst)
    {
        if (levelIndex < 0 || levelIndex >= levels.Count)
        {
            return false;
        }

        var parent = levels[levelIndex];
        if (itemIndex < 0 || itemIndex >= parent.Items.Length)
        {
            return false;
        }

        var item = parent.Items[itemIndex];
        if (item.Items is null || item.Disabled)
        {
            return false;
        }

        // Already open: keep it (hovering over the parent item must not rebuild it every move).
        if (levels.Count > levelIndex + 1 && levels[levelIndex + 1].ParentIndex == itemIndex)
        {
            CloseLevelsAbove(levelIndex + 1);
            if (highlightFirst && levels[levelIndex + 1].Highlighted < 0)
            {
                var open = levels[levelIndex + 1];
                open.Highlighted = NextActionable(open.Items, -1, +1);
            }
            return true;
        }

        var children = ToArray(item.Items);
        if (!HasVisibleItem(children))
        {
            return false;
        }

        CloseLevelsAbove(levelIndex);
        parent.Highlighted = itemIndex;

        var level = CreateLevel(children, itemIndex);
        float height = CapHeight(level.Bounds.Height);
        level.Bounds = PlaceSubmenu(
            new Size(level.Bounds.Width, height), parent.Bounds, parent.ItemTop(itemIndex), Viewport, Metrics, PixelRatio);
        FinishLevel(level);
        levels.Add(level);

        if (highlightFirst)
        {
            level.Highlighted = NextActionable(level.Items, -1, +1);
        }

        return true;
    }

    /// <summary>
    /// Finds the panel and item under <paramref name="point"/>, searching the top-most panel first.
    /// Level is -1 when the point is outside every panel; item is -1 in a panel's padding.
    /// </summary>
    internal (int Level, int Item) HitTest(Point point)
    {
        for (int i = levels.Count - 1; i >= 0; i--)
        {
            var level = levels[i];
            if (level.Bounds.Contains(point))
            {
                return (i, level.ItemAt(point));
            }
        }

        return (-1, -1);
    }

    /// <summary>Records pointer movement; reports whether the travel threshold has been crossed.</summary>
    internal void NotePointer(Point point)
    {
        if (PointerTravelled)
        {
            return;
        }

        float dx = point.X - PointerAtOpen.X;
        float dy = point.Y - PointerAtOpen.Y;
        if ((dx * dx) + (dy * dy) > DragActivateThreshold * DragActivateThreshold)
        {
            PointerTravelled = true;
        }
    }

    /// <summary>
    /// Moves the highlight in the top-most panel to the next selectable item in
    /// <paramref name="direction"/> (+1 down, -1 up), wrapping and skipping separators and
    /// disabled items. Returns true when the highlight changed.
    /// </summary>
    internal bool MoveHighlight(int direction)
    {
        if (levels.Count == 0)
        {
            return false;
        }

        var level = levels[^1];
        int next = NextActionable(level.Items, level.Highlighted, direction);
        return SetHighlight(level, next);
    }

    /// <summary>Highlights the first (<paramref name="last"/> false) or last selectable item of the top-most panel.</summary>
    internal bool HighlightEdge(bool last)
    {
        if (levels.Count == 0)
        {
            return false;
        }

        var level = levels[^1];
        int index = last
            ? NextActionable(level.Items, -1, -1)
            : NextActionable(level.Items, -1, +1);
        return SetHighlight(level, index);
    }

    /// <summary>
    /// Highlights the next selectable item of the top-most panel whose label starts with
    /// <paramref name="letter"/> (case-insensitive), cycling through the matches.
    /// </summary>
    internal bool HighlightByLetter(char letter)
    {
        if (levels.Count == 0 || char.IsWhiteSpace(letter))
        {
            return false;
        }

        var level = levels[^1];
        int n = level.Items.Length;
        int start = level.Highlighted;
        for (int step = 1; step <= n; step++)
        {
            int i = (((start + step) % n) + n) % n;
            var item = level.Items[i];
            if (!IsActionable(item))
            {
                continue;
            }

            string label = item.Label!.TrimStart();
            if (label.Length > 0 && char.ToUpperInvariant(label[0]) == char.ToUpperInvariant(letter))
            {
                return SetHighlight(level, i);
            }
        }

        return false;
    }

    /// <summary>
    /// Updates the highlight for the pointer at <paramref name="point"/>: highlights the item
    /// under it, opens a submenu when hovering its item and closes submenus of other items.
    /// Returns true when anything visible changed.
    /// </summary>
    internal bool HoverAt(Point point)
    {
        if (levels.Count == 0)
        {
            return false;
        }

        var (levelIndex, itemIndex) = HitTest(point);
        if (levelIndex < 0)
        {
            // Off every panel: a panel with an open submenu keeps its parent item lit, the
            // top-most panel loses its highlight.
            var top = levels[^1];
            return SetHighlight(top, -1);
        }

        var level = levels[levelIndex];
        int target = itemIndex >= 0 && IsActionable(level.Items[itemIndex]) ? itemIndex : -1;

        bool changed = false;
        bool hasOpenChild = levels.Count > levelIndex + 1;
        if (target < 0)
        {
            // Padding, a separator or a disabled item. Keep the parent item of an open submenu
            // lit (moving the pointer across a separator towards the submenu must not close it).
            if (!hasOpenChild)
            {
                changed |= SetHighlight(level, -1);
            }
            return changed;
        }

        if (hasOpenChild && levels[levelIndex + 1].ParentIndex != target)
        {
            CloseLevelsAbove(levelIndex);
            changed = true;
        }

        changed |= SetHighlight(level, target);
        if (level.Items[target].Items is not null && levels.Count == levelIndex + 1)
        {
            changed |= OpenSubmenu(levelIndex, target, highlightFirst: false);
        }

        return changed;
    }

    /// <summary>
    /// What activating item <paramref name="itemIndex"/> of panel <paramref name="levelIndex"/>
    /// does. A submenu item opens its submenu (and returns null); an action item returns its
    /// handler, which the caller invokes after closing the menu. Disabled items and separators
    /// return null and change nothing.
    /// </summary>
    internal Action? Activate(int levelIndex, int itemIndex, bool fromKeyboard)
    {
        if (levelIndex < 0 || levelIndex >= levels.Count)
        {
            return null;
        }

        var level = levels[levelIndex];
        if (itemIndex < 0 || itemIndex >= level.Items.Length || !IsActionable(level.Items[itemIndex]))
        {
            return null;
        }

        var item = level.Items[itemIndex];
        if (item.Items is not null)
        {
            OpenSubmenu(levelIndex, itemIndex, highlightFirst: fromKeyboard);
            return null;
        }

        return item.OnClick ?? NoOp;
    }

    /// <summary>Scrolls the panel at <paramref name="levelIndex"/> by <paramref name="delta"/> logical px.</summary>
    internal bool Scroll(int levelIndex, float delta)
    {
        if (levelIndex < 0 || levelIndex >= levels.Count)
        {
            return false;
        }

        var level = levels[levelIndex];
        float next = Math.Clamp(level.ScrollOffset + delta, 0f, level.MaxScroll);
        if (next == level.ScrollOffset)
        {
            return false;
        }

        level.ScrollOffset = next;
        CloseLevelsAbove(levelIndex);
        return true;
    }

    /// <summary>Whether <paramref name="item"/> can be highlighted and activated.</summary>
    internal static bool IsActionable(ContextMenuItem item)
    {
        return item.Kind is MenuItemKind.Action or MenuItemKind.Submenu or MenuItemKind.Toggle or MenuItemKind.Radio
            && item.Label is not null
            && !item.Disabled;
    }

    /// <summary>
    /// The next selectable item from <paramref name="from"/> in <paramref name="direction"/>,
    /// wrapping; -1 from <paramref name="from"/> starts at the first (down) or last (up) item.
    /// Returns -1 when no item is selectable.
    /// </summary>
    internal static int NextActionable(ContextMenuItem[] items, int from, int direction)
    {
        int n = items.Length;
        if (n == 0)
        {
            return -1;
        }

        int step = direction >= 0 ? 1 : -1;
        int start = from >= 0 ? from : (step > 0 ? -1 : n);
        for (int k = 1; k <= n; k++)
        {
            int i = (((start + (step * k)) % n) + n) % n;
            if (IsActionable(items[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Places the root panel. At a point: the top-left corner at the point, flipped to the left
    /// and/or above it when it would cross the window's right/bottom edge. Against an anchor:
    /// below it and left-aligned, flipped above or right-aligned when it would not fit. Either
    /// way the panel is finally clamped inside the window margin, and edges are snapped to
    /// whole device pixels.
    /// </summary>
    internal static Rect PlaceRoot(Size panel, MenuPlacement placement, Size viewport, float pixelRatio = 1f)
    {
        var a = placement.Anchor;
        float w = panel.Width;
        float h = panel.Height;
        float x;
        float y;

        if (placement.IsPoint)
        {
            x = a.X;
            y = a.Y;
            if (viewport.Width > 0f && x + w > viewport.Width - EdgeMargin)
            {
                x = a.X - w;
            }
            if (viewport.Height > 0f && y + h > viewport.Height - EdgeMargin && a.Y - h >= EdgeMargin)
            {
                y = a.Y - h;
            }
        }
        else
        {
            x = a.X;
            y = a.Bottom + AnchorGap;
            if (viewport.Width > 0f && x + w > viewport.Width - EdgeMargin)
            {
                x = a.Right - w;
            }
            if (viewport.Height > 0f && y + h > viewport.Height - EdgeMargin && a.Y - AnchorGap - h >= EdgeMargin)
            {
                y = a.Y - AnchorGap - h;
            }
        }

        return Snap(Clamp(new Rect(x, y, w, h), viewport), pixelRatio);
    }

    /// <summary>
    /// Places a submenu beside its parent panel, its first item level with the parent item
    /// (<paramref name="itemTop"/>): to the right, or to the left when it would cross the window's
    /// right edge (the roomier side when it fits on neither); then clamped inside the window.
    /// </summary>
    internal static Rect PlaceSubmenu(Size panel, Rect parent, float itemTop, Size viewport, MenuMetrics metrics, float pixelRatio = 1f)
    {
        float w = panel.Width;
        float h = panel.Height;
        float right = parent.Right - SubmenuOverlap;
        float left = parent.X - w + SubmenuOverlap;
        float x = right;
        if (viewport.Width > 0f && right + w > viewport.Width - EdgeMargin)
        {
            // Left when it fits there; when it fits on neither side, the side with more room
            // (the clamp below then overlaps the parent as little as possible).
            float roomRight = viewport.Width - EdgeMargin - parent.Right;
            float roomLeft = parent.X - EdgeMargin;
            x = left >= EdgeMargin || roomLeft > roomRight ? left : right;
        }

        float y = itemTop - metrics.PaddingV;
        return Snap(Clamp(new Rect(x, y, w, h), viewport), pixelRatio);
    }

    /// <summary>Default text measurer: shaped advance width with the window's font, else an estimate.</summary>
    internal static float MeasureText(string text, float fontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        string? font = LayoutSolver.DefaultFontPath;
        if (font is not null)
        {
            var options = new TextLayoutOptions
            {
                FontPath = font,
                FontSize = fontSize,
                MaxWidth = float.PositiveInfinity,
                NoWrap = true,
            };
            return TextLayoutEngine.Layout(text, options).AdvanceBox.Width;
        }

        return text.Length * fontSize * 0.6f;
    }

    private static bool SetHighlight(MenuLevel level, int index)
    {
        if (level.Highlighted == index)
        {
            return false;
        }

        level.Highlighted = index;
        level.EnsureVisible(index);
        return true;
    }

    private MenuLevel CreateLevel(ContextMenuItem[] items, int parentIndex)
    {
        var m = Metrics;
        var level = new MenuLevel(items, parentIndex)
        {
            PaddingV = m.PaddingV,
        };

        float maxContentWidth = Viewport.Width > 0f ? Viewport.Width - (EdgeMargin * 2f) - (m.InsetH * 2f) : float.PositiveInfinity;
        float y = 0f;
        float labelWidth = 0f;
        float shortcutWidth = 0f;
        float contentWidth = 0f;
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            float height;
            switch (item.Kind)
            {
                case MenuItemKind.Separator:
                    height = m.SeparatorHeight;
                    break;

                case MenuItemKind.Header:
                    height = m.HeaderHeight;
                    labelWidth = Math.Max(labelWidth, measure(item.Label ?? "", m.HeaderFontSize));
                    break;

                case MenuItemKind.Custom:
                {
                    var size = measureNode(item.Content, maxContentWidth);
                    height = Math.Max(1f, MathF.Ceiling(size.Height));
                    contentWidth = Math.Max(contentWidth, size.Width);
                    break;
                }

                default:
                    height = m.ItemHeight;
                    labelWidth = Math.Max(labelWidth, measure(item.Label ?? "", m.FontSize));
                    if (!string.IsNullOrEmpty(item.Shortcut))
                    {
                        shortcutWidth = Math.Max(shortcutWidth, measure(item.Shortcut, m.ShortcutFontSize));
                    }
                    if (item.Icon is { IsLayoutEmpty: false })
                    {
                        level.HasIcons = true;
                    }
                    if (item.Items is not null)
                    {
                        level.HasSubmenus = true;
                    }
                    if (item.Kind is MenuItemKind.Toggle or MenuItemKind.Radio)
                    {
                        level.HasChecks = true;
                    }
                    break;
            }

            level.ItemTops[i] = y;
            level.ItemHeights[i] = height;
            y += height;
        }

        level.ContentHeight = y;
        level.ShortcutWidth = MathF.Ceiling(shortcutWidth);

        // LabelSlack absorbs the difference between this measurement and the painter's shaped
        // run (hinting, weight), so the widest label never ellipsizes in its own menu.
        float width = (m.TextInset * 2f)
            + (level.HasChecks ? m.CheckColumn : 0f)
            + (level.HasIcons ? m.IconColumn : 0f)
            + labelWidth + LabelSlack
            + (shortcutWidth > 0f ? m.ShortcutGap + shortcutWidth : 0f)
            + (level.HasSubmenus ? m.SubmenuArrowWidth : 0f);
        width = Math.Max(width, contentWidth + (m.InsetH * 2f));
        width = MathF.Ceiling(Math.Max(width, m.MinWidth));
        if (Viewport.Width > 0f)
        {
            width = Math.Min(width, Math.Max(m.MinWidth, Viewport.Width - (EdgeMargin * 2f)));
        }

        level.Bounds = new Rect(0f, 0f, width, y + (m.PaddingV * 2f));
        level.OpenedAt = Stopwatch.GetTimestamp();
        return level;
    }

    private static void FinishLevel(MenuLevel level)
    {
        float overflow = level.ContentHeight - level.ViewportHeight;
        level.MaxScroll = overflow > 0.5f ? overflow : 0f;
    }

    private float CapHeight(float height)
    {
        if (Viewport.Height <= 0f)
        {
            return height;
        }

        return Math.Min(height, Math.Max(Metrics.ItemHeight, Viewport.Height - (EdgeMargin * 2f)));
    }

    private static Rect Clamp(Rect r, Size viewport)
    {
        float x = r.X;
        float y = r.Y;
        if (viewport.Width > 0f)
        {
            x = Math.Min(x, viewport.Width - EdgeMargin - r.Width);
            x = Math.Max(x, EdgeMargin);
        }
        if (viewport.Height > 0f)
        {
            y = Math.Min(y, viewport.Height - EdgeMargin - r.Height);
            y = Math.Max(y, EdgeMargin);
        }

        return new Rect(x, y, r.Width, r.Height);
    }

    private static Rect Snap(Rect r, float pixelRatio)
    {
        float s = pixelRatio > 0f ? pixelRatio : 1f;
        // Position rounds to the nearest device pixel; size rounds up, so snapping never makes
        // the panel shorter than its content (which would show a scroll thumb for a fraction
        // of a pixel at fractional scales).
        float x = MathF.Round(r.X * s) / s;
        float y = MathF.Round(r.Y * s) / s;
        float w = MathF.Ceiling((r.Width * s) - 0.001f) / s;
        float h = MathF.Ceiling((r.Height * s) - 0.001f) / s;
        return new Rect(x, y, w, h);
    }

    private static bool HasVisibleItem(IReadOnlyList<ContextMenuItem> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (!items[i].IsSeparator)
            {
                return true;
            }
        }

        return false;
    }

    private static ContextMenuItem[] ToArray(IEnumerable<ContextMenuItem> items)
    {
        return items as ContextMenuItem[] ?? [.. items];
    }

    private static void NoOp()
    {
    }
}
