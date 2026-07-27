namespace Cascade.UI;

/// <summary>
/// A tab bar control for switching between multiple navigators. Each tab
/// wraps its own <see cref="Navigator"/> — switching tabs is a selection
/// state change, not stack navigation.
/// </summary>
/// <remarks>
/// <code>
/// TabBar(
///     tabs: [
///         new Tab(icon: Icons.Home, label: "Home", index: 0),
///         new Tab(icon: Icons.User, label: "Profile", index: 1)
///     ],
///     selected:  selectedTab,
///     onSelect:  index =&gt; { selectedTab = index; }
/// )
/// </code>
/// </remarks>
public class TabBar : Node
{
    /// <summary>
    /// Creates a tab bar with the specified tabs, selection state, and callback.
    /// </summary>
    /// <param name="tabs">The tab definitions to display.</param>
    /// <param name="selected">The index of the currently selected tab.</param>
    /// <param name="onSelect">Callback invoked when the user selects a tab.</param>
    /// <param name="transition">
    /// Transition used when switching tabs. Defaults to <see cref="PageTransition.Fade"/>.
    /// </param>
    public TabBar(
        IReadOnlyList<Tab> tabs,
        int selected,
        Action<int> onSelect,
        PageTransition? transition = null)
    {
        Tabs = tabs;
        Selected = selected;
        OnSelect = onSelect;
        Transition = transition ?? PageTransition.Fade;
    }

    /// <summary>The tab definitions.</summary>
    public IReadOnlyList<Tab> Tabs { get; }

    /// <summary>The index of the currently selected tab.</summary>
    public int Selected { get; }

    /// <summary>Callback invoked when a tab is selected.</summary>
    public Action<int> OnSelect { get; }

    /// <summary>Transition applied when switching between tabs.</summary>
    public PageTransition Transition { get; }
}

/// <summary>
/// Defines a single tab in a <see cref="TabBar"/>.
/// </summary>
public class Tab
{
    /// <summary>
    /// Creates a tab definition.
    /// </summary>
    /// <param name="icon">The icon node displayed in the tab.</param>
    /// <param name="label">The text label below the icon.</param>
    /// <param name="index">The zero-based index of this tab.</param>
    public Tab(Node icon, string label, int index)
    {
        Icon = icon;
        Label = label;
        Index = index;
    }

    /// <summary>The icon node displayed in the tab.</summary>
    public Node Icon { get; }

    /// <summary>The text label below the icon.</summary>
    public string Label { get; }

    /// <summary>The zero-based index of this tab.</summary>
    public int Index { get; }
}
