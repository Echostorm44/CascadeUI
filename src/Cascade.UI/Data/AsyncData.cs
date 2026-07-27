namespace Cascade.UI;

/// <summary>
/// A discriminated union representing the state of an asynchronous value.
/// Can be Loading, Loaded, Error, or Refreshing. This is a value type that
/// cannot be null — the <see cref="State"/> enum forces explicit handling
/// of all states.
/// </summary>
/// <typeparam name="T">The type of the loaded value.</typeparam>
public readonly record struct AsyncData<T>
{
    /// <summary>
    /// The current state of the async operation.
    /// </summary>
    public AsyncDataState State { get; private init; }

    /// <summary>
    /// The loaded value. Valid only when <see cref="State"/> is
    /// <see cref="AsyncDataState.Success"/> or <see cref="AsyncDataState.Refreshing"/>.
    /// </summary>
    public T Value { get; private init; }

    /// <summary>
    /// The error from a failed fetch. Valid only when <see cref="State"/> is
    /// <see cref="AsyncDataState.Error"/>.
    /// </summary>
    public Exception? Error { get; private init; }

    /// <summary>
    /// True when data has been loaded at least once (<see cref="Value"/> is available
    /// even during a refresh). False only during initial load.
    /// </summary>
    public bool HasValue { get; private init; }

    /// <summary>
    /// True when currently loading (initial load or refresh).
    /// </summary>
    public bool IsLoading => State is AsyncDataState.Loading or AsyncDataState.Refreshing;

    /// <summary>
    /// True when in error state.
    /// </summary>
    public bool IsError => State == AsyncDataState.Error;

    /// <summary>
    /// True when loaded and not refreshing.
    /// </summary>
    public bool IsReady => State == AsyncDataState.Success;

    /// <summary>
    /// Creates an <see cref="AsyncData{T}"/> in the Loading state with no value.
    /// </summary>
    public static AsyncData<T> Loading() => new()
    {
        State = AsyncDataState.Loading,
        Value = default!,
        Error = null,
        HasValue = false
    };

    /// <summary>
    /// Creates an <see cref="AsyncData{T}"/> in the Loaded state with the given value.
    /// </summary>
    public static AsyncData<T> Loaded(T value) => new()
    {
        State = AsyncDataState.Success,
        Value = value,
        Error = null,
        HasValue = true
    };

    /// <summary>
    /// Creates an <see cref="AsyncData{T}"/> in the Error state with the given exception.
    /// </summary>
    public static AsyncData<T> Failed(Exception error) => new()
    {
        State = AsyncDataState.Error,
        Value = default!,
        Error = error,
        HasValue = false
    };

    /// <summary>
    /// Creates an <see cref="AsyncData{T}"/> in the Refreshing state, retaining
    /// the <paramref name="previousValue"/> while a new fetch is in progress.
    /// </summary>
    public static AsyncData<T> Refreshing(T previousValue) => new()
    {
        State = AsyncDataState.Refreshing,
        Value = previousValue,
        Error = null,
        HasValue = true
    };
}
