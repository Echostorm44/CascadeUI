namespace Cascade.UI;

/// <summary>
/// Displays a user or entity avatar — either an image loaded from a URL/path,
/// or automatically generated initials in a consistently-colored circle.
/// </summary>
public sealed class Avatar : Node
{
    /// <summary>The diameter of an avatar of size <paramref name="size"/> (Md when null).</summary>
    internal static float PixelSize(AvatarSize? size) => size switch
    {
        AvatarSize.Xs => 20f,
        AvatarSize.Sm => 28f,
        AvatarSize.Lg => 48f,
        AvatarSize.Xl => 64f,
        AvatarSize.Xxl => 96f,
        _ => 36f,
    };

    /// <summary>
    /// Creates an anonymous avatar with a generic person silhouette.
    /// </summary>
    public Avatar()
    {
        Name = null;
        Url = null;
        Path = null;
        Fallback = null;
        Initials = null;
    }

    /// <summary>
    /// Creates an initials-only avatar derived from a name.
    /// </summary>
    /// <param name="name">Full name used to generate initials and a consistent color.</param>
    public Avatar(string name)
    {
        Name = name;
        Url = null;
        Path = null;
        Fallback = name;
        Initials = GenerateInitials(name);
    }

    /// <summary>
    /// Creates an image avatar from a URL with an initials fallback.
    /// </summary>
    /// <param name="url">Avatar image URL.</param>
    /// <param name="fallback">Fallback name for initials if the image fails to load.</param>
    public Avatar(string url, string fallback)
    {
        Name = null;
        Url = url;
        Path = null;
        Fallback = fallback;
        Initials = GenerateInitials(fallback);
    }

    /// <summary>
    /// Creates an image avatar from a local file path with an initials fallback.
    /// </summary>
    /// <param name="path">Local file path to the avatar image.</param>
    /// <param name="fallback">Fallback name for initials if the image fails to load.</param>
    /// <param name="fromPath">Disambiguator (unused).</param>
    public Avatar(string path, string fallback, bool fromPath)
    {
        Name = null;
        Url = null;
        Path = path;
        Fallback = fallback;
        Initials = GenerateInitials(fallback);
    }

    /// <summary>Full name (for initials generation).</summary>
    public string? Name { get; }

    /// <summary>Image URL, or null.</summary>
    public string? Url { get; }

    /// <summary>Local file path, or null.</summary>
    public string? Path { get; }

    /// <summary>Fallback name used for initials when the image fails.</summary>
    public string? Fallback { get; }

    /// <summary>Generated initials for display.</summary>
    internal string? Initials { get; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal AvatarSize? SizePreset { get; set; }
    internal float? CustomSize { get; set; }
    internal AvatarShape ShapeValue { get; set; } = AvatarShape.Circle;
    internal PresenceStatus? PresenceValue { get; set; }
    internal BadgePosition PresencePosition { get; set; } = BadgePosition.BottomRight;

    // ── Fluent modifiers ──────────────────────────────────────────────

    /// <summary>Sets the avatar size from a preset.</summary>
    public Avatar Size(AvatarSize size)
    {
        SizePreset = size;
        CustomSize = null;
        return this;
    }

    /// <summary>Sets a custom avatar size in logical pixels.</summary>
    public Avatar Size(float pixels)
    {
        CustomSize = pixels;
        SizePreset = null;
        return this;
    }

    /// <summary>Sets the avatar shape.</summary>
    public Avatar Shape(AvatarShape shape)
    {
        ShapeValue = shape;
        return this;
    }

    /// <summary>Shows a presence indicator dot.</summary>
    public Avatar Presence(PresenceStatus status, BadgePosition position = BadgePosition.BottomRight)
    {
        PresenceValue = status;
        PresencePosition = position;
        return this;
    }

    private static string? GenerateInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        char first = char.ToUpperInvariant(parts[0][0]);
        if (parts.Length == 1)
        {
            return new string(first, 1);
        }

        char last = char.ToUpperInvariant(parts[^1][0]);
        return string.Concat(first, last);
    }
}

