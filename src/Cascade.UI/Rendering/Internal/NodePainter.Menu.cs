using System.Diagnostics;

namespace Cascade.UI;

/// <summary>
/// Paints the window's open menu (<see cref="MenuOverlay"/>): context menus, list item menus and
/// split-button dropdowns. Drawn after the whole tree in window coordinates, so it sits above
/// every control and every ScrollView's retained layer, and is never captured into one.
/// </summary>
internal sealed partial class NodePainter
{
    // Entrance fade/scale of a newly opened panel.
    private const float MenuEntranceMs = 120f;

    // Lays out item icon nodes once per opened panel (not per frame).
    private LayoutEngine? menuIconLayout;

    /// <summary>The menu to paint this frame (the window dispatcher's), or null.</summary>
    internal MenuOverlay? Menu { get; set; }

    private void PaintMenuOverlay()
    {
        var open = Menu;
        if (open is null || !open.IsOpen)
        {
            return;
        }

#if CASCADE_DEVTOOLS
        // Attribute the menu's draws for whodrew: it is not a node in the tree.
        string? previousProvenance = currentProvenanceId;
        if (DrawProvenance.CaptureEnabled)
        {
            currentProvenanceId = "ContextMenu";
            ctx.SetDrawProvenance(currentProvenanceId);
        }
#endif
        var levels = open.Levels;
        for (int i = 0; i < levels.Count; i++)
        {
            PaintMenuLevel(open, levels[i]);
        }
#if CASCADE_DEVTOOLS
        if (DrawProvenance.CaptureEnabled)
        {
            currentProvenanceId = previousProvenance;
            ctx.SetDrawProvenance(previousProvenance);
        }
#endif
    }

    private void PaintMenuLevel(MenuOverlay open, MenuLevel level)
    {
        var st = theme.Select;
        var m = open.Metrics;
        var b = level.Bounds;

        ScopeGuard opacityScope = default;
        ScopeGuard scaleScope = default;
        if (!ControlStateAnimator.ReducedMotion)
        {
            float t = Math.Clamp((float)Stopwatch.GetElapsedTime(level.OpenedAt).TotalMilliseconds / MenuEntranceMs, 0f, 1f);
            t = 1f - ((1f - t) * (1f - t));
            if (t < 0.999f)
            {
                float scale = 0.97f + (0.03f * t);
                scaleScope = ctx.PushScale(scale, scale, new Point(b.X, b.Y));
                opacityScope = ctx.PushOpacity(t);
                ControlStateAnimator.SignalActiveTransition();
            }
        }

        try
        {
            PaintShadow(st.DropdownShadow, b, st.DropdownRadius);
            ctx.DrawRect(b, st.DropdownBackground, radius: st.DropdownRadius);
            if (st.BorderWidth > 0)
            {
                ctx.DrawRect(b, stroke: new Stroke(st.BorderColor, st.BorderWidth), radius: st.DropdownRadius);
            }

            if (level.HasIcons && !level.IconsLaidOut)
            {
                LayOutMenuIcons(level, m);
            }

            float viewTop = b.Y + level.PaddingV;
            float viewBottom = b.Bottom - level.PaddingV;
            using var clip = ctx.PushClip(new Rect(b.X, viewTop, b.Width, viewBottom - viewTop));

            float textInset = m.TextInset;
            float iconX = b.X + textInset;
            float labelX = iconX + (level.HasIcons ? m.IconColumn : 0f);
            float arrowWidth = level.HasSubmenus ? m.SubmenuArrowWidth : 0f;
            float shortcutRight = b.Right - textInset - arrowWidth;
            float labelRight = level.ShortcutWidth > 0f
                ? shortcutRight - level.ShortcutWidth - m.ShortcutGap
                : shortcutRight;

            for (int i = 0; i < level.Items.Length; i++)
            {
                float y = level.ItemTop(i);
                float h = level.ItemHeights[i];
                if (y + h <= viewTop || y >= viewBottom)
                {
                    continue;
                }

                var item = level.Items[i];
                if (item.Label is null)
                {
                    float sepY = MathF.Round(y + (h / 2f));
                    ctx.DrawLine(
                        new Point(b.X + m.InsetH + 4f, sepY),
                        new Point(b.Right - m.InsetH - 4f, sepY),
                        new Stroke(st.BorderColor, 1f));
                    continue;
                }

                bool enabled = !item.Disabled;
                if (i == level.Highlighted && enabled)
                {
                    var highlight = new Rect(b.X + m.InsetH, y, b.Width - (m.InsetH * 2f), h);
                    ctx.DrawRect(highlight, st.ItemHoverBackground, radius: Math.Min(8f, st.DropdownRadius));
                }

                float alpha = enabled ? 1f : 0.4f;
                var textColor = (item.Style == MenuItemStyle.Destructive ? theme.Colors.Danger : st.TextColor).ScaleAlpha(alpha);

                if (item.Icon is { IsLayoutEmpty: false } icon)
                {
                    PaintMenuIcon(icon, iconX, y, h, m, alpha);
                }

                // A label with no shortcut may run into the (empty) shortcut column.
                float labelEnd = string.IsNullOrEmpty(item.Shortcut) ? shortcutRight : labelRight;
                PaintText(item.Label, new Rect(labelX, y, Math.Max(0f, labelEnd - labelX), h), 0f, textColor,
                    fontSize: m.FontSize, overflow: TextOverflow.Ellipsis);

                if (!string.IsNullOrEmpty(item.Shortcut))
                {
                    PaintText(item.Shortcut, new Rect(labelRight, y, Math.Max(0f, shortcutRight - labelRight), h), 0f,
                        st.TextColor.ScaleAlpha(0.5f * alpha), fontSize: m.ShortcutFontSize, alignment: TextAlignment.End);
                }

                if (item.Items is not null)
                {
                    PaintSubmenuArrow(b.Right - textInset - (arrowWidth / 2f), y + (h / 2f), textColor);
                }
            }

            if (level.MaxScroll > 0f)
            {
                PaintMenuScrollThumb(level, viewTop, viewBottom - viewTop);
            }
        }
        finally
        {
            opacityScope.Dispose();
            scaleScope.Dispose();
        }
    }

