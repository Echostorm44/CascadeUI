#pragma warning disable CA2000, CA1812, IL2026

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class DeepLinkTests
{
    private sealed class SettingsPage : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    private sealed class UserProfilePage : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    private sealed class GenericPage : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    private static (DeepLinkResolver resolver, RouteResolver routes) CreateResolver()
    {
        var routes = new RouteResolver();
        routes.MarkScanned();
        var resolver = new DeepLinkResolver(routes);
        return (resolver, routes);
    }

    [Test]
    public async Task Resolve_ValidUri_ReturnsMatch()
    {
        var (resolver, routes) = CreateResolver();
        routes.Register(typeof(SettingsPage), "/settings/profile");
        resolver.RegisterScheme("myapp");

        var result = resolver.Resolve("myapp://settings/profile");

        bool isNotNull = result is not null;
        bool isCorrectType = result?.Route.ComponentType == typeof(SettingsPage);
        await Assert.That(isNotNull).IsTrue();
        await Assert.That(isCorrectType).IsTrue();
    }

    [Test]
    public async Task Resolve_RelativePath_ReturnsMatch()
    {
        var (resolver, routes) = CreateResolver();
        routes.Register(typeof(SettingsPage), "/settings");

        var result = resolver.Resolve("/settings");

        bool isNotNull = result is not null;
        bool isCorrectType = result?.Route.ComponentType == typeof(SettingsPage);
        await Assert.That(isNotNull).IsTrue();
        await Assert.That(isCorrectType).IsTrue();
    }

    [Test]
    public async Task Resolve_UnknownRoute_ReturnsNull()
    {
        var (resolver, routes) = CreateResolver();
        routes.Register(typeof(SettingsPage), "/settings");
        resolver.RegisterScheme("myapp");

        var result = resolver.Resolve("myapp://nonexistent/path");

        bool isNull = result is null;
        await Assert.That(isNull).IsTrue();
    }

    [Test]
    public async Task Resolve_MalformedUri_ReturnsNull()
    {
        var (resolver, _) = CreateResolver();

        var result = resolver.Resolve("   ");

        bool isNull = result is null;
        await Assert.That(isNull).IsTrue();
    }

    [Test]
    public async Task Resolve_QueryParameters_Merged()
    {
        var (resolver, routes) = CreateResolver();
        routes.Register(typeof(UserProfilePage), "/users/{id}");
        resolver.RegisterScheme("myapp");

        var result = resolver.Resolve("myapp://users/123?tab=settings");

        bool isNotNull = result is not null;
        await Assert.That(isNotNull).IsTrue();

        string? idValue = result?.Route.Parameters["id"];
        await Assert.That(idValue).IsEqualTo("123");

        bool hasTabQuery = result?.QueryParameters.ContainsKey("tab") == true;
        await Assert.That(hasTabQuery).IsTrue();

        string? tabValue = result?.QueryParameters["tab"];
        await Assert.That(tabValue).IsEqualTo("settings");

        // Query params are merged into route params (route params take precedence)
        bool routeHasTab = result?.Route.Parameters.ContainsKey("tab") == true;
        await Assert.That(routeHasTab).IsTrue();
    }

    [Test]
    public async Task Resolve_Fragment_Extracted()
    {
        var (resolver, routes) = CreateResolver();
        routes.Register(typeof(GenericPage), "/page");
        resolver.RegisterScheme("myapp");

        var result = resolver.Resolve("myapp://page#section");

        bool isNotNull = result is not null;
        await Assert.That(isNotNull).IsTrue();
        await Assert.That(result!.Fragment).IsEqualTo("section");
    }

    [Test]
    public async Task Resolve_UnregisteredScheme_ReturnsNull()
    {
        var (resolver, routes) = CreateResolver();
        routes.Register(typeof(SettingsPage), "/settings");
        resolver.RegisterScheme("myapp");

        var result = resolver.Resolve("unknown://settings");

        bool isNull = result is null;
        await Assert.That(isNull).IsTrue();
    }

    [Test]
    public async Task RouteResolver_ResolveUri_ExtractsPath()
    {
        var resolver = new RouteResolver();
        resolver.MarkScanned();
        resolver.Register(typeof(SettingsPage), "/settings/profile");

        var match = resolver.ResolveUri("https://example.com/settings/profile?foo=bar");

        bool isNotNull = match is not null;
        bool isCorrectType = match?.ComponentType == typeof(SettingsPage);
        await Assert.That(isNotNull).IsTrue();
        await Assert.That(isCorrectType).IsTrue();
    }

    [Test]
    public async Task RouteResolver_ResolveUri_Null_ReturnsNull()
    {
        var resolver = new RouteResolver();
        resolver.MarkScanned();

        var matchNull = resolver.ResolveUri(null!);
        var matchEmpty = resolver.ResolveUri("");
        var matchWhitespace = resolver.ResolveUri("   ");

        await Assert.That(matchNull is null).IsTrue();
        await Assert.That(matchEmpty is null).IsTrue();
        await Assert.That(matchWhitespace is null).IsTrue();
    }
}
