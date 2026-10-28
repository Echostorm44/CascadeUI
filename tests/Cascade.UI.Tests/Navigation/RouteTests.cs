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
        resolver.UseRegisteredRoutesOnly();
        return resolver;
    }

    [Test]
    public async Task Register_AddsRoute()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home", _ => new HomeRoutePage());

        int count = resolver.RouteCount;
        int expected = 1;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task Register_MultipleRoutes_IncrementsCount()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home", _ => new HomeRoutePage());
        resolver.Register(typeof(UserDetailRoutePage), "/users/{id}", _ => new UserDetailRoutePage());

        int count = resolver.RouteCount;
        int expected = 2;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task Resolve_StaticPath_ReturnsCorrectType()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home", _ => new HomeRoutePage());

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
        resolver.Register(typeof(UserDetailRoutePage), "/users/{id}", _ => new UserDetailRoutePage());

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
        resolver.Register(typeof(ProductRoutePage), "/products/{category}/{id}", _ => new ProductRoutePage());

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
        resolver.Register(typeof(HomeRoutePage), "/home", _ => new HomeRoutePage());

        var match = resolver.Resolve("/nonexistent");
        bool isNull = match is null;
        await Assert.That(isNull).IsTrue();
    }

    [Test]
    public async Task Register_DuplicateRoute_ThrowsInvalidOperationException()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home", _ => new HomeRoutePage());

        var action = () => resolver.Register(typeof(UnroutedPage), "/home", _ => new UnroutedPage());
        await Assert.That(action).ThrowsException();
    }

    [Test]
    public async Task Register_SameComponentTwice_KeepsOneRoute()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home", _ => new HomeRoutePage());
        resolver.Register(typeof(HomeRoutePage), "/home", _ => new HomeRoutePage());

        await Assert.That(resolver.RouteCount).IsEqualTo(1);
    }

    [Test]
    public async Task Resolve_CaseInsensitive()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/Home", _ => new HomeRoutePage());

        var match = resolver.Resolve("/HOME");
        bool isNotNull = match is not null;
        await Assert.That(isNotNull).IsTrue();
    }

    [Test]
    public async Task Resolve_LeadingTrailingSlashesIgnored()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home/", _ => new HomeRoutePage());

        var match = resolver.Resolve("home");
        bool isNotNull = match is not null;
        await Assert.That(isNotNull).IsTrue();
    }

    [Test]
    public async Task Reset_ClearsAllRoutes()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home", _ => new HomeRoutePage());
        resolver.Register(typeof(UserDetailRoutePage), "/users/{id}", _ => new UserDetailRoutePage());

        resolver.Reset();

        int count = resolver.RouteCount;
        int expected = 0;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task Resolve_ParameterWithSpecialChars_WorksCorrectly()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(UserDetailRoutePage), "/users/{id}", _ => new UserDetailRoutePage());

        var match = resolver.Resolve("/users/abc-123");
        string? id = match?.Parameters["id"];
        await Assert.That(id).IsEqualTo("abc-123");
    }

    [Test]
    public async Task Register_AfterReset_WorksAgain()
    {
        var resolver = CreateResolver();
        resolver.Register(typeof(HomeRoutePage), "/home", _ => new HomeRoutePage());
        resolver.Reset();
        resolver.Register(typeof(HomeRoutePage), "/home", _ => new HomeRoutePage());

        int count = resolver.RouteCount;
        int expected = 1;
        await Assert.That(count).IsEqualTo(expected);
    }

    private sealed class TypedUserRoutePage : Component
    {
        public int Id { get; set; }
        protected override Node Render() => Node.Empty;
    }

    // ── Generated route table ──────────────────────────────────────────
    // The pages below carry [Route]; this project runs Cascade.Generators, so these tests
    // exercise the factories the generator actually emits, the same ones an app gets.

    [Test]
    public async Task Generated_ResolverFindsRoutePagesWithoutManualRegistration()
    {
        var match = new RouteResolver().Resolve("/generated/users/42");

        await Assert.That(match!.ComponentType).IsEqualTo(typeof(GeneratedUserPage));
        await Assert.That(((GeneratedUserPage)match.Factory(match.Parameters)).Id).IsEqualTo(42);
    }

    [Test]
    public async Task Generated_FactoryConvertsEachValueToItsPropertyType()
    {
        var ticket = Guid.NewGuid();
        var match = new RouteResolver().Resolve("/generated/users/7")!;
        match.Parameters["tab"] = "photos";
        match.Parameters["score"] = "1,234.5";
        match.Parameters["ticket"] = ticket.ToString();
        match.Parameters["name"] = "Ada";
        match.Parameters["pinned"] = "true";

        var page = (GeneratedUserPage)match.Factory(match.Parameters);

        await Assert.That(page.Id).IsEqualTo(7);
        await Assert.That(page.Tab).IsEqualTo(GeneratedTab.Photos);
        await Assert.That(page.Score).IsEqualTo(1234.5);
        await Assert.That(page.Ticket).IsEqualTo(ticket);
        await Assert.That(page.Name).IsEqualTo("Ada");
        await Assert.That(page.Pinned).IsEqualTo(true);
    }

    [Test]
    public async Task Generated_FactoryLeavesDefaultsForValuesThatDoNotParse()
    {
        var match = new RouteResolver().Resolve("/generated/users/1")!;
        var page = (GeneratedUserPage)match.Factory(new Dictionary<string, string> { ["id"] = "x", ["tab"] = "nope" });

        await Assert.That(page.Id).IsEqualTo(0);
        await Assert.That(page.Tab).IsNull();
    }

    [Test]
    public async Task Generated_StringConstructorGetsPathValuesInPatternOrder()
    {
        var match = new RouteResolver().Resolve("/generated/albums/summer/photos/7")!;
        var page = (GeneratedPhotoPage)match.Factory(match.Parameters);

        await Assert.That(page.Album).IsEqualTo("summer");
        await Assert.That(page.Photo).IsEqualTo("7");
    }

    [Test]
    public async Task Generated_NavigatePushesTheBuiltPage()
    {
        var navigator = new Navigator(new HomeRoutePage(), PageTransition.None);
        var pushed = new List<Component>();
        navigator.PagePushed += pushed.Add;

        navigator.Navigate("/generated/users/9");

        await Assert.That(pushed.Count == 1 && pushed[0] is GeneratedUserPage { Id: 9 }).IsTrue();
    }

    [Test]
    public async Task TypedRoutePattern_ParsesAndMatches()
    {
        var resolver = new RouteResolver();
        resolver.Reset();
        resolver.Register(typeof(TypedUserRoutePage), "/users/{id:int}", _ => new TypedUserRoutePage());
        resolver.UseRegisteredRoutesOnly();

        var match = resolver.Resolve("/users/42");
        await Assert.That(match).IsNotNull();
        await Assert.That(match!.Parameters["id"]).IsEqualTo("42");
    }
}

internal enum GeneratedTab { Info, Photos }

[Route("/generated/users/{id:int}")]
internal sealed class GeneratedUserPage : Component
{
    public int Id { get; set; }
    public GeneratedTab? Tab { get; set; }
    public double Score { get; set; }
    public Guid Ticket { get; set; }
    public string Name { get; set; } = "";
    public bool Pinned { get; set; }
    protected override Node Render() => Node.Empty;
}

[Route("/generated/albums/{album}/photos/{photo}")]
internal sealed class GeneratedPhotoPage(string album, string photo) : Component
{
    public string Album => album;
    public string Photo => photo;
    protected override Node Render() => Node.Empty;
}
