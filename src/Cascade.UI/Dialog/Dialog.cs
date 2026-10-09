namespace Cascade.UI;

/// <summary>
/// Shows modal dialogs. High-level factory methods (Alert, Confirm, Prompt, Progress) cover
/// the common cases; <see cref="ShowAsync{TComponent,TResult}(DialogOptions?)"/> shows any
/// component as a dialog.
/// </summary>
/// <remarks>
/// <para>
/// A dialog is drawn over a dimmed backdrop above the whole window and is modal: the pointer
/// and keyboard cannot reach anything beneath it, Tab cycles inside it, and focus returns to
/// where it was when it closes. Escape (and a click on the backdrop) dismisses it unless
/// <see cref="DialogOptions.Dismissable"/> is false. Dialogs stack: a dialog can open
/// another, and the newest is on top.
/// </para>
/// <para>
/// Every method may be called from any thread; the dialog is shown on the UI thread. The
/// returned task completes when the dialog closes — with the value passed to
/// <see cref="Return{TResult}"/> / <c>DialogContext.Close(value)</c>, or null (false for
/// <see cref="ConfirmAsync"/>) when it is dismissed. The methods need a running window
/// (after <c>App.Run</c> has started).
/// </para>
/// </remarks>
public static class Dialog
{
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

        return Open<bool>(
            nameof(AlertAsync),
            (manager, completion) => BuiltIn(manager, new AlertDialogView(title, message, button), completion, title, AccessibleRole.Dialog, dismissable: true));
    }

    /// <summary>
    /// Shows a binary decision dialog. Returns true if confirmed, false if cancelled
    /// (the cancel button, Escape, or a click on the backdrop).
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Decision message text.</param>
    /// <param name="confirmLabel">Label for the confirm button.</param>
    /// <param name="cancelLabel">Label for the cancel button.</param>
    /// <param name="defaultButton">Which button has focus when the dialog opens, so Enter activates it.</param>
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

        var role = style == DialogStyle.Destructive ? AccessibleRole.AlertDialog : AccessibleRole.Dialog;
        return Open<bool>(
            nameof(ConfirmAsync),
            (manager, completion) => BuiltIn(
                manager,
                new ConfirmDialogView(title, message, confirmLabel, cancelLabel, defaultButton, style),
                completion,
                title,
                role,
                dismissable: true));
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
    /// Validation function called on every keystroke. The confirm button (and Enter) is
    /// disabled while it returns an error; its message is shown below the field once the user
    /// has typed.
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

        return Open<string>(
            nameof(PromptAsync),
            (manager, completion) => BuiltIn(
                manager,
                new PromptDialogView(title, message, placeholder, value ?? string.Empty, confirmLabel, cancelLabel, validate),
                completion,
                title,
                AccessibleRole.Dialog,
                dismissable: true));
    }

    // ── Progress dialog ───────────────────────────────────────────────

    /// <summary>
    /// Shows a modal progress dialog for long operations. Returns an
    /// <see cref="IProgressDialog"/> handle that controls advancement and
    /// dismissal. Dispose the handle to close the dialog. The dialog cannot be dismissed with
    /// Escape or the backdrop.
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

        var handle = new ProgressDialogHandle(title, message, cancellable);
        var source = new OverlayResultSource<bool>();
        OverlayThreading.RunOnUiThread(
            () =>
            {
                if (handle.IsDisposed)
                {
                    return;
                }

                var manager = OverlayThreading.RequireManager(nameof(ShowProgress));
                var entry = BuiltIn(manager, new ProgressDialogView(handle), source, title, AccessibleRole.Dialog, dismissable: false);
                handle.Entry = entry;
                manager.Open(entry);
            },
            source.Fail);
        return handle;
    }

    // ── Custom dialogs ────────────────────────────────────────────────

    /// <summary>
    /// Opens a new <typeparamref name="TComponent"/> as a modal dialog and completes when it
    /// closes. The component closes it with <c>DialogContext.Close(value)</c> or
    /// <see cref="Return{TResult}"/> (the task receives the value) or with
    /// <c>DialogContext.Close()</c> / <see cref="Dismiss"/> (the task receives null).
    /// </summary>
    /// <typeparam name="TComponent">The dialog component type.</typeparam>
    /// <typeparam name="TResult">The result type returned by the dialog.</typeparam>
    /// <param name="options">Dialog configuration options.</param>
    public static Task<TResult?> ShowAsync<TComponent, TResult>(
        DialogOptions? options = null)
        where TComponent : Component, new()
    {
        var resolved = options ?? new DialogOptions();
        return Open<TResult>(
            nameof(ShowAsync),
            (manager, completion) => Custom(manager, OverlayKind.Dialog, new TComponent(), completion, resolved));
    }

    /// <summary>
    /// Opens a new <typeparamref name="TComponent"/> as a modal dialog with no return value;
    /// completes when it closes.
    /// </summary>
    /// <typeparam name="TComponent">The dialog component type.</typeparam>
    /// <param name="options">Dialog configuration options.</param>
    public static Task ShowAsync<TComponent>(
        DialogOptions? options = null)
        where TComponent : Component, new()
    {
        var resolved = options ?? new DialogOptions();
        return Open<object>(
            nameof(ShowAsync),
            (manager, completion) => Custom(manager, OverlayKind.Dialog, new TComponent(), completion, resolved));
    }

    /// <summary>
    /// Opens <paramref name="content"/> as a modal dialog and completes with its result when it
    /// closes. Use this overload to pass the dialog its inputs:
    /// <c>await Dialog.ShowAsync&lt;string&gt;(new RenameDialog { CurrentName = name })</c>.
    /// Pass a new instance each time; a component cannot be shown twice.
    /// </summary>
    /// <typeparam name="TResult">The result type returned by the dialog.</typeparam>
    /// <param name="content">The dialog's content component.</param>
    /// <param name="options">Dialog configuration options.</param>
    public static Task<TResult?> ShowAsync<TResult>(Component content, DialogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var resolved = options ?? new DialogOptions();
        return Open<TResult>(
            nameof(ShowAsync),
            (manager, completion) => Custom(manager, OverlayKind.Dialog, content, completion, resolved));
    }

    /// <summary>
    /// Opens <paramref name="content"/> as a modal dialog with no return value; completes when
    /// it closes. Pass a new instance each time; a component cannot be shown twice.
    /// </summary>
    /// <param name="content">The dialog's content component.</param>
    /// <param name="options">Dialog configuration options.</param>
    public static Task ShowAsync(Component content, DialogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var resolved = options ?? new DialogOptions();
        return Open<object>(
            nameof(ShowAsync),
            (manager, completion) => Custom(manager, OverlayKind.Dialog, content, completion, resolved));
    }

    // ── Closing from inside ───────────────────────────────────────────

    /// <summary>
    /// Closes the current dialog and resolves its awaiting <c>ShowAsync</c> call with
    /// <paramref name="value"/>. The current dialog is the one handling the input event that
    /// calls this (a button inside it), else the topmost. Prefer <c>DialogContext.Close(value)</c>,
    /// which always names the dialog the component is in.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="value">The value to return to the caller.</param>
    /// <exception cref="InvalidOperationException">No dialog, sheet or popover is open (when called on the UI thread).</exception>
    public static void Return<TResult>(TResult value)
    {
        CloseCurrent(value, nameof(Return));
    }

    /// <summary>
    /// Closes the current dialog (or popover) without a value: the awaiting call receives null
    /// (false for <see cref="ConfirmAsync"/>). The same as the user pressing Escape.
    /// </summary>
    /// <exception cref="InvalidOperationException">No dialog, sheet or popover is open (when called on the UI thread).</exception>
    public static void Dismiss()
    {
        CloseCurrent(null, nameof(Dismiss));
    }

    // ── Internals ─────────────────────────────────────────────────────

    /// <summary>Builds the entry and opens it on the UI thread; the task completes when it closes.</summary>
    internal static Task<TResult?> Open<TResult>(string api, Func<OverlayManager, IOverlayCompletion, OverlayEntry> build)
    {
        var source = new OverlayResultSource<TResult?>();
        OverlayThreading.RunOnUiThread(
            () =>
            {
                var manager = OverlayThreading.RequireManager($"Dialog.{api}");
                manager.Open(build(manager, source));
            },
            source.Fail);
        return source.Task;
    }

    /// <summary>An entry for a caller's component shown with <paramref name="options"/>.</summary>
    internal static OverlayEntry Custom(
        OverlayManager manager,
        OverlayKind kind,
        Component content,
        IOverlayCompletion completion,
        DialogOptions options,
        string? accessibleLabel = null)
    {
        ClaimContent(content);
        return new OverlayEntry(manager, kind, content, completion)
        {
            Title = options.Title,
            AccessibleLabel = options.Title ?? accessibleLabel,
            Role = options.Style == DialogStyle.Destructive ? AccessibleRole.AlertDialog : AccessibleRole.Dialog,
            Dismissable = options.Dismissable,
            ShowBackdrop = options.ShowBackdrop,
            BackdropOpacity = options.BackdropOpacity,
            BlocksInput = true,
            Size = options.Size,
            Position = options.Position.Kind,
            Anchor = options.Position.Anchor,
            Animation = options.Animation.Kind,
            EnterModel = options.Animation.EnterModel,
            ExitModel = options.Animation.ExitModel,
        };
    }

    /// <summary>
    /// Marks <paramref name="content"/> as the content of a new overlay.
    /// </summary>
    /// <exception cref="InvalidOperationException">It is already the content of an overlay.</exception>
    internal static void ClaimContent(Component content)
    {
        if (content.OverlayEntry is not null)
        {
            throw new InvalidOperationException(
                $"This {content.GetType().Name} instance is already shown in (or was shown in) a dialog, sheet or popover. Pass a new instance each time.");
        }
    }

    private static OverlayEntry BuiltIn(
        OverlayManager manager,
        Component content,
        IOverlayCompletion completion,
        string title,
        AccessibleRole role,
        bool dismissable)
    {
        return new OverlayEntry(manager, OverlayKind.Dialog, content, completion)
        {
            AccessibleLabel = title,
            Role = role,
            Dismissable = dismissable,
            ShowBackdrop = true,
            BlocksInput = true,
            Animation = DialogAnimationKind.Scale,
        };
    }

    private static void CloseCurrent(object? value, string api)
    {
        OverlayThreading.RunOnUiThread(() =>
        {
            var manager = InputDispatcher.Active?.Overlays;
            var target = manager?.CurrentTarget()
                ?? throw new InvalidOperationException($"Dialog.{api}: no dialog, sheet or popover is open.");
            manager!.Close(target, value);
        });
    }

    /// <summary>The <see cref="IProgressDialog"/> returned by <see cref="ShowProgress"/>.</summary>
    internal sealed class ProgressDialogHandle : IProgressDialog
    {
        private readonly Lock gate = new();
        private string? message;
        private float progressValue;
        private bool isCancelled;
        private bool isDisposed;

        internal ProgressDialogHandle(string title, string? message, bool cancellable)
        {
            Title = title;
            this.message = message;
            IsCancellable = cancellable;
        }

        internal string Title { get; }

        internal string? Message
        {
            get
            {
                lock (gate)
                {
                    return message;
                }
            }
        }

        internal float ProgressValue
        {
            get
            {
                lock (gate)
                {
                    return progressValue;
                }
            }
        }

        internal bool IsCancellable { get; }

        internal bool IsDisposed
        {
            get
            {
                lock (gate)
                {
                    return isDisposed;
                }
            }
        }

        /// <summary>The dialog showing this progress (set on the UI thread when it opens).</summary>
        internal OverlayEntry? Entry { get; set; }

        /// <summary>The view to refresh on <see cref="Update"/> (set when it mounts).</summary>
        internal ProgressDialogView? View { get; set; }

        public bool IsCancelled
        {
            get
            {
                lock (gate)
                {
                    return isCancelled;
                }
            }
        }

        public event Action OnCancelled = delegate { };

        public void Update(float value, string? message = null)
        {
            lock (gate)
            {
                progressValue = Math.Clamp(value, 0f, 1f);
                if (message is not null)
                {
                    this.message = message;
                }
            }

            OverlayThreading.RunOnUiThread(() =>
            {
                View?.Refresh();
                if (message is { Length: > 0 } && Entry is { IsClosing: false })
                {
                    Accessibility.Announce(message, AnnouncePriority.Low);
                }
            });
        }

        /// <summary>The Cancel button: marks the operation cancelled and raises <see cref="OnCancelled"/> once.</summary>
        internal void Cancel()
        {
            lock (gate)
            {
                if (!IsCancellable || isCancelled)
                {
                    return;
                }

                isCancelled = true;
            }

            OnCancelled.Invoke();
            View?.Refresh();
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
            }

            OverlayThreading.RunOnUiThread(() =>
            {
                if (Entry is { } entry)
                {
                    entry.Manager.Close(entry, null);
                }
            });
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
