using System.Diagnostics;

namespace Cascade.UI;

/// <summary>What part of a <see cref="TabBar"/> a point is over.</summary>
internal enum TabStripPartKind : byte
{
    /// <summary>Nothing interactive (the strip background, a gap, a hidden tab).</summary>
    None,

    /// <summary>A tab (not its close button).</summary>
    Tab,

    /// <summary>A closable tab's close button.</summary>
    Close,

    /// <summary>The scroll-back arrow (left, or up when vertical).</summary>
    ScrollBack,

    /// <summary>The scroll-forward arrow (right, or down when vertical).</summary>
    ScrollForward,

    /// <summary>The button that opens the overflow menu.</summary>
    OverflowMenu,
}

/// <summary>A part of a tab bar: its kind and, for a tab or close button, the tab's display position.</summary>
internal readonly record struct TabStripPart(TabStripPartKind Kind, int Position)
{
    internal static readonly TabStripPart None = new(TabStripPartKind.None, -1);
}

/// <summary>
/// The measurements a tab strip is laid out with, read from the active theme's
/// <see cref="TabTheme"/>. Measure, paint, hit testing and accessibility all take them from here,
/// so they cannot disagree.
/// </summary>
internal readonly record struct TabStripMetrics(
    float Height,
    float PaddingH,
    float ItemGap,
    float IconSize,
    float IconGap,
    float CloseSize,
    float DirtyDotSize,
    float MinTabWidth,
    float MaxTabWidth,
    float ScrollButtonSize,
    float FontSize,
    string? FontPath,
    float BadgeFontSize,
    string? BadgeFontPath,
    float BadgePaddingH,
    float BadgeHeight)
{
    internal static TabStripMetrics FromTheme(CascadeTheme theme)
    {
        var t = theme.Tabs;
        var badge = theme.Badge;
        return new TabStripMetrics(
            Height:           t.Height,
            PaddingH:         t.ItemPaddingH,
            ItemGap:          t.ItemGap,
            IconSize:         t.IconSize,
            IconGap:          t.IconGap,
            CloseSize:        t.CloseButtonSize,
            DirtyDotSize:     t.DirtyDotSize,
            MinTabWidth:      t.MinTabWidth,
            MaxTabWidth:      MathF.Max(t.MinTabWidth, t.MaxTabWidth),
            ScrollButtonSize: t.ScrollButtonSize,
            FontSize:         t.TextStyle.Size,
            FontPath:         FontPathFor(t.TextStyle.Weight),
            BadgeFontSize:    badge.TextStyle.Size,
            BadgeFontPath:    FontPathFor(badge.TextStyle.Weight),
            BadgePaddingH:    badge.PaddingH,
            BadgeHeight:      badge.Height);
    }

    /// <summary>
    /// The font a tab label is measured and drawn with: the layout's semibold face for
    /// semibold-or-heavier styles, else the regular face (null when no font is loaded).
    /// </summary>
    internal static string? FontPathFor(FontWeight weight)
    {
        if (weight >= FontWeight.SemiBold && LayoutSolver.SemiBoldFontPath is { } semiBold)
        {
            return semiBold;
        }

        return LayoutSolver.DefaultFontPath;
    }
}

/// <summary>
/// Where a tab's content sits inside the tab, along the tab's horizontal content run
/// (icon, label, badge, then the trailing close/dirty slot against the far edge).
/// Offsets are relative to the tab's left edge.
/// </summary>
internal readonly record struct TabContentLayout(
    float IconX,
    float LabelX,
    float LabelWidth,
    bool LabelTruncated,
    float BadgeX,
    float BadgeWidth,
    float TrailingX);

/// <summary>
/// A tab bar's interaction and animation state. One instance follows a bar across renders (the
/// reconciler passes it from each replaced node to its replacement).
/// </summary>
internal sealed class TabBarState
{
    /// <summary>The laid-out strip; rebuilt when the size, tabs or theme change.</summary>
    internal TabStripGeometry Geometry { get; } = new();

