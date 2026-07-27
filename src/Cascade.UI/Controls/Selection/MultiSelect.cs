using System;
using System.Collections.Generic;
using System.Linq;

namespace Cascade.UI;

/// <summary>
/// Internal interface for multi-select controls, used by the layout solver,
/// node painter, and input dispatcher without knowing the generic type parameter.
/// </summary>
internal interface IMultiSelectNode
{
    LocKey Placeholder { get; }
    LocKey Label { get; }
    bool IsNodeDisabled { get; }
    bool IsOpen { get; set; }

    /// <summary>Total number of selectable options.</summary>
    int OptionCount { get; }

    /// <summary>Index of the currently highlighted (hovered) option, or -1 for none.</summary>
    int HighlightedIndex { get; set; }

    /// <summary>Scroll offset for long dropdown lists.</summary>
    int ScrollOffset { get; set; }

    /// <summary>Dropdown bounds in absolute coordinates for hit testing.</summary>
    Rect DropdownBounds { get; set; }

    /// <summary>Dropdown item height in logical pixels, set by the painter from theme.</summary>
    float DropdownItemHeight { get; set; }

    /// <summary>Number of currently selected items.</summary>
    int SelectedCount { get; }

    /// <summary>Whether pills are shown in the closed field.</summary>
    bool ShowPills { get; }

    /// <summary>Whether to show "N selected" count text.</summary>
    bool ShowCount { get; }

    /// <summary>Maximum number of pills visible in the closed field.</summary>
    int? MaxPillsVisibleCount { get; }

    /// <summary>Labels of currently selected items for pill display.</summary>
    IReadOnlyList<string> SelectedPillLabels { get; }

    /// <summary>Whether the given option index is currently selected.</summary>
    bool IsItemSelected(int index);

    /// <summary>Toggles selection of the option at the given index.</summary>
    void ToggleItem(int index);

    /// <summary>Gets the display label for an option by index.</summary>
    string GetOptionLabel(int index);

    /// <summary>Toggles the dropdown open/closed state.</summary>
    void ToggleOpen();

    /// <summary>Closes the dropdown without changes.</summary>
    void Close();
}

/// <summary>
/// Multi-item selection dropdown. Selected items display as pills in the field.
/// Each dropdown option has a checkbox for unambiguous multi-selection.
/// </summary>
/// <typeparam name="T">The type of the selectable values.</typeparam>
public sealed class MultiSelect<T> : Node, IMultiSelectNode where T : notnull
{
    /// <summary>
    /// Creates a multi-select dropdown.
    /// </summary>
    /// <param name="value">Two-way binding to the list of selected values.</param>
    /// <param name="options">The available options.</param>
    /// <param name="placeholder">Placeholder text shown when nothing is selected.</param>
    /// <param name="maxSelected">Maximum number of items that can be selected. Null for unlimited.</param>
    /// <param name="label">Optional label displayed above the select.</param>
    public MultiSelect(
        Bindable<IReadOnlyList<T>> value,
        IReadOnlyList<SelectOption<T>> options,
        LocKey placeholder = default,
        int? maxSelected = null,
        LocKey label = default)
    {
        Value = value;
        Options = options;
        Placeholder = placeholder;
        MaxSelected = maxSelected;
        Label = label;
    }

    /// <summary>Two-way binding to the list of selected values.</summary>
    public Bindable<IReadOnlyList<T>> Value { get; }

    /// <summary>The available options.</summary>
    public IReadOnlyList<SelectOption<T>> Options { get; }

    /// <summary>Placeholder text shown when nothing is selected.</summary>
    public LocKey Placeholder { get; }

    /// <summary>Maximum number of items that can be selected.</summary>
    public int? MaxSelected { get; }

    /// <summary>Optional label.</summary>
    public LocKey Label { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal Func<T, Node>? OptionRenderer { get; set; }
    internal bool ShowSelectedCountValue { get; set; }
    internal bool ShowSelectAll { get; set; }
    internal int? MaxPillsVisibleValue { get; set; }
    internal bool PillsInFieldValue { get; set; } = true;
    internal bool IsSearchable { get; set; }
    internal List<Func<IReadOnlyList<T>, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }
    internal LocKey AccessibleLabelValue { get; set; }

