namespace Cascade.UI;

/// <summary>
/// Internal per-node storage for layout modifiers and computed layout results.
/// Populated by <see cref="LayoutModifiers"/> and consumed by the layout solver.
/// </summary>
internal sealed class LayoutNodeData
{
    // ── Sizing modifiers ──────────────────────────────────────────────

    internal float? ExplicitWidth;
    internal float? ExplicitHeight;
    internal float? MinWidthMod;
    internal float? MaxWidthMod;
    internal float? MinHeightMod;
    internal float? MaxHeightMod;
    internal float? AspectRatio;

    // ── Flex ──────────────────────────────────────────────────────────

    internal float GrowFactor;

    // ── Spacing ──────────────────────────────────────────────────────

    internal EdgeInsets Padding;
    internal EdgeInsets Margin;

    // ── Alignment ────────────────────────────────────────────────────

    internal Alignment? NodeAlignment;

    // ── Visibility ───────────────────────────────────────────────────

    internal bool IsVisible = true;
    internal bool ClipContent;

    // ── Visual-only modifiers (do not affect layout) ─────────────────

    internal ColorValue? BackgroundColor;
    internal float Opacity = 1f;
    internal float? CornerRadiusValue;
    internal float TranslateX;
    internal float TranslateY;
    internal float Scale = 1f;
    internal Angle Rotation;

    // ── Border (visual-only — does not affect layout) ────────────────

    internal ColorValue? BorderColor;
    internal Gradient? BorderGradient;
    internal float BorderWidth;
    internal float BorderRadiusValue;
    internal ColorValue? BorderTopColor;
    internal ColorValue? BorderBottomColor;
    internal ColorValue? BorderLeftColor;
    internal ColorValue? BorderRightColor;
    internal float? BorderTopWidth;
    internal float? BorderBottomWidth;
    internal float? BorderLeftWidth;
    internal float? BorderRightWidth;

    // ── Container override ───────────────────────────────────────────

    internal float? SpacingOverride;

    // ── Computed layout results ──────────────────────────────────────

    internal Rect Bounds;
    internal Size MeasuredSize;

    /// <summary>
    /// Distance from the top of the node to its first text baseline (including padding).
    /// NaN means "no baseline available" — the node has no text content.
    /// Used by <see cref="CrossAxisAlignment.Baseline"/> in Row layout.
    /// </summary>
    internal float FirstBaseline = float.NaN;

    // ── Scroll modifiers ─────────────────────────────────────────────

    internal ScrollNodeData? ScrollData;

    // ── Interaction modifiers ────────────────────────────────────────

    internal DragNodeData? DragData;
    internal FocusNodeData? FocusData;
    internal GestureNodeData? GestureData;

    // ── SplitView modifiers ──────────────────────────────────────────

    internal SplitViewNodeData? SplitData;

    // ── TreeView modifiers ───────────────────────────────────────────

    internal TreeViewNodeData? TreeData;

    // ── Expander modifiers ───────────────────────────────────────────

    internal ExpanderNodeData? ExpanderData;

    // ── Dirty tracking ───────────────────────────────────────────────

    internal int LayoutVersion;
    internal int LastConstraintHash;

    // ── Accessibility modifiers ──────────────────────────────────────

    internal AccessibleRole A11yRole;
    internal string? A11yLabel;
    internal string? A11yDescription;
    internal LiveRegionMode A11yLiveRegion;
    internal int A11yTabIndex;
    internal bool A11yFocusable;
    internal bool A11yDisabled;
    internal Dictionary<string, string>? A11yState;
}
