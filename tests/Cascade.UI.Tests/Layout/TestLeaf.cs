using Cascade.UI;

namespace Cascade.UI.Tests;

/// <summary>
/// A concrete leaf node for testing. Sets explicit width and height so that
/// the layout solver treats it as a fixed-size element.
/// </summary>
internal sealed class TestLeaf : Node
{
    private readonly Size desiredSize;

    public TestLeaf(float width, float height)
    {
        desiredSize = new Size(width, height);
        if (width > 0)
        {
            LayoutData.ExplicitWidth = width;
        }
        if (height > 0)
        {
            LayoutData.ExplicitHeight = height;
        }
    }

    internal Size DesiredSize => desiredSize;
}