    // ── IMultiSelectNode runtime state ────────────────────────────────

    internal bool IsOpen { get; set; }
    internal int HighlightedIndex { get; set; } = -1;
    internal Rect DropdownBoundsValue { get; set; }
    internal int ScrollOffsetValue { get; set; }
    internal float DropdownItemHeightValue { get; set; } = 32f;

    // Buffered selection: tracks changes while dropdown is open so that
    // Value.OnChange (which may trigger Invalidate/re-render) is only
    // called once when the dropdown closes, not on every item toggle.
    internal List<T>? PendingSelection { get; set; }

    // ── IMultiSelectNode explicit implementation ──────────────────────

    bool IMultiSelectNode.IsNodeDisabled => IsDisabled;
    bool IMultiSelectNode.IsOpen
    {
        get => IsOpen;
        set => IsOpen = value;
    }
    int IMultiSelectNode.OptionCount => Options.Count;

    int IMultiSelectNode.HighlightedIndex
    {
        get => HighlightedIndex;
        set => HighlightedIndex = value;
    }

    int IMultiSelectNode.ScrollOffset
    {
        get => ScrollOffsetValue;
        set => ScrollOffsetValue = value;
    }

    Rect IMultiSelectNode.DropdownBounds
    {
        get => DropdownBoundsValue;
        set => DropdownBoundsValue = value;
    }

    float IMultiSelectNode.DropdownItemHeight
    {
        get => DropdownItemHeightValue;
        set => DropdownItemHeightValue = value;
    }

    int IMultiSelectNode.SelectedCount => PendingSelection?.Count ?? Value.Value.Count;
    bool IMultiSelectNode.ShowPills => PillsInFieldValue;
    bool IMultiSelectNode.ShowCount => ShowSelectedCountValue;
    int? IMultiSelectNode.MaxPillsVisibleCount => MaxPillsVisibleValue;

    IReadOnlyList<string> IMultiSelectNode.SelectedPillLabels
    {
        get
        {
            IEnumerable<T> selected = PendingSelection ?? (IEnumerable<T>)Value.Value;
            var labels = new List<string>();
            foreach (T val in selected)
            {
                var opt = Options.FirstOrDefault(o => EqualityComparer<T>.Default.Equals(o.Value, val));
                if (opt != null)
                {
                    labels.Add(opt.Label.Resolve());
                }
            }
            return labels;
        }
    }

    bool IMultiSelectNode.IsItemSelected(int index)
    {
        if (index < 0 || index >= Options.Count)
        {
            return false;
        }

        T optionValue = Options[index].Value;
        IEnumerable<T> selected = PendingSelection ?? (IEnumerable<T>)Value.Value;
        return selected.Any(v => EqualityComparer<T>.Default.Equals(v, optionValue));
    }

    void IMultiSelectNode.ToggleItem(int index)
    {
        if (index < 0 || index >= Options.Count)
        {
            return;
        }

        // Ensure pending selection is initialized
        PendingSelection ??= Value.Value.ToList();

        T optionValue = Options[index].Value;
        int existingIndex = PendingSelection.FindIndex(v => EqualityComparer<T>.Default.Equals(v, optionValue));

        if (existingIndex >= 0)
        {
            PendingSelection.RemoveAt(existingIndex);
        }
        else
        {
            if (MaxSelected.HasValue && PendingSelection.Count >= MaxSelected.Value)
            {
                return;
            }
            PendingSelection.Add(optionValue);
        }
    }

    string IMultiSelectNode.GetOptionLabel(int index)
    {
        if (index < 0 || index >= Options.Count)
        {
            return string.Empty;
        }
        return Options[index].Label.Resolve();
    }

