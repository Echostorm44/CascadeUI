using System.Runtime.CompilerServices;

namespace Cascade.UI;

/// <summary>
/// A typed, refactor-safe key for hero transition elements. Define as a static
/// readonly field on the destination page — the field name becomes the slot
/// identity. No magic strings are involved at any point.
/// </summary>
/// <remarks>
/// <code>
/// // On the destination page:
/// public static readonly HeroSlot Cover = HeroSlot.Define();
/// public static readonly HeroSlot Title = HeroSlot.Define();
///
/// // In Render() — tag the hero element:
/// Image(album.CoverArt).NavigationHero(Cover.For(album.Id))
///
/// // On the source page — reference the destination's slot:
/// Image(album.CoverArt).NavigationHero(AlbumDetailPage.Cover.For(album.Id))
/// </code>
/// </remarks>
public sealed class HeroSlot
{
    private readonly string name;

    private HeroSlot(string name)
    {
        this.name = name;
    }

    /// <summary>
    /// Creates a new hero slot. The slot name is captured automatically from
    /// the field name via <see cref="CallerMemberNameAttribute"/>.
    /// </summary>
    /// <param name="name">
    /// Captured automatically — do not pass explicitly.
    /// </param>
    public static HeroSlot Define([CallerMemberName] string name = "")
    {
        return new HeroSlot(name);
    }

    /// <summary>
    /// Creates a discriminated hero key for use in lists. The <paramref name="id"/>
    /// distinguishes this item's hero from other items using the same slot.
    /// </summary>
    /// <param name="id">A unique identifier for this item (typically a database key).</param>
    public HeroKey For(object id)
    {
        return new HeroKey(this, id);
    }

    /// <summary>The slot name (captured from the field name at definition).</summary>
    internal string Name => name;

    /// <summary>
    /// Implicit conversion to <see cref="HeroKey"/> for one-to-one heroes
    /// that are not in a list and don't need a discriminator.
    /// </summary>
    public static implicit operator HeroKey(HeroSlot slot)
    {
        return new HeroKey(slot, null);
    }
}

/// <summary>
/// A fully-qualified hero identity: a <see cref="HeroSlot"/> plus an optional
/// item discriminator. Two hero keys match when their slot names are equal and
/// their IDs are equal (or both null for one-to-one heroes).
/// </summary>
public readonly struct HeroKey : IEquatable<HeroKey>
{
    /// <summary>The hero slot this key belongs to.</summary>
    public HeroSlot Slot { get; }

    /// <summary>
    /// The item discriminator, or null for one-to-one heroes.
    /// </summary>
    public object? Id { get; }

    internal HeroKey(HeroSlot slot, object? id)
    {
        Slot = slot;
        Id = id;
    }

    /// <summary>The slot name portion of this key.</summary>
    internal string SlotName => Slot.Name;

    /// <inheritdoc/>
    public bool Equals(HeroKey other)
    {
        return string.Equals(SlotName, other.SlotName, StringComparison.Ordinal)
            && Equals(Id, other.Id);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is HeroKey other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(SlotName, Id);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return Id is null ? SlotName : $"{SlotName}:{Id}";
    }

    public static bool operator ==(HeroKey left, HeroKey right) => left.Equals(right);
    public static bool operator !=(HeroKey left, HeroKey right) => !left.Equals(right);
}
