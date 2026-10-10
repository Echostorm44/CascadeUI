namespace Cascade.UI;

/// <summary>
/// Performs point-to-node hit testing on the laid-out node tree.
/// Walks depth-first (reverse child order so topmost children are tested first)
/// and returns the deepest visible node whose layout bounds contain the point.
/// </summary>
internal static class HitTester
{
    /// <summary>
    /// Finds the deepest node at the given point. Returns null if no node contains the point.
    /// </summary>
    internal static Node? HitTest(Node root, float x, float y)
    {
        if (root.IsLayoutEmpty || !root.LayoutData.IsVisible)
        {
            return null;
        }

        return HitTestCore(root, x, y, null);
    }

    // Hit-tests node, recording in `path` (when given) every node the search descended into that
    // contains the point, outermost first: the press's ancestry, which nodes do not store.
    private static Node? HitTestCore(Node node, float x, float y, List<Node>? path)
    {
        if (path is null)
        {
            return HitTestNode(node, x, y, null);
        }

        int mark = path.Count;
        path.Add(node);
        Node? hit = HitTestNode(node, x, y, path);
        if (hit is null)
        {
            path.RemoveRange(mark, path.Count - mark);
        }
        return hit;
    }

    private static Node? HitTestNode(Node node, float x, float y, List<Node>? path)
    {
        // NavigationTransitionHost: only hit-test the incoming page
        if (node is NavigationTransitionHost nth)
        {
            if (nth.IncomingPage is not null)
            {
                return HitTestCore(nth.IncomingPage, x, y, path);
            }

            return null;
        }

        // Component nodes: traverse into the rendered tree transparently
        if (node is Component comp && comp.RenderedTree is { } rendered)
        {
            // Nested components have real bounds — check and transform coordinates.
            // Root component has zero-size bounds, so pass coordinates through as-is.
            var compBounds = node.LayoutData.Bounds;
            float cx = x;
            float cy = y;
            if (compBounds.Width > 0 || compBounds.Height > 0)
            {
                if (!compBounds.Contains(new Point(x, y)))
                {
                    return null;
                }

                cx = x - compBounds.X;
                cy = y - compBounds.Y;
            }

            if (!rendered.IsLayoutEmpty && rendered.LayoutData.IsVisible)
            {
                return HitTestCore(rendered, cx, cy, path);
            }

            return null;
        }

        var data = node.LayoutData;
        var bounds = data.Bounds;

        // Check if the point is within this node's bounds
        if (!bounds.Contains(new Point(x, y)))
        {
            return null;
        }

        // Transform to local coordinates for child hit testing.
        // Layout bounds are relative to parent, so children's bounds
        // are expressed in this node's local coordinate space.
        // Padding shifts children into the content area, so subtract it
        // to convert from node-space to content-space coordinates.
        var padding = data.Padding;
        float localX = x - bounds.X - padding.Left;
        float localY = y - bounds.Y - padding.Top;

        // Check children in reverse order (last child = visually topmost)
        var children = GetChildren(node);
        if (children != null)
        {
            for (int i = children.Count - 1; i >= 0; i--)
            {
                var child = children[i];
                if (child.IsLayoutEmpty || !child.LayoutData.IsVisible)
                {
                    continue;
                }

                var hit = HitTestCore(child, localX, localY, path);
                if (hit != null)
                {
                    // If the hit child is interactive, it takes priority
                    if (IsInteractive(hit))
                    {
                        return hit;
                    }

                    // If the hit child is passive but this node has gesture
                    // handlers, return this node so the gesture fires
                    if (IsInteractive(node))
                    {
                        return node;
                    }

                    return hit;
                }
            }
        }

        // ScrollView — single child with scroll offset adjustment
        if (node is ScrollView scrollView)
        {
            var content = scrollView.Content;
            if (content != null && !content.IsLayoutEmpty && content.LayoutData.IsVisible)
            {
                // Content is translated by -scrollY during paint, so add scrollY
                // to convert viewport coordinates back to content coordinates.
                float scrollY = scrollView.OffsetY;
                var hit = HitTestCore(content, localX, localY + scrollY, path);
                if (hit != null)
                {
                    return hit;
                }
            }

            return node;
        }

        // SplitView — two children (First and Second) with relative bounds
        if (node is SplitView splitView)
        {
            // Test second pane first (painted last, visually on top)
            if (!splitView.Second.IsLayoutEmpty && splitView.Second.LayoutData.IsVisible)
            {
                var hit = HitTestCore(splitView.Second, localX, localY, path);
                if (hit != null)
                {
                    return hit;
                }
            }

            if (!splitView.First.IsLayoutEmpty && splitView.First.LayoutData.IsVisible)
            {
                var hit = HitTestCore(splitView.First, localX, localY, path);
                if (hit != null)
                {
                    return hit;
                }
            }

            // No child hit — this is the divider area
            return node;
        }

        // Single-child containers
        var singleChild = GetSingleChild(node);
        if (singleChild != null && !singleChild.IsLayoutEmpty && singleChild.LayoutData.IsVisible)
        {
            // If this node is interactive (e.g. Card with ClickHandler), return it
            // directly rather than diving into non-interactive children, since
            // FindParent cannot walk back up from a child to find us.
            if (IsInteractive(node))
            {
                return node;
            }

            var hit = HitTestCore(singleChild, localX, localY, path);
            if (hit != null)
            {
                return hit;
            }
        }

        return node;
    }

