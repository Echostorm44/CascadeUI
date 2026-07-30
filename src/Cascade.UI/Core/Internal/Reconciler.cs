namespace Cascade.UI;

/// <summary>
/// The tree reconciler that takes old and new node trees from
/// <see cref="Component.Render"/>, diffs them to compute minimal updates,
/// and applies updates in-place to the live tree.
/// </summary>
/// <remarks>
/// <para>
/// The reconciler handles: same-type node reuse, type changes (unmount old,
/// mount new), keyed list reordering, and Component instance preservation.
/// </para>
/// <para>
/// When a Component appears in both old and new trees (same type and key),
/// the reconciler reuses the existing <see cref="ComponentHost"/> and triggers
/// a re-render on it rather than mounting a new instance.
/// </para>
/// </remarks>
internal sealed class Reconciler
{
    private readonly RenderScheduler scheduler;
    private readonly ComponentHost? parentHost;

    internal Reconciler(RenderScheduler scheduler, ComponentHost? parentHost = null)
    {
        this.scheduler = scheduler;
        this.parentHost = parentHost;
    }

    /// <summary>
    /// Reconciles an old rendered tree with a new rendered tree. Returns the
    /// resulting tree with Component instances preserved where types match.
    /// Mounts new components and unmounts removed components.
    /// </summary>
    internal Node Reconcile(
        Node? oldTree,
        Node newTree,
        Dictionary<string, ComponentHost> oldHosts,
        Dictionary<string, ComponentHost> newHosts,
        int depth)
    {
        var plan = NodeDiffer.Diff(oldTree, newTree);

        if (!plan.HasChanges && oldTree is not null)
        {
            return oldTree;
        }

        foreach (var op in plan.Operations)
        {
            ApplyOperation(op, oldHosts, newHosts, depth);
        }

        WalkForNewComponents(newTree, oldHosts, newHosts, depth);

        // If the root node is a reused Component, return the old instance
        // (which has RenderedTree set) so layout can traverse through it.
        if (newTree is Component newComp)
        {
            var key = GetComponentKey(newComp);
            if (newHosts.TryGetValue(key, out var host) && host.Component != newComp)
            {
                return host.Component;
            }
        }

        return newTree;
    }

    /// <summary>
    /// Performs initial mount reconciliation — walks the entire new tree
    /// and mounts any Component nodes found.
    /// </summary>
    internal void MountTree(Node tree, Dictionary<string, ComponentHost> hosts, int depth)
    {
        WalkAndMount(tree, hosts, depth, index: 0);
    }

    /// <summary>
    /// Unmounts all components in the given host map.
    /// </summary>
    internal static void UnmountAll(Dictionary<string, ComponentHost> hosts)
    {
        foreach (var host in hosts.Values)
        {
            if (host.IsMounted)
            {
                host.Unmount();
            }
        }

        hosts.Clear();
    }

    private void ApplyOperation(
        DiffOperation op,
        Dictionary<string, ComponentHost> oldHosts,
        Dictionary<string, ComponentHost> newHosts,
        int depth)
    {
        switch (op.Kind)
        {
            case DiffKind.Update:
                TransferInteractiveState(op.OldNode!, op.NewNode!);
                break;

            case DiffKind.Delete:
                HandleDelete(op.OldNode!, oldHosts);
                break;

            case DiffKind.Replace:
                HandleDelete(op.OldNode!, oldHosts);
                break;

            case DiffKind.ReuseComponent:
                HandleReuseComponent(op, oldHosts, newHosts, depth);
                break;
        }
    }

