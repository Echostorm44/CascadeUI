namespace Cascade.UI;

/// <summary>What a wheel event was routed to by <see cref="InputDispatcher.HandleScrollEvent"/>.</summary>
internal enum ScrollTargetKind
{
    /// <summary>Nothing took the event (no scrollable view under the pointer).</summary>
    None,

    /// <summary>An open context menu panel.</summary>
    Menu,

    /// <summary>An open Select / MultiSelect / Combobox dropdown; offsets count options, not pixels.</summary>
    Dropdown,

    /// <summary>An open calendar popup; the wheel changes the month/year shown, there is no offset.</summary>
    Calendar,

    /// <summary>The TextArea being edited.</summary>
    TextArea,

    /// <summary>A DataTable or DataGrid (they own their row scroll offset).</summary>
    Table,

    /// <summary>A virtualized ListView (it owns its scroll offset).</summary>
    ListView,

    /// <summary>A ScrollView.</summary>
    ScrollView,

    /// <summary>A node's own <c>OnScroll</c> gesture handler.</summary>
    Gesture,
}

/// <summary>
/// The result of the last wheel event: which view took it, its vertical offset and maximum after
/// the event, and whether the offset moved. The DevTools <c>scroll</c> verb reports this, so an
/// agent sees the offset of the list or grid that actually scrolled rather than the page's.
/// </summary>
internal readonly record struct ScrollOutcome(ScrollTargetKind Kind, Node? Target, float OffsetY, float MaxY, bool Moved)
{
    /// <summary>No view took the event.</summary>
    internal static readonly ScrollOutcome Nothing = new(ScrollTargetKind.None, null, 0f, 0f, false);
}
