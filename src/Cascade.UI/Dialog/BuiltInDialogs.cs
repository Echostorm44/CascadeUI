namespace Cascade.UI;

/// <summary>
/// Building blocks shared by the built-in dialogs, all from the active theme's
/// <see cref="DialogTheme"/>: title and body text, the button row, and the padded panel.
/// </summary>
internal static class DialogParts
{
    internal const float SectionSpacing = 20f;
    internal const float TextSpacing = 8f;
    internal const float ButtonSpacing = 8f;

    internal static Label Title(string text, DialogTheme dialog)
    {
        return new Label(text).Style(dialog.TitleStyle).Color(dialog.TitleColor);
    }

    internal static Label Body(string text, DialogTheme dialog)
    {
        return new Label(text).Style(dialog.BodyStyle).Color(dialog.BodyColor).Wrap(TextWrap.WordWrap);
    }

    /// <summary>Title above an optional message.</summary>
    internal static Node Header(string title, string? message, DialogTheme dialog)
    {
        return string.IsNullOrEmpty(message)
            ? Title(title, dialog)
            : new Column(spacing: TextSpacing, children: [Title(title, dialog), Body(message, dialog)]);
    }

    /// <summary>
    /// The confirm/cancel row in the platform's arrangement: Fluent puts the primary button
    /// first and splits the row evenly (WinUI ContentDialog); Apple and Material right-align
    /// the buttons with the primary one last.
    /// </summary>
    internal static Node Buttons(CascadeTheme theme, Button primary, Button? secondary)
    {
        if (secondary is null)
        {
            return new Row(spacing: ButtonSpacing, mainAxisAlignment: MainAxisAlignment.End, children: [primary]);
        }

        if (theme is FluentTheme)
        {
            return new Row(spacing: ButtonSpacing, children: [primary.Grow(1f), secondary.Grow(1f)]);
        }

        return new Row(spacing: ButtonSpacing, mainAxisAlignment: MainAxisAlignment.End, children: [secondary, primary]);
    }

    /// <summary>The padded, theme-width column every built-in dialog is laid out in.</summary>
    internal static Column Panel(DialogTheme dialog, params Node[] sections)
    {
        return new Column(spacing: SectionSpacing, children: sections)
            .Padding(dialog.PaddingH, dialog.PaddingV)
            .Width(dialog.MaxWidth);
    }

    /// <summary>The secondary (cancel) button style.</summary>
    internal static Button Secondary(Button button)
    {
        return button.Variant("outline");
    }
}

/// <summary><see cref="Dialog.AlertAsync"/>: a message and one button.</summary>
internal sealed class AlertDialogView : Component
{
    private readonly string title;
    private readonly string message;
    private readonly string buttonLabel;

    internal AlertDialogView(string title, string message, string buttonLabel)
    {
        this.title = title;
        this.message = message;
        this.buttonLabel = buttonLabel;
    }

    protected override Node Render()
    {
        var theme = ThemeSwitcher.Current;
        var button = new Button(buttonLabel, Close).AutoFocus();
        return DialogParts.Panel(
            theme.Dialog,
            DialogParts.Header(title, message, theme.Dialog),
            DialogParts.Buttons(theme, button, null));
    }

    private void Close()
    {
        DialogContext.Close(true);
    }
}

/// <summary><see cref="Dialog.ConfirmAsync"/>: confirm (true) or cancel (false).</summary>
internal sealed class ConfirmDialogView : Component
{
    private readonly string title;
    private readonly string message;
    private readonly string confirmLabel;
    private readonly string cancelLabel;
    private readonly DialogDefault defaultButton;
    private readonly DialogStyle style;

    internal ConfirmDialogView(
        string title,
        string message,
        string confirmLabel,
        string cancelLabel,
        DialogDefault defaultButton,
        DialogStyle style)
    {
        this.title = title;
        this.message = message;
        this.confirmLabel = confirmLabel;
        this.cancelLabel = cancelLabel;
        this.defaultButton = defaultButton;
        this.style = style;
    }

    protected override Node Render()
    {
        var theme = ThemeSwitcher.Current;
        var confirm = new Button(confirmLabel, Confirm);
        if (style == DialogStyle.Destructive)
        {
            confirm = confirm.Variant("destructive");
        }

        var cancel = DialogParts.Secondary(new Button(cancelLabel, Cancel));
        if (defaultButton == DialogDefault.Cancel)
        {
            cancel = cancel.AutoFocus();
        }
        else
        {
            confirm = confirm.AutoFocus();
        }

        return DialogParts.Panel(
            theme.Dialog,
            DialogParts.Header(title, message, theme.Dialog),
            DialogParts.Buttons(theme, confirm, cancel));
    }

    private void Confirm()
    {
        DialogContext.Close(true);
    }

    private void Cancel()
    {
        DialogContext.Close(false);
    }
}

/// <summary>
/// <see cref="Dialog.PromptAsync"/>: one text field. Enter in the field confirms; the confirm
/// button and Enter are disabled while <c>validate</c> reports an error, whose message shows
/// below the field once the user has typed.
/// </summary>
internal sealed class PromptDialogView : Component
{
    private static readonly Hotkey EnterKey = new(ModifierKeys.None, Cascade.UI.Key.Enter);
    private static readonly Hotkey NumPadEnterKey = new(ModifierKeys.None, Cascade.UI.Key.NumPadEnter);