    /// <summary>
    /// Transfers interactive state from an old node to its replacement so that
    /// focus, hover, press, and animation state survive tree rebuilds.
    /// </summary>
    private static void TransferInteractiveState(Node from, Node to)
    {
        to.IsHovered = from.IsHovered;
        to.IsPressed = from.IsPressed;
        FocusManager.NotifyNodeReplaced(from, to);
        InputDispatcher.NotifyNodeReplaced(from, to);
        ControlStateAnimator.TransferState(from, to);
        // Carry a chart's entrance-animation slot to its replacement so an in-place
        // re-render reads as the same slot (data change) rather than a fresh entrance.
        ChartAnimationTracker.TransferState(from, to);

        // Preserve scroll position and layer state across re-renders
        if (from is ScrollView oldSv && to is ScrollView newSv)
        {
            newSv.OffsetY = oldSv.OffsetY;
            newSv.MaxY = oldSv.MaxY;
            newSv.ScrollbarTrackBounds = oldSv.ScrollbarTrackBounds;
            newSv.ScrollbarThumbHeight = oldSv.ScrollbarThumbHeight;
            newSv.Control.IsLayerDirty = oldSv.Control.IsLayerDirty;
            newSv.Control.LayerHandle = oldSv.Control.LayerHandle;
            newSv.Control.LayerWidth = oldSv.Control.LayerWidth;
            newSv.Control.LayerHeight = oldSv.Control.LayerHeight;
            newSv.Control.CapturedThemeVersion = oldSv.Control.CapturedThemeVersion;
            newSv.Control.CapturedFocusedEditable = oldSv.Control.CapturedFocusedEditable;
        }

        // Preserve a virtualized ListView's scroll offset across re-renders so the
        // list doesn't jump back to the top when its data changes.
        if (from is IListViewNode oldLv && to is IListViewNode newLv)
        {
            newLv.OffsetY = oldLv.OffsetY;
        }

        // Preserve an uncontrolled Expander's open/closed state across re-renders so a
        // section the user opened stays open (the new node is seeded to its initial value).
        if (from is Expander oldExp && to is Expander newExp && newExp.ExpandedBind.OnChange == null)
        {
            newExp.ExpandedState = oldExp.ExpandedState;
        }

        // Preserve DateRangePicker popup state across re-renders so the calendar
        // popup survives Invalidate() cycles triggered by date selection callbacks.
        if (from is DateRangePicker oldDrp && to is DateRangePicker newDrp)
        {
            newDrp.IsCalendarOpen = oldDrp.IsCalendarOpen;
            newDrp.DisplayedMonth = oldDrp.DisplayedMonth;
            newDrp.DisplayedYear = oldDrp.DisplayedYear;
            newDrp.SelectionPhase = oldDrp.SelectionPhase;
            newDrp.HighlightedDayLeft = oldDrp.HighlightedDayLeft;
            newDrp.HighlightedDayRight = oldDrp.HighlightedDayRight;
            newDrp.HoverDate = oldDrp.HoverDate;
            newDrp.HighlightedPreset = oldDrp.HighlightedPreset;
            newDrp.CalendarBounds = oldDrp.CalendarBounds;
            newDrp.CalendarCellSize = oldDrp.CalendarCellSize;
            newDrp.CalendarGridTop = oldDrp.CalendarGridTop;
            newDrp.LeftGridLeft = oldDrp.LeftGridLeft;
            newDrp.RightGridLeft = oldDrp.RightGridLeft;
            newDrp.LeftGridStartDate = oldDrp.LeftGridStartDate;
            newDrp.RightGridStartDate = oldDrp.RightGridStartDate;
            newDrp.PrevMonthBounds = oldDrp.PrevMonthBounds;
            newDrp.NextMonthBounds = oldDrp.NextMonthBounds;
            newDrp.PresetBounds = oldDrp.PresetBounds;
        }

        // Preserve DatePicker popup state across re-renders
        if (from is DatePicker oldDp && to is DatePicker newDp)
        {
            newDp.IsCalendarOpen = oldDp.IsCalendarOpen;
            newDp.DisplayedMonth = oldDp.DisplayedMonth;
            newDp.DisplayedYear = oldDp.DisplayedYear;
            newDp.CalendarBounds = oldDp.CalendarBounds;
            newDp.CalendarCellSize = oldDp.CalendarCellSize;
            newDp.CalendarGridTop = oldDp.CalendarGridTop;
            newDp.CalendarGridLeft = oldDp.CalendarGridLeft;
            newDp.CalendarGridStartDate = oldDp.CalendarGridStartDate;
            newDp.CalendarGridCellCount = oldDp.CalendarGridCellCount;
            newDp.HighlightedDay = oldDp.HighlightedDay;
            newDp.ViewMode = oldDp.ViewMode;
            newDp.YearGridStart = oldDp.YearGridStart;
            newDp.PrevMonthBounds = oldDp.PrevMonthBounds;
            newDp.NextMonthBounds = oldDp.NextMonthBounds;
            newDp.HeaderLabelBounds = oldDp.HeaderLabelBounds;
        }

        // Preserve DateTimePicker popup state across re-renders
        if (from is DateTimePicker oldDtp && to is DateTimePicker newDtp)
        {
            newDtp.IsCalendarOpen = oldDtp.IsCalendarOpen;
            newDtp.DisplayedMonth = oldDtp.DisplayedMonth;
            newDtp.DisplayedYear = oldDtp.DisplayedYear;
            newDtp.CalendarBounds = oldDtp.CalendarBounds;
            newDtp.CalendarCellSize = oldDtp.CalendarCellSize;
            newDtp.CalendarGridTop = oldDtp.CalendarGridTop;
            newDtp.CalendarGridLeft = oldDtp.CalendarGridLeft;
            newDtp.CalendarGridStartDate = oldDtp.CalendarGridStartDate;
            newDtp.HighlightedDay = oldDtp.HighlightedDay;
            newDtp.SelectedDate = oldDtp.SelectedDate;
            newDtp.SelectedHour = oldDtp.SelectedHour;
            newDtp.SelectedMinute = oldDtp.SelectedMinute;
            newDtp.IsPm = oldDtp.IsPm;
            newDtp.PrevMonthBounds = oldDtp.PrevMonthBounds;
            newDtp.NextMonthBounds = oldDtp.NextMonthBounds;
            newDtp.HourUpBounds = oldDtp.HourUpBounds;
            newDtp.HourDownBounds = oldDtp.HourDownBounds;
            newDtp.MinuteUpBounds = oldDtp.MinuteUpBounds;
            newDtp.MinuteDownBounds = oldDtp.MinuteDownBounds;
            newDtp.AmPmBounds = oldDtp.AmPmBounds;
        }

        // Preserve DataGrid/DataTable cell text cache across re-renders when
        // the backing data source is reference-equal. The cache is a pure
        // perf optimization; identity-mismatch causes a fresh build which
        // is always correct.
        if (from is ITabularDataNode oldTab && to is ITabularDataNode newTab)
        {
            if (oldTab.CellTextCache != null
                && oldTab.CellTextCacheKey != null
                && ReferenceEquals(oldTab.CellTextCacheKey, newTab.CellTextCacheKey))
            {
                newTab.CellTextCache = oldTab.CellTextCache;
            }
        }

        // Preserve HeatMapChart layout cache (rowLabels, colLabels, grid,
        // min/max) when the backing cellsList is reference-equal. Same
        // identity-key pattern as the tabular cache above.
        if (from is HeatMapChart oldHeat && to is HeatMapChart newHeat)
        {
            if (oldHeat.layoutCache != null
                && oldHeat.layoutCacheKey != null
                && ReferenceEquals(oldHeat.layoutCacheKey, newHeat.layoutCacheKey))
            {
                newHeat.layoutCache = oldHeat.layoutCache;
            }
        }

        // Preserve AreaChart paint caches (x-axis labels, gridline labels,
        // pooled float buffers) when seriesList identity is unchanged.
        if (from is AreaChart oldArea && to is AreaChart newArea)
        {
            if (oldArea.paintCacheKey != null
                && ReferenceEquals(oldArea.paintCacheKey, newArea.paintCacheKey))
            {
                newArea.xLabelCache = oldArea.xLabelCache;
                newArea.gridLabelCache = oldArea.gridLabelCache;
                newArea.gridCeilKey = oldArea.gridCeilKey;
                newArea.gridFloorKey = oldArea.gridFloorKey;
                newArea.yPositionsPool = oldArea.yPositionsPool;
                newArea.prevBaselinePool = oldArea.prevBaselinePool;
            }
        }

        // Preserve Select dropdown open state across re-renders so the dropdown
        // survives Invalidate() cycles triggered by selection in other controls.
        if (from is ISelectNode oldSelect && to is ISelectNode newSelect)
        {
            newSelect.IsOpen = oldSelect.IsOpen;
            newSelect.HighlightedIndex = oldSelect.HighlightedIndex;
            newSelect.ScrollOffset = oldSelect.ScrollOffset;
        }

        // Preserve Combobox dropdown open state across re-renders
        if (from is IComboboxNode oldCb && to is IComboboxNode newCb)
        {
            newCb.IsOpen = oldCb.IsOpen;
            newCb.HighlightedIndex = oldCb.HighlightedIndex;
            newCb.ScrollOffset = oldCb.ScrollOffset;
            newCb.SearchText = oldCb.SearchText;
        }

        // Preserve MultiSelect dropdown open state across re-renders
        if (from is IMultiSelectNode oldMs && to is IMultiSelectNode newMs)
        {
            newMs.IsOpen = oldMs.IsOpen;
            newMs.HighlightedIndex = oldMs.HighlightedIndex;
            newMs.ScrollOffset = oldMs.ScrollOffset;
            NodePainter.TransferPillAnimState((Node)oldMs, (Node)newMs);
        }
    }