    /// <summary>
    /// Walks from the hit node up toward the root, finding the first ancestor (or self)
    /// that has a gesture handler or is an interactive control (Button, TextInput, etc.).
    /// </summary>
    internal static Node? FindInteractiveAncestor(Node node)
    {
        Node? current = node;
        while (current != null)
        {
            if (IsInteractive(current))
            {
                return current;
            }

            current = FindParent(current);
        }

        return null;
    }

    /// <summary>
    /// The window drag area (<c>.WindowDragArea()</c>) a press at (<paramref name="x"/>,
    /// <paramref name="y"/>) lands in, or null when there is none or an interactive node (a button,
    /// a text box, a list) sits between the pressed node and the area: pressing a control inside a
    /// drag area works the control.
    /// </summary>
    internal static Node? FindWindowDragAreaAt(Node root, float x, float y)
    {
        if (root.IsLayoutEmpty || !root.LayoutData.IsVisible)
        {
            return null;
        }

        var path = new List<Node>();
        if (HitTestCore(root, x, y, path) is null)
        {
            return null;
        }
        for (int i = path.Count - 1; i >= 0; i--)
        {
            Node node = path[i];
            if (IsInteractive(node) || node is IListViewNode)
            {
                return null;
            }
            if (node.LayoutData.GestureData is { WindowDrag: true })
            {
                return node;
            }
        }
        return null;
    }

    internal static bool IsInteractive(Node node)
    {
        // Nodes with gesture handlers are interactive
        if (node.LayoutData.GestureData != null)
        {
            var g = node.LayoutData.GestureData;
            if (g.Tap != null || g.DoubleTap != null || g.LongPress != null ||
                g.PointerDown != null || g.PointerUp != null || g.PointerMove != null ||
                g.PointerEnter != null || g.PointerLeave != null ||
                g.Scroll != null || g.Pan != null || g.ContextMenu != null)
            {
                return true;
            }
        }

        // Known interactive control types
        return node is Button or LinkButton or IconButton or
               TextInput or TextArea or PasswordInput or PinInput or
               MentionInput or TagInput or
               Checkbox or Toggle or Slider or RangeSlider or Rating or HotkeyPicker or
               IRadioButton or
               SplitView or
               Expander or
               ISegmentedControl or
               IToggleGroup or
               INumberInput or
               ISelectNode or
               DatePicker or
               TabBar or
               ToolBar or
               Breadcrumb or
               Banner { OnDismiss: not null } or
               StepIndicator { StepClickHandler: not null } or
               Card { ClickHandler: not null } or
               Tag { OnToggle: not null } or
               Tag { OnRemove: not null } or
               ITabularDataNode;
        // NOTE: IListViewNode is intentionally NOT here — it delegates to its
        // rendered content (see GetSingleChild), so hit-testing must descend into
        // the rows (e.g. a per-row remove button) rather than stop at the list.
    }

    internal static IReadOnlyList<Node>? GetChildren(Node node)
    {
        return node switch
        {
            Row row => row.Children,
            Column col => col.Children,
            Stack stack => stack.Children,
            Grid grid => grid.Children,
            Accordion acc => acc.Sections,
            _ => null
        };
    }

    internal static Node? GetSingleChild(Node node)
    {
        return node switch
        {
            Center center       => center.Child,
            Card card           => card.Content,
            KeyHandler kh       => kh.Content,
            Badge badge         => badge.Child,
            Expander expander   => expander.Content,
            IRadioGroup rg      => rg.Content,
            IRadioButton rb when !rb.NodeLabel.IsLayoutEmpty => rb.NodeLabel,
            FormValidator fv    => fv.Content,
            AnimatePresence ap  => ap.Child,
            IListViewNode lvn   => lvn.GetContentNode(),
            ITreeView tv        => tv.GetContentNode(),
            _ => null
        };
    }

