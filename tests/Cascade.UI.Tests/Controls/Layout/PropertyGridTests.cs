#pragma warning disable CA2000, CA1812
using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class PropertyGridTests
{
    [Test]
    public async Task StringPropertyCreation()
    {
        string value = "hello";
        var prop = Property.String("Name", () => value, v => { value = v; });

        var expectedName = "Name";
        var expectedKind = PropertyEditorKind.String;
        await Assert.That(prop.Name).IsEqualTo(expectedName);
        await Assert.That(prop.EditorKind).IsEqualTo(expectedKind);
        await Assert.That(prop.Getter).IsNotNull();
        await Assert.That(prop.Setter).IsNotNull();
    }

    [Test]
    public async Task FloatPropertyWithConstraints()
    {
        float value = 1.5f;
        var prop = Property.Float("Width", () => value, v => { value = v; }, min: 0f, max: 100f, step: 0.5f, format: "F1");

        var expectedMin = 0f;
        var expectedMax = 100f;
        var expectedStep = 0.5f;
        var expectedFormat = "F1";
        await Assert.That(prop.MinValue).IsEqualTo(expectedMin);
        await Assert.That(prop.MaxValue).IsEqualTo(expectedMax);
        await Assert.That(prop.StepValue).IsEqualTo(expectedStep);
        await Assert.That(prop.FormatString).IsEqualTo(expectedFormat);
    }

    [Test]
    public async Task IntPropertyWithConstraints()
    {
        int value = 5;
        var prop = Property.Int("Count", () => value, v => { value = v; }, min: 0, max: 10);

        var expectedKind = PropertyEditorKind.Int;
        var expectedMin = 0;
        var expectedMax = 10;
        await Assert.That(prop.EditorKind).IsEqualTo(expectedKind);
        await Assert.That(prop.MinIntValue).IsEqualTo(expectedMin);
        await Assert.That(prop.MaxIntValue).IsEqualTo(expectedMax);
    }

    [Test]
    public async Task BoolPropertyCreation()
    {
        bool value = true;
        var prop = Property.Bool("Active", () => value, v => { value = v; });

        var expectedKind = PropertyEditorKind.Bool;
        await Assert.That(prop.EditorKind).IsEqualTo(expectedKind);
        await Assert.That(prop.Getter).IsNotNull();
    }

    [Test]
    public async Task EnumPropertyCreation()
    {
        var value = SplitOrientation.Horizontal;
        var prop = Property.Enum("Orientation", () => value, v => { value = v; });

        var expectedKind = PropertyEditorKind.Enum;
        var expectedType = typeof(SplitOrientation);
        await Assert.That(prop.EditorKind).IsEqualTo(expectedKind);
        await Assert.That(prop.EnumType).IsEqualTo(expectedType);
    }

    [Test]
    public async Task ReadOnlyPropertyHasNoSetter()
    {
        var prop = Property.ReadOnly("Status", () => (object)"active");

        var expectedReadOnly = true;
        await Assert.That(prop.IsReadOnly).IsEqualTo(expectedReadOnly);
        await Assert.That(prop.Setter).IsNull();
    }

    [Test]
    public async Task PropertyGroupWithVisibility()
    {
        bool visible = true;
        var prop = Property.String("Name", () => "", v => { });
        var group = new PropertyGroup("Advanced", () => visible, new[] { prop });

        var expectedName = "Advanced";
        var expectedCount = 1;
        await Assert.That(group.Name).IsEqualTo(expectedName);
        await Assert.That(group.Properties.Count).IsEqualTo(expectedCount);
        var visiblePredicate = (object?)group.Visible;
        await Assert.That(visiblePredicate).IsNotNull();
    }

    [Test]
    public async Task PropertyGroupWithoutVisibility()
    {
        var prop = Property.Bool("Flag", () => true, v => { });
        var group = new PropertyGroup("Basic", prop);

        var visiblePredicate = (object?)group.Visible;
        await Assert.That(visiblePredicate).IsNull();
    }

    [Test]
    public async Task PropertyGridWithExplicitGroups()
    {
        var group1 = new PropertyGroup("G1", Property.String("A", () => "", v => { }));
        var group2 = new PropertyGroup("G2", Property.Int("B", () => 0, v => { }));
        var grid = new PropertyGrid(new[] { group1, group2 });

        var expectedCount = 2;
        await Assert.That(grid.Groups.Count).IsEqualTo(expectedCount);
        await Assert.That(grid.Target).IsNull();
    }

    [Test]
    public async Task PropertyGridWithTarget()
    {
        var target = new { Name = "Test", Value = 42 };
        var grid = new PropertyGrid(target);

        await Assert.That(grid.Target).IsNotNull();
        var expectedGroupCount = 0;
        await Assert.That(grid.Groups.Count).IsEqualTo(expectedGroupCount);
    }

    [Test]
    public async Task UpdateIntervalSetsValue()
    {
        var prop = Property.Float("Speed", () => 1.0f, v => { });
        var interval = TimeSpan.FromMilliseconds(100);
        prop.UpdateInterval(interval);

        await Assert.That(prop.UpdateIntervalValue).IsEqualTo(interval);
    }

    [Test]
    public async Task MultiLinePropertyCreation()
    {
        var prop = Property.MultiLine("Description", () => "text", v => { });

        var expectedKind = PropertyEditorKind.MultiLine;
        await Assert.That(prop.EditorKind).IsEqualTo(expectedKind);
    }

    [Test]
    public async Task CustomPropertyCreation()
    {
        var prop = Property.Custom("Custom", () => 42, v => { }, v => Node.Empty);

        var expectedKind = PropertyEditorKind.Custom;
        await Assert.That(prop.EditorKind).IsEqualTo(expectedKind);
        await Assert.That(prop.CustomEditor).IsNotNull();
    }

    [Test]
    public async Task DatePropertyCreation()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var prop = Property.Date("Date", () => today, v => { });

        var expectedKind = PropertyEditorKind.Date;
        await Assert.That(prop.EditorKind).IsEqualTo(expectedKind);
    }

    [Test]
    public async Task ColorPropertyCreation()
    {
        var color = ColorValue.Transparent;
        var prop = Property.Color("Color", () => color, v => { });

        var expectedKind = PropertyEditorKind.Color;
        await Assert.That(prop.EditorKind).IsEqualTo(expectedKind);
    }
}
