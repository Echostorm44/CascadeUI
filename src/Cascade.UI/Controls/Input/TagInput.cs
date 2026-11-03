namespace Cascade.UI;

/// <summary>
/// Free-text entry that produces a list of removable pill tags. Tags are
/// created by typing and pressing a delimiter key. Supports suggestions,
/// drag-to-reorder, and per-tag validation.
/// </summary>
public sealed class TagInput : Node, IValidatable, ISuggestionHost, IPumpedNode
{
    /// <summary>
    /// Creates a tag input bound to a list of strings.
    /// </summary>
    /// <param name="value">Two-way binding to the tag list.</param>
    /// <param name="placeholder">Placeholder text shown when no tags and input is empty.</param>
    /// <param name="delimiter">Which key(s) create a new tag.</param>
    /// <param name="maxTags">Maximum number of tags allowed. Null for unlimited.</param>
    /// <param name="label">Optional label displayed above the input.</param>
    public TagInput(
        Bindable<IReadOnlyList<string>> value,
        LocKey placeholder = default,
        TagDelimiter delimiter = TagDelimiter.EnterAndComma,
        int? maxTags = null,
        LocKey label = default)
    {
        Value = value;
        Placeholder = placeholder;
        Delimiter = delimiter;
        MaxTags = maxTags;
        Label = label;
    }

    /// <summary>Two-way binding to the tag list.</summary>
    public Bindable<IReadOnlyList<string>> Value { get; }

    /// <summary>Placeholder text shown when no tags and input is empty.</summary>
    public LocKey Placeholder { get; }

    /// <summary>Which key(s) create a new tag.</summary>
    public TagDelimiter Delimiter { get; }

    /// <summary>Maximum number of tags allowed.</summary>
    public int? MaxTags { get; }

    /// <summary>Optional label.</summary>
    public LocKey Label { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal List<Func<string, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;

    // ── Validation (see Validation) ──
    // The rules check one tag at a time: the text being typed, then each tag already added.
    ValidationTrigger IValidatable.ValidationTriggerMode => ValidationTriggerMode;
    bool IValidatable.HasValidationRules => ValidationRules.Count > 0;
    object? IValidatable.ValidationValue => InputBuffer + "\u001e" + string.Join("\u001f", CurrentTags);
    TimeSpan? IValidatable.ValidationDebounce => null;
    ValidationState? IValidatable.ValidationState { get; set; }

    ValidationResult IValidatable.RunValidation()
    {
        string pending = InputBuffer.Trim();
        if (pending.Length > 0 && ValidateTag(pending) is { IsValid: false } typed)
        {
            return typed;
        }
        foreach (string tag in CurrentTags)
        {
            if (ValidateTag(tag) is { IsValid: false } failed)
            {
                return failed;
            }
        }
        return ValidationResult.Ok;
    }
    internal Func<string, IEnumerable<string>>? SuggestionSyncSource { get; set; }
    internal Func<string, Task<IEnumerable<string>>>? SuggestionAsyncSource { get; set; }

    // ── Suggestions (see Suggestions) ──
    private Suggestions suggestions = new();
    Suggestions ISuggestionHost.Suggestions => suggestions;
    bool ISuggestionHost.HasSuggestionSource => SuggestionSyncSource is not null || SuggestionAsyncSource is not null;
    string ISuggestionHost.SuggestionQuery => InputBuffer;

    // Tags already added aren't suggested again (unless duplicates are allowed).
    IEnumerable<SuggestionItem>? ISuggestionHost.SuggestSync(string query) =>
        SuggestionSyncSource is { } sync ? Fresh(sync(query) ?? []).Select(text => new SuggestionItem(text)) : null;
    Task<IEnumerable<SuggestionItem>>? ISuggestionHost.SuggestAsync(string query) =>
        SuggestionAsyncSource is { } source ? MapAsync(source(query)) : null;

    private async Task<IEnumerable<SuggestionItem>> MapAsync(Task<IEnumerable<string>> texts) =>
        [.. Fresh((await texts.ConfigureAwait(false)) ?? []).Select(text => new SuggestionItem(text))];

    private IEnumerable<string> Fresh(IEnumerable<string> texts) =>
        AllowDuplicateValues ? texts : texts.Where(text => !CurrentTags.Contains(text, StringComparer.OrdinalIgnoreCase));

    void IPumpedNode.Pump() => suggestions.Update(this, ReferenceEquals(FocusManager.FocusedElement, this));

    void IPumpedNode.AdoptFrom(Node previous)
    {
        if (previous is TagInput old)
        {
            suggestions = old.suggestions;
        }
    }
    internal bool AllowDuplicateValues { get; set; }
    internal Action<string>? OnDuplicateHandler { get; set; }
    internal bool IsReorderEnabled { get; set; }
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }

