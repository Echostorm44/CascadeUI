namespace Cascade.UI;

/// <summary>
/// A typed key for <see cref="LocalStorage"/> access. Provides compile-time type
/// safety and a fallback value for when the key does not exist or deserialization fails.
/// </summary>
/// <typeparam name="T">The type of the stored value.</typeparam>
/// <param name="Key">The string key used for storage lookup.</param>
/// <param name="Fallback">
/// The default value returned when the key does not exist or deserialization fails.
/// </param>
public readonly record struct StorageKey<T>(string Key, T Fallback = default!);
