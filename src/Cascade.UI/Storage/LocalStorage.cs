using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cascade.UI;

/// <summary>
/// Provides persistent key-value storage for application data that survives
/// app restarts. All operations are synchronous and thread-safe. Data is scoped
/// per application identity.
/// </summary>
public static class LocalStorage
{
    private static IStorageEngine engine = new JsonStorageEngine();
    private static readonly Lock optionsLock = new();
    private static JsonSerializerOptions serializerOptions = new();

    /// <summary>
    /// Raised when a key is modified or removed. The argument is the affected key.
    /// </summary>
    public static event Action<string>? Changed;

    /// <summary>
    /// Gets a value by string key, returning <paramref name="fallback"/> if the key
    /// does not exist or deserialization fails.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization may require runtime code generation.")]
    public static T Get<T>(string key, T fallback)
    {
        ArgumentNullException.ThrowIfNull(key);

        var json = engine.Read(key);
        if (json is null)
        {
            return fallback;
        }

        try
        {
            JsonSerializerOptions options;
            lock (optionsLock)
            {
                options = serializerOptions;
            }

            return JsonSerializer.Deserialize<T>(json, options) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    /// <summary>
    /// Gets a value by string key, returning <c>default(T)</c> if the key
    /// does not exist or deserialization fails.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization may require runtime code generation.")]
    public static T? Get<T>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var json = engine.Read(key);
        if (json is null)
        {
            return default;
        }

        try
        {
            JsonSerializerOptions options;
            lock (optionsLock)
            {
                options = serializerOptions;
            }

            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>
    /// Gets a value using a typed <see cref="StorageKey{T}"/>, returning the
    /// key's declared fallback if the key does not exist or deserialization fails.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization may require runtime code generation.")]
    public static T Get<T>(StorageKey<T> storageKey)
    {
        return Get(storageKey.Key, storageKey.Fallback);
    }

    /// <summary>
    /// Sets a value by string key. The value is persisted immediately.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization may require runtime code generation.")]
    public static void Set<T>(string key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);

        JsonSerializerOptions options;
        lock (optionsLock)
        {
            options = serializerOptions;
        }

        var json = JsonSerializer.Serialize(value, options);
        engine.Write(key, json);
        Changed?.Invoke(key);
    }

    /// <summary>
    /// Sets a value using a typed <see cref="StorageKey{T}"/>. The value is
    /// persisted immediately and type-checked at compile time.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization may require runtime code generation.")]
    public static void Set<T>(StorageKey<T> storageKey, T value)
    {
        Set(storageKey.Key, value);
    }

    /// <summary>
    /// Removes a key from storage.
    /// </summary>
    public static void Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        engine.Remove(key);
        Changed?.Invoke(key);
    }

    /// <summary>
    /// Removes a typed <see cref="StorageKey{T}"/> from storage.
    /// </summary>
    public static void Remove<T>(StorageKey<T> storageKey)
    {
        Remove(storageKey.Key);
    }

    /// <summary>
    /// Returns <c>true</c> if the given string key exists in storage.
    /// </summary>
    public static bool Exists(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return engine.Exists(key);
    }

    /// <summary>
    /// Returns <c>true</c> if the given typed key exists in storage.
    /// </summary>
    public static bool Exists<T>(StorageKey<T> storageKey)
    {
        return Exists(storageKey.Key);
    }

    /// <summary>
    /// Removes all keys scoped to this application.
    /// </summary>
    public static void Clear()
    {
        engine.Clear();
    }

    /// <summary>
    /// Removes all keys matching <paramref name="keyPrefix"/>.
    /// </summary>
    public static void Clear(string keyPrefix)
    {
        ArgumentNullException.ThrowIfNull(keyPrefix);
        engine.ClearPrefix(keyPrefix);
    }

    /// <summary>
    /// Lists all stored keys, optionally filtered by a prefix.
    /// </summary>
    public static IReadOnlyList<string> Keys(string? prefix = null)
    {
        return engine.Keys(prefix);
    }

    /// <summary>
    /// Configures LocalStorage options such as custom JSON converters.
    /// </summary>
    public static void Configure(Action<LocalStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new LocalStorageOptions();
        configure(options);

        lock (optionsLock)
        {
            serializerOptions = options.BuildSerializerOptions();
        }
    }

    /// <summary>
    /// Replaces the storage engine. Used by the test framework to substitute
    /// <see cref="InMemoryStorage"/> for <see cref="JsonStorageEngine"/>.
    /// </summary>
    internal static void UseEngine(IStorageEngine storageEngine)
    {
        ArgumentNullException.ThrowIfNull(storageEngine);
        engine = storageEngine;
    }

    /// <summary>
    /// Resets LocalStorage to default state. Used by tests to restore isolation.
    /// </summary>
    internal static void ResetForTesting()
    {
        engine = new InMemoryStorage();
        lock (optionsLock)
        {
            serializerOptions = new JsonSerializerOptions();
        }

        Changed = null;
    }
}
