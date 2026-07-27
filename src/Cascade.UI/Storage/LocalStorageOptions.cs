using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cascade.UI;

/// <summary>
/// Configuration options for <see cref="LocalStorage"/>, including custom
/// JSON converters for types that need specialized serialization.
/// </summary>
public sealed class LocalStorageOptions
{
    private readonly List<JsonConverter> converters = [];

    /// <summary>
    /// Registers a custom <see cref="JsonConverter{T}"/> for storage serialization.
    /// </summary>
    public void AddConverter<T>(JsonConverter<T> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        converters.Add(converter);
    }

    /// <summary>
    /// Builds a <see cref="JsonSerializerOptions"/> instance with all registered converters.
    /// </summary>
    internal JsonSerializerOptions BuildSerializerOptions()
    {
        var options = new JsonSerializerOptions();
        foreach (var converter in converters)
        {
            options.Converters.Add(converter);
        }

        return options;
    }
}
