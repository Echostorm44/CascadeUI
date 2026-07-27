#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class BadgeTests
{
    [Test]
    public async Task Constructor_Count_StoresValues()
    {
        var child = new Label("Inbox");
        var badge = new Badge(5, child, BadgePosition.BottomLeft);

        var count = badge.Count;
        bool same = ReferenceEquals(badge.Child, child);
        var position = badge.Position;
        var expectedCount = 5;
        var expectedPosition = BadgePosition.BottomLeft;
        await Assert.That(count).IsEqualTo(expectedCount);
        await Assert.That(same).IsTrue();
        await Assert.That(position).IsEqualTo(expectedPosition);
    }

    [Test]
    public async Task Constructor_Count_IsNotDot()
    {
        var badge = new Badge(1, Node.Empty);

        bool isDot = badge.IsDot;
        await Assert.That(isDot).IsFalse();
    }

    [Test]
    public async Task Constructor_Dot_SetsDot()
    {
        var badge = new Badge(true, Node.Empty, BadgePosition.TopLeft);

        bool isDot = badge.IsDot;
        await Assert.That(isDot).IsTrue();
    }

    [Test]
    public async Task Constructor_Dot_CountIsNull()
    {
        var badge = new Badge(true, Node.Empty);

        bool isNull = badge.Count == null;
        await Assert.That(isNull).IsTrue();
    }

    [Test]
    public async Task Constructor_Content_StoresContent()
    {
        var content = new Label("!");
        var badge = new Badge(content, Node.Empty);

        bool same = ReferenceEquals(badge.Content, content);
        await Assert.That(same).IsTrue();
    }

    [Test]
    public async Task Max_DefaultIs99()
    {
        var badge = new Badge(1, Node.Empty);

        int max = badge.MaxCount;
        var expected = 99;
        await Assert.That(max).IsEqualTo(expected);
    }

    [Test]
    public async Task Max_SetsValue()
    {
        var badge = new Badge(1, Node.Empty).Max(250);

        int max = badge.MaxCount;
        var expected = 250;
        await Assert.That(max).IsEqualTo(expected);
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var badge = new Badge(1, Node.Empty);
        var result = badge.Max(10);

        bool same = ReferenceEquals(badge, result);
        await Assert.That(same).IsTrue();
    }
}