    /// <summary>
    /// Runs per-tag validation for the given tag value.
    /// Returns the first failing result or <see cref="ValidationResult.Ok"/>.
    /// </summary>
    internal ValidationResult ValidateTag(string tag)
    {
        foreach (var rule in ValidationRules)
        {
            var result = rule(tag);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return ValidationResult.Ok;
    }

    // ── Internal editing state ────────────────────────────────────

    /// <summary>Current text in the input field (not yet committed as a tag).</summary>
    internal string InputBuffer { get; set; } = "";

    /// <summary>Caret position within the input buffer.</summary>
    internal int CaretIndex { get; set; }

    /// <summary>Absolute viewport bounds, set by the painter for hit testing.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>Bounds of the text input area within the control (viewport coords).</summary>
    internal Rect InputAreaBounds { get; set; }

    /// <summary>Bounds of each tag's remove (×) button (viewport coords). Index matches tag index.</summary>
    internal List<Rect> TagRemoveBounds { get; } = [];

    /// <summary>Index of the tag whose × button is hovered (-1 = none).</summary>
    internal int HoveredRemoveIndex { get; set; } = -1;

    /// <summary>Whether the control has focus.</summary>
    internal bool IsFocused { get; set; }

    /// <summary>
    /// When true, the next TagInput instance created should auto-restore focus.
    /// Set by AddTag/RemoveTagAt before the Invalidate-triggered re-render.
    /// </summary>
    internal static bool PendingFocusRestore { get; set; }

    /// <summary>
    /// Working copy of the tag list maintained by InputDispatcher across re-renders.
    /// Avoids Bindable staleness when FocusManager holds a stale node reference.
    /// </summary>
    internal List<string>? LiveTags { get; set; }

    /// <summary>
    /// Gets the current tag list — <see cref="LiveTags"/> if set (during editing),
    /// otherwise falls back to the Bindable snapshot <see cref="Value"/>.
    /// </summary>
    internal IReadOnlyList<string> CurrentTags => (IReadOnlyList<string>?)LiveTags ?? Value.Value;

    // ── Tag management methods ────────────────────────────────────

    internal void AddTag(string tag)
    {
        string trimmed = tag.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }

        var current = CurrentTags;

        // Check duplicates
        if (!AllowDuplicateValues && current.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            OnDuplicateHandler?.Invoke(trimmed);
            return;
        }

        // Check max tags
        if (MaxTags.HasValue && current.Count >= MaxTags.Value)
        {
            return;
        }

        // Validate: a rejected tag stays in the box with the rule's message under it.
        var result = ValidateTag(trimmed);
        if (!result.IsValid)
        {
            Validation.StateOf(this).Result = result;
            SharedScheduler.Instance.RequestFrame();
            return;
        }

        var newList = new List<string>(current) { trimmed };
        LiveTags = newList;
        PendingFocusRestore = true;
        Value.OnChange(newList);
        InputBuffer = "";
        CaretIndex = 0;
    }

    // ── Drag to reorder (.ReorderEnabled) ─────────────────────────

    /// <summary>Each pill's rect in the control's own coordinates (set by the painter).</summary>
    internal List<Rect> PillBounds { get; } = [];

    /// <summary>The pill pressed (a drag may start from it), or -1.</summary>
    internal int DragIndex { get; set; } = -1;

    /// <summary>Where the press began, in the control's coordinates.</summary>
    internal Point DragStart { get; set; }

    /// <summary>The press has moved far enough to be a drag.</summary>
    internal bool Dragging { get; set; }

    /// <summary>Where the dragged tag would go: the index it would be inserted before.</summary>
    internal int DropIndex { get; set; } = -1;

    /// <summary>The pill at a point (not on its remove button), or -1.</summary>
    internal int PillAt(Point local)
    {
        for (int i = 0; i < PillBounds.Count; i++)
        {
            var r = PillBounds[i];
            if (r.Contains(local))
            {
                return local.X >= r.Right - 24f ? -1 : i; // the right end is the remove button
            }
        }
        return -1;
    }

