namespace Cascade.UI;

/// <summary>
/// Describes a single editable property in a <see cref="PropertyGrid"/>.
/// Instances are created via the <see cref="Property"/> static factory methods.
/// </summary>
public sealed class PropertyDefinition
{
    internal PropertyDefinition()
    {
    }

    /// <summary>Display name of the property shown in the label column.</summary>
    public string Name { get; internal init; } = "";

    /// <summary>The kind of editor for this property.</summary>
    internal PropertyEditorKind EditorKind { get; init; }

    /// <summary>Getter delegate returning the current value as object.</summary>
    internal Delegate? Getter { get; init; }

    /// <summary>Setter delegate accepting the new value.</summary>
    internal Delegate? Setter { get; init; }

    /// <summary>Whether this property is read-only.</summary>
    internal bool IsReadOnly { get; init; }

    /// <summary>Optional min constraint for numeric properties.</summary>
    internal float? MinValue { get; init; }

    /// <summary>Optional max constraint for numeric properties.</summary>
    internal float? MaxValue { get; init; }

    /// <summary>Optional step for float properties.</summary>
    internal float? StepValue { get; init; }

    /// <summary>Optional format string for float properties.</summary>
    internal string? FormatString { get; init; }

    /// <summary>Optional min constraint for integer properties.</summary>
    internal int? MinIntValue { get; init; }

    /// <summary>Optional max constraint for integer properties.</summary>
    internal int? MaxIntValue { get; init; }

    /// <summary>Custom editor factory for Custom properties.</summary>
    internal Delegate? CustomEditor { get; init; }

    /// <summary>The enum type for Enum properties.</summary>
    internal Type? EnumType { get; init; }

    /// <summary>Update interval for throttling the get accessor. Set by fluent API.</summary>
    internal TimeSpan? UpdateIntervalValue { get; set; }
}

/// <summary>
/// Identifies the kind of inline editor used for a <see cref="PropertyDefinition"/>.
/// </summary>
internal enum PropertyEditorKind
{
    String,
    Float,
    Int,
    Bool,
    Enum,
    Color,
    Date,
    MultiLine,
    Custom,
    ReadOnly
}

/// <summary>
/// Static factory methods for creating <see cref="PropertyDefinition"/> instances
/// with type-appropriate inline editors.
/// </summary>
#pragma warning disable CA1716 // Property is the spec-defined API name for this factory class
public static class Property
#pragma warning restore CA1716
{
    /// <summary>Creates a string property edited via a text input.</summary>
    public static PropertyDefinition String(string name, Func<string> get, Action<string> set)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);
        return new PropertyDefinition
        {
            Name = name,
            EditorKind = PropertyEditorKind.String,
            Getter = get,
            Setter = set
        };
    }

    /// <summary>Creates a float property edited via a number input with optional constraints.</summary>
    public static PropertyDefinition Float(
        string name,
        Func<float> get,
        Action<float> set,
        float? min = null,
        float? max = null,
        float? step = null,
        string? format = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);
        return new PropertyDefinition
        {
            Name = name,
            EditorKind = PropertyEditorKind.Float,
            Getter = get,
            Setter = set,
            MinValue = min,
            MaxValue = max,
            StepValue = step,
            FormatString = format
        };
    }

    /// <summary>Creates an integer property edited via an integer number input.</summary>
    public static PropertyDefinition Int(
        string name,
        Func<int> get,
        Action<int> set,
        int? min = null,
        int? max = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);
        return new PropertyDefinition
        {
            Name = name,
            EditorKind = PropertyEditorKind.Int,
            Getter = get,
            Setter = set,
            MinIntValue = min,
            MaxIntValue = max
        };
    }

    /// <summary>Creates a boolean property edited via a toggle switch.</summary>
    public static PropertyDefinition Bool(string name, Func<bool> get, Action<bool> set)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);
        return new PropertyDefinition
        {
            Name = name,
            EditorKind = PropertyEditorKind.Bool,
            Getter = get,
            Setter = set
        };
    }

    /// <summary>Creates an enum property edited via a select dropdown. Options are derived from the enum type.</summary>
    public static PropertyDefinition Enum<T>(string name, Func<T> get, Action<T> set) where T : struct, System.Enum
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);
        return new PropertyDefinition
        {
            Name = name,
            EditorKind = PropertyEditorKind.Enum,
            Getter = get,
            Setter = set,
            EnumType = typeof(T)
        };
    }

    /// <summary>Creates a color property edited via a color swatch with a color picker popover.</summary>
    public static PropertyDefinition Color(string name, Func<ColorValue> get, Action<ColorValue> set)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);
        return new PropertyDefinition
        {
            Name = name,
            EditorKind = PropertyEditorKind.Color,
            Getter = get,
            Setter = set
        };
    }

    /// <summary>Creates a date property edited via a date picker.</summary>
    public static PropertyDefinition Date(string name, Func<DateOnly> get, Action<DateOnly> set)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);
        return new PropertyDefinition
        {
            Name = name,
            EditorKind = PropertyEditorKind.Date,
            Getter = get,
            Setter = set
        };
    }

    /// <summary>Creates a multiline string property edited via a text area.</summary>
    public static PropertyDefinition MultiLine(string name, Func<string> get, Action<string> set)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);
        return new PropertyDefinition
        {
            Name = name,
            EditorKind = PropertyEditorKind.MultiLine,
            Getter = get,
            Setter = set
        };
    }

    /// <summary>Creates a property with a custom editor node.</summary>
    public static PropertyDefinition Custom<T>(string name, Func<T> get, Action<T> set, Func<T, Node> editor)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(editor);
        return new PropertyDefinition
        {
            Name = name,
            EditorKind = PropertyEditorKind.Custom,
            Getter = get,
            Setter = set,
            CustomEditor = editor
        };
    }

    /// <summary>Creates a read-only display property with no editor.</summary>
    public static PropertyDefinition ReadOnly(string name, Func<object> get)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(get);
        return new PropertyDefinition
        {
            Name = name,
            EditorKind = PropertyEditorKind.ReadOnly,
            Getter = get,
            IsReadOnly = true
        };
    }
}

