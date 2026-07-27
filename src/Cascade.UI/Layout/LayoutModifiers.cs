namespace Cascade.UI;

/// <summary>
/// Fluent layout modifier methods available on all <see cref="Node"/> instances.
/// These methods return the node with the modifier applied, enabling chaining.
/// </summary>
public static class LayoutModifiers
{
    // ── Sizing ────────────────────────────────────────────────────────

    /// <summary>Sets an explicit width in logical pixels.</summary>
    public static TNode Width<TNode>(this TNode node, float width) where TNode : Node
    {
        node.LayoutData.ExplicitWidth = width;
        return node;
    }

    /// <summary>Sets an explicit height in logical pixels.</summary>
    public static TNode Height<TNode>(this TNode node, float height) where TNode : Node
    {
        node.LayoutData.ExplicitHeight = height;
        return node;
    }

    /// <summary>Sets both width and height to the same value (square).</summary>
    public static TNode Size<TNode>(this TNode node, float widthAndHeight) where TNode : Node
    {
        node.LayoutData.ExplicitWidth = widthAndHeight;
        node.LayoutData.ExplicitHeight = widthAndHeight;
        return node;
    }

    /// <summary>Sets explicit width and height.</summary>
    public static TNode Size<TNode>(this TNode node, float width, float height) where TNode : Node
    {
        node.LayoutData.ExplicitWidth = width;
        node.LayoutData.ExplicitHeight = height;
        return node;
    }

    /// <summary>Sets the size from a <see cref="Cascade.UI.Size"/> value.</summary>
    public static TNode Size<TNode>(this TNode node, Size size) where TNode : Node
    {
        node.LayoutData.ExplicitWidth = size.Width;
        node.LayoutData.ExplicitHeight = size.Height;
        return node;
    }

    /// <summary>Sets a minimum width constraint.</summary>
    public static TNode MinWidth<TNode>(this TNode node, float minWidth) where TNode : Node
    {
        node.LayoutData.MinWidthMod = minWidth;
        return node;
    }

    /// <summary>Sets a maximum width constraint.</summary>
    public static TNode MaxWidth<TNode>(this TNode node, float maxWidth) where TNode : Node
    {
        node.LayoutData.MaxWidthMod = maxWidth;
        return node;
    }

    /// <summary>Sets a minimum height constraint.</summary>
    public static TNode MinHeight<TNode>(this TNode node, float minHeight) where TNode : Node
    {
        node.LayoutData.MinHeightMod = minHeight;
        return node;
    }

    /// <summary>Sets a maximum height constraint.</summary>
    public static TNode MaxHeight<TNode>(this TNode node, float maxHeight) where TNode : Node
    {
        node.LayoutData.MaxHeightMod = maxHeight;
        return node;
    }

    /// <summary>Sets min/max constraints on both axes.</summary>
    public static TNode ConstrainedSize<TNode>(
        this TNode node,
        float? minWidth = null,
        float? maxWidth = null,
        float? minHeight = null,
        float? maxHeight = null) where TNode : Node
    {
        var data = node.LayoutData;
        if (minWidth.HasValue)
        {
            data.MinWidthMod = minWidth.Value;
        }
        if (maxWidth.HasValue)
        {
            data.MaxWidthMod = maxWidth.Value;
        }
        if (minHeight.HasValue)
        {
            data.MinHeightMod = minHeight.Value;
        }
        if (maxHeight.HasValue)
        {
            data.MaxHeightMod = maxHeight.Value;
        }
        return node;
    }

    /// <summary>Constrains the node to a specific aspect ratio (width / height).</summary>
    public static TNode AspectRatio<TNode>(this TNode node, float widthOverHeight) where TNode : Node
    {
        node.LayoutData.AspectRatio = widthOverHeight;
        return node;
    }

    // ── Flex ──────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the flex grow factor. In a Row or Column, the node takes available
    /// space proportional to its grow value relative to siblings.
    /// </summary>
    public static TNode Grow<TNode>(this TNode node, float factor) where TNode : Node
    {
        node.LayoutData.GrowFactor = factor;
        return node;
    }

    /// <summary>Shorthand for <c>.Grow(1)</c> — takes all remaining space.</summary>
    public static TNode Expand<TNode>(this TNode node) where TNode : Node
    {
        node.LayoutData.GrowFactor = 1f;
        return node;
    }

    // ── Padding ───────────────────────────────────────────────────────

    /// <summary>Sets equal padding on all sides.</summary>
    public static TNode Padding<TNode>(this TNode node, float all) where TNode : Node
    {
        node.LayoutData.Padding = EdgeInsets.All(all);
        return node;
    }

