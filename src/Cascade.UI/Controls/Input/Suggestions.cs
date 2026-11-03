namespace Cascade.UI;

/// <summary>One suggestion: the text it puts in the field, and optionally the app's content to show for it.</summary>
internal sealed record SuggestionItem(string Text, Func<Node>? Render = null);

/// <summary>A text field that offers suggestions as you type (TextInput autocomplete, TagInput suggestions).</summary>
internal interface ISuggestionHost
{
    /// <summary>The field's suggestion list state.</summary>
    Suggestions Suggestions { get; }

    /// <summary>Whether a suggestion source is set.</summary>
    bool HasSuggestionSource { get; }

    /// <summary>The text being completed (the field's text, or a tag field's pending tag).</summary>
    string SuggestionQuery { get; }

    /// <summary>Suggestions from a synchronous source, or null when the source is async.</summary>
    IEnumerable<SuggestionItem>? SuggestSync(string query);

    /// <summary>Suggestions from an async source, or null when the source is synchronous.</summary>
    Task<IEnumerable<SuggestionItem>>? SuggestAsync(string query);
}

/// <summary>
/// The suggestion list under a text field: what it shows for the current text, which row is
/// highlighted, and whether Escape put it away. Results come from the field's source: a synchronous
/// one at once, an async one through an <see cref="OptionLoader{TResult}"/> once typing pauses.
/// </summary>
internal sealed class Suggestions
{
    /// <summary>The most suggestions shown at once.</summary>
    internal const int MaxShown = 8;

    private static readonly TimeSpan AsyncDelay = TimeSpan.FromMilliseconds(200);

    private OptionLoader<SuggestionItem> loader = new();
    private IReadOnlyList<SuggestionItem> syncItems = [];
    private string? syncQuery;
    private string? dismissedFor;
    private string? lastQuery;

    /// <summary>The highlighted row, or -1.</summary>
    internal int Highlight { get; set; } = -1;

    /// <summary>Where the list was drawn (window coordinates), for clicks.</summary>
    internal Rect PopupBounds { get; set; }

    /// <summary>Height of one row, as drawn.</summary>
    internal float RowHeight { get; set; } = 32f;

    /// <summary>The suggestions for the current text.</summary>
    internal IReadOnlyList<SuggestionItem> Items { get; private set; } = [];

    /// <summary>Whether the list is on show: the field is focused, has text, has suggestions, and Escape hasn't put them away.</summary>
    internal bool IsOpen { get; private set; }

    /// <summary>Refreshes for the field's current text. Called once a frame (layout).</summary>
    internal void Update(ISuggestionHost host, bool focused)
    {
        string query = host.SuggestionQuery;
        if (query != lastQuery)
        {
            lastQuery = query;
            Highlight = -1;
            if (dismissedFor is not null && dismissedFor != query)
            {
                dismissedFor = null;
            }
        }

        if (!focused || !host.HasSuggestionSource || string.IsNullOrWhiteSpace(query))
        {
            IsOpen = false;
            return;
        }

        if (host.SuggestSync(query) is { } sync)
        {
            if (query != syncQuery)
            {
                syncQuery = query;
                syncItems = [.. sync.Take(MaxShown)];
            }
            Items = syncItems;
        }
        else
        {
            loader.Want(query, AsyncDelay);
            loader.Pump(q => host.SuggestAsync(q) ?? Task.FromResult<IEnumerable<SuggestionItem>>([]));
            Items = [.. loader.Results.Take(MaxShown)];
        }

        // A suggestion identical to what is typed has nothing to offer.
        IsOpen = dismissedFor != query && Items.Count > 0
            && !(Items.Count == 1 && string.Equals(Items[0].Text, query, StringComparison.Ordinal));
        if (!IsOpen)
        {
            Highlight = -1;
        }
    }

    /// <summary>Puts the list away until the text changes (Escape).</summary>
    internal void Dismiss()
    {
        dismissedFor = lastQuery;
        IsOpen = false;
        Highlight = -1;
    }

    /// <summary>Moves the highlight by <paramref name="delta"/> rows, wrapping.</summary>
    internal void Move(int delta)
    {
        int count = Items.Count;
        if (count == 0)
        {
            return;
        }
        Highlight = Highlight < 0 ? (delta > 0 ? 0 : count - 1) : ((Highlight + delta) % count + count) % count;
    }

    /// <summary>The row at window point (<paramref name="x"/>, <paramref name="y"/>), or -1.</summary>
    internal int RowAt(float x, float y)
    {
        if (!IsOpen || !PopupBounds.Contains(new Point(x, y)) || RowHeight <= 0)
        {
            return -1;
        }
        int row = (int)((y - PopupBounds.Y) / RowHeight);
        return row >= 0 && row < Items.Count ? row : -1;
    }

    /// <summary>Takes over a replaced field's list state, so a re-render doesn't close or reload it.</summary>
    internal void AdoptFrom(Suggestions previous)
    {
        // The request in flight and what is on show carry over to the new node.
        Highlight = previous.Highlight;
        dismissedFor = previous.dismissedFor;
        lastQuery = previous.lastQuery;
        syncQuery = previous.syncQuery;
        syncItems = previous.syncItems;
        Items = previous.Items;
        IsOpen = previous.IsOpen;
        PopupBounds = previous.PopupBounds;
        RowHeight = previous.RowHeight;
        loader = previous.loader;
    }
}
