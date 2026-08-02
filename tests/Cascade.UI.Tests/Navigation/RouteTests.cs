#pragma warning disable CA2000, CA1812, IL2026

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class RouteTests
{
    private sealed class HomeRoutePage : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    private sealed class UserDetailRoutePage : Component
    {
        public UserDetailRoutePage()
        {
            UserId = string.Empty;
        }

        public UserDetailRoutePage(string id)
        {
            UserId = id;
        }

        public string UserId { get; }

        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    private sealed class ProductRoutePage : Component
    {
        public ProductRoutePage()
        {
            Category = string.Empty;
            ProductId = string.Empty;
        }

        public ProductRoutePage(string category, string id)
        {
            Category = category;
            ProductId = id;
        }

        public string Category { get; }
        public string ProductId { get; }

        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    private sealed class UnroutedPage : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    private static RouteResolver CreateResolver()
    {
        var resolver = new RouteResolver();
        // Pre-scan with empty set to prevent auto-scanning (which could find
        // [Route] attributes on other test classes and cause duplicates).
        resolver.MarkScanned();
        return resolver;
    }

    [Test]
    public async Task Register_AddsRoute()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home");

        int count = resolver.RouteCount;
        int expected = 1;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task Register_MultipleRoutes_IncrementsCount()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home");
        resolver.Register(typeof(UserDetailRoutePage), "/users/{id}");

        int count = resolver.RouteCount;
        int expected = 2;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task Resolve_StaticPath_ReturnsCorrectType()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home");

        var match = resolver.Resolve("/home");
        bool isNotNull = match is not null;
        bool isCorrectType = match?.ComponentType == typeof(HomeRoutePage);

        await Assert.That(isNotNull).IsTrue();
        await Assert.That(isCorrectType).IsTrue();
    }

    [Test]
    public async Task Resolve_PathWithParameter_ExtractsParameter()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(UserDetailRoutePage), "/users/{id}");

        var match = resolver.Resolve("/users/42");
        bool hasIdParam = match?.Parameters.ContainsKey("id") == true;
        string? idValue = match?.Parameters["id"];

        await Assert.That(hasIdParam).IsTrue();
        await Assert.That(idValue).IsEqualTo("42");
    }

    [Test]
    public async Task Resolve_PathWithMultipleParameters_ExtractsAll()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(ProductRoutePage), "/products/{category}/{id}");

        var match = resolver.Resolve("/products/electronics/99");

        string? category = match?.Parameters["category"];
        string? id = match?.Parameters["id"];

        await Assert.That(category).IsEqualTo("electronics");
        await Assert.That(id).IsEqualTo("99");
    }

    [Test]
    public async Task Resolve_MissingRoute_ReturnsNull()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home");

        var match = resolver.Resolve("/nonexistent");
        bool isNull = match is null;
        await Assert.That(isNull).IsTrue();
    }

    [Test]
    public async Task Register_DuplicateRoute_ThrowsInvalidOperationException()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home");

        var action = () => resolver.Register(typeof(UnroutedPage), "/home");
        await Assert.That(action).ThrowsException();
    }

    [Test]
    public async Task Resolve_CaseInsensitive()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/Home");

        var match = resolver.Resolve("/HOME");
        bool isNotNull = match is not null;
        await Assert.That(isNotNull).IsTrue();
    }

    [Test]
    public async Task Resolve_LeadingTrailingSlashesIgnored()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home/");

        var match = resolver.Resolve("home");
        bool isNotNull = match is not null;
        await Assert.That(isNotNull).IsTrue();
    }

    [Test]
    public async Task Reset_ClearsAllRoutes()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home");
        resolver.Register(typeof(UserDetailRoutePage), "/users/{id}");

        resolver.Reset();

        int count = resolver.RouteCount;
        int expected = 0;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task Resolve_ParameterWithSpecialChars_WorksCorrectly()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(UserDetailRoutePage), "/users/{id}");

        var match = resolver.Resolve("/users/abc-123");
        string? id = match?.Parameters["id"];
        await Assert.That(id).IsEqualTo("abc-123");
    }

    [Test]
    public async Task Register_AfterReset_WorksAgain()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home");
        resolver.Reset();
        resolver.Register(typeof(HomeRoutePage), "/home");

        int count = resolver.RouteCount;
        int expected = 1;
        await Assert.That(count).IsEqualTo(expected);
    }

    private sealed class TypedUserRoutePage : Component
    {
        public int Id { get; set; }
        protected override Node Render() => Node.Empty;
    }

    [Test]
    public async Task TypedRouteParam_BindsToTypedProperty()
    {
        // A component with a public parameterless ctor + matching typed property gets the
        // route value converted and bound (the contract CASCADENAV002 validates).
        var page = Navigator.CreateComponentFromRoute(
            typeof(TypedUserRoutePage),
            new System.Collections.Generic.Dictionary<string, string> { ["id"] = "42" });

        await Assert.That(page is TypedUserRoutePage).IsTrue();
        await Assert.That(((TypedUserRoutePage)page).Id).IsEqualTo(42);
    }

    [Test]
    public async Task TypedRoutePattern_ParsesAndMatches()
    {
        var resolver = new RouteResolver();
        resolver.Reset();
        resolver.Register(typeof(TypedUserRoutePage), "/users/{id:int}");
        resolver.MarkScanned();

        var match = resolver.Resolve("/users/42");
        await Assert.That(match).IsNotNull();
        await Assert.That(match!.Parameters["id"]).IsEqualTo("42");
    }
}
