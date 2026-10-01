namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// Avatar background colours come from a hash of the name. string.GetHashCode is randomized per
/// process, so the colour used to change on every launch; the hash must be a fixed function.
/// </summary>
public class AvatarColorTests
{
    [Test]
    public async Task NameHash_IsFnv1aOfTheName()
    {
        // FNV-1a reference values: empty string is the offset basis; "a" is a published vector.
        await Assert.That(NodePainter.StableNameHash("")).IsEqualTo(2166136261u);
        await Assert.That(NodePainter.StableNameHash("a")).IsEqualTo(0xE40C292Cu);
    }
}