    private void WalkForNewComponents(
        Node tree,
        Dictionary<string, ComponentHost> oldHosts,
        Dictionary<string, ComponentHost> newHosts,
        int depth)
    {
        if (tree is Component component)
        {
            var key = GetComponentKey(component);
            if (!newHosts.ContainsKey(key))
            {
                if (oldHosts.TryGetValue(key, out var existing) && existing.IsMounted)
                {
                    newHosts[key] = existing;
                    oldHosts.Remove(key);
                }
                else
                {
                    var host = new ComponentHost(component, scheduler, depth + 1, parentHost);
                    newHosts[key] = host;
                    host.Mount();
                    host.CompleteMountAsync();
                }
            }

            return;
        }

        var children = NodeDiffer.GetChildren(tree);
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child is Component childComp && childComp.ReconciliationKey is null)
            {
                var key = GetComponentKey(childComp, i);
                if (!newHosts.ContainsKey(key))
                {
                    if (oldHosts.TryGetValue(key, out var existing) && existing.IsMounted)
                    {
                        newHosts[key] = existing;
                        oldHosts.Remove(key);
                    }
                    else
                    {
                        var host = new ComponentHost(childComp, scheduler, depth + 1, parentHost);
                        newHosts[key] = host;
                        host.Mount();
                        host.CompleteMountAsync();
                    }
                }
            }
            else
            {
                WalkForNewComponents(child, oldHosts, newHosts, depth);
            }
        }
    }

    private void HandleDelete(Node node, Dictionary<string, ComponentHost> oldHosts)
    {
        if (node is Component component)
        {
            var key = GetComponentKey(component);
            if (oldHosts.TryGetValue(key, out var host))
            {
                if (host.IsMounted)
                {
                    scheduler.RemoveDirty(host);
                    host.Unmount();
                }

                oldHosts.Remove(key);
            }

            return;
        }

        // A NavigationTransitionHost is deleted when its transition completes and
        // the navigator returns the incoming page directly. That incoming page
        // survives — it becomes the navigator's child and is re-claimed by
        // WalkForNewComponents — so it must NOT be unmounted here, or it would be
        // dropped and remounted, firing OnMounted twice per navigation. The
        // outgoing tree is a frozen snapshot that was never mounted. (If the
        // navigator itself is torn down, UnmountAll cleans up the incoming by key.)
        if (node is NavigationTransitionHost)
        {
            return;
        }

        var children = NodeDiffer.GetChildren(node);
        for (int i = 0; i < children.Count; i++)
        {
            HandleDelete(children[i], oldHosts);
        }
    }

    private void HandleReuseComponent(
        DiffOperation op,
        Dictionary<string, ComponentHost> oldHosts,
        Dictionary<string, ComponentHost> newHosts,
        int depth)
    {
        var oldComponent = (Component)op.OldNode!;
        var key = GetComponentKey(oldComponent);

        if (oldHosts.TryGetValue(key, out var host))
        {
            newHosts[key] = host;
            oldHosts.Remove(key);
            // Parent re-rendered and produced this child again — mark dirty
            // so it re-renders to pick up changed captured state.
            scheduler.MarkDirty(host);
        }
    }

    private void WalkAndMount(Node node, Dictionary<string, ComponentHost> hosts, int depth, int index)
    {
        if (node is Component component)
        {
            var key = GetComponentKey(component, index);
            var host = new ComponentHost(component, scheduler, depth + 1, parentHost);
            hosts[key] = host;
            host.Mount();
            host.CompleteMountAsync();
            return;
        }

        var children = NodeDiffer.GetChildren(node);
        for (int i = 0; i < children.Count; i++)
        {
            WalkAndMount(children[i], hosts, depth + 1, i);
        }
    }

    internal static string GetComponentKey(Component component, int positionalIndex = 0)
    {
        if (component.ReconciliationKey is not null)
        {
            return $"{component.GetType().Name}:{component.ReconciliationKey}";
        }

        return $"{component.GetType().Name}@{positionalIndex}";
    }
}