    /// <summary>The part under the pointer.</summary>
    internal TabStripPart Hover { get; set; } = TabStripPart.None;

    /// <summary>The part the pointer pressed (until release).</summary>
    internal TabStripPart Pressed { get; set; } = TabStripPart.None;

    /// <summary>
    /// The keyboard cursor (display position) under manual activation, or -1 to follow the
    /// selected tab. Validated on every use, so a stale value after the tabs change is harmless.
    /// </summary>
    internal int FocusPosition { get; set; } = -1;

    /// <summary>The window bounds of the bar's content box, stamped by the painter (for DevTools).</summary>
    internal Rect PaintedBounds { get; set; }

    // The natural size, cached across renders: a bar re-measures only when its tabs, the theme or
    // the font change.
    internal int MeasureKey { get; set; }
    internal Size NaturalSize { get; set; }
    internal bool HasNaturalSize { get; set; }

    // Animations. Each is advanced by the painter; the bar keeps the frame loop awake only while
    // one of them is moving.
    internal AnimationChannel Scroll;
    internal AnimationChannel IndicatorStart;
    internal AnimationChannel IndicatorEnd;
    internal AnimationChannel HoverFade;
    internal bool IndicatorInitialized;
    internal int IndicatorPosition = -1;
    internal bool ScrollInitialized;
    internal int RevealedPosition = -1;
    internal int RevealedGeometryVersion = -1;
    internal int HoverFadePosition = -1;
    internal long LastAdvanceTimestamp;
    private bool animatingAtLastAdvance;

    /// <summary>The scroll offset along the strip, clamped to the current geometry.</summary>
    internal float ScrollOffset => Math.Clamp(Scroll.Current, 0f, Geometry.MaxScroll);

    /// <summary>Whether any animation is still moving.</summary>
    internal bool IsAnimating =>
        Scroll.IsAnimating || IndicatorStart.IsAnimating || IndicatorEnd.IsAnimating || HoverFade.IsAnimating;

    /// <summary>Advances every animation to now.</summary>
    internal void Advance()
    {
        long now = Stopwatch.GetTimestamp();
        if (LastAdvanceTimestamp == 0)
        {
            LastAdvanceTimestamp = now;
            return;
        }

        float dt = (float)Stopwatch.GetElapsedTime(LastAdvanceTimestamp, now).TotalSeconds;
        LastAdvanceTimestamp = now;
        if (dt > 0.1f)
        {
            dt = 0.1f;
        }

        // An animation started since the last frame (a click, a key, a new selection) begins
        // now: the idle time before it must not count as elapsed animation time.
        if (!animatingAtLastAdvance)
        {
            dt = 0f;
        }

        Scroll.Advance(dt);
        IndicatorStart.Advance(dt);
        IndicatorEnd.Advance(dt);
        HoverFade.Advance(dt);
        animatingAtLastAdvance = IsAnimating;
    }
}

/// <summary>
/// The laid-out tab strip of one <see cref="TabBar"/>, in the bar's local (content-box)
/// coordinates. Tab positions are along the strip's main axis (x when horizontal, y when
/// vertical) in strip coordinates: 0 is the start of the viewport at scroll offset 0.
/// </summary>
internal sealed class TabStripGeometry
{
    // ── Inputs (the cache key) ───────────────────────────────────────
    internal float Width;
    internal float Height;
    internal TabPosition Placement;
    internal TabOverflow Overflow;
    internal int ContentHash;
    internal int RevealPosition;
    internal int ThemeVersion;
    internal string? FontPath;
    internal bool IsValid;

    /// <summary>Bumped on every rebuild, so dependants (scroll-into-view) can tell the strip changed.</summary>
    internal int Version;

    // ── Outputs ──────────────────────────────────────────────────────
    internal TabStripMetrics Metrics;
    internal bool Vertical;
    internal int Count;
    internal float[] Start = [];
    internal float[] Extent = [];
    internal float[] TabWidth = [];
    internal float[] LabelNatural = [];
    internal float[] BadgeWidth = [];
    internal bool[] Hidden = [];

