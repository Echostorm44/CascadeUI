namespace Cascade.UI;

/// <summary>
/// Controls when a <see cref="PersistStateAttribute"/>-marked field is persisted.
/// </summary>
public enum PersistWhen
{
    /// <summary>
    /// Persist on every change (default). Ensures crash recovery.
    /// </summary>
    Immediate,

    /// <summary>
    /// Persist only when the app is closing. Suitable for frequently changing
    /// state that only needs to survive app restart, not crash.
    /// </summary>
    AppClose,

    /// <summary>
    /// Persist only when <c>PersistState.Save()</c> is called explicitly.
    /// </summary>
    Manual
}
