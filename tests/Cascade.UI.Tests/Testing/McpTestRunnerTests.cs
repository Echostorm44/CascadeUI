#pragma warning disable CA2000, CA1812

using System.Text.Json.Nodes;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Cascade.UI.Testing;

namespace Cascade.UI.Tests.Testing;

public sealed class McpTestRunnerTests
{
    private static JsonObject MakeTextResult(string text)
    {
        return new JsonObject
        {
            ["content"] = new JsonArray(
                (JsonNode)new JsonObject { ["type"] = "text", ["text"] = text }),
        };
    }

    // ── McpAssertions ──

    [Test]
    public async Task McpAssertions_IsNotNull_ThrowsOnNull()
    {
        JsonNode? result = null;
        bool threw = false;
        try
        {
            result.Should().IsNotNull();
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task McpAssertions_IsNotNull_PassesOnValue()
    {
        JsonNode? result = JsonValue.Create("hello");
        bool threw = false;
        try
        {
            result.Should().IsNotNull();
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsFalse();
    }

    [Test]
    public async Task McpAssertions_ContainsText_FindsText()
    {
        JsonNode result = MakeTextResult("Hello World");

        bool threw = false;
        try
        {
            result.Should().ContainsText("World");
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsFalse();
    }

    [Test]
    public async Task McpAssertions_ContainsText_ThrowsOnMissing()
    {
        JsonNode result = MakeTextResult("Hello World");

        bool threw = false;
        try
        {
            result.Should().ContainsText("Missing");
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task McpAssertions_DoesNotContainText_PassesWhenAbsent()
    {
        JsonNode result = MakeTextResult("Hello");

        bool threw = false;
        try
        {
            result.Should().DoesNotContainText("World");
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsFalse();
    }

    [Test]
    public async Task McpAssertions_ContainsComponent_FindsComponent()
    {
        JsonNode result = MakeTextResult("Root\n  CheckoutPage\n    Button\n    Label");

        bool threw = false;
        try
        {
            result.Should().ContainsComponent("CheckoutPage");
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsFalse();
    }

    [Test]
    public async Task McpAssertions_ContainsComponent_ThrowsWhenMissing()
    {
        JsonNode result = MakeTextResult("Root\n  Button");

        bool threw = false;
        try
        {
            result.Should().ContainsComponent("CheckoutPage");
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsTrue();
    }

    // ── McpNodeAssertions ──

    [Test]
    public async Task McpNodeAssertions_IsEnabled_PassesForEnabled()
    {
        var result = new JsonObject
        {
            ["role"] = "Button",
            ["label"] = "Submit",
            ["enabled"] = true,
        };

        bool threw = false;
        try
        {
            ((JsonNode)result).Should().ContainsAccessibleNode().IsEnabled();
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsFalse();
    }

    [Test]
    public async Task McpNodeAssertions_IsEnabled_ThrowsForDisabled()
    {
        var result = new JsonObject
        {
            ["role"] = "Button",
            ["label"] = "Submit",
            ["enabled"] = false,
        };

        bool threw = false;
        try
        {
            ((JsonNode)result).Should().ContainsAccessibleNode().IsEnabled();
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task McpNodeAssertions_IsChecked_PassesForChecked()
    {
        var result = new JsonObject
        {
            ["role"] = "Checkbox",
            ["label"] = "Agree",
            ["checked"] = true,
        };

        bool threw = false;
        try
        {
            ((JsonNode)result).Should().ContainsAccessibleNode().IsChecked();
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsFalse();
    }

    [Test]
    public async Task McpNodeAssertions_IsUnchecked_PassesForUnchecked()
    {
        var result = new JsonObject
        {
            ["role"] = "Checkbox",
            ["label"] = "Agree",
            ["checked"] = false,
        };

        bool threw = false;
        try
        {
            ((JsonNode)result).Should().ContainsAccessibleNode().IsUnchecked();
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsFalse();
    }

    [Test]
    public async Task McpNodeAssertions_IsVisible_PassesForVisible()
    {
        var result = new JsonObject
        {
            ["role"] = "Button",
            ["visible"] = true,
        };

        bool threw = false;
        try
        {
            ((JsonNode)result).Should().ContainsAccessibleNode().IsVisible();
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsFalse();
    }

    [Test]
    public async Task McpNodeAssertions_WithRole_PassesForMatchingRole()
    {
        var result = new JsonObject
        {
            ["role"] = "Button",
            ["label"] = "Submit",
        };

        bool threw = false;
        try
        {
            ((JsonNode)result).Should().ContainsAccessibleNode().WithRole("Button");
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsFalse();
    }

    [Test]
    public async Task McpNodeAssertions_WithLabel_PassesForMatchingLabel()
    {
        var result = new JsonObject
        {
            ["role"] = "Button",
            ["label"] = "Submit",
        };

        bool threw = false;
        try
        {
            ((JsonNode)result).Should().ContainsAccessibleNode().WithLabel("Submit");
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsFalse();
    }

    // ── McpTestRunner construction ──

    [Test]
    public async Task McpTestRunner_Constructor_SetsClient()
    {
        // We can't create a real client without a process, but we can test
        // that the constructor validates null
        bool threw = false;
        try
        {
            _ = new McpTestRunner(null!);
        }
        catch (ArgumentNullException)
        {
            threw = true;
        }
        await Assert.That(threw).IsTrue();
    }

    // ── McpAssertions chaining ──

    [Test]
    public async Task McpAssertions_FluentChaining_Works()
    {
        JsonNode result = MakeTextResult("Test passed successfully");

        bool threw = false;
        try
        {
            result.Should()
                .IsNotNull()
                .ContainsText("passed")
                .DoesNotContainText("failed");
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsFalse();
    }

    [Test]
    public async Task McpAssertions_IsSuccess_PassesForNonError()
    {
        JsonNode result = MakeTextResult("Operation completed");

        bool threw = false;
        try
        {
            result.Should().IsSuccess();
        }
        catch (McpAssertionException)
        {
            threw = true;
        }
        await Assert.That(threw).IsFalse();
    }

    [Test]
    public async Task McpTestException_HasMessage()
    {
        var ex = new McpTestException("test error");
        await Assert.That(ex.Message).IsEqualTo("test error");
    }

    [Test]
    public async Task McpAssertionException_HasMessage()
    {
        var ex = new McpAssertionException("assertion failed");
        await Assert.That(ex.Message).IsEqualTo("assertion failed");
    }
}
