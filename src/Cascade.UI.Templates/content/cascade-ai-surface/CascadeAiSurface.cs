using Cascade.UI;

namespace CascadeAiSurface.Namespace;

/// <summary>
/// An AI-accessible surface that exposes tools and context to AI agents.
/// The [AiSurface] attribute makes this component discoverable via MCP.
/// </summary>
[AiSurface("CascadeAiSurface provides an AI-accessible interface for managing application data and user interactions.")]
public class CascadeAiSurface : Component
{
    private string query = "";

    [AiTool("Search for items matching the given query")]
    public Task<string> Search(string query)
    {
        this.query = query;
        return Task.FromResult($"Searching for: {query}");
    }

    [AiContext("Current search query entered by the user")]
    public string CurrentQuery => query;

    protected override Node Render() =>
        Column(
            children: new Node[]
            {
                TextInput(
                    value: Bind(query),
                    label:  "Search",
                    placeholder: "Enter search query..."
                ),
                Label($"Results for: {query}"),
            }
        );
}
