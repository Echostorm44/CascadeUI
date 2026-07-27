namespace Cascade.UI;

/// <summary>
/// Extension methods for navigation-related modifiers on nodes.
/// </summary>
public static class NavigationModifiers
{
    /// <summary>
    /// Tags this node as a hero element for navigation transitions.
    /// When a page transition occurs, the framework matches hero keys
    /// between the source and destination pages and animates the tagged
    /// elements smoothly between their positions.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <param name="node">The node to tag as a hero element.</param>
    /// <param name="key">
    /// A <see cref="HeroKey"/> identifying this hero element. Use
    /// <see cref="HeroSlot.For(object)"/> for list items or an implicit
    /// <see cref="HeroSlot"/> conversion for one-to-one heroes.
    /// </param>
    /// <returns>The node, for fluent chaining.</returns>
    public static TNode NavigationHero<TNode>(this TNode node, HeroKey key) where TNode : Node
    {
        node.HeroKeyValue = key;
        return node;
    }
}
