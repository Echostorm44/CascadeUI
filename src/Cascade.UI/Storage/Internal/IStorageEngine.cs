namespace Cascade.UI;

/// <summary>
/// Internal abstraction over the storage backend. Implemented by
/// <see cref="JsonStorageEngine"/> for production and <see cref="InMemoryStorage"/>
/// for testing. All values are stored as serialized JSON strings.
/// </summary>
internal interface IStorageEngine
{
    /// <summary>
    /// Reads the JSON string for the given key, or null if the key does not exist.
    /// </summary>
    string? Read(string key);

    /// <summary>
    /// Writes a JSON string for the given key.
    /// </summary>
    void Write(string key, string json);

    /// <summary>
    /// Removes a key from storage.
    /// </summary>
    void Remove(string key);

    /// <summary>
    /// Returns true if the key exists in storage.
    /// </summary>
    bool Exists(string key);

    /// <summary>
    /// Removes all keys.
    /// </summary>
    void Clear();

    /// <summary>
    /// Removes all keys matching the given prefix.
    /// </summary>
    void ClearPrefix(string prefix);

    /// <summary>
    /// Returns all keys, optionally filtered by prefix.
    /// </summary>
    IReadOnlyList<string> Keys(string? prefix);
}
