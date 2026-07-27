namespace Cascade.UI;

/// <summary>
/// Handle for a progress dialog that controls advancement and dismissal.
/// Dispose the handle to close the dialog. Use <c>using</c> blocks to
/// ensure the dialog is closed regardless of how the operation completes.
/// </summary>
public interface IProgressDialog : IDisposable
{
    /// <summary>
    /// True if the user has clicked the Cancel button.
    /// </summary>
    bool IsCancelled { get; }

    /// <summary>
    /// Updates the progress dialog with a new value and optional message.
    /// </summary>
    /// <param name="value">Progress value from 0.0 to 1.0.</param>
    /// <param name="message">Optional updated status message.</param>
    void Update(float value, string? message = null);

    /// <summary>
    /// Raised when the user clicks the Cancel button (if the dialog was
    /// created with <c>cancellable: true</c>). The dialog does not close
    /// automatically — the developer disposes the handle after reacting
    /// to cancellation.
    /// </summary>
    event Action OnCancelled;
}
