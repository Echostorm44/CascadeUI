namespace Cascade.UI;

/// <summary>
/// Paints a <see cref="TabBar"/>: the strip, each tab (hover/press background, icon, label,
/// badge, close button or unsaved-changes dot), the sliding selected indicator, the keyboard
/// focus ring and the overflow buttons. All geometry comes from <see cref="TabStripLayout"/>,
/// the same source the layout solver, hit testing and the accessibility tree use.
/// </summary>
internal sealed partial class NodePainter
{
    // Cap-height reference for placing every label on one shared baseline (centring each label on
    // its own ink would put "Overview" and "jpg" at different heights).
    private const string TabLabelReference = "H";

    private void PaintTabBar(TabBar bar, Rect bounds)
    {
        var t = theme.Tabs;
        var state = bar.State;
        state.PaintedBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        state.Advance();
        TabStripLayout.UpdateScroll(bar, animate: true);
        var g = TabStripLayout.Ensure(bar);
        float scroll = state.ScrollOffset;

        ScopeGuard disabledScope = bar.IsDisabled ? ctx.PushOpacity(t.DisabledOpacity) : default;
        try
        {
            ctx.DrawRect(bounds, t.Background);
            UpdateTabHoverFade(bar, t);

            int selected = bar.SelectedPosition;
            bool keyboardFocus = ReferenceEquals(FocusManager.FocusedElement, bar)
                && FocusManager.LastFocusWasKeyboard
                && !bar.IsDisabled;
            int keyboardPosition = keyboardFocus ? TabStripLayout.KeyboardPosition(bar) : -1;

            using (ctx.PushClip(g.Viewport))
            {
                for (int i = 0; i < g.Count; i++)
                {
                    var rect = g.TabRect(i, scroll);
                    if (rect.Width <= 0f || !rect.Intersects(g.Viewport))
                    {
                        continue;
                    }

                    PaintTab(bar, g, i, rect, selected == i, keyboardPosition == i, t);
                }
            }

            // The overflow buttons cover tabs scrolled under them; the separator runs across the
            // whole bar above both, and the indicator sits on the separator.
            PaintTabOverflowButtons(bar, g, scroll, t);
            PaintTabSeparator(bar, bounds, t);
            using (ctx.PushClip(g.Viewport))
            {
                PaintTabIndicator(bar, g, selected, scroll, t);
            }
        }
        finally
        {
            disabledScope.Dispose();
        }

        if (state.IsAnimating)
        {
            ControlStateAnimator.SignalActiveTransition();
        }
    }

    /// <summary>The hairline between the strip and the content it switches, on the bar's inner edge.</summary>
    private void PaintTabSeparator(TabBar bar, Rect bounds, TabTheme t)
    {
        if (t.BorderWidth <= 0f)
        {
            return;
        }

        // A hairline thinner than one device pixel (Apple's 0.5) would be smeared across a pixel
        // row at half coverage and all but vanish on a 1x display: draw at least one device pixel.
        float ratio = MathF.Max(1f, ctx.PixelRatio);
        float w = MathF.Max(MathF.Round(t.BorderWidth * ratio), 1f) / ratio;

        // The line's leading edge on whole device pixels, so it covers exact rows/columns.
        var line = bar.Placement switch
        {
            TabPosition.Bottom => new Rect(0f, SnapToDevice(0f, absoluteY, ratio), bounds.Width, w),
            TabPosition.Left => new Rect(SnapToDevice(bounds.Width - w, absoluteX, ratio), 0f, w, bounds.Height),
            TabPosition.Right => new Rect(SnapToDevice(0f, absoluteX, ratio), 0f, w, bounds.Height),
            _ => new Rect(0f, SnapToDevice(bounds.Height - w, absoluteY, ratio), bounds.Width, w),
        };
        ctx.DrawRect(line, t.BorderColor);
    }

    /// <summary>A local coordinate moved to the nearest device-pixel boundary, given the node's window offset.</summary>
    private static float SnapToDevice(float local, float origin, float ratio)
    {
        return (MathF.Round((origin + local) * ratio) / ratio) - origin;
    }

    /// <summary>Fades the hovered tab's background in (and the last one out) with the theme's state transition.</summary>
    private static void UpdateTabHoverFade(TabBar bar, TabTheme t)
    {
        var state = bar.State;
        var hover = state.Hover;
        int hovered = hover.Kind is TabStripPartKind.Tab or TabStripPartKind.Close && bar.IsTabEnabled(hover.Position)
            ? hover.Position
            : -1;
        var model = t.Transition.Model;

        if (hovered >= 0)
        {
            if (hovered != state.HoverFadePosition)
            {
                state.HoverFadePosition = hovered;
                state.HoverFade.SnapTo(0f);
            }

            if (state.HoverFade.Target < 1f)
            {
                state.HoverFade.SetTarget(1f, model);
            }

            return;
        }

        if (state.HoverFade.Target > 0f)
        {
            state.HoverFade.SetTarget(0f, model);
        }
    }

