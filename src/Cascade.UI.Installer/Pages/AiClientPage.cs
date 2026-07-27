#pragma warning disable CA2227

namespace Cascade.UI.Installer.Pages;

/// <summary>
/// Wizard page that displays detected AI clients with checkboxes.
/// Installed clients can be checked/unchecked; not-installed clients
/// are shown greyed out and disabled.
/// </summary>
/// <remarks>
/// Automatically inserted at <see cref="PagePosition.BeforeInstall"/> when
/// <see cref="InstallerConfig.AiClients"/> is set. On install, writes MCP
/// server entries to each selected client's config file. On upgrade, updates
/// entries if the exe path changed. On uninstall, removes entries.
/// </remarks>
public sealed class AiClientPage : WizardPage
{
    public AiClientPage()
    {
        Title = "AI Assistant Integration";
        Description = "Connect this app to AI assistants that support the Model Context Protocol (MCP).";
        Position = PagePosition.BeforeInstall;
    }

    /// <summary>
    /// The client entries to display. Each entry includes the client definition
    /// and whether it should be pre-selected.
    /// </summary>
    public IReadOnlyList<AiClientEntry> Clients { get; init; } = [];

    /// <summary>
    /// The set of client names the user has selected. Initialized from the
    /// pre-selected entries; updated by the wizard UI when checkboxes change.
    /// </summary>
    public HashSet<string> SelectedClients { get; set; } = [];

    /// <summary>
    /// Populates the page from an <see cref="AiClientIntegrations"/> builder.
    /// Sets up the client list and default selections based on what's installed.
    /// </summary>
    public static AiClientPage FromIntegrations(AiClientIntegrations integrations)
    {
        ArgumentNullException.ThrowIfNull(integrations);

        var page = new AiClientPage
        {
            Clients = integrations.Entries,
        };

        // Pre-select clients that are both preselected and installed
        foreach (var entry in integrations.Entries)
        {
            if (entry.Preselected && entry.Client.IsInstalled())
            {
                page.SelectedClients.Add(entry.Client.Name);
            }
        }

        return page;
    }

    /// <summary>
    /// Returns only the entries for clients that are installed on this machine.
    /// </summary>
    public IEnumerable<AiClientEntry> InstalledClients =>
        Clients.Where(e => e.Client.IsInstalled());

    /// <summary>
    /// Returns only the entries for clients that are not installed.
    /// </summary>
    public IEnumerable<AiClientEntry> NotInstalledClients =>
        Clients.Where(e => !e.Client.IsInstalled());

    /// <summary>
    /// Writes MCP server entries to all selected clients' config files.
    /// Called during install.
    /// </summary>
    public void WriteSelectedEntries(string serverKey, string commandPath, string[] args, string? description)
    {
        foreach (var entry in Clients)
        {
            if (SelectedClients.Contains(entry.Client.Name) && entry.Client.IsInstalled())
            {
                entry.Client.WriteEntry(serverKey, commandPath, args, description);
            }
        }
    }

    /// <summary>
    /// Updates MCP server entries for all clients that have an existing entry.
    /// Called during upgrade when the exe path may have changed.
    /// </summary>
    public void UpdateExistingEntries(string serverKey, string commandPath, string[] args, string? description)
    {
        foreach (var entry in Clients)
        {
            if (entry.Client.EntryExists(serverKey))
            {
                entry.Client.WriteEntry(serverKey, commandPath, args, description);
            }
        }
    }

    /// <summary>
    /// Removes MCP server entries from all clients' config files.
    /// Called during uninstall.
    /// </summary>
    public void RemoveAllEntries(string serverKey)
    {
        foreach (var entry in Clients)
        {
            entry.Client.RemoveEntry(serverKey);
        }
    }
}
