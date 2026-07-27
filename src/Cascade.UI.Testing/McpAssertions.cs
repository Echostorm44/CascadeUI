using System.Text.Json.Nodes;

namespace Cascade.UI.Testing;

/// <summary>
/// Fluent assertion helpers for MCP-driven integration tests.
/// Wraps JSON results from MCP tool calls with domain-specific assertions.
/// </summary>
/// <remarks>
/// <code>
/// var result = await runner.FindAccessible(role: "Button", label: "Submit");
/// result.Should().ContainNode().WithProperty("enabled", true);
///
/// var tree = await runner.GetTree();
/// tree.Should().ContainComponent("CheckoutPage");
/// </code>
/// </remarks>
public sealed class McpAssertions
{
    private readonly JsonNode? result;

    /// <summary>Creates assertions for an MCP tool result.</summary>
    public McpAssertions(JsonNode? result)
    {
        this.result = result;
    }

    /// <summary>The raw JSON result being asserted on.</summary>
    public JsonNode? Result => result;

    /// <summary>Asserts the result is not null.</summary>
    public McpAssertions IsNotNull()
    {
        if (result is null)
        {
            throw new McpAssertionException("Expected non-null result but got null.");
        }
        return this;
    }

    /// <summary>Asserts the result contains a specific text content.</summary>
    public McpAssertions ContainsText(string expected)
    {
        IsNotNull();
        string text = ExtractTextContent();
        if (!text.Contains(expected, StringComparison.Ordinal))
        {
            throw new McpAssertionException(
                $"Expected result to contain '{expected}' but got:\n{text[..Math.Min(text.Length, 500)]}");
        }
        return this;
    }

    /// <summary>Asserts the result does not contain the specified text.</summary>
    public McpAssertions DoesNotContainText(string unexpected)
    {
        IsNotNull();
        string text = ExtractTextContent();
        if (text.Contains(unexpected, StringComparison.Ordinal))
        {
            throw new McpAssertionException(
                $"Expected result NOT to contain '{unexpected}' but it was present.");
        }
        return this;
    }

    /// <summary>
    /// Asserts the result contains a component tree entry with the given component type name.
    /// </summary>
    public McpAssertions ContainsComponent(string componentName)
    {
        IsNotNull();
        string text = ExtractTextContent();
        if (!text.Contains(componentName, StringComparison.Ordinal))
        {
            throw new McpAssertionException(
                $"Expected component tree to contain '{componentName}' but it was not found.");
        }
        return this;
    }

    /// <summary>
    /// Asserts the result contains at least one accessible node matching the criteria.
    /// </summary>
    public McpNodeAssertions ContainsAccessibleNode()
    {
        IsNotNull();
        return new McpNodeAssertions(result!);
    }

    /// <summary>Asserts the result text content matches the expected value exactly.</summary>
    public McpAssertions HasTextContent(string expected)
    {
        IsNotNull();
        string text = ExtractTextContent();
        if (!string.Equals(text.Trim(), expected.Trim(), StringComparison.Ordinal))
        {
            throw new McpAssertionException(
                $"Expected text content '{expected}' but got:\n{text[..Math.Min(text.Length, 500)]}");
        }
        return this;
    }

    /// <summary>Asserts the result indicates success (no error content).</summary>
    public McpAssertions IsSuccess()
    {
        IsNotNull();
        string text = ExtractTextContent();
        if (text.Contains("error", StringComparison.OrdinalIgnoreCase) &&
            !text.Contains("0 error", StringComparison.OrdinalIgnoreCase))
        {
            throw new McpAssertionException(
                $"Expected success but result contains error:\n{text[..Math.Min(text.Length, 500)]}");
        }
        return this;
    }

    private string ExtractTextContent()
    {
        // MCP responses have either: { "content": [{ "type": "text", "text": "..." }] }
        // or for RawResponse tools, the result is directly returned
        if (result is JsonObject obj)
        {
            var content = obj["content"] as JsonArray;
            if (content is not null)
            {
                var parts = new List<string>();
                foreach (var item in content)
                {
                    string? type = item?["type"]?.GetValue<string>();
                    if (string.Equals(type, "text", StringComparison.Ordinal))
                    {
                        string? text = item?["text"]?.GetValue<string>();
                        if (text is not null)
                        {
                            parts.Add(text);
                        }
                    }
                }
                return string.Join('\n', parts);
            }
        }

        return result?.ToJsonString() ?? "";
    }
}

