namespace Cascade.UI;

/// <summary>
/// Walks a node tree to find all hero-tagged elements and capture their
/// geometry (bounds, corner radius) for hero transition animation.
/// </summary>
internal static class HeroGeometryCapture
{
    /// <summary>
    /// Captures all hero-tagged elements in the given node tree.
    /// Returns a list of <see cref="HeroCapture"/> with absolute bounds
    /// computed by summing layout offsets through the ancestor chain.
    /// </summary>
    internal static List<HeroCapture> Capture(Node? root)
    {
        var results = new List<HeroCapture>();
        if (root is null)
        {
            return results;
        }

        CaptureRecursive(root, 0f, 0f, results);
        return results;
    }

    private static void CaptureRecursive(Node node, float offsetX, float offsetY, List<HeroCapture> results)
    {
        if (node.IsLayoutEmpty)
        {
            return;
        }

        var bounds = node.LayoutData.Bounds;
        float absX = offsetX + bounds.X;
        float absY = offsetY + bounds.Y;

        if (node.HeroKeyValue.HasValue)
        {
            results.Add(new HeroCapture
            {
                Key = node.HeroKeyValue.Value,
                Bounds = new Rect(absX, absY, bounds.Width, bounds.Height),
                CornerRadius = node.LayoutData.CornerRadiusValue ?? 0f,
                SourceNode = node,
            });
        }

        // Account for padding when recursing into children
        float childOffsetX = absX + node.LayoutData.Padding.Left;
        float childOffsetY = absY + node.LayoutData.Padding.Top;

        // Recurse into children based on node type
        switch (node)
        {
            case Component comp when comp.RenderedTree is { } rendered:
                CaptureRecursive(rendered, absX, absY, results);
                break;

            case Column col:
                foreach (var child in col.Children)
                {
                    CaptureRecursive(child, childOffsetX, childOffsetY, results);
                }
                break;

            case Row row:
                foreach (var child in row.Children)
                {
                    CaptureRecursive(child, childOffsetX, childOffsetY, results);
                }
                break;

            case Stack stack:
                foreach (var child in stack.Children)
                {
                    CaptureRecursive(child, childOffsetX, childOffsetY, results);
                }
                break;

            case ScrollView sv:
                CaptureRecursive(sv.Content, childOffsetX, childOffsetY, results);
                break;

            case Center center:
                CaptureRecursive(center.Child, childOffsetX, childOffsetY, results);
                break;

            case Grid grid:
                foreach (var child in grid.Children)
                {
                    CaptureRecursive(child, childOffsetX, childOffsetY, results);
                }
                break;

            case NavigationTransitionHost nth:
                if (nth.IncomingPage is not null)
                {
                    CaptureRecursive(nth.IncomingPage, absX, absY, results);
                }
                break;
        }
    }
}