    /// <summary>Sets symmetric horizontal and vertical padding.</summary>
    public static TNode Padding<TNode>(this TNode node, float horizontal, float vertical) where TNode : Node
    {
        node.LayoutData.Padding = EdgeInsets.Symmetric(horizontal, vertical);
        return node;
    }

    /// <summary>Sets padding from an <see cref="EdgeInsets"/> value.</summary>
    public static TNode Padding<TNode>(this TNode node, EdgeInsets insets) where TNode : Node
    {
        node.LayoutData.Padding = insets;
        return node;
    }

    // ── Margin ────────────────────────────────────────────────────────

    /// <summary>Sets equal margin on all sides.</summary>
    public static TNode Margin<TNode>(this TNode node, float all) where TNode : Node
    {
        node.LayoutData.Margin = EdgeInsets.All(all);
        return node;
    }

    /// <summary>Sets symmetric horizontal and vertical margin.</summary>
    public static TNode Margin<TNode>(this TNode node, float horizontal, float vertical) where TNode : Node
    {
        node.LayoutData.Margin = EdgeInsets.Symmetric(horizontal, vertical);
        return node;
    }

    /// <summary>Sets margin from an <see cref="EdgeInsets"/> value.</summary>
    public static TNode Margin<TNode>(this TNode node, EdgeInsets insets) where TNode : Node
    {
        node.LayoutData.Margin = insets;
        return node;
    }

    // ── Alignment ─────────────────────────────────────────────────────

    /// <summary>Sets the alignment of this node within its parent (Stack, Page, etc.).</summary>
    public static TNode Alignment<TNode>(this TNode node, Alignment value) where TNode : Node
    {
        node.LayoutData.NodeAlignment = value;
        return node;
    }

    // ── Gravity ───────────────────────────────────────────────────────

    /// <summary>Sets vertical gravity within a Stack or Page content area.</summary>
    public static TNode Gravity<TNode>(this TNode node, VerticalGravity gravity) where TNode : Node
    {
        var existing = node.LayoutData.NodeAlignment ?? UI.Alignment.TopLeft;
        node.LayoutData.NodeAlignment = new Alignment(existing.X, ToAlignmentY(gravity));
        return node;
    }

    /// <summary>Sets horizontal gravity within a Stack or Page content area.</summary>
    public static TNode Gravity<TNode>(this TNode node, HorizontalGravity gravity) where TNode : Node
    {
        var existing = node.LayoutData.NodeAlignment ?? UI.Alignment.TopLeft;
        node.LayoutData.NodeAlignment = new Alignment(ToAlignmentX(gravity), existing.Y);
        return node;
    }

    /// <summary>Sets both vertical and horizontal gravity.</summary>
    public static TNode Gravity<TNode>(this TNode node, VerticalGravity vertical, HorizontalGravity horizontal) where TNode : Node
    {
        node.LayoutData.NodeAlignment = new Alignment(ToAlignmentX(horizontal), ToAlignmentY(vertical));
        return node;
    }

    // ── Visibility and Clipping ───────────────────────────────────────

    /// <summary>
    /// Shows or hides the node. Hidden nodes still occupy layout space.
    /// To remove from layout entirely, use a conditional ternary with Node.Empty.
    /// </summary>
    public static TNode Visible<TNode>(this TNode node, bool visible) where TNode : Node
    {
        node.LayoutData.IsVisible = visible;
        return node;
    }

    /// <summary>Clips child content that exceeds this node's bounds.</summary>
    public static TNode ClipToBounds<TNode>(this TNode node, bool clip = true) where TNode : Node
    {
        node.LayoutData.ClipContent = clip;
        return node;
    }

    // ── Container modifiers ───────────────────────────────────────────

    /// <summary>
    /// Sets the spacing between children — chainable equivalent of the
    /// spacing constructor parameter on Row, Column, and similar containers.
    /// </summary>
    public static TNode Spacing<TNode>(this TNode node, float spacing) where TNode : Node
    {
        node.LayoutData.SpacingOverride = spacing;
        return node;
    }

    // ── Borders ───────────────────────────────────────────────────────

    /// <summary>Applies a border on all sides.</summary>
    public static TNode Border<TNode>(this TNode node, ColorValue color, float width = 1f) where TNode : Node
    {
        node.LayoutData.BorderColor = color;
        node.LayoutData.BorderWidth = width;
        return node;
    }

    /// <summary>Applies a border on all sides with a corner radius.</summary>
    public static TNode Border<TNode>(this TNode node, ColorValue color, float width, float radius) where TNode : Node
    {
        node.LayoutData.BorderColor = color;
        node.LayoutData.BorderWidth = width;
        node.LayoutData.BorderRadiusValue = radius;
        return node;
    }