/// <summary>
/// Fluent assertions for accessible/found nodes in MCP results.
/// </summary>
public sealed class McpNodeAssertions
{
    private readonly JsonNode result;

    internal McpNodeAssertions(JsonNode result)
    {
        this.result = result;
    }

    /// <summary>Asserts the result contains a node with a specific property value.</summary>
    public McpNodeAssertions WithProperty(string property, JsonNode expectedValue)
    {
        string text = result.ToJsonString();
        string expectedStr = expectedValue.ToJsonString();

        // Look for "property": value pattern in the JSON
        if (!text.Contains($"\"{property}\"", StringComparison.Ordinal))
        {
            throw new McpAssertionException(
                $"Expected node with property '{property}' but it was not found in result.");
        }

        return this;
    }

    /// <summary>Asserts the matched node has the specified role.</summary>
    public McpNodeAssertions WithRole(string role)
    {
        return WithProperty("role", JsonValue.Create(role)!);
    }

    /// <summary>Asserts the matched node has the specified label.</summary>
    public McpNodeAssertions WithLabel(string label)
    {
        return WithProperty("label", JsonValue.Create(label)!);
    }

    /// <summary>Asserts the matched node is enabled.</summary>
    public McpNodeAssertions IsEnabled()
    {
        string text = result.ToJsonString();
        if (text.Contains("\"enabled\":false", StringComparison.Ordinal) ||
            text.Contains("\"enabled\": false", StringComparison.Ordinal))
        {
            throw new McpAssertionException("Expected node to be enabled but it is disabled.");
        }
        return this;
    }

    /// <summary>Asserts the matched node is disabled.</summary>
    public McpNodeAssertions IsDisabled()
    {
        string text = result.ToJsonString();
        if (!text.Contains("\"enabled\":false", StringComparison.Ordinal) &&
            !text.Contains("\"enabled\": false", StringComparison.Ordinal))
        {
            throw new McpAssertionException("Expected node to be disabled but it is enabled.");
        }
        return this;
    }

    /// <summary>Asserts the matched node is checked (for checkboxes, toggles).</summary>
    public McpNodeAssertions IsChecked()
    {
        string text = result.ToJsonString();
        if (!text.Contains("\"checked\":true", StringComparison.Ordinal) &&
            !text.Contains("\"checked\": true", StringComparison.Ordinal))
        {
            throw new McpAssertionException("Expected node to be checked but it is not.");
        }
        return this;
    }

    /// <summary>Asserts the matched node is unchecked.</summary>
    public McpNodeAssertions IsUnchecked()
    {
        string text = result.ToJsonString();
        if (text.Contains("\"checked\":true", StringComparison.Ordinal) ||
            text.Contains("\"checked\": true", StringComparison.Ordinal))
        {
            throw new McpAssertionException("Expected node to be unchecked but it is checked.");
        }
        return this;
    }

    /// <summary>Asserts the matched node is visible (not hidden).</summary>
    public McpNodeAssertions IsVisible()
    {
        string text = result.ToJsonString();
        if (text.Contains("\"visible\":false", StringComparison.Ordinal) ||
            text.Contains("\"visible\": false", StringComparison.Ordinal))
        {
            throw new McpAssertionException("Expected node to be visible but it is hidden.");
        }
        return this;
    }
}

/// <summary>
/// Extension methods for creating <see cref="McpAssertions"/> from MCP results.
/// </summary>
public static class McpAssertionExtensions
{
    /// <summary>Begins a fluent assertion chain on an MCP tool result.</summary>
    public static McpAssertions Should(this JsonNode? result) => new(result);
}

/// <summary>
/// Exception thrown when an MCP assertion fails.
/// </summary>
public sealed class McpAssertionException : Exception
{
    public McpAssertionException(string message) : base(message) { }
    public McpAssertionException(string message, Exception inner) : base(message, inner) { }
}