    private readonly string title;
    private readonly string message;
    private readonly string? placeholder;
    private readonly string confirmLabel;
    private readonly string cancelLabel;
    private readonly Func<string, ValidationResult>? validate;

    private string text;
    private ValidationResult validation;
    private bool edited;

    internal PromptDialogView(
        string title,
        string message,
        string? placeholder,
        string value,
        string confirmLabel,
        string cancelLabel,
        Func<string, ValidationResult>? validate)
    {
        this.title = title;
        this.message = message;
        this.placeholder = placeholder;
        this.confirmLabel = confirmLabel;
        this.cancelLabel = cancelLabel;
        this.validate = validate;
        text = value;
        validation = validate?.Invoke(value) ?? ValidationResult.Ok;
    }

    protected override Node Render()
    {
        var theme = ThemeSwitcher.Current;
        var input = new TextInput(Bind(text, OnTextChanged), placeholder: placeholder ?? string.Empty).AutoFocus();
        Node field = edited && validation.Message is { Length: > 0 } note
            ? new Column(spacing: 6f, children:
            [
                input,
                new Label(note)
                    .Style(theme.Typography.Caption)
                    .Color(validation.Status == ValidationStatus.Error ? theme.Colors.Danger : theme.Colors.Warning)
                    .Wrap(TextWrap.WordWrap)
                    .AccessibleRole(AccessibleRole.Text),
            ])
            : input;

        var confirm = new Button(confirmLabel, Confirm).Disabled(!validation.IsValid);
        var cancel = DialogParts.Secondary(new Button(cancelLabel, Cancel));

        var panel = DialogParts.Panel(
            theme.Dialog,
            DialogParts.Header(title, message, theme.Dialog),
            field,
            DialogParts.Buttons(theme, confirm, cancel));
        return new KeyHandler(panel, new KeyBinding(EnterKey, Confirm), new KeyBinding(NumPadEnterKey, Confirm));
    }

    private void OnTextChanged(string value)
    {
        text = value;
        validation = validate?.Invoke(value) ?? ValidationResult.Ok;
        edited = true;
    }

    private void Confirm()
    {
        if (!validation.IsValid)
        {
            edited = true;
            Invalidate();
            return;
        }

        DialogContext.Close(text);
    }

    private void Cancel()
    {
        DialogContext.Close();
    }
}

/// <summary>
/// <see cref="Dialog.ShowProgress"/>: title, status message, a progress bar and an optional
/// Cancel button. Reads the handle's state each render; the handle refreshes it on update.
/// </summary>
internal sealed class ProgressDialogView : Component
{
    private readonly Dialog.ProgressDialogHandle handle;

    internal ProgressDialogView(Dialog.ProgressDialogHandle handle)
    {
        this.handle = handle;
        handle.View = this;
    }

    /// <summary>Re-renders with the handle's current value and message (UI thread).</summary>
    internal void Refresh()
    {
        Invalidate();
    }

    protected override Node Render()
    {
        var theme = ThemeSwitcher.Current;
        var bar = new ProgressBar(handle.ProgressValue).AccessibleLabel(handle.Message ?? handle.Title);
        var header = DialogParts.Header(handle.Title, handle.Message, theme.Dialog);
        if (!handle.IsCancellable)
        {
            return DialogParts.Panel(theme.Dialog, header, bar);
        }

        var cancel = DialogParts.Secondary(new Button("Cancel", Cancel)).Disabled(handle.IsCancelled);
        return DialogParts.Panel(theme.Dialog, header, bar, DialogParts.Buttons(theme, cancel, null));
    }

    protected override void OnUnmounted()
    {
        if (ReferenceEquals(handle.View, this))
        {
            handle.View = null;
        }
    }

    private void Cancel()
    {
        handle.Cancel();
    }
}

/// <summary><see cref="BottomSheet.ShowActionsAsync"/>: one button per action and a cancel button.</summary>
internal sealed class ActionSheetView : Component
{
    private readonly string title;
    private readonly string[] actions;
    private readonly string cancelLabel;

    internal ActionSheetView(string title, IReadOnlyList<string> actions, string cancelLabel)
    {
        this.title = title;
        this.actions = [.. actions];
        this.cancelLabel = cancelLabel;
    }

    protected override Node Render()
    {
        var theme = ThemeSwitcher.Current;
        var dialog = theme.Dialog;
        var rows = new Node[actions.Length + 2];
        rows[0] = new Label(title)
            .Style(theme.Typography.BodySmall)
            .Color(theme.Colors.TextMuted)
            .TextAlign(TextAlignment.Center);
        for (int i = 0; i < actions.Length; i++)
        {
            string action = actions[i];
            rows[i + 1] = new Button(action, () => { DialogContext.Close(action); }).Variant("ghost");
        }

        rows[^1] = DialogParts.Secondary(new Button(cancelLabel, Cancel));
        return new Column(spacing: DialogParts.ButtonSpacing, children: rows)
            .Padding(dialog.PaddingH, dialog.PaddingV);
    }

    private void Cancel()
    {
        DialogContext.Close();
    }
}