    /// <summary>
    /// Finds the parent of a node by walking up through the tree.
    /// Returns null if the node has no tracked parent.
    /// </summary>
    private static Node? FindParent(Node node)
    {
        // LayoutNodeData does not store a parent reference.
        // For now, return null — parent walking is handled at the dispatch level
        // by tracking the path during hit testing.
        return null;
    }

    /// <summary>
    /// Finds a reorderable ListView whose (painter-stamped) reorder bounds contain
    /// the point. Used to start a control-level drag-to-reorder — the dragged rows
    /// are built content (not in the reconciled tree), so we locate the list node
    /// itself rather than walking up from the hit row.
    /// </summary>
    internal static IListViewNode? FindReorderableListViewAt(Node root, float x, float y)
    {
        return FindListViewAt(root, x, y, static lv => lv.IsReorderable);
    }

    /// <summary>
    /// Finds a virtualized ListView (owns its own scroll offset, content overflows)
    /// whose bounds contain the point. Used to route wheel events to the list.
    /// </summary>
    internal static IListViewNode? FindScrollableListViewAt(Node root, float x, float y)
    {
        return FindListViewAt(root, x, y, static lv => lv.MaxY > 0f);
    }

    /// <summary>Finds a selectable ListView whose bounds contain the point (click-to-select).</summary>
    internal static IListViewNode? FindSelectableListViewAt(Node root, float x, float y)
    {
        return FindListViewAt(root, x, y, static lv => lv.IsSelectable);
    }

    /// <summary>
    /// Finds a ListView with swipe actions whose bounds contain the point. Used to
    /// arm a control-level horizontal swipe.
    /// </summary>
    internal static IListViewNode? FindSwipeableListViewAt(Node root, float x, float y)
    {
        return FindListViewAt(root, x, y, static lv => lv.HasSwipeActions);
    }

    /// <summary>Finds a ListView with a per-item context menu whose bounds contain the point (right-click).</summary>
    internal static IListViewNode? FindContextMenuListViewAt(Node root, float x, float y)
    {
        return FindListViewAt(root, x, y, static lv => lv.HasItemContextMenu);
    }

    /// <summary>
    /// The top-most ListView matching <paramref name="predicate"/> whose painter-stamped
    /// <see cref="IListViewNode.ReorderBounds"/> (absolute) contain the point. Lists are
    /// control-level hit targets: their rows are built content, so the list is located itself.
    /// </summary>
    private static IListViewNode? FindListViewAt(Node root, float x, float y, Func<IListViewNode, bool> predicate)
    {
        if (root is Component comp && comp.RenderedTree is { } rendered)
        {
            return FindListViewAt(rendered, x, y, predicate);
        }

        if (root is IListViewNode lv && predicate(lv) && lv.ReorderBounds.Contains(new Point(x, y)))
        {
            return lv;
        }

        var children = GetChildren(root);
        if (children != null)
        {
            for (int i = children.Count - 1; i >= 0; i--)
            {
                var hit = FindListViewAt(children[i], x, y, predicate);
                if (hit != null)
                {
                    return hit;
                }
            }
        }

        if (root is ScrollView sv && sv.Content != null)
        {
            var hit = FindListViewAt(sv.Content, x, y, predicate);
            if (hit != null)
            {
                return hit;
            }
        }

        if (root is SplitView split)
        {
            return FindListViewAt(split.First, x, y, predicate)
                ?? FindListViewAt(split.Second, x, y, predicate);
        }

        var single = GetSingleChild(root);
        return single != null ? FindListViewAt(single, x, y, predicate) : null;
    }

    /// <summary>
    /// The window-logical bounds of <paramref name="target"/>: the same walk as
    /// <see cref="HitTest"/> (component offsets, padding, ScrollView scroll offsets, split panes,
    /// list rows), summing each ancestor's position. False when the node is not in the tree.
    /// </summary>
    internal static bool TryGetAbsoluteBounds(Node root, Node target, out Rect bounds)
    {
        return TryGetAbsoluteBoundsCore(root, target, 0f, 0f, out bounds);
    }

