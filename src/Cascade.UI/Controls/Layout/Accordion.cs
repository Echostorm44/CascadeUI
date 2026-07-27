namespace Cascade.UI;

/// <summary>
/// Controls whether multiple sections can be open simultaneously in an <see cref="Accordion"/>.
/// </summary>
public enum AccordionMode
{
    /// <summary>Any number of sections can be expanded simultaneously.</summary>
    MultiOpen,

    /// <summary>Opening one section collapses all others.</summary>
    SingleOpen
}

/// <summary>
/// A single collapsible section with a header and expandable content.
/// </summary>
public sealed class Expander : Node
{
    /// <summary>Creates an expander with a text header and unbound expanded state.</summary>
    public Expander(
        LocKey header,
        Node content,
        bool expanded = false,
        string? id = null)
    {
        HeaderText = header;
        HeaderNode = Node.Empty;
        Content = content;
        Expanded = expanded;
        ExpandedState = expanded;
        ExpandedBind = default;
        Id = id;
    }

    /// <summary>Creates an expander with a custom node header and unbound expanded state.</summary>
    public Expander(
        Node header,
        Node content,
        bool expanded = false,
        string? id = null)
    {
        HeaderText = default;
        HeaderNode = header;
        Content = content;
        Expanded = expanded;
        ExpandedState = expanded;
        ExpandedBind = default;
        Id = id;
    }

    /// <summary>Creates an expander with a text header and a two-way bound expanded state.</summary>
    public Expander(
        LocKey header,
        Node content,
        Bindable<bool> expanded,
        string? id = null)
    {
        HeaderText = header;
        HeaderNode = Node.Empty;
        Content = content;
        Expanded = false;
        ExpandedBind = expanded;
        Id = id;
    }

    /// <summary>Creates an expander with a custom node header and a two-way bound expanded state.</summary>
    public Expander(
        Node header,
        Node content,
        Bindable<bool> expanded,
        string? id = null)
    {
        HeaderText = default;
        HeaderNode = header;
        Content = content;
        Expanded = false;
        ExpandedBind = expanded;
        Id = id;
    }

    /// <summary>Text header (when using LocKey-based header).</summary>
    public LocKey HeaderText { get; }

    /// <summary>Custom node header (when using a rich header layout).</summary>
    public Node HeaderNode { get; }

    /// <summary>The expandable content shown when the section is open.</summary>
    public Node Content { get; }

    /// <summary>Initial expanded state (for unbound usage).</summary>
    public bool Expanded { get; }

    /// <summary>Two-way binding for the expanded state.</summary>
    public Bindable<bool> ExpandedBind { get; }

    /// <summary>
    /// Live expanded state for an <b>uncontrolled</b> expander (one created without a
    /// <see cref="Bindable{T}"/>). Seeded from the initial <see cref="Expanded"/> value and
    /// toggled by the input dispatcher on header click; carried across re-renders by the
    /// reconciler so the section stays open. Ignored when <see cref="ExpandedBind"/> is bound.
    /// </summary>
    internal bool ExpandedState { get; set; }

    /// <summary>
    /// The resolved current expanded state: the bound value when two-way bound, otherwise the
    /// uncontrolled <see cref="ExpandedState"/>. All layout/paint/input sites read this so the
    /// bound and uncontrolled paths never diverge.
    /// </summary>
    internal bool IsExpanded => ExpandedBind.OnChange != null ? ExpandedBind.Value : ExpandedState;

    /// <summary>Optional identifier used for accordion coordination and default selection.</summary>
    public string? Id { get; }
}

/// <summary>
/// Fluent extension methods for <see cref="Expander"/>.
/// </summary>
public static class ExpanderExtensions
{
    /// <summary>Registers a callback invoked when the expander is toggled.</summary>
    public static Expander OnToggle(this Expander expander, Action<bool> handler)
    {
        ArgumentNullException.ThrowIfNull(expander);
        ArgumentNullException.ThrowIfNull(handler);
        expander.LayoutData.ExpanderData ??= new ExpanderNodeData();
        expander.LayoutData.ExpanderData.OnToggleHandler = handler;
        return expander;
    }
}

/// <summary>
/// A group of <see cref="Expander"/> sections with coordinated open/close behavior.
/// </summary>
public sealed class Accordion : Node
{
    /// <summary>Creates an accordion allowing any number of sections to be open.</summary>
    public Accordion(params Expander[] sections)
    {
        Sections = sections;
        Mode = AccordionMode.MultiOpen;
        Default = null;
    }

    /// <summary>Creates an accordion with the specified open/close mode.</summary>
    public Accordion(AccordionMode mode, params Expander[] sections)
    {
        Sections = sections;
        Mode = mode;
        Default = null;
    }

    /// <summary>Creates an accordion with the specified mode and a default open section.</summary>
    public Accordion(AccordionMode mode, string @default, params Expander[] sections)
    {
        Sections = sections;
        Mode = mode;
        Default = @default;
    }

    /// <summary>The expander sections in this accordion.</summary>
    public IReadOnlyList<Expander> Sections { get; }

    /// <summary>Whether multiple or only one section can be open at a time.</summary>
    public AccordionMode Mode { get; }

    /// <summary>The id of the section that should be open by default.</summary>
    public string? Default { get; }
}
