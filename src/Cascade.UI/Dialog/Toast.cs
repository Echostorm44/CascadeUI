namespace Cascade.UI;

/// <summary>
/// Static methods for showing toast notifications. Toasts are brief,
/// non-modal feedback messages that appear in a corner of the screen,
/// stack when multiple arrive, and dismiss automatically. They do not
/// appear in the dialog stack and do not trap focus.
/// </summary>
public static class Toast
{
    private static readonly object syncRoot = new();
    private static readonly List<ToastEntry> activeToasts = [];

    internal static IReadOnlyList<ToastEntry> ActiveToasts => activeToasts;

    /// <summary>
    /// Hit-test bounds for each active toast, updated by the painter each frame.
    /// Each entry maps a toast ID to its screen rect and optional action button rect.
    /// </summary>
    internal static readonly List<ToastHitZone> HitZones = [];

    /// <summary>
    /// Shows a simple text toast with default type and duration.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public static void Show(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Show(new ToastOptions
        {
            Message = message
        });
    }

    /// <summary>
    /// Shows a typed toast with default icon and color accent.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="type">Visual type determining icon and color.</param>
    public static void Show(string message, ToastType type)
    {
        ArgumentNullException.ThrowIfNull(message);

        Show(new ToastOptions
        {
            Message = message,
            Type = type
        });
    }

    /// <summary>
    /// Shows a toast with an action button.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="action">An action button (e.g., "Undo").</param>
    /// <param name="duration">
    /// How long the toast remains visible. Defaults to 3 seconds.
    /// Use <see cref="Duration.Persistent"/> for no auto-dismiss.
    /// </param>
    /// <param name="type">Visual type determining icon and color.</param>
    public static void Show(
        string message,
        ToastAction action,
        Duration? duration = null,
        ToastType type = ToastType.Default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(action);

        var resolvedDuration = duration ?? Duration.Seconds(3);
        Show(new ToastOptions
        {
            Message = message,
            Type = type,
            Duration = resolvedDuration,
            Action = action
        });
    }

    /// <summary>
    /// Shows a toast with full configuration via a <see cref="ToastOptions"/> record.
    /// </summary>
    /// <param name="options">Complete toast configuration.</param>
    public static void Show(ToastOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Message);

        var entry = new ToastEntry(Guid.NewGuid(), options, Environment.TickCount64);
        lock (syncRoot)
        {
            activeToasts.Add(entry);
        }
    }

    /// <summary>
    /// Dismisses a single toast by its ID.
    /// </summary>
    internal static void Dismiss(Guid id)
    {
        lock (syncRoot)
        {
            activeToasts.RemoveAll(e => e.Id == id);
        }
    }

    /// <summary>
    /// Dismisses all currently visible toasts.
    /// </summary>
    public static void DismissAll()
    {
        lock (syncRoot)
        {
            activeToasts.Clear();
        }
    }

    /// <summary>
    /// Removes toasts whose duration has expired. Returns true if any were removed.
    /// </summary>
    /// <remarks>
    /// Zero-allocation when the list is empty (the common hot-path case: most
    /// frames have no toasts). Uses a manual loop rather than
    /// <see cref="List{T}.RemoveAll(Predicate{T})"/> because the predicate
    /// would capture <c>now</c> into a per-call closure.
    /// </remarks>
    internal static bool RemoveExpired()
    {
        // Fast path: empty list, nothing to do. This avoids taking the lock
        // and allocating a predicate closure on every frame.
        if (activeToasts.Count == 0)
        {
            return false;
        }

        long now = Environment.TickCount64;
        lock (syncRoot)
        {
            int count = activeToasts.Count;
            if (count == 0)
            {
                return false;
            }

            int writeIndex = 0;
            bool anyRemoved = false;
            for (int readIndex = 0; readIndex < count; readIndex++)
            {
                var entry = activeToasts[readIndex];
                bool expired = !entry.Options.Duration.IsPersistent
                    && (now - entry.CreatedTick) >= (long)entry.Options.Duration.TotalMilliseconds;

                if (expired)
                {
                    anyRemoved = true;
                    continue;
                }

                if (writeIndex != readIndex)
                {
                    activeToasts[writeIndex] = entry;
                }
                writeIndex++;
            }

            if (anyRemoved)
            {
                activeToasts.RemoveRange(writeIndex, count - writeIndex);
            }
            return anyRemoved;
        }
    }
}

internal sealed record ToastEntry(Guid Id, ToastOptions Options, long CreatedTick);

internal sealed class ToastHitZone
{
    public required Guid Id { get; init; }
    public required Rect Bounds { get; init; }
    public Rect ActionBounds { get; init; }
    public Action? OnAction { get; init; }
}

/// <summary>
/// Full configuration for a toast notification.
/// </summary>
public class ToastOptions
{
    /// <summary>The message to display (required).</summary>
    public required string Message { get; init; }

    /// <summary>Visual type determining default icon and color. Default: <see cref="ToastType.Default"/>.</summary>
    public ToastType Type { get; init; } = ToastType.Default;

    /// <summary>
    /// Optional custom icon. When null, the default icon for the <see cref="Type"/> is used.
    /// </summary>
    public Node? Icon { get; init; }

    /// <summary>
    /// How long the toast remains visible. Defaults to 3 seconds.
    /// Use <see cref="Duration.Persistent"/> for no auto-dismiss.
    /// </summary>
    public Duration Duration { get; init; } = Duration.Seconds(3);

    /// <summary>
    /// Screen position where the toast appears. Default: <see cref="ToastPosition.BottomRight"/>.
    /// </summary>
    public ToastPosition Position { get; init; } = ToastPosition.BottomRight;

    /// <summary>
    /// Optional action button displayed on the toast.
    /// </summary>
    public ToastAction? Action { get; init; }
}