    private static bool TryGetAbsoluteBoundsCore(Node node, Node target, float originX, float originY, out Rect bounds)
    {
        bounds = default;
        if (node.IsLayoutEmpty)
        {
            return false;
        }

        var data = node.LayoutData;
        var b = data.Bounds;

        if (node is Component comp && comp.RenderedTree is { } rendered)
        {
            if (ReferenceEquals(node, target))
            {
                bounds = new Rect(originX + b.X, originY + b.Y, b.Width, b.Height);
                return true;
            }

            return TryGetAbsoluteBoundsCore(rendered, target, originX + b.X, originY + b.Y, out bounds);
        }

        float x = originX + b.X;
        float y = originY + b.Y;
        if (ReferenceEquals(node, target))
        {
            bounds = new Rect(x, y, b.Width, b.Height);
            return true;
        }

        float contentX = x + data.Padding.Left;
        float contentY = y + data.Padding.Top;

        var children = GetChildren(node);
        if (children != null)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (TryGetAbsoluteBoundsCore(children[i], target, contentX, contentY, out bounds))
                {
                    return true;
                }
            }
        }

        if (node is ScrollView scrollView && scrollView.Content is { } content)
        {
            return TryGetAbsoluteBoundsCore(content, target, contentX, contentY - scrollView.OffsetY, out bounds);
        }

        if (node is SplitView splitView)
        {
            return TryGetAbsoluteBoundsCore(splitView.First, target, contentX, contentY, out bounds)
                || TryGetAbsoluteBoundsCore(splitView.Second, target, contentX, contentY, out bounds);
        }

        var single = GetSingleChild(node);
        return single != null && TryGetAbsoluteBoundsCore(single, target, contentX, contentY, out bounds);
    }
    /// <summary>
    /// Finds the innermost ScrollView whose bounds contain the given point.
    /// Used by InputDispatcher to route scroll events to the correct ScrollView.
    /// </summary>
    internal static ScrollView? FindScrollViewAt(Node root, float x, float y)
    {
        if (root.IsLayoutEmpty || !root.LayoutData.IsVisible)
        {
            return null;
        }

        return FindScrollViewCore(root, x, y);
    }

    private static ScrollView? FindScrollViewCore(Node node, float x, float y)
    {
        // Component nodes: traverse into the rendered tree transparently
        if (node is Component comp && comp.RenderedTree is { } rendered)
        {
            if (!rendered.IsLayoutEmpty && rendered.LayoutData.IsVisible)
            {
                return FindScrollViewCore(rendered, x, y);
            }

            return null;
        }

        var bounds = node.LayoutData.Bounds;
        if (!bounds.Contains(new Point(x, y)))
        {
            return null;
        }

        if (node is ScrollView sv)
        {
            return sv;
        }

        float localX = x - bounds.X - node.LayoutData.Padding.Left;
        float localY = y - bounds.Y - node.LayoutData.Padding.Top;

        // Check multi-children (Row, Column, Stack, Grid)
        var children = GetChildren(node);
        if (children != null)
        {
            for (int i = children.Count - 1; i >= 0; i--)
            {
                var child = children[i];
                if (child.IsLayoutEmpty || !child.LayoutData.IsVisible)
                {
                    continue;
                }

                var found = FindScrollViewCore(child, localX, localY);
                if (found != null)
                {
                    return found;
                }
            }
        }

        // Check single-child containers
        var singleChild = GetSingleChild(node);
        if (singleChild != null && !singleChild.IsLayoutEmpty && singleChild.LayoutData.IsVisible)
        {
            var found = FindScrollViewCore(singleChild, localX, localY);
            if (found != null)
            {
                return found;
            }
        }

        // SplitView — check both panes for nested ScrollViews
        if (node is SplitView splitView)
        {
            var found = FindScrollViewCore(splitView.Second, localX, localY);
            if (found != null)
            {
                return found;
            }

            found = FindScrollViewCore(splitView.First, localX, localY);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the deepest draggable node at the given point. Walks the tree looking
    /// for nodes with <c>DragData.IsDraggable == true</c>. Returns the closest
    /// (deepest) draggable ancestor of the leaf node at that position.
    /// </summary>
    internal static Node? FindDraggableAt(Node root, float x, float y)
    {
        if (root.IsLayoutEmpty || !root.LayoutData.IsVisible)
        {
            return null;
        }

        return FindDraggableCore(root, x, y);
    }

    private static Node? FindDraggableCore(Node node, float x, float y)
    {
        // Component nodes: traverse into the rendered tree transparently
        if (node is Component comp && comp.RenderedTree is { } rendered)
        {
            if (!rendered.IsLayoutEmpty && rendered.LayoutData.IsVisible)
            {
                return FindDraggableCore(rendered, x, y);
            }

            return null;
        }

        var bounds = node.LayoutData.Bounds;
        if (!bounds.Contains(new Point(x, y)))
        {
            return null;
        }

        float localX = x - bounds.X - node.LayoutData.Padding.Left;
        float localY = y - bounds.Y - node.LayoutData.Padding.Top;

        // Check children first (depth-first, reverse order)
        var children = GetChildren(node);
        if (children != null)
        {
            for (int i = children.Count - 1; i >= 0; i--)
            {
                var child = children[i];
                if (child.IsLayoutEmpty || !child.LayoutData.IsVisible)
                {
                    continue;
                }

                var found = FindDraggableCore(child, localX, localY);
                if (found != null)
                {
                    return found;
                }
            }
        }

        // ScrollView — adjust for scroll offset
        if (node is ScrollView sv)
        {
            var content = sv.Content;
            if (content != null && !content.IsLayoutEmpty && content.LayoutData.IsVisible)
            {
                float scrollY = InputDispatcher.ScrollViewOffsetY;
                var found = FindDraggableCore(content, localX, localY + scrollY);
                if (found != null)
                {
                    return found;
                }
            }
        }

        // SplitView — check both panes
        if (node is SplitView splitView)
        {
            var found = FindDraggableCore(splitView.Second, localX, localY);
            if (found != null)
            {
                return found;
            }

            found = FindDraggableCore(splitView.First, localX, localY);
            if (found != null)
            {
                return found;
            }
        }

        // Check single-child containers
        var singleChild = GetSingleChild(node);
        if (singleChild != null && !singleChild.IsLayoutEmpty && singleChild.LayoutData.IsVisible)
        {
            var found = FindDraggableCore(singleChild, localX, localY);
            if (found != null)
            {
                return found;
            }
        }

        // Return this node if it's draggable
        if (node.LayoutData.DragData?.IsDraggable == true)
        {
            return node;
        }

        return null;
    }

    /// <summary>
    /// Finds the deepest drop target at the given point. Walks the tree looking
    /// for nodes with <c>DragData.IsDropTarget == true</c>.
    /// </summary>
    internal static Node? FindDropTargetAt(Node root, float x, float y)
    {
        if (root.IsLayoutEmpty || !root.LayoutData.IsVisible)
        {
            return null;
        }

        return FindDropTargetCore(root, x, y);
    }

    private static Node? FindDropTargetCore(Node node, float x, float y)
    {
        // Component nodes: traverse into the rendered tree transparently
        if (node is Component comp && comp.RenderedTree is { } rendered)
        {
            if (!rendered.IsLayoutEmpty && rendered.LayoutData.IsVisible)
            {
                return FindDropTargetCore(rendered, x, y);
            }

            return null;
        }

        var bounds = node.LayoutData.Bounds;
        if (!bounds.Contains(new Point(x, y)))
        {
            return null;
        }

        float localX = x - bounds.X - node.LayoutData.Padding.Left;
        float localY = y - bounds.Y - node.LayoutData.Padding.Top;

        // Check children first (depth-first, reverse order)
        var children = GetChildren(node);
        if (children != null)
        {
            for (int i = children.Count - 1; i >= 0; i--)
            {
                var child = children[i];
                if (child.IsLayoutEmpty || !child.LayoutData.IsVisible)
                {
                    continue;
                }

                var found = FindDropTargetCore(child, localX, localY);
                if (found != null)
                {
                    return found;
                }
            }
        }

        // ScrollView — adjust for scroll offset
        if (node is ScrollView sv)
        {
            var content = sv.Content;
            if (content != null && !content.IsLayoutEmpty && content.LayoutData.IsVisible)
            {
                float scrollY = InputDispatcher.ScrollViewOffsetY;
                var found = FindDropTargetCore(content, localX, localY + scrollY);
                if (found != null)
                {
                    return found;
                }
            }
        }

        // SplitView — check both panes
        if (node is SplitView splitView)
        {
            var found = FindDropTargetCore(splitView.Second, localX, localY);
            if (found != null)
            {
                return found;
            }

            found = FindDropTargetCore(splitView.First, localX, localY);
            if (found != null)
            {
                return found;
            }
        }

        var singleChild = GetSingleChild(node);
        if (singleChild != null && !singleChild.IsLayoutEmpty && singleChild.LayoutData.IsVisible)
        {
            var found = FindDropTargetCore(singleChild, localX, localY);
            if (found != null)
            {
                return found;
            }
        }

        if (node.LayoutData.DragData?.IsDropTarget == true)
        {
            return node;
        }

        return null;
    }
}