    private void PaintTab(TabBar bar, TabStripGeometry g, int position, Rect rect, bool isSelected, bool hasKeyboardFocus, TabTheme t)
    {
        var tab = bar.Tabs[position];
        var state = bar.State;
        bool enabled = bar.IsTabEnabled(position);
        float hoverT = enabled && state.HoverFadePosition == position ? state.HoverFade.Current : 0f;
        bool pressed = enabled
            && state.Pressed.Kind == TabStripPartKind.Tab
            && state.Pressed.Position == position
            && state.Hover.Kind == TabStripPartKind.Tab
            && state.Hover.Position == position;

        var background = TabItemBackground(g.Vertical, g.Metrics.ItemGap, rect, t);
        if (pressed)
        {
            PaintPressedBackground(background, t);
        }
        else if (hoverT > 0.001f)
        {
            ctx.DrawRect(background, t.HoverBackground.ScaleAlpha(hoverT), radius: t.ItemRadius);
        }

        ColorValue color;
        if (!enabled)
        {
            color = t.InactiveTextColor.ScaleAlpha(t.DisabledOpacity);
        }
        else if (isSelected)
        {
            color = t.ActiveTextColor;
        }
        else if (hoverT > 0.001f)
        {
            color = ColorValue.Lerp(t.InactiveTextColor, theme.Colors.Text, hoverT);
        }
        else
        {
            color = t.InactiveTextColor;
        }

        string label = bar.LabelAt(position);
        var m = g.Metrics;
        var content = g.Content(position, tab.Icon.HasValue, label.Length > 0, tab.HasTrailingSlot);
        float centerY = rect.Y + (rect.Height / 2f);

        if (tab.Icon is { } icon)
        {
            PaintIconBitmap(icon, rect.X + content.IconX + (m.IconSize / 2f), centerY, m.IconSize, 1f, color,
                strokeWidthLogical: MathF.Max(1.5f, m.IconSize / 12f));
        }

        if (content.LabelWidth > 0f)
        {
            PaintTabLabel(label, rect.X + content.LabelX, centerY, content.LabelWidth, content.LabelTruncated, color, m);
        }

        if (content.BadgeWidth > 0f && tab.BadgeText is { } badgeText)
        {
            PaintTabBadge(badgeText, new Rect(rect.X + content.BadgeX, centerY - (m.BadgeHeight / 2f), content.BadgeWidth, m.BadgeHeight), enabled);
        }

        if (tab.HasTrailingSlot)
        {
            PaintTabTrailing(bar, g, position, rect, isSelected, hasKeyboardFocus, enabled, color, t);
        }

        if (hasKeyboardFocus)
        {
            var ring = background.Inflate(-1f);
            ctx.DrawRect(ring, stroke: new Stroke(t.FocusRingColor, t.FocusRingWidth), radius: MathF.Max(2f, t.ItemRadius));
        }
    }

    /// <summary>
    /// The pressed background: the theme's pressed colour, or the hover colour deepened with a
    /// light wash of the active text colour.
    /// </summary>
    private void PaintPressedBackground(Rect rect, TabTheme t)
    {
        if (t.PressedBackground is { } pressed)
        {
            ctx.DrawRect(rect, pressed, radius: t.ItemRadius);
            return;
        }

        ctx.DrawRect(rect, t.HoverBackground, radius: t.ItemRadius);
        ctx.DrawRect(rect, t.ActiveTextColor.ScaleAlpha(0.08f), radius: t.ItemRadius);
    }

    /// <summary>A tab's hover/pressed background: the tab inset across the strip by the theme's item inset.</summary>
    private static Rect TabItemBackground(bool vertical, float itemGap, Rect rect, TabTheme t)
    {
        float inset = t.ItemInset;
        float along = itemGap > 0f ? 0f : MathF.Min(inset / 2f, 2f);
        return vertical
            ? new Rect(rect.X + inset, rect.Y + along, MathF.Max(0f, rect.Width - (inset * 2f)), MathF.Max(0f, rect.Height - (along * 2f)))
            : new Rect(rect.X + along, rect.Y + inset, MathF.Max(0f, rect.Width - (along * 2f)), MathF.Max(0f, rect.Height - (inset * 2f)));
    }