    /// <summary>Length of all visible tabs along the main axis, gaps included.</summary>
    internal float ContentExtent;

    /// <summary>The area tabs are drawn and hit-tested in (between the overflow buttons).</summary>
    internal Rect Viewport;

    internal Rect BackButton;
    internal Rect ForwardButton;
    internal Rect MenuButton;
    internal float MaxScroll;

    internal bool HasScrollButtons => BackButton.Width > 0f;

    internal bool HasMenuButton => MenuButton.Width > 0f;

    /// <summary>Number of tabs moved into the overflow menu.</summary>
    internal int HiddenCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < Count; i++)
            {
                if (Hidden[i])
                {
                    n++;
                }
            }

            return n;
        }
    }

    /// <summary>The tab's rectangle at <paramref name="scroll"/>, bar-local; empty for a tab in the overflow menu.</summary>
    internal Rect TabRect(int position, float scroll)
    {
        if (position < 0 || position >= Count || Hidden[position])
        {
            return default;
        }

        if (Vertical)
        {
            return new Rect(Viewport.X, Viewport.Y + Start[position] - scroll, Viewport.Width, Extent[position]);
        }

        return new Rect(Viewport.X + Start[position] - scroll, Viewport.Y, Extent[position], Viewport.Height);
    }

    /// <summary>The part of the tab's rectangle inside the viewport; empty when scrolled away or hidden.</summary>
    internal Rect VisibleTabRect(int position, float scroll)
    {
        var rect = TabRect(position, scroll);
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return default;
        }

        return rect.Intersect(Viewport);
    }

    /// <summary>The scroll offset that brings a tab fully into view with the least movement.</summary>
    internal float RevealOffset(int position, float scroll)
    {
        if (position < 0 || position >= Count || Hidden[position] || MaxScroll <= 0f)
        {
            return Math.Clamp(scroll, 0f, MaxScroll);
        }

        float viewport = Vertical ? Viewport.Height : Viewport.Width;
        float start = Start[position];
        float end = start + Extent[position];
        float target = scroll;
        if (start < target)
        {
            target = start;
        }
        else if (end > target + viewport)
        {
            target = end - viewport;
        }

        return Math.Clamp(target, 0f, MaxScroll);
    }

    /// <summary>
    /// The offset one step back (<paramref name="direction"/> -1) or forward (+1) from
    /// <paramref name="scroll"/>: far enough to reveal the next tab that is cut off on that side.
    /// </summary>
    internal float StepOffset(float scroll, int direction)
    {
        float viewport = Vertical ? Viewport.Height : Viewport.Width;
        if (direction > 0)
        {
            for (int i = 0; i < Count; i++)
            {
                float end = Start[i] + Extent[i];
                if (!Hidden[i] && end > scroll + viewport + 0.5f)
                {
                    return Math.Clamp(end - viewport, 0f, MaxScroll);
                }
            }

            return MaxScroll;
        }

        for (int i = Count - 1; i >= 0; i--)
        {
            if (!Hidden[i] && Start[i] < scroll - 0.5f)
            {
                return Math.Clamp(Start[i], 0f, MaxScroll);
            }
        }

        return 0f;
    }

    /// <summary>The leading and trailing edges of a tab's content (between its paddings), along the main axis in strip coordinates.</summary>
    internal (float Start, float End) IndicatorSpan(int position)
    {
        if (position < 0 || position >= Count || Hidden[position])
        {
            return (0f, 0f);
        }

        if (Vertical)
        {
            return (Start[position], Start[position] + Extent[position]);
        }

        float inset = MathF.Min(Metrics.PaddingH, Extent[position] / 4f);
        return (Start[position] + inset, Start[position] + Extent[position] - inset);
    }

    /// <summary>Lays out a tab's icon, label, badge and trailing slot within its width.</summary>
    internal TabContentLayout Content(int position, bool hasIcon, bool hasLabel, bool hasTrailing)
    {
        var m = Metrics;
        float width = TabWidth[position];
        float labelNatural = LabelNatural[position];
        float badge = BadgeWidth[position];

        float iconPart = hasIcon ? m.IconSize + (hasLabel || badge > 0f ? m.IconGap : 0f) : 0f;
        float badgePart = badge > 0f ? badge + (hasLabel ? m.IconGap : 0f) : 0f;
        float trailingPart = hasTrailing ? m.IconGap + m.CloseSize : 0f;

        float room = MathF.Max(0f, width - (m.PaddingH * 2f) - iconPart - badgePart - trailingPart);
        float label = hasLabel ? MathF.Min(labelNatural, room) : 0f;
        bool truncated = hasLabel && labelNatural > room + 0.5f;

        // Horizontal tabs centre their content when a minimum width makes them wider than it;
        // vertical tabs (all as wide as the bar) start it at the leading padding.
        float group = iconPart + label + badgePart;
        float available = MathF.Max(0f, width - (m.PaddingH * 2f) - trailingPart);
        float x = m.PaddingH + (Vertical ? 0f : MathF.Max(0f, (available - group) / 2f));

        float iconX = x;
        float labelX = x + iconPart;
        float badgeX = labelX + label + (badge > 0f && hasLabel ? m.IconGap : 0f);
        float trailingX = width - m.PaddingH - m.CloseSize;
        return new TabContentLayout(iconX, labelX, label, truncated, badgeX, badge, trailingX);
    }
}

