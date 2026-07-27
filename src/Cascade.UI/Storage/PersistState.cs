using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Cascade.UI;

/// <summary>
/// Static API for managing persisted component state. Provides methods to
/// clear and manually save state for <see cref="PersistStateAttribute"/>-marked fields.
/// </summary>
public static class PersistState
{
    private const string KeyPrefix = "persist:";

    /// <summary>
    /// Clears all persisted state for the specified component type.
    /// </summary>
    public static void Clear<TComponent>() where TComponent : Component
    {
        LocalStorage.Clear($"{KeyPrefix}{typeof(TComponent).Name}.");
    }

    /// <summary>
    /// Clears persisted state for a specific field of the specified component type.
    /// </summary>
    public static void Clear<TComponent>(string fieldName) where TComponent : Component
    {
        ArgumentNullException.ThrowIfNull(fieldName);
        LocalStorage.Remove($"{KeyPrefix}{typeof(TComponent).Name}.{fieldName}");
    }

    /// <summary>
    /// Clears all persisted state for the entire application.
    /// </summary>
    public static void ClearAll()
    {
        LocalStorage.Clear(KeyPrefix);
    }

    /// <summary>
    /// Manually saves the current value of a <see cref="PersistWhen.Manual"/>-marked
    /// field to storage.
    /// </summary>
    [RequiresUnreferencedCode("Uses reflection to read component fields and JSON serialization.")]
    [RequiresDynamicCode("Uses reflection to read component fields and JSON serialization.")]
    public static void Save<TComponent>(TComponent component, string fieldName) where TComponent : Component
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(fieldName);

        var field = typeof(TComponent).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (field is null)
        {
            throw new ArgumentException(
                $"Field '{fieldName}' not found on type '{typeof(TComponent).Name}'.",
                nameof(fieldName));
        }

        var value = field.GetValue(component);
        var key = $"{KeyPrefix}{typeof(TComponent).Name}.{fieldName}";
        LocalStorage.Set(key, value);
    }
}
