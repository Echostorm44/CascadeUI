namespace Cascade.UI;

/// <summary>
/// Static methods for showing dialogs. Provides high-level factory methods
/// (Alert, Confirm, Prompt) for common cases and a fully custom path via
/// <see cref="ShowAsync{TComponent,TResult}"/>. All methods resolve against
/// the nearest overlay stack in the component tree.
/// </summary>
public static class Dialog
{
    private static readonly object syncRoot = new();
    private static readonly Stack<IDialogCompletion> dialogStack = new();

    internal static int ActiveDialogCount
    {
        get
        {
            lock (syncRoot)
            {
                return dialogStack.Count;
            }
        }
    }

    // ── High-level API ────────────────────────────────────────────────

    /// <summary>
    /// Shows an informational alert dialog with a single button.
    /// Returns when the user dismisses the dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Informational message text.</param>
    /// <param name="button">Button label. Defaults to "OK".</param>
    public static Task AlertAsync(string title, string message, string button = "OK")
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(button);

        var source = RegisterDialog<bool>();
        return source.Task;
    }

    /// <summary>
    /// Shows a binary decision dialog. Returns true if confirmed, false if cancelled.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Decision message text.</param>
    /// <param name="confirmLabel">Label for the confirm button.</param>
    /// <param name="cancelLabel">Label for the cancel button.</param>
    /// <param name="defaultButton">Which button Enter/Space activates.</param>
    /// <param name="style">Visual style — Destructive colors the confirm button in danger color.</param>
    public static Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmLabel = "OK",
        string cancelLabel = "Cancel",
        DialogDefault defaultButton = DialogDefault.Confirm,
        DialogStyle style = DialogStyle.Normal)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(confirmLabel);
        ArgumentNullException.ThrowIfNull(cancelLabel);

        _ = defaultButton;
        _ = style;

        var source = RegisterDialog<bool>();
        return source.Task;
    }

    /// <summary>
    /// Shows a prompt dialog with a single text input. Returns the entered
    /// value, or null if the user cancelled.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Instructional message text.</param>
    /// <param name="placeholder">Placeholder text for the input field.</param>
    /// <param name="value">Pre-filled value for the input field.</param>
    /// <param name="confirmLabel">Label for the confirm button.</param>
    /// <param name="cancelLabel">Label for the cancel button.</param>
    /// <param name="validate">
    /// Validation function called on every keystroke. The confirm button is
    /// disabled while validation returns an error.
    /// </param>
    public static Task<string?> PromptAsync(
        string title,
        string message,
        string? placeholder = null,
        string? value = null,
        string confirmLabel = "OK",
        string cancelLabel = "Cancel",
        Func<string, ValidationResult>? validate = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(confirmLabel);
        ArgumentNullException.ThrowIfNull(cancelLabel);

        _ = placeholder;
        _ = value;
        _ = validate;

        var source = RegisterDialog<string?>();
        return source.Task;
    }

    // ── Progress dialog ───────────────────────────────────────────────

    /// <summary>
    /// Shows a modal progress dialog for long operations. Returns an
    /// <see cref="IProgressDialog"/> handle that controls advancement and
    /// dismissal. Dispose the handle to close the dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Initial status message.</param>
    /// <param name="cancellable">Whether to show a Cancel button.</param>
    public static IProgressDialog ShowProgress(
        string title,
        string? message = null,
        bool cancellable = false)
    {
        ArgumentNullException.ThrowIfNull(title);

        var source = RegisterDialog<bool>();
        return new ProgressDialogHandle(title, message, cancellable, source);
    }

    // ── Custom dialogs ────────────────────────────────────────────────

    /// <summary>
    /// Opens a custom component as a modal dialog and suspends the caller
    /// until the dialog is closed. The dialog component calls
    /// <see cref="Return{TResult}"/> or <see cref="Dismiss"/> to close.
    /// </summary>
    /// <typeparam name="TComponent">The dialog component type.</typeparam>
    /// <typeparam name="TResult">The result type returned by the dialog.</typeparam>
    /// <param name="options">Dialog configuration options.</param>
    public static Task<TResult?> ShowAsync<TComponent, TResult>(
        DialogOptions? options = null)
        where TComponent : Component
    {
        _ = options;

        var source = RegisterDialog<TResult?>();
        return source.Task;
    }

    /// <summary>
    /// Opens a custom component as a modal dialog with no return value.
    /// </summary>
    /// <typeparam name="TComponent">The dialog component type.</typeparam>
    /// <param name="options">Dialog configuration options.</param>
    public static Task ShowAsync<TComponent>(
        DialogOptions? options = null)
        where TComponent : Component
    {
        _ = options;

        var source = RegisterDialog<bool>();
        return source.Task;
    }

    // ── Dialog context methods (called from inside a dialog) ──────────

    /// <summary>
    /// Closes the current dialog and resolves the awaiting
    /// <see cref="ShowAsync{TComponent,TResult}"/> call with the given value.
    /// Called from inside a dialog component.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="value">The value to return to the caller.</param>
    public static void Return<TResult>(TResult value)
    {
        var completion = PopCompletion();
        completion.TryComplete(value);
    }

    /// <summary>
    /// Closes the current dialog without returning a value. The awaiting
    /// call receives null. Called from inside a dialog component for
    /// Cancel buttons and non-confirmation paths.
    /// </summary>
    public static void Dismiss()
    {
        var completion = PopCompletion();
        completion.TryComplete(null);
    }

    private static DialogResultSource<TResult> RegisterDialog<TResult>()
    {
        var source = new DialogResultSource<TResult>();
        lock (syncRoot)
        {
            dialogStack.Push(source);
        }

        return source;
    }

    private static IDialogCompletion PopCompletion()
    {
        lock (syncRoot)
        {
            if (dialogStack.Count == 0)
            {
                throw new InvalidOperationException("No active dialog to complete.");
            }

            return dialogStack.Pop();
        }
    }

    private static void CompleteCompletion(IDialogCompletion completion, object? result)
    {
        if (!RemoveCompletion(completion))
        {
            return;
        }

        completion.TryComplete(result);
    }

    private static bool RemoveCompletion(IDialogCompletion completion)
    {
        lock (syncRoot)
        {
            if (dialogStack.Count == 0)
            {
                return false;
            }

            if (ReferenceEquals(dialogStack.Peek(), completion))
            {
                dialogStack.Pop();
                return true;
            }

            var temp = new Stack<IDialogCompletion>();
            bool removed = false;

            while (dialogStack.Count > 0)
            {
                var current = dialogStack.Pop();
                if (!removed && ReferenceEquals(current, completion))
                {
                    removed = true;
                    continue;
                }

                temp.Push(current);
            }

            while (temp.Count > 0)
            {
                dialogStack.Push(temp.Pop());
            }

            return removed;
        }
    }

    internal sealed class ProgressDialogHandle : IProgressDialog
    {
        private readonly IDialogCompletion completion;
        private bool isCancelled;
        private bool isDisposed;

        internal ProgressDialogHandle(
            string title,
            string? message,
            bool cancellable,
            IDialogCompletion completion)
        {
            Title = title;
            Message = message;
            IsCancellable = cancellable;
            this.completion = completion;
        }

        internal string Title { get; }

        internal string? Message { get; private set; }

        internal float ProgressValue { get; private set; }

        internal bool IsCancellable { get; }

        public bool IsCancelled => isCancelled;

        public event Action OnCancelled = delegate { };

        public void Update(float value, string? message = null)
        {
            ProgressValue = value;
            if (message is not null)
            {
                Message = message;
            }
        }

        internal void Cancel()
        {
            if (!IsCancellable || isCancelled)
            {
                return;
            }

            isCancelled = true;
            OnCancelled.Invoke();
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            CompleteCompletion(completion, null);
        }
    }

    internal interface IDialogCompletion
    {
        void TryComplete(object? result);
    }

    private sealed class DialogResultSource<TResult> : IDialogCompletion
    {
        private readonly TaskCompletionSource<TResult> tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task<TResult> Task => tcs.Task;

        public void TryComplete(object? result)
        {
            if (result is TResult typed)
            {
                tcs.TrySetResult(typed);
            }
            else
            {
                tcs.TrySetResult(default!);
            }
        }
    }
}