/// <summary>
/// Measure, arrangement, hit testing and keyboard geometry for <see cref="TabBar"/>, shared by
/// the layout solver, the painter, the input dispatcher and the accessibility tree.
/// </summary>
internal static class TabStripLayout
{
    /// <summary>Extra room around a close button that still counts as hitting it.</summary>
    internal const float CloseHitSlop = 4f;

    // Average advance per character (relative to the font size) used when no font is loaded,
    // matching the layout solver's estimate for other text.
    private const float EstimatedCharWidthRatio = 0.55f;

    /// <summary>
    /// The bar's size under <paramref name="constraints"/>: a horizontal bar fills the offered
    /// width (its separator line spans the content it switches) and is one tab tall; a vertical
    /// bar is as wide as its widest tab and fills the offered height. Unbounded, it is the size
    /// of its tabs.
    /// </summary>
    internal static Size Measure(TabBar bar, LayoutConstraints constraints)
    {
        var natural = NaturalSize(bar);
        float width = natural.Width;
        float height = natural.Height;

        if (bar.IsVertical)
        {
            if (!float.IsPositiveInfinity(constraints.MaxHeight))
            {
                height = constraints.MaxHeight;
            }
        }
        else if (!float.IsPositiveInfinity(constraints.MaxWidth))
        {
            width = constraints.MaxWidth;
        }

        return new Size(constraints.ConstrainWidth(width), constraints.ConstrainHeight(height));
    }

    /// <summary>The size the tabs need with nothing cut off, cached in the bar's state.</summary>
    internal static Size NaturalSize(TabBar bar)
    {
        var theme = ThemeSwitcher.Current;
        var metrics = TabStripMetrics.FromTheme(theme);
        int key = HashCode.Combine(bar.ContentHash, bar.IsVertical, ThemeSwitcher.Version, metrics.FontPath, metrics.Height);
        var state = bar.State;
        if (state.HasNaturalSize && state.MeasureKey == key)
        {
            return state.NaturalSize;
        }

        int count = bar.Tabs.Count;
        float total = 0f;
        float widest = 0f;
        for (int i = 0; i < count; i++)
        {
            float w = Math.Clamp(NaturalTabWidth(bar, i, metrics, out _, out _), metrics.MinTabWidth, metrics.MaxTabWidth);
            total += w;
            widest = MathF.Max(widest, w);
        }

        Size size;
        if (bar.IsVertical)
        {
            size = new Size(MathF.Max(widest, metrics.MinTabWidth), (count * metrics.Height) + (MathF.Max(0, count - 1) * metrics.ItemGap));
        }
        else
        {
            size = new Size(total + (MathF.Max(0, count - 1) * metrics.ItemGap), metrics.Height);
        }

        state.MeasureKey = key;
        state.NaturalSize = size;
        state.HasNaturalSize = true;
        return size;
    }

