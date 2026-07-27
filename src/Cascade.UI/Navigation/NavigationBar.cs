namespace Cascade.UI;

/// <summary>
/// A navigation bar control that the developer explicitly places in a page's
/// render tree. Never appears automatically — if a page does not render a
/// NavigationBar, there is no navigation bar.
/// </summary>
/// <remarks>
/// Two usage modes:
/// <para>
/// <b>Title bar mode</b> — a toolbar-style bar at the top of a page:
/// <code>
/// NavigationBar(
///     title:    "Orders",
///     trailing: IconButton(Icons.Share, onClick: OnShare)
/// )
/// </code>
/// </para>
/// <para>
/// <b>Section-switching mode</b> — a sidebar, rail, bottom, or top navigation
/// with NavItem children and a two-way active binding:
/// <code>
/// NavigationBar(Bind(currentSection), style: NavBarStyle.Sidebar,
///     NavItem(id: "inbox",  icon: Icons.Inbox,  label: "Inbox",  badge: unreadCount),
///     NavItem(id: "sent",   icon: Icons.Send,   label: "Sent"),
///     NavItem(id: "drafts", icon: Icons.File,    label: "Drafts")
/// )
/// </code>
/// </para>
/// </remarks>
public class NavigationBar : Node
{
    // ── Title-bar mode constructor ───────────────────────────────────

    /// <summary>
    /// Creates a title-bar navigation bar with the specified content and appearance options.
    /// </summary>
    public NavigationBar(
        string title,
        string? subtitle = null,
        bool largeTitle = false,
        bool? showBack = null,
        string? backLabel = null,
        Node? leading = null,
        Node? trailing = null,
        SearchBarOptions? search = null,
        NavBarBackground? background = null,
        bool bottomBorder = true,
        NavBarHeight height = NavBarHeight.Standard)
    {
        Mode = NavBarMode.TitleBar;
        Title = title;
        Subtitle = subtitle;
        LargeTitle = largeTitle;
        ShowBack = showBack;
        BackLabel = backLabel;
        Leading = leading ?? Node.Empty;
        Trailing = trailing ?? Node.Empty;
        Search = search;
        Background = background ?? NavBarBackground.Solid;
        BottomBorder = bottomBorder;
        Height = height;
        Style = NavBarStyle.Top;
        Items = [];
    }

    // ── Section-switching mode constructor ────────────────────────────

    /// <summary>
    /// Creates a section-switching navigation bar with the specified items and style.
    /// </summary>
    /// <param name="active">Two-way binding to the active section id.</param>
    /// <param name="style">Layout style: Sidebar, Rail, Bottom, or Top.</param>
    /// <param name="items">The navigation items.</param>
    public NavigationBar(
        Bindable<string> active,
        NavBarStyle style,
        params NavItem[] items)
    {
        Mode = NavBarMode.SectionSwitching;
        Active = active;
        Style = style;
        Items = items;
        Title = string.Empty;
        Leading = Node.Empty;
        Trailing = Node.Empty;
        Background = NavBarBackground.Solid;
        BottomBorder = true;
        Height = NavBarHeight.Standard;
    }

    // ── Shared properties ────────────────────────────────────────────

    /// <summary>Which mode this NavigationBar is operating in.</summary>
    public NavBarMode Mode { get; }

    /// <summary>Layout style for section-switching mode.</summary>
    public NavBarStyle Style { get; }

    /// <summary>The navigation items (section-switching mode only).</summary>
    public IReadOnlyList<NavItem> Items { get; }

    /// <summary>Two-way binding to the active section id (section-switching mode only).</summary>
    public Bindable<string>? Active { get; }

    // ── Title-bar mode properties ────────────────────────────────────

    /// <summary>Main title text.</summary>
    public string Title { get; }

    /// <summary>Optional subtitle below the title.</summary>
    public string? Subtitle { get; }

    /// <summary>Whether to display a large title that collapses on scroll.</summary>
    public bool LargeTitle { get; }

    /// <summary>Override for back button visibility. Null means automatic.</summary>
    public bool? ShowBack { get; }

    /// <summary>Custom label for the back button.</summary>
    public string? BackLabel { get; }

    /// <summary>Additional leading content after the back button.</summary>
    public Node Leading { get; }

    /// <summary>Trailing content (right side).</summary>
    public Node Trailing { get; }

    /// <summary>Search bar configuration, or null if no search.</summary>
    public SearchBarOptions? Search { get; }

    /// <summary>Background appearance mode.</summary>
    public NavBarBackground Background { get; }

