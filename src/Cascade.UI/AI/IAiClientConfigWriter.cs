namespace Cascade.UI;

/// <summary>
/// Interface for reading and writing AI client MCP configuration entries.
/// The common JSON format is handled by <see cref="JsonMcpConfigWriter"/>;
/// implement this interface for clients with non-standard formats.
/// </summary>
public interface IAiClientConfigWriter
{
    /// <summary>
    /// Returns true if an MCP server entry for this app already exists
    /// in the config file at <paramref name="configPath"/>.
    /// </summary>
    bool EntryExists(string configPath, string serverKey);

    /// <summary>
    /// Writes or updates the MCP server entry in the config file.
    /// Creates the file if it does not exist.
    /// </summary>
    void WriteEntry(string configPath, string serverKey, string commandPath, string[] args, string? description);

    /// <summary>
    /// Removes the MCP server entry from the config file.
    /// Does nothing if the entry does not exist.
    /// </summary>
    void RemoveEntry(string configPath, string serverKey);
}