    private void PaintTabLabel(string label, float x, float centerY, float width, bool truncated, ColorValue color, TabStripMetrics m)
    {
        float lineTop;
        var visual = ctx.MeasureGlyphVisualBounds(TabLabelReference, m.FontSize, m.FontPath);
        if (visual.HasValue)
        {
            lineTop = centerY - visual.Value.VisualCenterY;
        }
        else
        {
            lineTop = centerY - (ctx.MeasureText(TabLabelReference, m.FontSize, m.FontPath).Height / 2f);
        }

        // One unbroken line, ellipsized when the tab is narrower than the label. The measured width
        // gets half a pixel of slack so a label that exactly fits is never cut by rounding.
        ctx.DrawText(label, MathF.Round(x), MathF.Round(lineTop), m.FontSize, color,
            fontPath: m.FontPath,
            overflow: truncated ? TextOverflow.Ellipsis : TextOverflow.Clip,
            maxWidth: width + (truncated ? 0f : 0.5f),
            maxLines: 1,
            noWrap: true);
    }

    private void PaintTabBadge(string text, Rect pill, bool enabled)
    {
        var b = theme.Badge;
        float radius = MathF.Min(b.Radius, pill.Height / 2f);
        var background = enabled ? b.Background : b.Background.ScaleAlpha(theme.Tabs.DisabledOpacity);
        var foreground = enabled ? b.TextColor : b.TextColor.ScaleAlpha(theme.Tabs.DisabledOpacity);
        ctx.DrawRect(pill, background, radius: radius);
        PaintText(text, pill, 0f, foreground,
            fontSize: b.TextStyle.Size,
            alignment: TextAlignment.Center,
            fontWeight: b.TextStyle.Weight);
    }

    /// <summary>
    /// The trailing slot: the close button on the selected, hovered or keyboard-focused closable
    /// tab, otherwise the unsaved-changes dot when the tab is dirty.
    /// </summary>
    private void PaintTabTrailing(
        TabBar bar, TabStripGeometry g, int position, Rect rect, bool isSelected, bool hasKeyboardFocus,
        bool enabled, ColorValue color, TabTheme t)
    {
        var tab = bar.Tabs[position];
        var state = bar.State;
        var slot = TabStripLayout.CloseRect(g, position, rect);
        bool tabHovered = state.Hover.Kind is TabStripPartKind.Tab or TabStripPartKind.Close
            && state.Hover.Position == position;
        bool showClose = tab.CloseHandler is not null && enabled && (isSelected || tabHovered || hasKeyboardFocus);

        if (showClose)
        {
            bool closeHovered = state.Hover.Kind == TabStripPartKind.Close && state.Hover.Position == position;
            bool closePressed = closeHovered
                && state.Pressed.Kind == TabStripPartKind.Close
                && state.Pressed.Position == position;
            if (closeHovered)
            {
                // A wash of the label colour over the tab: visible on any tab background.
                var area = slot.Inflate(2f);
                ctx.DrawRect(area, color.ScaleAlpha(closePressed ? 0.24f : 0.14f), radius: area.Width / 2f);
            }

            float arm = slot.Width * 0.28f;
            var c = slot.Center;
            var stroke = new Stroke(color, 1.5f, StrokeCap.Round, StrokeJoin.Round);
            ctx.DrawLine(new Point(c.X - arm, c.Y - arm), new Point(c.X + arm, c.Y + arm), stroke);
            ctx.DrawLine(new Point(c.X + arm, c.Y - arm), new Point(c.X - arm, c.Y + arm), stroke);
            return;
        }

        if (tab.IsDirty)
        {
            float r = g.Metrics.DirtyDotSize / 2f;
            ctx.DrawCircle(slot.Center, r, fill: color);
        }
    }

    /// <summary>
    /// The selected indicator: a bar along the strip's inner edge under the selected tab's content.
    /// It slides between tabs with the theme's indicator transition when the selection moves, and
    /// jumps (no slide) when only the layout moved it — a resize or a tab added before it.
    /// </summary>
    private void PaintTabIndicator(TabBar bar, TabStripGeometry g, int selected, float scroll, TabTheme t)
    {
        var state = bar.State;
        if (selected < 0 || selected >= g.Count || g.Hidden[selected] || t.IndicatorHeight <= 0f)
        {
            state.IndicatorInitialized = false;
            state.IndicatorPosition = -1;
            return;
        }

        var (start, end) = g.IndicatorSpan(selected);
        if (!state.IndicatorInitialized)
        {
            state.IndicatorStart.SnapTo(start);
            state.IndicatorEnd.SnapTo(end);
            state.IndicatorInitialized = true;
        }
        else if (state.IndicatorPosition != selected && !ControlStateAnimator.ReducedMotion)
        {
            var model = t.IndicatorTransition.Model;
            state.IndicatorStart.SetTarget(start, model);
            state.IndicatorEnd.SetTarget(end, model);
        }
        else if (MathF.Abs(state.IndicatorStart.Target - start) > 0.01f || MathF.Abs(state.IndicatorEnd.Target - end) > 0.01f)
        {
            // Same tab, moved by layout: follow it without a slide.
            state.IndicatorStart.SnapTo(start);
            state.IndicatorEnd.SnapTo(end);
        }

        state.IndicatorPosition = selected;

        float from = state.IndicatorStart.Current - scroll;
        float to = state.IndicatorEnd.Current - scroll;
        float thickness = t.IndicatorHeight;
        float radius = MathF.Min(t.IndicatorRadius, thickness / 2f);
        var vp = g.Viewport;
        var rect = bar.Placement switch
        {
            TabPosition.Bottom => new Rect(vp.X + from, vp.Y, to - from, thickness),
            TabPosition.Left => new Rect(vp.Right - thickness, vp.Y + from, thickness, to - from),
            TabPosition.Right => new Rect(vp.X, vp.Y + from, thickness, to - from),
            _ => new Rect(vp.X + from, vp.Bottom - thickness, to - from, thickness),
        };

        if (rect.Width > 0f && rect.Height > 0f)
        {
            ctx.DrawRect(rect, t.IndicatorColor, radius: radius);
        }
    }

