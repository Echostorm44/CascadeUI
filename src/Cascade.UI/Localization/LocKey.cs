namespace Cascade.UI;

/// <summary>
/// A localization key that resolves to a translated string at runtime.
/// Has an implicit conversion from <see cref="string"/>, so plain string
/// literals are always valid wherever a <see cref="LocKey"/> is expected.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LocKey"/> values are typically source-generated from JSON string
/// files, producing a strongly typed static class <c>S</c> with nested classes
/// mirroring the JSON structure. Magic strings for localization keys do not exist.
/// </para>
/// <para>
/// Localization is built in and encouraged but never required. String literals
/// work everywhere via implicit conversion:
/// </para>
/// <code>
/// // Both are valid:
/// Button(S.Common.Save, onClick: Save)      // localized
/// Button("Save", onClick: Save)              // plain string — always works
/// </code>
/// </remarks>
public readonly struct LocKey : IEquatable<LocKey>
{
    /// <summary>
    /// The raw key or literal string value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a localization key from a key string.
    /// </summary>
    /// <param name="value">The localization key or literal string value.</param>
    public LocKey(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Implicit conversion from <see cref="string"/> to <see cref="LocKey"/>.
    /// This is what makes localization optional — plain string literals are
    /// always accepted wherever a <see cref="LocKey"/> is expected.
    /// </summary>
    public static implicit operator LocKey(string value)
    {
        return new LocKey(value);
    }

    /// <summary>
    /// Creates an interpolated localization key with format arguments.
    /// The arguments are substituted into the translated string at runtime.
    /// </summary>
    /// <param name="args">The values to substitute into the localized format string.</param>
    /// <returns>A new <see cref="FormattedLocKey"/> ready for display.</returns>
    public FormattedLocKey With(params object[] args)
    {
        return new FormattedLocKey(this, args);
    }

    /// <summary>
    /// Resolves this key to a display string using the current locale.
    /// </summary>
    public string Resolve()
    {
        return LocaleRegistry.Resolve(Value);
    }

    /// <inheritdoc/>
    public bool Equals(LocKey other)
    {
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is LocKey other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return Value?.GetHashCode(StringComparison.Ordinal) ?? 0;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return Value ?? string.Empty;
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(LocKey left, LocKey right)
    {
        return left.Equals(right);
    }

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(LocKey left, LocKey right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// A localization key combined with format arguments. Created by calling
/// <see cref="LocKey.With"/> to provide interpolation values that are
/// substituted into the translated string at runtime.
/// </summary>
/// <remarks>
/// <para>
/// Example usage with the source-generated <c>S</c> class:
/// </para>
/// <code>
/// S.NewUser.SuccessBody.With(email)  // interpolated LocKey
/// </code>
/// </remarks>
public readonly struct FormattedLocKey
{
    /// <summary>
    /// The base localization key.
    /// </summary>
    public LocKey Key { get; }

    /// <summary>
    /// The format arguments to substitute into the localized string.
    /// </summary>
    public object[] Args { get; }

    /// <summary>
    /// Creates a formatted localization key.
    /// </summary>
    /// <param name="key">The base localization key.</param>
    /// <param name="args">The format arguments.</param>
    public FormattedLocKey(LocKey key, object[] args)
    {
        Key = key;
        Args = args;
    }

    /// <summary>
    /// Resolves this formatted key to a display string using the current locale,
    /// substituting the format arguments into the translated template.
    /// </summary>
    public string Resolve()
    {
        string template = Key.Resolve();
        if (Args == null || Args.Length == 0)
        {
            return template;
        }
        return string.Format(template, Args);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        try
        {
            return Resolve();
        }
        catch (FormatException)
        {
            return Key.Value ?? string.Empty;
        }
    }
}
