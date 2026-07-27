namespace Cascade.UI;

/// <summary>
/// Controls whether the theme resolves to its light or dark color set.
/// </summary>
public enum ThemeMode
{
    /// <summary>Follow the operating system preference (default).</summary>
    System,

    /// <summary>Always use the light color set.</summary>
    Light,

    /// <summary>Always use the dark color set.</summary>
    Dark,
}
