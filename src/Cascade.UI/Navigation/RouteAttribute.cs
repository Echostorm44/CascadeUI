namespace Cascade.UI;

/// <summary>
/// Maps a component to a URL-style path pattern for deep link resolution.
/// Entirely optional — pages without this attribute work normally for all
/// typed navigation but are unreachable via deep link.
/// </summary>
/// <remarks>
/// <para>
/// Route parameter types must match constructor parameter types. The source
/// generator validates this at compile time. Supported route parameter types:
/// <c>string</c>, <c>int</c>, <c>long</c>, <c>Guid</c>, <c>bool</c>.
/// </para>
/// <code>
/// [Route("/products/{productId}")]
/// public class ProductDetailPage : Component
/// {
///     public ProductDetailPage(string productId) { ... }
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RouteAttribute : Attribute
{
    /// <summary>
    /// Creates a new route attribute with the specified path pattern.
    /// </summary>
    /// <param name="pattern">
    /// URL-style path pattern (e.g., "/products/{productId}").
    /// Parameters in braces are matched to constructor parameters by name.
    /// </param>
    public RouteAttribute(string pattern)
    {
        Pattern = pattern;
    }

    /// <summary>
    /// The URL-style path pattern for this route.
    /// </summary>
    public string Pattern { get; }
}