    /// <summary>The insertion index for a point: before the pill whose left half it is over, on the nearest row.</summary>
    internal int InsertionIndexAt(Point local)
    {
        if (PillBounds.Count == 0)
        {
            return 0;
        }
        // The row nearest the point.
        float rowY = PillBounds.Select(r => r.Y).Distinct().OrderBy(y => Math.Abs(local.Y - (y + PillBounds[0].Height / 2f))).First();
        int last = -1;
        for (int i = 0; i < PillBounds.Count; i++)
        {
            var r = PillBounds[i];
            if (Math.Abs(r.Y - rowY) > 0.5f)
            {
                continue;
            }
            if (local.X < r.X + r.Width / 2f)
            {
                return i;
            }
            last = i;
        }
        return last + 1;
    }

    /// <summary>Moves the tag at <paramref name="from"/> to before the tag now at <paramref name="insertBefore"/>.</summary>
    internal void MoveTag(int from, int insertBefore)
    {
        var current = CurrentTags;
        if (from < 0 || from >= current.Count)
        {
            return;
        }
        int to = Math.Clamp(insertBefore > from ? insertBefore - 1 : insertBefore, 0, current.Count - 1);
        if (to == from)
        {
            return;
        }
        var list = new List<string>(current);
        string tag = list[from];
        list.RemoveAt(from);
        list.Insert(to, tag);
        LiveTags = list;
        Value.OnChange(list);
    }

    internal void RemoveTagAt(int index)
    {
        var current = CurrentTags;
        if (index < 0 || index >= current.Count)
        {
            return;
        }

        var newList = new List<string>(current);
        newList.RemoveAt(index);
        LiveTags = newList;
        PendingFocusRestore = true;
        Value.OnChange(newList);
    }
}

/// <summary>
/// Extension methods for <see cref="TagInput"/> providing fluent modifiers.
/// </summary>
public static class TagInputExtensions
{
    /// <summary>Adds a per-tag validation rule applied when each tag is created.</summary>
    public static TagInput Validate(this TagInput input, Func<string, ValidationResult> rule)
    {
        input.ValidationRules.Add(rule);
        return input;
    }

    /// <summary>Sets when validation fires.</summary>
    public static TagInput ValidateOn(this TagInput input, ValidationTrigger trigger)
    {
        input.ValidationTriggerMode = trigger;
        return input;
    }

    /// <summary>Adds a suggestion source for autocomplete as the user types.</summary>
    public static TagInput Suggestions(this TagInput input, Func<string, IEnumerable<string>> source)
    {
        input.SuggestionSyncSource = source;
        return input;
    }

    /// <summary>Adds an async suggestion source for autocomplete.</summary>
    public static TagInput Suggestions(this TagInput input, Func<string, Task<IEnumerable<string>>> source)
    {
        input.SuggestionAsyncSource = source;
        return input;
    }

    /// <summary>Controls whether duplicate tags are allowed.</summary>
    public static TagInput AllowDuplicates(this TagInput input, bool allow, Action<string>? onDuplicate = null)
    {
        input.AllowDuplicateValues = allow;
        input.OnDuplicateHandler = onDuplicate;
        return input;
    }

    /// <summary>Enables drag-to-reorder for tags: drag a tag (not its ×) to where it should go.</summary>
    public static TagInput ReorderEnabled(this TagInput input, bool enabled = true)
    {
        input.IsReorderEnabled = enabled;
        return input;
    }

    /// <summary>Disables the input.</summary>
    public static TagInput Disabled(this TagInput input, bool disabled = true)
    {
        input.IsDisabled = disabled;
        return input;
    }

    /// <summary>Makes the input read-only.</summary>
    public static TagInput ReadOnly(this TagInput input, bool readOnly = true)
    {
        input.IsReadOnly = readOnly;
        return input;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static TagInput AccessibleLabel(this TagInput input, LocKey label)
    {
        input.LayoutData.A11yLabel = label.Resolve();
        return input;
    }
}

/// <summary>
/// Defines which key(s) create a new tag in a <see cref="TagInput"/>.
/// </summary>
public enum TagDelimiter
{
    /// <summary>Enter key only.</summary>
    Enter,

    /// <summary>Comma key only.</summary>
    Comma,

    /// <summary>Either Enter or Comma (default).</summary>
    EnterAndComma,

    /// <summary>Tab key (useful when comma is valid in a tag).</summary>
    Tab
}