/// <summary>
/// Fluent extension methods for <see cref="PropertyDefinition"/>.
/// </summary>
public static class PropertyDefinitionExtensions
{
    /// <summary>Caps how frequently the get accessor is called during high-frequency updates.</summary>
    public static PropertyDefinition UpdateInterval(this PropertyDefinition property, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.UpdateIntervalValue = interval;
        return property;
    }
}

/// <summary>
/// A named group of properties displayed together with a collapsible header row.
/// </summary>
public sealed class PropertyGroup
{
    /// <summary>Creates a property group from a params array of property definitions.</summary>
    public PropertyGroup(string name, params PropertyDefinition[] properties)
    {
        Name = name;
        Properties = properties;
        Visible = null;
    }

    /// <summary>Creates a property group with a runtime visibility predicate.</summary>
    public PropertyGroup(string name, Func<bool> visible, IReadOnlyList<PropertyDefinition> properties)
    {
        Name = name;
        Properties = properties;
        Visible = visible;
    }

    /// <summary>Display name shown in the group header row.</summary>
    public string Name { get; }

    /// <summary>The property definitions in this group.</summary>
    public IReadOnlyList<PropertyDefinition> Properties { get; }

    /// <summary>
    /// Optional predicate controlling group visibility at runtime.
    /// When null, the group is always visible.
    /// </summary>
    public Func<bool>? Visible { get; }
}

/// <summary>
/// A two-column key/value inspector. Labels on the left, type-appropriate
/// inline editors on the right. Supports auto-reflection via source generator
/// or explicit property definitions with grouping.
/// </summary>
public sealed class PropertyGrid : Node
{
    /// <summary>Creates a property grid that auto-reflects the target object's public properties.</summary>
    public PropertyGrid(object target)
    {
        Target = target;
        Groups = [];
    }

    /// <summary>Creates a property grid with explicitly defined property groups.</summary>
    public PropertyGrid(IReadOnlyList<PropertyGroup> groups)
    {
        Target = null;
        Groups = groups;
    }

    /// <summary>
    /// Target object for auto-reflection via source generator.
    /// Null when using explicit property groups.
    /// </summary>
    public object? Target { get; }

    /// <summary>Explicitly defined property groups.</summary>
    public IReadOnlyList<PropertyGroup> Groups { get; }

    // ── Runtime state (set by painter/input dispatcher) ──────────────

    /// <summary>Tracks which groups are collapsed by index.</summary>
    internal HashSet<int> CollapsedGroups { get; } = [];

    /// <summary>Index of the currently hovered row (flattened), or -1.</summary>
    internal int HoveredRow { get; set; } = -1;

    /// <summary>Absolute bounds in viewport coordinates for hit testing.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>Row height used by the painter.</summary>
    internal float RowHeight { get; set; } = 28f;

    /// <summary>Group header height used by the painter.</summary>
    internal float GroupHeaderHeight { get; set; } = 32f;

    /// <summary>Toggles collapse state for a group.</summary>
    internal void ToggleGroup(int groupIndex)
    {
        if (!CollapsedGroups.Remove(groupIndex))
        {
            CollapsedGroups.Add(groupIndex);
        }
    }
}
