using System;

namespace Cascade.UI.DevTools;

#if DEBUG

/// <summary>
/// Visual debug overlays that can be toggled independently over the running app.
/// Each overlay renders semi-transparent annotations over the existing scene.
/// </summary>
[Flags]
public enum DevToolsOverlay
{
    /// <summary>No overlays active.</summary>
    None = 0,

    /// <summary>
    /// Shows layout bounds as colored rectangles around every node.
    /// Toggle: Ctrl+Shift+L
    /// </summary>
    LayoutBounds = 1 << 0,

    /// <summary>
    /// Shows padding (green) and margin (orange) areas.
    /// Toggle: Ctrl+Shift+M
    /// </summary>
    PaddingMargin = 1 << 1,

    /// <summary>
    /// Shows accessibility labels as floating badges next to each accessible node.
    /// Toggle: Ctrl+Shift+A
    /// </summary>
    AccessibilityLabels = 1 << 2,

    /// <summary>
    /// Highlights regions that were repainted in the last frame.
    /// Toggle: Ctrl+Shift+R
    /// </summary>
    RepaintRegions = 1 << 3,

    /// <summary>
    /// Shows numbered indicators for the keyboard focus order.
    /// Toggle: Ctrl+Shift+F
    /// </summary>
    FocusOrder = 1 << 4,
}

/// <summary>
/// Renders visual debug overlays on top of the application scene graph.
/// When overlays are active, this class injects annotation nodes into
/// the render tree after the main scene is composed.
/// </summary>
internal static class VisualOverlays
{
    private static DevToolsOverlay activeOverlays = DevToolsOverlay.None;

    /// <summary>Gets the currently active overlay flags.</summary>
    public static DevToolsOverlay ActiveOverlays => activeOverlays;

    /// <summary>Sets the active overlays.</summary>
    public static void SetOverlays(DevToolsOverlay overlays)
    {
        activeOverlays = overlays;
    }

    /// <summary>Toggles a single overlay on or off.</summary>
    public static void Toggle(DevToolsOverlay overlay)
    {
        activeOverlays ^= overlay;
    }

    /// <summary>Returns true if the given overlay is currently active.</summary>
    public static bool IsActive(DevToolsOverlay overlay)
    {
        return (activeOverlays & overlay) == overlay;
    }

    /// <summary>Clears all overlays.</summary>
    public static void ClearAll()
    {
        activeOverlays = DevToolsOverlay.None;
    }

    /// <summary>
    /// Renders the active overlays into the scene graph. Called by the
    /// framework renderer after the main scene is composed.
    /// </summary>
    internal static void Render(OverlayRenderContext context)
    {
        if (activeOverlays == DevToolsOverlay.None)
        {
            return;
        }

        if (IsActive(DevToolsOverlay.LayoutBounds))
        {
            RenderLayoutBounds(context);
        }

        if (IsActive(DevToolsOverlay.PaddingMargin))
        {
            RenderPaddingMargin(context);
        }

        if (IsActive(DevToolsOverlay.AccessibilityLabels))
        {
            RenderAccessibilityLabels(context);
        }

        if (IsActive(DevToolsOverlay.RepaintRegions))
        {
            RenderRepaintRegions(context);
        }

        if (IsActive(DevToolsOverlay.FocusOrder))
        {
            RenderFocusOrder(context);
        }
    }

    private static void RenderLayoutBounds(OverlayRenderContext context)
    {
        // Walk the node tree and draw colored rectangles for each node's bounds.
        // Uses a rotating color palette so nested nodes are visually distinguishable.
        var nodes = NodeTreeWalker.GetAllNodeBounds();
        var colors = new[]
        {
            ColorValue.FromRgba(0.0f, 0.5f, 1.0f, 0.3f),  // Blue
            ColorValue.FromRgba(0.0f, 0.8f, 0.2f, 0.3f),  // Green
            ColorValue.FromRgba(1.0f, 0.5f, 0.0f, 0.3f),  // Orange
            ColorValue.FromRgba(0.8f, 0.0f, 0.8f, 0.3f),  // Purple
        };

        int colorIndex = 0;
        foreach (var (nodeId, bounds, depth) in nodes)
        {
            var color = colors[depth % colors.Length];
            context.DrawRect(bounds, color, strokeWidth: 1.0f);
            colorIndex++;
        }
    }

