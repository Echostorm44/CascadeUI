namespace Cascade.UI;

/// <summary>
/// Provides conditional values based on the current <see cref="WindowSizeClass"/>.
/// Values are resolved reactively — components re-render when the size class changes.
/// Unspecified size classes fall back to the next smaller specified value.
/// </summary>
public static class ResponsiveValue
{
    /// <summary>
    /// Returns the appropriate <c>float</c> value for the current window size class.
    /// Unspecified tiers fall back to the next smaller tier's value.
    /// </summary>
    /// <param name="compact">Value for Compact size class (required, serves as base fallback).</param>
    /// <param name="medium">Value for Medium size class, or <c>null</c> to use compact.</param>
    /// <param name="expanded">Value for Expanded size class, or <c>null</c> to use medium.</param>
    /// <param name="large">Value for Large size class, or <c>null</c> to use expanded.</param>
    public static float Of(
        float compact,
        float? medium = null,
        float? expanded = null,
        float? large = null)
    {
        var sizeClass = WindowContext.Current.SizeClass;
        return sizeClass switch
        {
            WindowSizeClass.Large => large ?? expanded ?? medium ?? compact,
            WindowSizeClass.Expanded => expanded ?? medium ?? compact,
            WindowSizeClass.Medium => medium ?? compact,
            _ => compact
        };
    }

    /// <summary>
    /// Returns the appropriate value of type <typeparamref name="T"/> for the current
    /// window size class. Unspecified tiers fall back to the next smaller tier's value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="compact">Value for Compact size class (required, serves as base fallback).</param>
    /// <param name="medium">Value for Medium size class, or <c>default</c> to use compact.</param>
    /// <param name="expanded">Value for Expanded size class, or <c>default</c> to use medium.</param>
    /// <param name="large">Value for Large size class, or <c>default</c> to use expanded.</param>
    public static T Of<T>(
        T compact,
        T? medium = default,
        T? expanded = default,
        T? large = default) where T : struct
    {
        var sizeClass = WindowContext.Current.SizeClass;
        return sizeClass switch
        {
            WindowSizeClass.Large => large ?? expanded ?? medium ?? compact,
            WindowSizeClass.Expanded => expanded ?? medium ?? compact,
            WindowSizeClass.Medium => medium ?? compact,
            _ => compact
        };
    }
}