/// <summary>
/// A stacked overlap of multiple avatars representing a group.
/// </summary>
public sealed class GroupAvatar : Node
{
    /// <summary>Creates a group avatar display.</summary>
    /// <param name="avatars">Avatar information for each member.</param>
    /// <param name="max">Maximum avatars shown before an overflow "+N" badge.</param>
    public GroupAvatar(IReadOnlyList<AvatarInfo> avatars, int max = 3)
    {
        Avatars = avatars;
        Max = max;
    }

    /// <summary>Avatar info for each group member.</summary>
    public IReadOnlyList<AvatarInfo> Avatars { get; }

    /// <summary>Maximum avatars displayed before overflow.</summary>
    public int Max { get; }

    /// <summary>Sets the avatar size for all members in the group.</summary>
    public GroupAvatar Size(AvatarSize size)
    {
        SizePreset = size;
        return this;
    }

    internal AvatarSize? SizePreset { get; private set; }

    /// <summary>The diameter of each member avatar.</summary>
    internal float MemberSize => Avatar.PixelSize(SizePreset ?? AvatarSize.Md);

    /// <summary>How far each avatar is from the previous: they overlap by a third.</summary>
    internal float Step => MathF.Round(MemberSize * 0.68f);

    /// <summary>The avatars drawn: up to <see cref="Max"/> members.</summary>
    internal int ShownCount => Math.Min(Avatars.Count, Math.Max(Max, 0));

    /// <summary>Members not shown, counted on a "+N" badge after the avatars.</summary>
    internal int OverflowCount => Avatars.Count - ShownCount;

    /// <summary>The group's width: each shown avatar and the overflow badge, overlapping.</summary>
    internal float GroupWidth
    {
        get
        {
            int circles = ShownCount + (OverflowCount > 0 ? 1 : 0);
            return circles == 0 ? 0f : MemberSize + (circles - 1) * Step;
        }
    }

    // One Avatar per member, kept across paints so hover animation state stays put.
    private Avatar[]? memberAvatars;

    internal Avatar MemberAvatar(int index)
    {
        memberAvatars ??= new Avatar[Avatars.Count];
        return memberAvatars[index] ??= Avatars[index].AvatarUrl is { Length: > 0 } url
            ? new Avatar(url, Avatars[index].Name)
            : new Avatar(Avatars[index].Name);
    }
}

/// <summary>
/// Information for a single avatar in a <see cref="GroupAvatar"/>.
/// </summary>
/// <param name="Name">Display name (used for initials fallback).</param>
/// <param name="AvatarUrl">Optional image URL.</param>
public readonly record struct AvatarInfo(string Name, string? AvatarUrl = null);

/// <summary>
/// Preset sizes for <see cref="Avatar"/> controls.
/// </summary>
public enum AvatarSize
{
    /// <summary>Extra small — 20px.</summary>
    Xs,

    /// <summary>Small — 28px.</summary>
    Sm,

    /// <summary>Medium — 36px (default).</summary>
    Md,

    /// <summary>Large — 48px.</summary>
    Lg,

    /// <summary>Extra large — 64px.</summary>
    Xl,

    /// <summary>Double extra large — 96px.</summary>
    Xxl
}

/// <summary>
/// Avatar shape.
/// </summary>
public enum AvatarShape
{
    /// <summary>Fully rounded circle (default).</summary>
    Circle,

    /// <summary>Rounded square with small radius.</summary>
    Rounded,

    /// <summary>No rounding — square.</summary>
    Square
}

/// <summary>
/// Online presence status for avatar indicators.
/// </summary>
public enum PresenceStatus
{
    /// <summary>User is online and available.</summary>
    Online,

    /// <summary>User is away or idle.</summary>
    Away,

    /// <summary>User is busy.</summary>
    Busy,

    /// <summary>User does not want to be disturbed.</summary>
    DoNotDisturb,

    /// <summary>User is offline.</summary>
    Offline,

    /// <summary>Status is unknown.</summary>
    Unknown
}

/// <summary>
/// Badge position relative to a parent node.
/// </summary>
public enum BadgePosition
{
    /// <summary>Top-left corner.</summary>
    TopLeft,

    /// <summary>Top-right corner (default for count badges).</summary>
    TopRight,

    /// <summary>Bottom-left corner.</summary>
    BottomLeft,

    /// <summary>Bottom-right corner (default for presence indicators).</summary>
    BottomRight
}
