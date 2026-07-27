namespace Cascade.UI;

/// <summary>
/// Builder for configuring which AI clients an app integrates with.
/// Used in installer configuration to declare opt-in AI client setup.
/// </summary>
/// <remarks>
/// <para>Basic usage with all known clients pre-selected:</para>
/// <code>
/// AiClientIntegrations.Default()
/// </code>
/// <para>With selective pre-selection:</para>
/// <code>
/// AiClientIntegrations.Default(
///     preselected: [KnownAiClient.ClaudeDesktop, KnownAiClient.ContinueDev]
/// )
/// </code>
/// <para>With custom clients:</para>
/// <code>
/// AiClientIntegrations.Default()
///     .Add(new CustomAiClient { ... })
/// </code>
/// </remarks>
public sealed class AiClientIntegrations
{
    private readonly List<AiClientEntry> entries = [];

    /// <summary>
    /// Creates integrations with all known clients, all pre-selected by default.
    /// </summary>
    public static AiClientIntegrations Default()
    {
        var integrations = new AiClientIntegrations();
        foreach (var client in KnownAiClient.All)
        {
            integrations.entries.Add(new AiClientEntry(client, true));
        }
        return integrations;
    }

    /// <summary>
    /// Creates integrations with all known clients. Only those in
    /// <paramref name="preselected"/> are checked by default; others are shown
    /// but unchecked.
    /// </summary>
    public static AiClientIntegrations Default(params AiClientDefinition[] preselected)
    {
        var preselectedSet = new HashSet<AiClientDefinition>(preselected);
        var integrations = new AiClientIntegrations();
        foreach (var client in KnownAiClient.All)
        {
            integrations.entries.Add(new AiClientEntry(client, preselectedSet.Contains(client)));
        }
        return integrations;
    }

    /// <summary>
    /// Creates an empty integrations builder with no clients.
    /// </summary>
    public static AiClientIntegrations Empty() => new();

    /// <summary>
    /// Adds a custom AI client definition to the integrations list.
    /// Custom clients are always pre-selected.
    /// </summary>
    public AiClientIntegrations Add(CustomAiClient custom)
    {
        ArgumentNullException.ThrowIfNull(custom);
        entries.Add(new AiClientEntry(custom.ToDefinition(), true));
        return this;
    }

    /// <summary>
    /// Returns all client entries in the order they were added.
    /// </summary>
    public IReadOnlyList<AiClientEntry> Entries => entries;

    /// <summary>
    /// Generates a stable MCP server key for the given app ID.
    /// Format: <c>cascade-{short-guid}</c> where short-guid is the first
    /// 8 characters of the AppId GUID without hyphens, lowercased.
    /// </summary>
#pragma warning disable CA1308 // Normalize strings to uppercase — spec requires lowercase server keys
    public static string ServerKeyForApp(string appId)
    {
        ArgumentNullException.ThrowIfNull(appId);
        string clean = appId.Replace("-", "", StringComparison.Ordinal)
            .Replace("{", "", StringComparison.Ordinal)
            .Replace("}", "", StringComparison.Ordinal);
        string shortId = clean.Length >= 8 ? clean[..8].ToLowerInvariant() : clean.ToLowerInvariant();
        return $"cascade-{shortId}";
    }
#pragma warning restore CA1308
}

/// <summary>
/// An AI client with its pre-selection state for the installer wizard.
/// </summary>
public sealed record AiClientEntry(AiClientDefinition Client, bool Preselected);
