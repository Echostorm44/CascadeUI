namespace Cascade.UI;

/// <summary>
/// Accessibility modifiers for any node. Controls infer their role and name (a <see cref="Button"/>
/// is a button named by its text); a custom element built from layout nodes — a clickable row of a
/// label and key caps, say — has to declare them.
/// </summary>
public static class AccessibilityModifiers
{
    /// <summary>Sets the node's accessible role, overriding the one inferred from its type.</summary>
    public static T AccessibleRole<T>(this T node, AccessibleRole role) where T : Node
    {
        node.LayoutData.A11yRole = role;
        return node;
    }

    /// <summary>
    /// Sets the node's accessible name, overriding its visible text. An empty label marks the node
    /// decorative.
    /// </summary>
    public static T AccessibleLabel<T>(this T node, LocKey label) where T : Node
    {
        node.LayoutData.A11yLabel = label.Resolve();
        return node;
    }
}
