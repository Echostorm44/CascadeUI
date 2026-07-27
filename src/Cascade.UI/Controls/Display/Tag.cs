namespace Cascade.UI;

/// <summary>
/// A standalone inline tag or chip. Appears in tag lists, filter bars,
/// contact fields, and anywhere a labeled removable item is needed.
/// Supports read-only, toggleable, and removable states.
/// </summary>
public sealed class Tag : Node
{
    /// <summary>
    /// Creates a read-only tag.
    /// </summary>
    /// <param name="label">The tag label text.</param>
    public Tag(string label)
    {
        Label = label;
        OnRemove = null;
        OnToggle = null;
        Selected = null;
        Leading = Node.Empty;
        TagIcon = Node.Empty;
    }

    /// <summary>
    /// Creates a removable tag.
    /// </summary>
    /// <param name="label">The tag label text.</param>
    /// <param name="onRemove">Callback when the remove button is clicked.</param>
    public Tag(string label, Action onRemove)
    {
        Label = label;
        OnRemove = onRemove;
        OnToggle = null;
        Selected = null;
        Leading = Node.Empty;
        TagIcon = Node.Empty;
    }

    /// <summary>
    /// Creates a toggleable tag.
    /// </summary>
    /// <param name="label">The tag label text.</param>
    /// <param name="selected">Two-way binding for the selected state.</param>
    /// <param name="onToggle">Optional callback on toggle.</param>
    public Tag(string label, Bindable<bool> selected, Action<bool>? onToggle = null)
    {
        Label = label;
        OnRemove = null;
        OnToggle = onToggle;
        Selected = selected;
        Leading = Node.Empty;
        TagIcon = Node.Empty;
    }

    /// <summary>The tag label text.</summary>
    public string Label { get; }

    /// <summary>Callback when the remove button is clicked.</summary>
    public Action? OnRemove { get; }

    /// <summary>Callback when the tag is toggled.</summary>
    public Action<bool>? OnToggle { get; }

    /// <summary>Two-way binding for the selected state.</summary>
    public Bindable<bool>? Selected { get; }

    /// <summary>Leading content (e.g., an avatar or icon).</summary>
    public Node Leading { get; private set; }

    /// <summary>Icon displayed before the label.</summary>
    public Node TagIcon { get; private set; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal ColorValue? TagColorOverride { get; set; }

    /// <summary>Sets the tag color.</summary>
    public Tag Color(ColorValue color)
    {
        TagColorOverride = color;
        return this;
    }

    /// <summary>Sets a leading icon.</summary>
    public Tag Icon(Node icon)
    {
        TagIcon = icon;
        return this;
    }

    /// <summary>Sets leading content (e.g., a small avatar).</summary>
    public Tag LeadingContent(Node leading)
    {
        Leading = leading;
        return this;
    }
}
