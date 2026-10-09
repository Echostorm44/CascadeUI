namespace Cascade.UI;

/// <summary>Which editor a <see cref="BatchEditDialogView"/> shows: the column's own kind of editor.</summary>
internal enum BatchEditorKind
{
    /// <summary>A text field.</summary>
    Text,

    /// <summary>A multi-line text area.</summary>
    MultiLine,

    /// <summary>A text field that only accepts a number.</summary>
    Number,

    /// <summary>A date picker.</summary>
    Date,

    /// <summary>A select over the column's options.</summary>
    Select,

    /// <summary>A toggle.</summary>
    Bool,
}

/// <summary>
/// What a "Set [Column] for selected rows…" dialog edits: the column, how many rows it will
/// change, the editor, and the value the selected rows share (empty / unset when they differ).
/// </summary>
internal sealed record BatchEditorSpec(
    string Header,
    int RowCount,
    BatchEditorKind Kind,
    string InitialText,
    bool InitialBool,
    DateOnly? InitialDate,
    IReadOnlyList<string> OptionLabels,
    int InitialOption,
    Func<string, bool> IsValidText);

/// <summary>The value the user chose in a <see cref="BatchEditDialogView"/>; which field counts depends on the editor.</summary>
internal sealed record BatchEditValue(string Text, bool Bool, DateOnly? Date, int OptionIndex);

/// <summary>
/// The dialog behind DataGrid's built-in "Set [Column] for selected rows…" batch items: the
/// column's editor, prefilled with the value the selected rows share, and Apply/Cancel. Apply
/// (or Enter, except in a multi-line field) closes it with a <see cref="BatchEditValue"/>; Cancel,
/// Escape or a click outside closes it with null. Apply is disabled until the value is usable
/// (a number that parses, a date, an option).
/// </summary>
internal sealed class BatchEditDialogView : Component
{
    private static readonly Hotkey EnterKey = new(ModifierKeys.None, Cascade.UI.Key.Enter);
    private static readonly Hotkey NumPadEnterKey = new(ModifierKeys.None, Cascade.UI.Key.NumPadEnter);

    private readonly BatchEditorSpec spec;
    private string text;
    private bool flag;
    private DateOnly? date;
    private int option;

    internal BatchEditDialogView(BatchEditorSpec spec)
    {
        this.spec = spec;
        text = spec.InitialText;
        flag = spec.InitialBool;
        date = spec.InitialDate;
        option = spec.InitialOption;
    }

    /// <summary>The dialog title: "Set Genre".</summary>
    internal static string TitleFor(string header) => $"Set {header}";

    /// <summary>The line under the title: "For 40 selected rows".</summary>
    internal static string MessageFor(int rowCount) => rowCount == 1 ? "For 1 selected row" : $"For {rowCount} selected rows";

    protected override Node Render()
    {
        var theme = ThemeSwitcher.Current;
        var apply = new Button("Apply", Apply).Disabled(!IsValid);
        var cancel = DialogParts.Secondary(new Button("Cancel", Cancel));
        var panel = DialogParts.Panel(
            theme.Dialog,
            DialogParts.Header(TitleFor(spec.Header), MessageFor(spec.RowCount), theme.Dialog),
            Editor(),
            DialogParts.Buttons(theme, apply, cancel));

        // Enter applies, as in the prompt dialog — except in a text area, where it is a new line.
        return spec.Kind == BatchEditorKind.MultiLine
            ? panel
            : new KeyHandler(panel, new KeyBinding(EnterKey, Apply), new KeyBinding(NumPadEnterKey, Apply));
    }

    private bool IsValid => spec.Kind switch
    {
        BatchEditorKind.Text or BatchEditorKind.MultiLine or BatchEditorKind.Number => spec.IsValidText(text),
        BatchEditorKind.Date => date is not null,
        BatchEditorKind.Select => option >= 0 && option < spec.OptionLabels.Count,
        _ => true,
    };

    private Node Editor()
    {
        string placeholder = spec.InitialText.Length == 0 && spec.RowCount > 1 ? "Multiple values" : string.Empty;
        return spec.Kind switch
        {
            BatchEditorKind.MultiLine => new TextArea(Bind(text, OnTextChanged), placeholder: placeholder).AutoFocus(),
            BatchEditorKind.Date => new DatePicker(Bind(date, OnDateChanged)).AutoFocus(),
            BatchEditorKind.Select => new Select<int>(Bind(option, OnOptionChanged), OptionList(), placeholder: "Choose…").AutoFocus(),
            BatchEditorKind.Bool => new Toggle(Bind(flag, OnFlagChanged), label: spec.Header).AutoFocus(),
            _ => new TextInput(Bind(text, OnTextChanged), placeholder: placeholder).AutoFocus(),
        };
    }

    private IReadOnlyList<SelectOption<int>> OptionList()
    {
        var options = new SelectOption<int>[spec.OptionLabels.Count];
        for (int i = 0; i < options.Length; i++)
        {
            options[i] = new SelectOption<int>(i, spec.OptionLabels[i]);
        }

        return options;
    }

    private void OnTextChanged(string value)
    {
        text = value;
    }

    private void OnDateChanged(DateOnly? value)
    {
        date = value;
    }

    private void OnOptionChanged(int value)
    {
        option = value;
    }

    private void OnFlagChanged(bool value)
    {
        flag = value;
    }

    private void Apply()
    {
        if (!IsValid)
        {
            return;
        }

        DialogContext.Close(new BatchEditValue(text, flag, date, option));
    }

    private void Cancel()
    {
        DialogContext.Close();
    }
}