    /// <summary>A tab's width with its whole label, before the theme's min/max width applies.</summary>
    private static float NaturalTabWidth(TabBar bar, int position, TabStripMetrics m, out float labelWidth, out float badgeWidth)
    {
        var tab = bar.Tabs[position];
        string label = bar.LabelAt(position);
        bool hasLabel = label.Length > 0;
        labelWidth = hasLabel ? MeasureText(label, m.FontSize, m.FontPath) : 0f;
        badgeWidth = tab.BadgeText is { } text
            ? MathF.Max(m.BadgeHeight, MeasureText(text, m.BadgeFontSize, m.BadgeFontPath) + (m.BadgePaddingH * 2f))
            : 0f;

        float width = m.PaddingH * 2f;
        if (tab.Icon.HasValue)
        {
            width += m.IconSize + (hasLabel || badgeWidth > 0f ? m.IconGap : 0f);
        }

        width += labelWidth;
        if (badgeWidth > 0f)
        {
            width += badgeWidth + (hasLabel ? m.IconGap : 0f);
        }

        if (tab.HasTrailingSlot)
        {
            width += m.IconGap + m.CloseSize;
        }

        return MathF.Ceiling(width);
    }

    /// <summary>
    /// Shaped single-line width of <paramref name="text"/> — the same measurement the painter's
    /// one-line text uses; falls back to an estimate when no font is loaded (headless tests).
    /// </summary>
    internal static float MeasureText(string text, float fontSize, string? fontPath)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        if (fontPath is null)
        {
            return text.Length * fontSize * EstimatedCharWidthRatio;
        }

