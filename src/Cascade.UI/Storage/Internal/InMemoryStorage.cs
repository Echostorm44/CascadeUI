namespace Cascade.UI;

/// <summary>
/// In-memory storage engine for testing. Same API as <see cref="JsonStorageEngine"/>
/// but backed by a dictionary with no file I/O.
/// </summary>
internal sealed class InMemoryStorage : IStorageEngine
{
    private readonly Lock syncLock = new();
    private readonly Dictionary<string, string> store = new();

    public string? Read(string key)
    {
        lock (syncLock)
        {
            return store.GetValueOrDefault(key);
        }
    }

    public void Write(string key, string json)
    {
        lock (syncLock)
        {
            store[key] = json;
        }
    }

    public void Remove(string key)
    {
        lock (syncLock)
        {
            store.Remove(key);
        }
    }

    public bool Exists(string key)
    {
        lock (syncLock)
        {
            return store.ContainsKey(key);
        }
    }

    public void Clear()
    {
        lock (syncLock)
        {
            store.Clear();
        }
    }

    public void ClearPrefix(string prefix)
    {
        lock (syncLock)
        {
            var keysToRemove = store.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();

            foreach (var key in keysToRemove)
            {
                store.Remove(key);
            }
        }
    }

    public IReadOnlyList<string> Keys(string? prefix)
    {
        lock (syncLock)
        {
            if (prefix is null)
            {
                return store.Keys.ToList();
            }

            return store.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();
        }
    }
}
