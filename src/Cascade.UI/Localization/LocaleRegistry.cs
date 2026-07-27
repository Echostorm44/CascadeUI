namespace Cascade.UI;

/// <summary>
/// Thread-safe runtime locale registry. Loaded strings are registered by the
/// source-generated S class or by application code. If no string is registered
/// for a key, the key's raw Value is returned as-is (graceful fallback).
/// </summary>
public static class LocaleRegistry
{
    private static readonly Lock syncLock = new();
    private static string currentLocale = "en";
    private static readonly Dictionary<string, Dictionary<string, string>> locales = new();

    /// <summary>Gets or sets the current locale code (e.g., "en", "fr", "ja").</summary>
    public static string CurrentLocale
    {
        get
        {
            lock (syncLock)
            {
                return currentLocale;
            }
        }
        set
        {
            lock (syncLock)
            {
                currentLocale = value ?? "en";
            }
        }
    }

    /// <summary>Registers a batch of key/value strings for a locale.</summary>
    public static void Register(string locale, IReadOnlyDictionary<string, string> strings)
    {
        ArgumentNullException.ThrowIfNull(locale);
        ArgumentNullException.ThrowIfNull(strings);
        lock (syncLock)
        {
            if (!locales.TryGetValue(locale, out var dict))
            {
                dict = new Dictionary<string, string>(StringComparer.Ordinal);
                locales[locale] = dict;
            }
            foreach (var kvp in strings)
            {
                dict[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>Resolves a key to a string in the current locale. Falls back to key value.</summary>
    internal static string Resolve(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return key ?? string.Empty;
        }
        lock (syncLock)
        {
            if (locales.TryGetValue(currentLocale, out var dict) && dict.TryGetValue(key, out var value))
            {
                return value;
            }
            if (currentLocale != "en" && locales.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enValue))
            {
                return enValue;
            }
        }
        return key;
    }

    /// <summary>Clears all registered locales. Primarily for testing.</summary>
    internal static void Clear()
    {
        lock (syncLock)
        {
            locales.Clear();
            currentLocale = "en";
        }
    }
}