    /// <summary>Whether to show a bottom separator line.</summary>
    public bool BottomBorder { get; }

    /// <summary>Bar height mode.</summary>
    public NavBarHeight Height { get; }

    // ── Collapsible sidebar ──────────────────────────────────────────

    /// <summary>Whether this sidebar is collapsible.</summary>
    public bool IsCollapsible { get; private set; }

    /// <summary>Two-way binding to the collapsed state.</summary>
    public Bindable<bool>? Collapsed { get; private set; }

    /// <summary>Width when collapsed (shows icons only). Default 56.</summary>
    public float CollapseWidth { get; private set; } = 56;

    /// <summary>Width when expanded. Default 240.</summary>
    public float ExpandWidth { get; private set; } = 240;

    /// <summary>Position of the collapse/expand button.</summary>
    public CollapseButtonPosition CollapseButtonPos { get; private set; } = CollapseButtonPosition.Bottom;

    /// <summary>Whether to show the collapse button.</summary>
    public bool ShowCollapseButton { get; private set; }

    /// <summary>
    /// Configures this sidebar as collapsible.
    /// </summary>
    public NavigationBar Collapsible(
        bool enabled,
        Bindable<bool>? collapsed = null,
        float collapseWidth = 56,
        float expandWidth = 240)
    {
        IsCollapsible = enabled;
        Collapsed = collapsed;
        CollapseWidth = collapseWidth;
        ExpandWidth = expandWidth;
        return this;
    }

    /// <summary>
    /// Adds a collapse/expand toggle button to the sidebar.
    /// </summary>
    public NavigationBar CollapseButton(CollapseButtonPosition position = CollapseButtonPosition.Bottom)
    {
        ShowCollapseButton = true;
        CollapseButtonPos = position;
        return this;
    }
}

/// <summary>
/// Indicates whether a <see cref="NavigationBar"/> is operating as a title bar
/// (top-of-page toolbar) or as a section-switching navigation control.
/// </summary>
public enum NavBarMode
{
    /// <summary>Title bar with back button, title, actions.</summary>
    TitleBar,

    /// <summary>Section-switching with NavItem children.</summary>
    SectionSwitching
}

/// <summary>
/// Background appearance modes for <see cref="NavigationBar"/>.
/// </summary>
public class NavBarBackground
{
    private NavBarBackground()
    {
    }

    private NavBarBackground(Brush brush)
    {
        CustomBrush = brush;
    }

    /// <summary>The custom brush, or null for non-custom backgrounds.</summary>
    internal Brush? CustomBrush { get; }

    /// <summary>Opaque theme surface color (default).</summary>
    public static NavBarBackground Solid { get; } = new();

    /// <summary>Backdrop blur (frosted glass) — content scrolls behind.</summary>
    public static NavBarBackground Blur { get; } = new();

    /// <summary>Fully transparent — title floats over content.</summary>
    public static NavBarBackground Transparent { get; } = new();

    /// <summary>Custom brush background.</summary>
    public static NavBarBackground Custom(Brush brush)
    {
        return new NavBarBackground(brush);
    }
}

/// <summary>
/// Height mode for <see cref="NavigationBar"/>.
/// </summary>
public enum NavBarHeight
{
    /// <summary>Standard navigation bar height.</summary>
    Standard,

    /// <summary>Compact navigation bar height.</summary>
    Compact
}

/// <summary>
/// Configuration for the search bar within a <see cref="NavigationBar"/>.
/// </summary>
public class SearchBarOptions
{
    /// <summary>Placeholder text shown when the search field is empty.</summary>
    public string? Placeholder { get; init; }

    /// <summary>Called on every keystroke with the current query text.</summary>
    public Action<string>? OnChanged { get; init; }

    /// <summary>Called when the user submits the search (presses Enter).</summary>
    public Action<string>? OnSubmit { get; init; }
}

/// <summary>
/// Holds a typed reference to a <see cref="NavigationBar"/>, providing
/// programmatic control such as expanding the search bar.
/// </summary>
public sealed class NavigationBarRef
{
    private bool searchExpanded;

    /// <summary>Whether the search bar is currently expanded.</summary>
    public bool IsSearchExpanded => searchExpanded;

    /// <summary>
    /// Programmatically expands the search bar.
    /// </summary>
    public void ExpandSearch()
    {
        searchExpanded = true;
    }

    /// <summary>
    /// Programmatically collapses the search bar.
    /// </summary>
    public void CollapseSearch()
    {
        searchExpanded = false;
    }
}