    private void PaintTabOverflowButtons(TabBar bar, TabStripGeometry g, float scroll, TabTheme t)
    {
        if (g.HasScrollButtons)
        {
            bool canBack = scroll > 0.5f;
            bool canForward = scroll < g.MaxScroll - 0.5f;
            PaintTabStripButton(bar, g, g.BackButton, TabStripPartKind.ScrollBack, canBack, active: false, t);
            PaintChevron(g.BackButton.Center, g.Vertical ? ChevronDirection.Up : ChevronDirection.Left,
                TabButtonColor(canBack && !bar.IsDisabled, t));
            PaintTabStripButton(bar, g, g.ForwardButton, TabStripPartKind.ScrollForward, canForward, active: false, t);
            PaintChevron(g.ForwardButton.Center, g.Vertical ? ChevronDirection.Down : ChevronDirection.Right,
                TabButtonColor(canForward && !bar.IsDisabled, t));
        }

        if (g.HasMenuButton)
        {
            bool open = Menu is { IsOpen: true } menu && ReferenceEquals(menu.Owner, bar);
            PaintTabStripButton(bar, g, g.MenuButton, TabStripPartKind.OverflowMenu, enabled: true, active: open, t);
            PaintChevron(g.MenuButton.Center, ChevronDirection.Down, TabButtonColor(!bar.IsDisabled, t));
        }
    }

    private ColorValue TabButtonColor(bool enabled, TabTheme t)
    {
        return enabled ? theme.Colors.Text : t.InactiveTextColor.ScaleAlpha(t.DisabledOpacity);
    }

    /// <summary>An overflow button's background: the strip colour (it covers tabs scrolled under it), plus hover/press/open feedback.</summary>
    private void PaintTabStripButton(TabBar bar, TabStripGeometry g, Rect rect, TabStripPartKind kind, bool enabled, bool active, TabTheme t)
    {
        ctx.DrawRect(rect, t.Background);
        var state = bar.State;
        bool hovered = enabled && !bar.IsDisabled && state.Hover.Kind == kind;
        bool pressed = hovered && state.Pressed.Kind == kind;
        if (!hovered && !active)
        {
            return;
        }

        var item = TabItemBackground(g.Vertical, itemGap: 0f, rect, t);
        if (pressed || active)
        {
            PaintPressedBackground(item, t);
            return;
        }

        ctx.DrawRect(item, t.HoverBackground, radius: t.ItemRadius);
    }

    private enum ChevronDirection
    {
        Left,
        Right,
        Up,
        Down,
    }

    private void PaintChevron(Point center, ChevronDirection direction, ColorValue color)
    {
        const float half = 4f;
        const float depth = 2.5f;
        var stroke = new Stroke(color, 1.5f, StrokeCap.Round, StrokeJoin.Round);
        Point a;
        Point tip;
        Point b;
        switch (direction)
        {
            case ChevronDirection.Left:
                a = new Point(center.X + depth, center.Y - half);
                tip = new Point(center.X - depth, center.Y);
                b = new Point(center.X + depth, center.Y + half);
                break;
            case ChevronDirection.Right:
                a = new Point(center.X - depth, center.Y - half);
                tip = new Point(center.X + depth, center.Y);
                b = new Point(center.X - depth, center.Y + half);
                break;
            case ChevronDirection.Up:
                a = new Point(center.X - half, center.Y + depth);
                tip = new Point(center.X, center.Y - depth);
                b = new Point(center.X + half, center.Y + depth);
                break;
            default:
                a = new Point(center.X - half, center.Y - depth);
                tip = new Point(center.X, center.Y + depth);
                b = new Point(center.X + half, center.Y - depth);
                break;
        }

        ctx.DrawLine(a, tip, stroke);
        ctx.DrawLine(tip, b, stroke);
    }
}