    /// <summary>
    /// Applies a gradient border. The gradient fills a ring of the given width
    /// around the node's rounded rectangle, leaving the interior (the node's own
    /// background) untouched. Use for emphasis states — a selected card, a focused
    /// tile — where a flat colour reads plainer than a soft gradient stroke.
    /// </summary>
    public static TNode Border<TNode>(this TNode node, Gradient gradient, float width, float radius) where TNode : Node
    {
        node.LayoutData.BorderGradient = gradient;
        node.LayoutData.BorderWidth = width;
        node.LayoutData.BorderRadiusValue = radius;
        return node;
    }

    /// <summary>Applies a border on the top edge only.</summary>
    public static TNode BorderTop<TNode>(this TNode node, ColorValue color, float width = 1f) where TNode : Node
    {
        node.LayoutData.BorderTopColor = color;
        node.LayoutData.BorderTopWidth = width;
        return node;
    }

    /// <summary>Applies a border on the bottom edge only.</summary>
    public static TNode BorderBottom<TNode>(this TNode node, ColorValue color, float width = 1f) where TNode : Node
    {
        node.LayoutData.BorderBottomColor = color;
        node.LayoutData.BorderBottomWidth = width;
        return node;
    }

    /// <summary>Applies a border on the left edge only.</summary>
    public static TNode BorderLeft<TNode>(this TNode node, ColorValue color, float width = 1f) where TNode : Node
    {
        node.LayoutData.BorderLeftColor = color;
        node.LayoutData.BorderLeftWidth = width;
        return node;
    }

    /// <summary>Applies a border on the right edge only.</summary>
    public static TNode BorderRight<TNode>(this TNode node, ColorValue color, float width = 1f) where TNode : Node
    {
        node.LayoutData.BorderRightColor = color;
        node.LayoutData.BorderRightWidth = width;
        return node;
    }

    // ── Background ────────────────────────────────────────────────────

    /// <summary>Sets the background color of this node.</summary>
    public static TNode Background<TNode>(this TNode node, ColorValue color) where TNode : Node
    {
        node.LayoutData.BackgroundColor = color;
        return node;
    }

    // ── Corner Radius ─────────────────────────────────────────────────

    /// <summary>Sets a uniform corner radius on all corners.</summary>
    public static TNode CornerRadius<TNode>(this TNode node, float radius) where TNode : Node
    {
        node.LayoutData.CornerRadiusValue = radius;
        return node;
    }

    // ── Opacity ───────────────────────────────────────────────────────

    /// <summary>Sets the opacity of this node (0.0 transparent, 1.0 opaque).</summary>
    public static TNode Opacity<TNode>(this TNode node, float opacity) where TNode : Node
    {
        node.LayoutData.Opacity = opacity;
        return node;
    }

    // ── Transform ─────────────────────────────────────────────────────

    /// <summary>Applies a horizontal translation in logical pixels.</summary>
    public static TNode TranslateX<TNode>(this TNode node, float x) where TNode : Node
    {
        node.LayoutData.TranslateX = x;
        return node;
    }

    /// <summary>Applies a vertical translation in logical pixels.</summary>
    public static TNode TranslateY<TNode>(this TNode node, float y) where TNode : Node
    {
        node.LayoutData.TranslateY = y;
        return node;
    }

    /// <summary>Applies a uniform scale transform.</summary>
    public static TNode Scale<TNode>(this TNode node, float scale) where TNode : Node
    {
        node.LayoutData.Scale = scale;
        return node;
    }

    /// <summary>Applies a rotation transform.</summary>
    public static TNode Rotate<TNode>(this TNode node, Angle angle) where TNode : Node
    {
        node.LayoutData.Rotation = angle;
        return node;
    }

    // ── Private helpers ──────────────────────────────────────────────

    private static float ToAlignmentX(HorizontalGravity gravity)
    {
        return gravity switch
        {
            HorizontalGravity.Left => -1f,
            HorizontalGravity.Center => 0f,
            HorizontalGravity.Right => 1f,
            HorizontalGravity.LeftThird => -2f / 3f,
            HorizontalGravity.RightThird => 2f / 3f,
            _ => -1f
        };
    }

    private static float ToAlignmentY(VerticalGravity gravity)
    {
        return gravity switch
        {
            VerticalGravity.Top => -1f,
            VerticalGravity.Center => 0f,
            VerticalGravity.Bottom => 1f,
            VerticalGravity.TopThird => -2f / 3f,
            VerticalGravity.BottomThird => 2f / 3f,
            _ => -1f
        };
    }
}
