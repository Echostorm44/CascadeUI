namespace Cascade.UI;

/// <summary>
/// Represents the state of an asynchronous value.
/// </summary>
public enum AsyncDataState
{
    /// <summary>Initial load — no value yet.</summary>
    Loading,

    /// <summary>Value is available.</summary>
    Success,

    /// <summary>Failed — error is available.</summary>
    Error,

    /// <summary>Reloading — previous value is still available.</summary>
    Refreshing
}
