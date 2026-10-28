using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace Cascade.UI;

/// <summary>
/// Factory for creating reactive storage bindings that persist to
/// <see cref="LocalStorage"/> and notify subscribers on change.
/// </summary>
public static class StorageSignal
{
    /// <summary>
    /// Creates a reactive binding to a storage key identified by string.
    /// Reading <see cref="StorageSignal{T}.Value"/> in <c>Render()</c> subscribes
    /// to changes. Writing persists to storage and notifies subscribers.
    /// Uses reflection-based JSON; for trimmed or Native AOT apps use the overload taking a <see cref="JsonTypeInfo{T}"/>.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization may require runtime code generation.")]
    public static StorageSignal<T> Create<T>(string key, T fallback)
    {
        var initialValue = LocalStorage.Get(key, fallback);
        var backing = new StorageSignalBacking<T>(key, initialValue, fallback, value => LocalStorage.Set(key, value));
        return new StorageSignal<T>(backing);
    }

    /// <summary>
    /// Creates a reactive binding to a typed <see cref="StorageKey{T}"/>.
    /// Uses reflection-based JSON; for trimmed or Native AOT apps use the overload taking a <see cref="JsonTypeInfo{T}"/>.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization may require runtime code generation.")]
    public static StorageSignal<T> Create<T>(StorageKey<T> key)
    {
        return Create(key.Key, key.Fallback);
    }

    /// <summary>
    /// Creates a reactive binding to a storage key with source-generated JSON metadata
    /// (<c>MyJsonContext.Default.Int32</c> and the like) — the trimming- and Native AOT-safe overload.
    /// </summary>
    public static StorageSignal<T> Create<T>(string key, T fallback, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        var initialValue = LocalStorage.Get(key, fallback, typeInfo);
        var backing = new StorageSignalBacking<T>(key, initialValue, fallback, value => LocalStorage.Set(key, value, typeInfo));
        return new StorageSignal<T>(backing);
    }

    /// <summary>Creates a reactive binding to a typed key with source-generated JSON metadata (trimming- and AOT-safe).</summary>
    public static StorageSignal<T> Create<T>(StorageKey<T> key, JsonTypeInfo<T> typeInfo)
    {
        return Create(key.Key, key.Fallback, typeInfo);
    }
}

/// <summary>
/// A reactive value backed by <see cref="LocalStorage"/>. Reading
/// <see cref="Value"/> inside <c>Render()</c> subscribes the component to changes.
/// Writing <see cref="Value"/> persists to storage and triggers re-renders.
/// </summary>
/// <typeparam name="T">The type of the stored value.</typeparam>
public readonly struct StorageSignal<T>
{
    private readonly StorageSignalBacking<T> backing;

    internal StorageSignal(StorageSignalBacking<T> backing)
    {
        this.backing = backing;
    }

    /// <summary>
    /// Gets or sets the current value. Reading in <c>Render()</c> subscribes to
    /// changes. Writing persists to <see cref="LocalStorage"/> and notifies all
    /// subscribers.
    /// </summary>
    public T Value
    {
        get
        {
            SignalTracker.RecordRead(backing.Source);
            return backing.CurrentValue;
        }
        set
        {
            if (EqualityComparer<T>.Default.Equals(backing.CurrentValue, value))
            {
                return;
            }

            backing.CurrentValue = value;
            backing.Persist(value);
            SignalTracker.NotifyWrite(backing.Source);
        }
    }
}

/// <summary>
/// Backing store for a <see cref="StorageSignal{T}"/>. Holds the cached value,
/// the storage key, the reactive signal source, and how to persist a new value
/// (decided by the factory: reflection JSON or source-generated metadata).
/// Shared by value since <see cref="StorageSignal{T}"/> is a readonly struct.
/// </summary>
internal sealed class StorageSignalBacking<T>
{
    private readonly Action<T> persist;

    internal string Key { get; }
    internal T CurrentValue { get; set; }
    internal T Fallback { get; }
    internal SignalSource Source { get; }

    internal StorageSignalBacking(string key, T initialValue, T fallback, Action<T> persist)
    {
        Key = key;
        CurrentValue = initialValue;
        Fallback = fallback;
        this.persist = persist;
        Source = new SignalSource($"StorageSignal<{typeof(T).Name}>({key})");
    }

    internal void Persist(T value) => persist(value);
}
