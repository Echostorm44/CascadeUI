namespace Cascade.UI;

/// <summary>
/// Pre-built settings component that displays all known and custom AI clients
/// with their connection status and Connect/Disconnect actions.
/// </summary>
/// <remarks>
/// <para>Drop into any settings page:</para>
/// <code>
/// new AiSurfaceSettings(integrations, serverKey, commandPath, args)
/// </code>
/// <para>
/// The component builds its node tree at construction time and rebuilds after
/// Connect/Disconnect actions. The mutable <c>renderedTree</c> field triggers
/// re-rendering through the reactive system.
/// </para>
/// </remarks>
public partial class AiSurfaceSettings : Component
{
    private readonly AiClientIntegrations integrations;
    private readonly string serverKey;
    private readonly string commandPath;
    private readonly string[] args;
    private readonly string? description;
    private Node renderedTree;

    /// <summary>
    /// Creates the AI surface settings panel.
    /// </summary>
    /// <param name="integrations">The client integrations builder.</param>
    /// <param name="serverKey">The MCP server key used in config files.</param>
    /// <param name="commandPath">Absolute path to the application executable.</param>
    /// <param name="args">Command-line arguments for the MCP server.</param>
    /// <param name="description">Optional description written into the config entry.</param>
    public AiSurfaceSettings(
        AiClientIntegrations integrations,
        string serverKey,
        string commandPath,
        string[] args,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(integrations);
        ArgumentNullException.ThrowIfNull(serverKey);
        ArgumentNullException.ThrowIfNull(commandPath);
        ArgumentNullException.ThrowIfNull(args);

        this.integrations = integrations;
        this.serverKey = serverKey;
        this.commandPath = commandPath;
        this.args = args;
        this.description = description;
        renderedTree = BuildTree();
    }

    /// <inheritdoc/>
    protected override Node Render()
    {
        return renderedTree;
    }

    private Node BuildTree()
    {
        if (integrations.Entries.Count == 0)
        {
            return new Label("No AI clients configured.");
        }

        var children = new Node[integrations.Entries.Count];
        for (int i = 0; i < integrations.Entries.Count; i++)
        {
            children[i] = BuildClientRow(integrations.Entries[i]);
        }

        return new Column(spacing: 8, children: children);
    }

    private Node BuildClientRow(AiClientEntry entry)
    {
        var client = entry.Client;
        bool installed = client.IsInstalled();
        bool connected = installed && client.EntryExists(serverKey);

        string status = connected ? "Connected" : installed ? "Not connected" : "Not installed";

        var info = new Column(
            spacing: 2,
            children: [new Label(client.Name), new Label(client.Description), new Label(status)]);

        Node action;
        if (!installed)
        {
            action = Node.Empty;
        }
        else if (connected)
        {
            action = new Button("Disconnect", onClick: () => { Disconnect(client); });
        }
        else
        {
            action = new Button("Connect", onClick: () => { Connect(client); });
        }

        return new Row(
            spacing: 12,
            crossAxisAlignment: CrossAxisAlignment.Center,
            children: [info, action]);
    }

    private void Connect(AiClientDefinition client)
    {
        client.WriteEntry(serverKey, commandPath, args, description);
        renderedTree = BuildTree();
    }

    private void Disconnect(AiClientDefinition client)
    {
        client.RemoveEntry(serverKey);
        renderedTree = BuildTree();
    }
}
