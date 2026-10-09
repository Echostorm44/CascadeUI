namespace Cascade.UI;

/// <summary>
/// One action node as the painter last laid it out for a row: the row's display index, the
/// action's position in the row's list, the node, and its rectangle relative to the table's left
/// edge and the row's top edge. Row-relative rather than absolute so a hit test after a scroll
/// (before the next paint) still lands on the right button — the row's own position comes from
/// <see cref="TabularRowGeometry"/>, which applies the current scroll.
/// </summary>
internal readonly record struct PaintedRowAction(int Row, int Index, Node Node, Rect Bounds, float VisibleLeft)
{
    /// <summary>Whether a row-relative point is on the painted (not clipped) part of the action.</summary>
    internal bool Contains(float relX, float relY)
    {
        return relX >= VisibleLeft && Bounds.Contains(new Point(relX, relY));
    }
}

/// <summary>
/// The inline per-row action buttons of a <see cref="DataTable{T}"/> or <see cref="DataGrid{T}"/>
/// (<c>RowActions(row => [...])</c>): a strip at the right end of every row, after the last
/// column. Holds the nodes built for each row, the strip width, and the hover / press / keyboard
/// state that the painter draws and the input dispatcher drives. The nodes are built lazily per
/// row and kept for the life of the control node, so a hovered button keeps its animation state
/// from frame to frame.
/// </summary>
internal abstract class TabularRowActions
{
    /// <summary>Horizontal gap between two actions of a row.</summary>
    internal const float Gap = 4f;

    /// <summary>Horizontal padding on each side of the strip.</summary>
    internal const float Padding = 8f;

    /// <summary>
    /// Rows measured to size the strip. The first rows, not the visible ones, for the same reason
    /// as <see cref="DataColumnWidth.Auto"/> columns: a strip that changes width while scrolling
    /// is worse than one that is sized once.
    /// </summary>
    internal const int SampleRows = 50;

    private readonly List<PaintedRowAction> painted = [];

    /// <summary>
    /// Width of the strip including its padding, or 0 before the painter has measured it (and
    /// when no sampled row has an action). Column widths are resolved in what is left.
    /// </summary>
    internal float StripWidth { get; set; }

    /// <summary>Whether <see cref="StripWidth"/> has been measured for the current data.</summary>
    internal bool IsMeasured { get; set; }

    /// <summary>
    /// The action of the selected row that has keyboard focus (Right/Left move it, Enter/Space
    /// activate it), or -1 when the row itself has focus.
    /// </summary>
    internal int FocusedIndex { get; set; } = -1;

    /// <summary>The control under the pointer inside an action, or null.</summary>
    internal Node? HoveredTarget { get; set; }

    /// <summary>The control pressed inside an action (between mouse down and up), or null.</summary>
    internal Node? PressedTarget { get; set; }

    /// <summary>The actions the painter laid out in its last pass.</summary>
    internal IReadOnlyList<PaintedRowAction> Painted => painted;

    /// <summary>The action nodes of display row <paramref name="displayRow"/> (empty when it has none).</summary>
    internal abstract IReadOnlyList<Node> ForDisplayRow(int displayRow);

    /// <summary>Forgets the previous pass's layout; called by the painter before it draws rows.</summary>
    internal void BeginPaint()
    {
        painted.Clear();
    }

    /// <summary>
    /// Lays out the actions of one row, right-aligned in the strip, and records their row-relative
    /// rectangles. Each node's <see cref="LayoutNodeData.Bounds"/> is placed at
    /// (<paramref name="originX"/>, <paramref name="originY"/>) + its row-relative position, in the
    /// caller's paint coordinates, so the painter can hand it straight to <c>PaintRecursive</c>.
    /// </summary>
    internal IReadOnlyList<Node> LayoutRow(int row, float tableWidth, float rowHeight, float stripWidth, float originX, float originY)
    {
        var nodes = ForDisplayRow(row);
        if (nodes.Count == 0 || stripWidth <= 0f)
        {
            return nodes;
        }

        // A strip narrowed to fit the table clips its leftmost actions; record only the part that
        // is painted, so a click on the column beside it is not taken for an action.
        float stripLeft = tableWidth - stripWidth;
        float contentWidth = Math.Max(0f, stripWidth - (2f * Padding));
        float total = 0f;
        for (int i = 0; i < nodes.Count; i++)
        {
            var size = Measure(nodes[i], contentWidth, rowHeight);
            total += size.Width + (i > 0 ? Gap : 0f);
        }

        float x = tableWidth - Padding - total;
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var size = node.LayoutData.MeasuredSize;
            float y = (rowHeight - size.Height) / 2f;
            node.LayoutData.Bounds = new Rect(originX + x, originY + y, size.Width, size.Height);
            float visibleLeft = Math.Max(x, stripLeft);
            if (x + size.Width > visibleLeft)
            {
                painted.Add(new PaintedRowAction(row, i, node, new Rect(x, y, size.Width, size.Height), visibleLeft));
            }

            x += size.Width + Gap;
        }