    private void LayOutMenuIcons(MenuLevel level, MenuMetrics m)
    {
        menuIconLayout ??= new LayoutEngine();
        var box = LayoutConstraints.Loose(new Size(m.IconColumn, m.ItemHeight));
        foreach (var item in level.Items)
        {
            if (item.Icon is { IsLayoutEmpty: false } icon)
            {
                menuIconLayout.Layout(icon, box);
            }
        }

        level.IconsLaidOut = true;
    }

    private void PaintMenuIcon(Node icon, float x, float itemTop, float itemHeight, MenuMetrics m, float alpha)
    {
        var size = icon.LayoutData.Bounds;
        float ix = x + MathF.Max(0f, (m.IconColumn - 4f - size.Width) / 2f);
        float iy = itemTop + ((itemHeight - size.Height) / 2f);

        float savedX = absoluteX;
        float savedY = absoluteY;
        absoluteX = ix;
        absoluteY = iy;
        ScopeGuard fade = alpha < 1f ? ctx.PushOpacity(alpha) : default;
        using (ctx.PushTranslate(ix, iy))
        {
            PaintRecursive(icon);
        }
        fade.Dispose();
        absoluteX = savedX;
        absoluteY = savedY;
    }

    private void PaintSubmenuArrow(float centerX, float centerY, ColorValue color)
    {
        const float half = 4f;
        var stroke = new Stroke(color, 1.5f);
        ctx.DrawLine(new Point(centerX - (half / 2f), centerY - half), new Point(centerX + (half / 2f), centerY), stroke);
        ctx.DrawLine(new Point(centerX + (half / 2f), centerY), new Point(centerX - (half / 2f), centerY + half), stroke);
    }

    private void PaintMenuScrollThumb(MenuLevel level, float trackTop, float trackHeight)
    {
        float content = level.ContentHeight;
        float thumbHeight = Math.Max(16f, trackHeight * (trackHeight / content));
        float thumbTop = trackTop + ((trackHeight - thumbHeight) * (level.ScrollOffset / level.MaxScroll));
        var thumb = new Rect(level.Bounds.Right - 5f, thumbTop, 3f, thumbHeight);
        ctx.DrawRect(thumb, theme.Select.TextColor.ScaleAlpha(0.3f), radius: 1.5f);
    }
}
