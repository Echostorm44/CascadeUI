namespace Cascade.UI;

/// <summary>
/// A page container node returned by <see cref="Component"/>'s Page() factory.
/// Combines a <see cref="NavigationBar"/> with page content in a layout that
/// the Navigator renders. The bar is managed by the navigator (back button,
/// title transitions, etc.) while the page owns the content.
/// </summary>
internal sealed class PageHost : Node
{
    /// <summary>The page title, shown in the navigation bar.</summary>
    internal string Title { get; init; } = string.Empty;

    /// <summary>Whether to display a large title that collapses on scroll.</summary>
    internal bool LargeTitle { get; init; }

    /// <summary>Trailing action nodes for the navigation bar.</summary>
    internal IReadOnlyList<Node> TrailingBar { get; init; } = [];

    /// <summary>The page's content node.</summary>
    internal Node Content { get; init; } = Node.Empty;

    /// <summary>The bar style for this page.</summary>
    internal NavigationBarStyle BarStyle { get; init; } = NavigationBarStyle.Default;
}
