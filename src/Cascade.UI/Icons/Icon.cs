namespace Cascade.UI;

/// <summary>
/// An immutable value type representing an icon, carrying SVG path data,
/// default size, and an accessible name. Icons are static fields on
/// source-generated classes — not enums, not strings.
/// </summary>
/// <remarks>
/// <para>
/// At runtime, icons are drawn as GPU path draw calls (Etch). There is
/// no SVG parsing and no file I/O on the hot path — the source generator
/// has full access to all path data at compile time.
/// </para>
/// <para>
/// An icon may contain multiple SVG sub-paths (e.g., a bell icon with a
/// separate clapper path). All paths share the same view box and are
/// rendered as a single draw call.
/// </para>
/// </remarks>
public readonly struct Icon : IEquatable<Icon>
{
    private readonly string[] paths;
    private readonly float viewBoxWidth;
    private readonly float viewBoxHeight;

    /// <summary>
    /// Creates an icon from SVG path data.
    /// </summary>
    /// <param name="paths">One or more SVG path data strings.</param>
    /// <param name="viewBox">The coordinate space of the icon (e.g., 24×24).</param>
    /// <param name="defaultSize">Default render size in logical pixels.</param>
    /// <param name="accessibleName">Accessible name for screen readers.</param>
    public Icon(string[] paths, Size viewBox, float defaultSize, string accessibleName)
    {
        this.paths = paths;
        viewBoxWidth = viewBox.Width;
        viewBoxHeight = viewBox.Height;
        DefaultSize = defaultSize;
        AccessibleName = accessibleName;
    }

    /// <summary>
    /// Creates an icon from a single SVG path data string.
    /// </summary>
    /// <param name="path">SVG path data string.</param>
    /// <param name="viewBox">The coordinate space of the icon (e.g., 24×24).</param>
    /// <param name="defaultSize">Default render size in logical pixels.</param>
    /// <param name="accessibleName">Accessible name for screen readers.</param>
    public Icon(string path, Size viewBox, float defaultSize, string accessibleName)
        : this([path], viewBox, defaultSize, accessibleName)
    {
    }

    /// <summary>
    /// The SVG path data strings. An icon may contain multiple sub-paths.
    /// </summary>
    public ReadOnlySpan<string> Paths => paths;

    /// <summary>
    /// The coordinate space of the icon (e.g., 24×24 for Lucide icons).
    /// </summary>
    public Size ViewBox => new(viewBoxWidth, viewBoxHeight);

    /// <summary>
    /// Default render size in logical pixels.
    /// </summary>
    public float DefaultSize { get; }

    /// <summary>
    /// Accessible name for screen readers.
    /// </summary>
    public string AccessibleName { get; }

    /// <inheritdoc/>
    public bool Equals(Icon other)
    {
        if (viewBoxWidth != other.viewBoxWidth || viewBoxHeight != other.viewBoxHeight)
        {
            return false;
        }

        if (DefaultSize != other.DefaultSize)
        {
            return false;
        }

        if (!string.Equals(AccessibleName, other.AccessibleName, StringComparison.Ordinal))
        {
            return false;
        }

        if (paths is null && other.paths is null)
        {
            return true;
        }

        if (paths is null || other.paths is null || paths.Length != other.paths.Length)
        {
            return false;
        }

        for (int i = 0; i < paths.Length; i++)
        {
            if (!string.Equals(paths[i], other.paths[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is Icon other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(viewBoxWidth);
        hash.Add(viewBoxHeight);
        hash.Add(DefaultSize);
        if (paths != null)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                hash.Add(paths[i], StringComparer.Ordinal);
            }
        }
        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return AccessibleName ?? "Icon";
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Icon left, Icon right)
    {
        return left.Equals(right);
    }

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Icon left, Icon right)
    {
        return !left.Equals(right);
    }
}