    private static void RenderPaddingMargin(OverlayRenderContext context)
    {
        // Walk the tree and for each node that has padding or margin,
        // draw colored overlays: green for padding, orange for margin.
        var paddingColor = ColorValue.FromRgba(0.0f, 0.8f, 0.0f, 0.2f);
        var marginColor = ColorValue.FromRgba(1.0f, 0.5f, 0.0f, 0.2f);

        var models = NodeTreeWalker.GetAllBoxModels();
        foreach (var model in models)
        {
            // Draw margin area
            if (model.Margin != EdgeInsets.Zero)
            {
                var outerRect = model.OuterBounds;
                var innerRect = ShrinkRect(outerRect, model.Margin);
                context.DrawRectDifference(outerRect, innerRect, marginColor);
            }

            // Draw padding area
            if (model.Padding != EdgeInsets.Zero)
            {
                var contentOuter = ExpandRect(model.ContentBounds, model.Padding);
                context.DrawRectDifference(contentOuter, model.ContentBounds, paddingColor);
            }
        }
    }

    private static void RenderAccessibilityLabels(OverlayRenderContext context)
    {
        // Draw floating badges showing the accessible label and role
        // next to each node that has accessibility annotations.
        var tree = AccessibilityPanel.CaptureAccessibilityTree();
        RenderAccessibilityNode(context, tree);
    }

    private static void RenderAccessibilityNode(OverlayRenderContext context, AccessibleNode node)
    {
        if (!string.IsNullOrEmpty(node.Label))
        {
            var bounds = NodeTreeWalker.GetNodeBoundsById(node.NodeId);
            if (bounds.HasValue)
            {
                string text = $"[{node.Role}] {node.Label}";
                var badgeBounds = new Rect(bounds.Value.X, bounds.Value.Y - 16, text.Length * 6 + 8, 16);
                context.DrawFilledRect(badgeBounds, ColorValue.FromRgba(0.0f, 0.0f, 0.0f, 0.7f));
                context.DrawText(text, badgeBounds.X + 4, badgeBounds.Y + 12, ColorValue.FromRgba(1.0f, 1.0f, 1.0f, 1.0f), fontSize: 10);
            }
        }

        foreach (var child in node.Children)
        {
            RenderAccessibilityNode(context, child);
        }
    }

    private static void RenderRepaintRegions(OverlayRenderContext context)
    {
        // Highlight rectangles that were repainted in the current frame.
        // The renderer marks dirty regions; we overlay them in semi-transparent red.
        var regions = context.GetDirtyRegions();
        var highlightColor = ColorValue.FromRgba(1.0f, 0.0f, 0.0f, 0.15f);
        foreach (var region in regions)
        {
            context.DrawFilledRect(region, highlightColor);
        }
    }

    private static void RenderFocusOrder(OverlayRenderContext context)
    {
        // Draw numbered circles showing the focus tab order.
        var focusOrder = AccessibilityPanel.GetFocusOrder();
        var badgeColor = ColorValue.FromRgba(0.2f, 0.4f, 1.0f, 0.8f);
        var textColor = ColorValue.FromRgba(1.0f, 1.0f, 1.0f, 1.0f);

        foreach (var entry in focusOrder)
        {
            float cx = entry.Bounds.X - 8;
            float cy = entry.Bounds.Y - 8;
            context.DrawCircle(cx, cy, 10, badgeColor);
            context.DrawText(entry.Order.ToString(), cx - 4, cy + 4, textColor, fontSize: 10);
        }
    }

    private static Rect ShrinkRect(Rect rect, EdgeInsets insets)
    {
        return new Rect(
            rect.X + insets.Left,
            rect.Y + insets.Top,
            rect.Width - insets.Left - insets.Right,
            rect.Height - insets.Top - insets.Bottom);
    }

    private static Rect ExpandRect(Rect rect, EdgeInsets insets)
    {
        return new Rect(
            rect.X - insets.Left,
            rect.Y - insets.Top,
            rect.Width + insets.Left + insets.Right,
            rect.Height + insets.Top + insets.Bottom);
    }
}

/// <summary>
/// Abstraction over the renderer for drawing overlay annotations.
/// The concrete implementation is provided by the platform renderer.
/// </summary>
internal abstract class OverlayRenderContext
{
    /// <summary>Draws a rectangle outline.</summary>
    public abstract void DrawRect(Rect bounds, ColorValue color, float strokeWidth);

    /// <summary>Draws a filled rectangle.</summary>
    public abstract void DrawFilledRect(Rect bounds, ColorValue color);

    /// <summary>
    /// Draws the area between two rectangles (outer minus inner).
    /// Used for padding/margin visualization.
    /// </summary>
    public abstract void DrawRectDifference(Rect outer, Rect inner, ColorValue color);

    /// <summary>Draws text at the given position.</summary>
    public abstract void DrawText(string text, float x, float y, ColorValue color, float fontSize);

    /// <summary>Draws a filled circle.</summary>
    public abstract void DrawCircle(float cx, float cy, float radius, ColorValue color);

    /// <summary>Returns the list of dirty (repainted) regions from the current frame.</summary>
    public abstract IReadOnlyList<Rect> GetDirtyRegions();
}

#endif
