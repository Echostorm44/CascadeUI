using System.Collections.Concurrent;
using System.ComponentModel;

namespace Cascade.UI;

/// <summary>
/// How a re-rendered parent's new arguments reach a child component that is already mounted.
/// The source generator registers, for every component, an adopter that copies its inputs (readonly
/// fields assigned from constructor parameters) from the instance the parent just built onto the
/// live one. Generated code calls <see cref="Register"/>; apps do not.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ComponentInputs
{
    private static readonly ConcurrentDictionary<Type, Action<Component, Component>> adopters = new();

    /// <summary>Registers the input adopter of <paramref name="componentType"/> (generated code).</summary>
    public static void Register(Type componentType, Action<Component, Component> adopt)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(adopt);
        adopters[componentType] = adopt;
    }

    /// <summary>Copies <paramref name="newer"/>'s inputs onto <paramref name="live"/> when both are the same registered type.</summary>
    internal static void Adopt(Component live, Component newer)
    {
        Type type = live.GetType();
        if (type == newer.GetType() && adopters.TryGetValue(type, out var adopt))
        {
            adopt(live, newer);
        }
    }
}
