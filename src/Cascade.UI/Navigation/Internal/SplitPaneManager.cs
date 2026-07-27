namespace Cascade.UI;

/// <summary>
/// Manages N independent navigation stacks for a <see cref="SplitNavigator"/>.
/// Each pane has its own navigator, and the manager tracks which pane is
/// currently active (has focus).
/// </summary>
internal sealed class SplitPaneManager
{
    private readonly IReadOnlyList<Navigator> panes;

    internal SplitPaneManager(IReadOnlyList<Navigator> panes)
    {
        this.panes = panes;
        ActivePaneIndex = 0;
    }

    /// <summary>The number of panes managed.</summary>
    internal int PaneCount => panes.Count;

    /// <summary>The index of the currently active (focused) pane.</summary>
    internal int ActivePaneIndex { get; private set; }

    /// <summary>
    /// Gets the navigator for the pane at the specified index.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the index is outside the valid pane range.
    /// </exception>
    internal Navigator GetPane(int index)
    {
        if (index < 0 || index >= panes.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                $"Pane index {index} is out of range. Valid range: 0–{panes.Count - 1}.");
        }

        return panes[index];
    }

    /// <summary>
    /// Sets the active pane index.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the index is outside the valid pane range.
    /// </exception>
    internal void SetActivePane(int index)
    {
        if (index < 0 || index >= panes.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                $"Pane index {index} is out of range. Valid range: 0–{panes.Count - 1}.");
        }

        ActivePaneIndex = index;
    }

    /// <summary>
    /// Returns all pane navigators as a read-only list.
    /// </summary>
    internal IReadOnlyList<Navigator> AllPanes => panes;
}