    void IMultiSelectNode.ToggleOpen()
    {
        IsOpen = !IsOpen;
        if (IsOpen)
        {
            HighlightedIndex = -1;
            ScrollOffsetValue = 0;
            PendingSelection = Value.Value.ToList();
        }
        else
        {
            CommitPendingSelection();
        }
    }

    void IMultiSelectNode.Close()
    {
        IsOpen = false;
        HighlightedIndex = -1;
        ScrollOffsetValue = 0;
        CommitPendingSelection();
    }

    private void CommitPendingSelection()
    {
        if (PendingSelection != null)
        {
            var committed = (IReadOnlyList<T>)PendingSelection;
            PendingSelection = null;
            Value.OnChange(committed);
        }
    }

    /// <summary>
    /// Runs all registered validation rules against the current value.
    /// Returns the first failing result or <see cref="ValidationResult.Ok"/>.
    /// </summary>
    internal ValidationResult RunValidation()
    {
        IReadOnlyList<T> currentValue = Value.Value;
        foreach (var rule in ValidationRules)
        {
            var result = rule(currentValue);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return ValidationResult.Ok;
    }
}

/// <summary>
/// Extension methods for <see cref="MultiSelect{T}"/> providing fluent modifiers.
/// </summary>
public static class MultiSelectExtensions
{
    /// <summary>Sets a custom renderer for options in the open dropdown.</summary>
    public static MultiSelect<T> RenderOption<T>(this MultiSelect<T> select, Func<T, Node> render)
        where T : notnull
    {
        select.OptionRenderer = render;
        return select;
    }

    /// <summary>Shows the selected count in the closed field header.</summary>
    public static MultiSelect<T> ShowSelectedCount<T>(this MultiSelect<T> select, bool show = true)
        where T : notnull
    {
        select.ShowSelectedCountValue = show;
        return select;
    }

    /// <summary>Adds a "Select all / Clear all" header item in the dropdown.</summary>
    public static MultiSelect<T> SelectAll<T>(this MultiSelect<T> select, bool show = true)
        where T : notnull
    {
        select.ShowSelectAll = show;
        return select;
    }

    /// <summary>Sets the maximum number of pills visible in the closed field.</summary>
    public static MultiSelect<T> MaxPillsVisible<T>(this MultiSelect<T> select, int max)
        where T : notnull
    {
        select.MaxPillsVisibleValue = max;
        return select;
    }

    /// <summary>Controls whether pills are shown in the closed field or replaced with "N selected" text.</summary>
    public static MultiSelect<T> PillsInField<T>(this MultiSelect<T> select, bool show = true)
        where T : notnull
    {
        select.PillsInFieldValue = show;
        return select;
    }

    /// <summary>Makes the dropdown searchable with local matching.</summary>
    public static MultiSelect<T> Searchable<T>(this MultiSelect<T> select)
        where T : notnull
    {
        select.IsSearchable = true;
        return select;
    }

    /// <summary>Adds a validation rule.</summary>
    public static MultiSelect<T> Validate<T>(this MultiSelect<T> select, Func<IReadOnlyList<T>, ValidationResult> rule)
        where T : notnull
    {
        select.ValidationRules.Add(rule);
        return select;
    }

    /// <summary>Sets when validation fires.</summary>
    public static MultiSelect<T> ValidateOn<T>(this MultiSelect<T> select, ValidationTrigger trigger)
        where T : notnull
    {
        select.ValidationTriggerMode = trigger;
        return select;
    }

    /// <summary>Disables the multi-select.</summary>
    public static MultiSelect<T> Disabled<T>(this MultiSelect<T> select, bool disabled = true)
        where T : notnull
    {
        select.IsDisabled = disabled;
        return select;
    }

    /// <summary>Makes the multi-select read-only.</summary>
    public static MultiSelect<T> ReadOnly<T>(this MultiSelect<T> select, bool readOnly = true)
        where T : notnull
    {
        select.IsReadOnly = readOnly;
        return select;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static MultiSelect<T> AccessibleLabel<T>(this MultiSelect<T> select, LocKey label)
        where T : notnull
    {
        select.AccessibleLabelValue = label;
        return select;
    }
}