        var options = new TextLayoutOptions
        {
            FontPath = fontPath,
            FontSize = fontSize,
            MaxLines = 1,
            NoWrap = true,
        };
        return TextLayoutEngine.Layout(text, options).BoundingBox.Width;
    }

    /// <summary>The bar's content box size (its laid-out bounds less padding).</summary>
    internal static Size ContentSize(TabBar bar)
    {
        var data = bar.LayoutData;
        return new Size(
            MathF.Max(0f, data.Bounds.Width - data.Padding.Horizontal),
            MathF.Max(0f, data.Bounds.Height - data.Padding.Vertical));
    }

    /// <summary>The display position the arrangement keeps on the bar: the keyboard cursor while it differs from the selection, else the selected tab.</summary>
    internal static int RevealPosition(TabBar bar)
    {
        int keyboard = KeyboardPosition(bar);
        return keyboard >= 0 ? keyboard : bar.SelectedPosition;
    }

    /// <summary>
    /// The tab the keyboard is on: under manual activation the cursor when it points at a valid
    /// tab, otherwise the selected tab, otherwise the first enabled tab (-1 when none).
    /// </summary>
    internal static int KeyboardPosition(TabBar bar)
    {
        int cursor = bar.State.FocusPosition;
        if (bar.ActivationMode == TabActivation.Manual && cursor >= 0 && cursor < bar.Tabs.Count)
        {
            return cursor;
        }

        int selected = bar.SelectedPosition;
        if (selected >= 0)
        {
            return selected;
        }

        return NextEnabled(bar, -1, +1, wrap: false);
    }

    /// <summary>
    /// The next enabled tab from <paramref name="from"/> in <paramref name="direction"/>;
    /// wrapping past the ends when <paramref name="wrap"/>. -1 when there is none.
    /// </summary>
    internal static int NextEnabled(TabBar bar, int from, int direction, bool wrap)
    {
        int count = bar.Tabs.Count;
        if (count == 0)
        {
            return -1;
        }

        int position = from;
        for (int step = 0; step < count; step++)
        {
            position += direction;
            if (position < 0 || position >= count)
            {
                if (!wrap)
                {
                    return -1;
                }

                position = position < 0 ? count - 1 : 0;
            }

            if (bar.IsTabEnabled(position))
            {
                return position;
            }
        }

        return -1;
    }

    /// <summary>
    /// The bar's strip laid out for its current size, tabs, overflow mode, theme and the tab
    /// that must stay on the bar; rebuilt only when one of those changed.
    /// </summary>
    internal static TabStripGeometry Ensure(TabBar bar)
    {
        var size = ContentSize(bar);
        var geometry = bar.State.Geometry;
        var metrics = TabStripMetrics.FromTheme(ThemeSwitcher.Current);
        int reveal = bar.OverflowMode == TabOverflow.Menu ? RevealPosition(bar) : -1;

        if (geometry.IsValid
            && geometry.Width == size.Width
            && geometry.Height == size.Height
            && geometry.Placement == bar.Placement
            && geometry.Overflow == bar.OverflowMode
            && geometry.ContentHash == bar.ContentHash
            && geometry.RevealPosition == reveal
            && geometry.ThemeVersion == ThemeSwitcher.Version
            && geometry.FontPath == metrics.FontPath
            && geometry.Metrics == metrics)
        {
            return geometry;
        }

        Arrange(bar, geometry, size, metrics, reveal);
        geometry.Width = size.Width;
        geometry.Height = size.Height;
        geometry.Placement = bar.Placement;
        geometry.Overflow = bar.OverflowMode;
        geometry.ContentHash = bar.ContentHash;
        geometry.RevealPosition = reveal;
        geometry.ThemeVersion = ThemeSwitcher.Version;
        geometry.FontPath = metrics.FontPath;
        geometry.IsValid = true;
        geometry.Version++;
        return geometry;
    }

    private static void Arrange(TabBar bar, TabStripGeometry g, Size size, TabStripMetrics m, int reveal)
    {
        int count = bar.Tabs.Count;
        g.Metrics = m;
        g.Vertical = bar.IsVertical;
        g.Count = count;
        if (g.Start.Length != count)
        {
            g.Start = new float[count];
            g.Extent = new float[count];
            g.TabWidth = new float[count];
            g.LabelNatural = new float[count];
            g.BadgeWidth = new float[count];
            g.Hidden = new bool[count];
        }

        float crossWidth = size.Width;
        float total = 0f;
        for (int i = 0; i < count; i++)
        {
            float natural = NaturalTabWidth(bar, i, m, out float label, out float badge);
            g.LabelNatural[i] = label;
            g.BadgeWidth[i] = badge;
            g.Hidden[i] = false;
            if (g.Vertical)
            {
                g.TabWidth[i] = crossWidth;
                g.Extent[i] = m.Height;
            }
            else
            {
                float width = Math.Clamp(natural, m.MinTabWidth, m.MaxTabWidth);
                g.TabWidth[i] = width;
                g.Extent[i] = width;
            }

            total += g.Extent[i] + (i > 0 ? m.ItemGap : 0f);
        }

        float mainSize = g.Vertical ? size.Height : size.Width;
        g.BackButton = default;
        g.ForwardButton = default;
        g.MenuButton = default;
        g.MaxScroll = 0f;
        g.Viewport = new Rect(0f, 0f, size.Width, size.Height);

        if (total > mainSize + 0.5f && count > 0)
        {
            float button = MathF.Min(m.ScrollButtonSize, mainSize / 3f);
            if (bar.OverflowMode == TabOverflow.Menu)
            {
                g.MenuButton = MainRect(g.Vertical, mainSize - button, button, size);
                g.Viewport = MainRect(g.Vertical, 0f, mainSize - button, size);
                HideOverflow(g, mainSize - button, reveal, m);
            }
            else
            {
                g.BackButton = MainRect(g.Vertical, 0f, button, size);
                g.ForwardButton = MainRect(g.Vertical, mainSize - button, button, size);
                g.Viewport = MainRect(g.Vertical, button, MathF.Max(0f, mainSize - (button * 2f)), size);
            }
        }

        // Positions along the strip; hidden (overflow-menu) tabs take no room.
        float cursor = 0f;
        bool first = true;
        for (int i = 0; i < count; i++)
        {
            if (g.Hidden[i])
            {
                g.Start[i] = 0f;
                continue;
            }

            if (!first)
            {
                cursor += m.ItemGap;
            }

            g.Start[i] = cursor;
            cursor += g.Extent[i];
            first = false;
        }

        g.ContentExtent = cursor;
        float viewportExtent = g.Vertical ? g.Viewport.Height : g.Viewport.Width;
        g.MaxScroll = bar.OverflowMode == TabOverflow.Scroll ? MathF.Max(0f, cursor - viewportExtent) : 0f;
    }

    /// <summary>
    /// Menu overflow: keeps the leading tabs that fit in <paramref name="room"/> and hides the
    /// rest, except that <paramref name="reveal"/> always stays on the bar (trailing tabs give way
    /// to it). A single tab wider than the room is narrowed to fit.
    /// </summary>
    private static void HideOverflow(TabStripGeometry g, float room, int reveal, TabStripMetrics m)
    {
        int count = g.Count;
        bool hasReveal = reveal >= 0 && reveal < count;
        float reserved = hasReveal ? g.Extent[reveal] : 0f;
        int included = hasReveal ? 1 : 0;
        float extents = reserved;
        bool full = false;

        for (int i = 0; i < count; i++)
        {
            if (i == reveal)
            {
                continue;
            }

            if (!full)
            {
                // Everything on the bar, this tab included, with a gap between each pair.
                float total = extents + g.Extent[i] + (included * m.ItemGap);
                if (total <= room + 0.5f)
                {
                    extents += g.Extent[i];
                    included++;
                    continue;
                }

                // Leading tabs stay contiguous: once one does not fit, the rest go to the menu.
                full = true;
            }

            g.Hidden[i] = true;
        }

        // Nothing fitted and nothing had to be shown: keep the first tab rather than an empty bar.
        if (included == 0 && count > 0)
        {
            g.Hidden[0] = false;
        }

        // A lone tab wider than the room is narrowed (its label ellipsizes).
        for (int i = 0; i < count; i++)
        {
            if (!g.Hidden[i] && g.Extent[i] > room)
            {
                g.Extent[i] = MathF.Max(0f, room);
                if (!g.Vertical)
                {
                    g.TabWidth[i] = g.Extent[i];
                }
            }
        }
    }

    private static Rect MainRect(bool vertical, float start, float length, Size size)
    {
        return vertical
            ? new Rect(0f, start, size.Width, length)
            : new Rect(start, 0f, length, size.Height);
    }

    /// <summary>The part of the bar at <paramref name="local"/> (bar content-box coordinates).</summary>
    internal static TabStripPart PartAt(TabBar bar, Point local)
    {
        var g = Ensure(bar);
        if (g.HasScrollButtons)
        {
            if (Within(g.BackButton, local))
            {
                return new TabStripPart(TabStripPartKind.ScrollBack, -1);
            }

            if (Within(g.ForwardButton, local))
            {
                return new TabStripPart(TabStripPartKind.ScrollForward, -1);
            }
        }

        if (g.HasMenuButton && Within(g.MenuButton, local))
        {
            return new TabStripPart(TabStripPartKind.OverflowMenu, -1);
        }

        if (!Within(g.Viewport, local))
        {
            return TabStripPart.None;
        }

        float scroll = bar.State.ScrollOffset;
        for (int i = 0; i < g.Count; i++)
        {
            var rect = g.TabRect(i, scroll);
            if (rect.Width <= 0f || !Within(rect, local))
            {
                continue;
            }

            var tab = bar.Tabs[i];
            if (tab.CloseHandler is not null && Within(CloseRect(g, i, rect).Inflate(CloseHitSlop), local))
            {
                return new TabStripPart(TabStripPartKind.Close, i);
            }

            return new TabStripPart(TabStripPartKind.Tab, i);
        }

        return TabStripPart.None;
    }

    /// <summary>A tab's close button, bar-local, given the tab's rectangle.</summary>
    internal static Rect CloseRect(TabStripGeometry g, int position, Rect tabRect)
    {
        float size = g.Metrics.CloseSize;
        float x = tabRect.X + g.TabWidth[position] - g.Metrics.PaddingH - size;
        float y = tabRect.Y + ((tabRect.Height - size) / 2f);
        return new Rect(x, y, size, size);
    }

    // Half-open on the far edges so neighbouring tabs never both claim a boundary point.
    private static bool Within(Rect rect, Point p)
    {
        return p.X >= rect.X && p.X < rect.Right && p.Y >= rect.Y && p.Y < rect.Bottom;
    }

    /// <summary>
    /// Brings the tab the bar must show (keyboard cursor or selection) into view after it or the
    /// strip changed: animated when the bar has been shown before, immediate on first layout.
    /// Also re-clamps the offset when the strip got shorter. Called before painting and before
    /// hit testing, so both see the same offset.
    /// </summary>
    internal static void UpdateScroll(TabBar bar, bool animate)
    {
        var g = Ensure(bar);
        var state = bar.State;
        int reveal = RevealPosition(bar);

        if (!state.ScrollInitialized)
        {
            state.Scroll.SnapTo(g.RevealOffset(reveal, 0f));
            state.ScrollInitialized = true;
            state.RevealedPosition = reveal;
            state.RevealedGeometryVersion = g.Version;
            return;
        }

        if (state.RevealedPosition != reveal || state.RevealedGeometryVersion != g.Version)
        {
            bool positionChanged = state.RevealedPosition != reveal;
            state.RevealedPosition = reveal;
            state.RevealedGeometryVersion = g.Version;
            float from = state.Scroll.IsAnimating ? state.Scroll.Target : state.Scroll.Current;
            float target = positionChanged ? g.RevealOffset(reveal, from) : Math.Clamp(from, 0f, g.MaxScroll);
            ScrollTo(state, target, animate && positionChanged);
            return;
        }

        if (!state.Scroll.IsAnimating && state.Scroll.Current > g.MaxScroll)
        {
            state.Scroll.SnapTo(g.MaxScroll);
        }
    }

    /// <summary>Moves the strip to <paramref name="target"/>, animated with the theme's motion unless reduced motion is on.</summary>
    internal static void ScrollTo(TabBarState state, float target, bool animate)
    {
        if (!animate || ControlStateAnimator.ReducedMotion)
        {
            state.Scroll.SnapTo(target);
            return;
        }

        state.Scroll.SetTarget(target, ThemeSwitcher.Current.Tabs.IndicatorTransition.Model);
    }

    /// <summary>The overflow menu's items: the tabs that are not on the bar, in order.</summary>
    internal static IReadOnlyList<ContextMenuItem> OverflowMenuItems(TabBar bar)
    {
        var g = Ensure(bar);
        var items = new List<ContextMenuItem>(g.HiddenCount);
        for (int i = 0; i < g.Count; i++)
        {
            if (!g.Hidden[i])
            {
                continue;
            }

            var tab = bar.Tabs[i];
            int index = tab.Index;
            var onSelect = bar.OnSelect;
            string label = bar.LabelAt(i);
            if (label.Length == 0)
            {
                label = tab.AccessibleLabelText ?? tab.Icon?.AccessibleName ?? string.Empty;
            }

            Node? icon = tab.Icon is { } glyph ? new IconView(glyph, g.Metrics.IconSize) : null;
            items.Add(ContextMenuItem.Action(
                label,
                () => { onSelect(index); },
                icon: icon,
                disabled: !bar.IsTabEnabled(i)));
        }

        return items;
    }
}