        return nodes;
    }

    /// <summary>
    /// Measures the strip from the first <see cref="SampleRows"/> rows: the widest row's actions
    /// plus padding. Idempotent until the data changes (a new control node starts unmeasured; the
    /// reconciler carries the width over while the data source is the same).
    /// </summary>
    internal void EnsureMeasured(int rowCount, float rowHeight)
    {
        if (IsMeasured)
        {
            return;
        }

        IsMeasured = true;
        float widest = 0f;
        int sample = Math.Min(rowCount, SampleRows);
        for (int r = 0; r < sample; r++)
        {
            var nodes = ForDisplayRow(r);
            float total = 0f;
            for (int i = 0; i < nodes.Count; i++)
            {
                total += Measure(nodes[i], float.PositiveInfinity, rowHeight).Width + (i > 0 ? Gap : 0f);
            }

            widest = Math.Max(widest, total);
        }

        StripWidth = widest > 0f ? MathF.Ceiling(widest + (2f * Padding)) : 0f;
    }

    private static Size Measure(Node node, float maxWidth, float rowHeight)
    {
        LayoutSolver.PerformLayout(node, LayoutConstraints.Loose(new Size(maxWidth, rowHeight)));
        return node.LayoutData.MeasuredSize;
    }

    /// <summary>The last-painted action <paramref name="index"/> of <paramref name="row"/>, if it was painted.</summary>
    internal bool TryGetPainted(int row, int index, out PaintedRowAction action)
    {
        foreach (var candidate in painted)
        {
            if (candidate.Row == row && candidate.Index == index)
            {
                action = candidate;
                return true;
            }
        }

        action = default;
        return false;
    }

    /// <summary>
    /// The action under a point of row <paramref name="row"/> (<paramref name="relX"/> from the
    /// table's left edge, <paramref name="relY"/> from the row's top edge) and the control inside
    /// it that the point lands on — the deepest interactive node, as everywhere else.
    /// </summary>
    internal bool TryHit(int row, float relX, float relY, out PaintedRowAction action, out Node target)
    {
        foreach (var candidate in painted)
        {
            if (candidate.Row != row || !candidate.Contains(relX, relY))
            {
                continue;
            }

            var nodeBounds = candidate.Node.LayoutData.Bounds;
            float nodeX = nodeBounds.X + (relX - candidate.Bounds.X);
            float nodeY = nodeBounds.Y + (relY - candidate.Bounds.Y);
            action = candidate;
            target = HitTester.HitTest(candidate.Node, nodeX, nodeY) ?? candidate.Node;
            return true;
        }

        action = default;
        target = null!;
        return false;
    }

    /// <summary>
    /// The control that activating action <paramref name="node"/> from the keyboard should press:
    /// the node itself when it is interactive, otherwise its first interactive descendant.
    /// </summary>
    internal static Node? FindActivatable(Node node)
    {
        if (HitTester.IsInteractive(node))
        {
            return node;
        }

        foreach (var child in NodeDiffer.GetChildren(node))
        {
            if (child is not null && FindActivatable(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Whether action <paramref name="node"/> is disabled (its activatable control is).</summary>
    internal static bool IsDisabled(Node node)
    {
        return FindActivatable(node) switch
        {
            Button button => button.IsDisabled,
            IconButton iconButton => iconButton.IsDisabled,
            LinkButton link => link.IsDisabled,
            null => true,
            _ => node.LayoutData.A11yDisabled,
        };
    }

    /// <summary>
    /// Carries the measured strip width and the keyboard position over from the node this one
    /// replaces (same data source). The nodes themselves are not carried: they were built by the
    /// previous <c>Render()</c> and may close over its state.
    /// </summary>
    internal void AdoptFrom(TabularRowActions previous)
    {
        StripWidth = previous.StripWidth;
        IsMeasured = previous.IsMeasured;
        FocusedIndex = previous.FocusedIndex;
    }
}

/// <summary>
/// <see cref="TabularRowActions"/> over items of type <typeparamref name="T"/>. Nodes are cached per
/// data row together with the item they were built for, so a row whose item changed (an insert
/// shifted the list) is rebuilt instead of showing another item's buttons.
/// </summary>
internal sealed class TabularRowActions<T> : TabularRowActions
{
    private readonly Func<T, IReadOnlyList<Node>> factory;
    private readonly Func<IReadOnlyList<T>> items;
    private readonly Func<int, int> dataRowOf;
    private Entry[]? cache;

    private struct Entry
    {
        public bool Built;
        public T Item;
        public IReadOnlyList<Node> Nodes;
    }

    internal TabularRowActions(Func<T, IReadOnlyList<Node>> factory, Func<IReadOnlyList<T>> items, Func<int, int> dataRowOf)
    {
        this.factory = factory;
        this.items = items;
        this.dataRowOf = dataRowOf;
    }

    internal override IReadOnlyList<Node> ForDisplayRow(int displayRow)
    {
        var list = items();
        if (displayRow < 0)
        {
            return [];
        }

        int dataRow = dataRowOf(displayRow);
        if ((uint)dataRow >= (uint)list.Count)
        {
            return [];
        }

        if (cache is null || cache.Length != list.Count)
        {
            cache = new Entry[list.Count];
        }

        T item = list[dataRow];
        ref var entry = ref cache[dataRow];
        if (entry.Built && (ReferenceEquals(entry.Item, item) || EqualityComparer<T>.Default.Equals(entry.Item, item)))
        {
            return entry.Nodes;
        }

        entry.Built = true;
        entry.Item = item;
        entry.Nodes = factory(item) ?? [];
        return entry.Nodes;
    }
}
