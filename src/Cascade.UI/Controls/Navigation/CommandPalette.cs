namespace Cascade.UI;

/// <summary>
/// A registered command that can be invoked from the <see cref="CommandPalette"/>.
/// </summary>
public sealed class Command
{
    public Command(
        string label,
        Icon icon = default,
        Hotkey? shortcut = null,
        Action? action = null,
        string? category = null)
    {
        Label = label;
        Icon = icon;
        Shortcut = shortcut;
        Action = action;
        Category = category;
    }

    /// <summary>Display label shown in the palette results.</summary>
    public string Label { get; }

    /// <summary>Leading icon shown next to the label.</summary>
    public Icon Icon { get; }

    /// <summary>Optional keyboard shortcut displayed as a badge.</summary>
    public Hotkey? Shortcut { get; }

    /// <summary>Action executed when the command is selected.</summary>
    public Action? Action { get; }

    /// <summary>Optional category for grouping commands in the palette.</summary>
    public string? Category { get; }
}

/// <summary>
/// A single result returned by an <see cref="ICommandProvider"/> search.
/// </summary>
public sealed class CommandResult
{
    /// <summary>Primary display label.</summary>
    public required string Label { get; init; }

    /// <summary>Optional secondary description text.</summary>
    public string? Description { get; init; }

    /// <summary>Optional leading icon.</summary>
    public Icon Icon { get; init; }

    /// <summary>Action executed when this result is selected.</summary>
    public required Action Execute { get; init; }
}

/// <summary>
/// Interface for custom search sources that provide results to the <see cref="CommandPalette"/>.
/// </summary>
public interface ICommandProvider
{
    /// <summary>Searches for commands matching the query string.</summary>
    Task<IEnumerable<CommandResult>> SearchAsync(string query);
}

/// <summary>
/// A keyboard-driven fuzzy search overlay for application commands.
/// Register commands globally; the framework handles the overlay UI,
/// search/filtering, keyboard navigation, and execution.
/// </summary>
public sealed class CommandPalette : Node
{
    private static readonly List<Command> globalCommands = new();
    private static readonly Dictionary<object, List<Command>> scopedCommands = new();
    private static readonly List<Command> recentCommands = new();
    private static bool isOpen;
    private static long openTick;

    /// <summary>Creates the command palette overlay node (typically placed in the root layout).</summary>
    public CommandPalette()
    {
        Instance = this;
    }

    /// <summary>The most recently created CommandPalette instance (typically the only one).</summary>
    internal static CommandPalette? Instance { get; private set; }

    /// <summary>All globally registered commands.</summary>
    internal static IReadOnlyList<Command> GlobalCommands => globalCommands;

    /// <summary>True if the command palette overlay is currently visible.</summary>
    internal static bool IsOpen => isOpen;

    /// <summary>Tick count when the palette was opened (for entrance animation).</summary>
    internal static long OpenTick => openTick;

    /// <summary>Recently executed commands.</summary>
    internal static IReadOnlyList<Command> RecentCommands => recentCommands;

    /// <summary>Custom search providers attached to this instance.</summary>
    internal IReadOnlyList<ICommandProvider> CustomProviders { get; set; } = Array.Empty<ICommandProvider>();

    /// <summary>Current search query text.</summary>
    internal string SearchText { get; set; } = "";

    /// <summary>Commands matching the current search query.</summary>
    internal List<Command> FilteredCommands { get; } = new();

    /// <summary>Index of the highlighted command in the filtered list.</summary>
    internal int HighlightedIndex { get; set; }

    /// <summary>Scroll offset for the results list.</summary>
    internal int ScrollOffset { get; set; }

    /// <summary>Viewport bounds for hit-testing (set during paint).</summary>
    internal Rect OverlayBounds { get; set; }

    /// <summary>Per-item bounds for hit-testing (set during paint).</summary>
    internal List<Rect> ItemBounds { get; } = new();

    /// <summary>Resets all static state. Used for test isolation.</summary>
    internal static void ResetAll()
    {
        globalCommands.Clear();
        scopedCommands.Clear();
        recentCommands.Clear();
        isOpen = false;
        openTick = 0;
    }

    /// <summary>Returns the commands registered for a specific owner, or empty if none.</summary>
    internal static IReadOnlyList<Command> GetScopedCommands(object owner)
    {
        if (scopedCommands.TryGetValue(owner, out var list))
        {
            return list;
        }

        return Array.Empty<Command>();
    }

    /// <summary>Registers one or more global commands.</summary>
    public static void Register(params Command[] commands)
    {
        globalCommands.AddRange(commands);
    }

    /// <summary>Registers commands scoped to a specific owner (typically a component).</summary>
    public static void Register(object owner, params Command[] commands)
    {
        if (!scopedCommands.TryGetValue(owner, out var list))
        {
            list = new List<Command>();
            scopedCommands[owner] = list;
        }

        list.AddRange(commands);
    }

    /// <summary>Removes all commands registered by the specified owner.</summary>
    public static void Unregister(object owner)
    {
        scopedCommands.Remove(owner);
    }

    /// <summary>Opens the command palette overlay.</summary>
    public static void Open()
    {
        isOpen = true;
        openTick = Environment.TickCount64;
        Instance?.SearchText = "";
        Instance?.UpdateFilter();
    }

    /// <summary>Closes the command palette overlay.</summary>
    public static void Close()
    {
        isOpen = false;
    }

    /// <summary>Clears the recent commands history.</summary>
    public static void ClearRecent()
    {
        recentCommands.Clear();
    }

    /// <summary>Updates the filtered command list based on current search text.</summary>
    internal void UpdateFilter()
    {
        FilteredCommands.Clear();
        ItemBounds.Clear();

        var allCommands = new List<Command>(globalCommands);
        foreach (var scoped in scopedCommands.Values)
        {
            allCommands.AddRange(scoped);
        }

        if (string.IsNullOrEmpty(SearchText))
        {
            if (recentCommands.Count > 0)
            {
                FilteredCommands.AddRange(recentCommands);
            }
            else
            {
                FilteredCommands.AddRange(allCommands);
            }
        }
        else
        {
            string query = SearchText.ToUpperInvariant();
            foreach (var cmd in allCommands)
            {
                if (cmd.Label.ToUpperInvariant().Contains(query, StringComparison.Ordinal) ||
                    (cmd.Category != null && cmd.Category.ToUpperInvariant().Contains(query, StringComparison.Ordinal)))
                {
                    FilteredCommands.Add(cmd);
                }
            }
        }

        HighlightedIndex = FilteredCommands.Count > 0 ? 0 : -1;
        ScrollOffset = 0;
    }

    /// <summary>Executes the currently highlighted command and closes the palette.</summary>
    internal void ExecuteHighlighted()
    {
        if (HighlightedIndex >= 0 && HighlightedIndex < FilteredCommands.Count)
        {
            var cmd = FilteredCommands[HighlightedIndex];

            // Track in recents (move to front, cap at 5)
            recentCommands.Remove(cmd);
            recentCommands.Insert(0, cmd);
            if (recentCommands.Count > 5)
            {
                recentCommands.RemoveAt(recentCommands.Count - 1);
            }

            Close();
            SearchText = "";
            cmd.Action?.Invoke();
        }
    }
}

/// <summary>
/// Fluent extension methods for <see cref="CommandPalette"/>.
/// </summary>
public static class CommandPaletteExtensions
{
    /// <summary>Adds custom search providers that supply additional results to the palette.</summary>
    public static CommandPalette Providers(this CommandPalette palette, params ICommandProvider[] providers)
    {
        palette.CustomProviders = providers;
        return palette;
    }
}