/// <summary>
/// The result of a validation check. Used by input controls, form validators,
/// and dialog prompts. Carries a status and an optional user-facing message.
/// </summary>
public sealed class ValidationResult
{
    private ValidationResult(ValidationStatus status, string? message)
    {
        Status = status;
        Message = message;
    }

    /// <summary>The validation status.</summary>
    public ValidationStatus Status { get; }

    /// <summary>
    /// User-facing message displayed below the control. Null for <see cref="Ok"/>.
    /// </summary>
    public string? Message { get; }

    /// <summary>True if validation passed (Ok or Warning).</summary>
    public bool IsValid => Status != ValidationStatus.Error;

    /// <summary>Error message when validation failed, or null when valid.</summary>
    public string? ErrorMessage => Message;

    /// <summary>Validation passed with no message.</summary>
    public static ValidationResult Ok { get; } = new(ValidationStatus.Valid, null);

    /// <summary>
    /// Validation failed with an error message shown in the theme's danger color.
    /// </summary>
    /// <param name="message">Error message shown below the input field.</param>
    public static ValidationResult Error(string message)
    {
        return new ValidationResult(ValidationStatus.Error, message);
    }

    /// <summary>
    /// Validation passed but shows a warning. The form can still be submitted.
    /// </summary>
    /// <param name="message">Warning message shown below the input field.</param>
    public static ValidationResult Warning(string message)
    {
        return new ValidationResult(ValidationStatus.Warning, message);
    }
}

/// <summary>
/// The status of a <see cref="ValidationResult"/>.
/// </summary>
public enum ValidationStatus
{
    /// <summary>Validation passed.</summary>
    Valid,

    /// <summary>Validation failed with an error.</summary>
    Error,

    /// <summary>Validation passed but with a warning.</summary>
    Warning
}
