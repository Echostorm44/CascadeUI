namespace Cascade.UI;

/// <summary>
/// Builds what a <see cref="NavigationBar"/> shows from ordinary nodes: in title-bar mode a row
/// with the back button, leading content, the title, search and trailing content (and a large
/// title under it); in section-switching mode its <see cref="NavItem"/>s laid out as a sidebar
/// (collapsible), a rail, a bottom bar or top tabs. Each NavItem is the clickable, focusable unit;
/// the bar paints its background (solid, translucent with a backdrop blur, none, or a brush).
/// </summary>
internal static class NavigationBarBuilder
{
    internal static Node Build(NavigationBar bar, CascadeTheme theme)
    {
        return bar.Mode == NavBarMode.TitleBar ? TitleBar(bar, theme) : Sections(bar, theme);
    }

    private static float BarHeight(NavigationBar bar, NavBarTheme t) => bar.Height == NavBarHeight.Compact ? Math.Max(32f, t.Height - 12f) : t.Height;

    private static Node TitleBar(NavigationBar bar, CascadeTheme theme)
    {
        var t = theme.NavBar;
        var colors = theme.Colors;
        var row = new List<Node>();

        bool back = bar.ShowBack ?? Navigation.CurrentNavigator?.CanGoBack == true;
        if (back)
        {
            row.Add(new Button(bar.BackLabel ?? "Back", () => Navigation.Pop(), Icons.ChevronLeft).Variant("ghost"));
        }
        if (!bar.Leading.IsLayoutEmpty)
        {
            row.Add(bar.Leading);
        }

        if (!bar.LargeTitle && !bar.IsSearchExpanded)
        {
            var title = new Label(bar.Title).Style(t.TitleStyle).Color(t.TitleColor).MaxLines(1).Overflow(TextOverflow.Ellipsis);
            row.Add(bar.Subtitle is { } subtitle
                ? new Column(spacing: 0f, children: [title, new Label(subtitle).FontSize(12f).Color(colors.TextMuted).MaxLines(1).Overflow(TextOverflow.Ellipsis)]).Expand()
                : title.Expand());
        }
        else
        {
            row.Add(new Spacer());
        }

        if (bar.Search is { } search)
        {
            if (bar.IsSearchExpanded)
            {
                var field = new TextInput(new Bindable<string>(bar.SearchText, text =>
                {
                    bar.SearchText = text;
                    search.OnChanged?.Invoke(text);
                }), search.Placeholder ?? "Search").AutoFocus().Expand();
                row[^1] = new KeyHandler(field,
                    new KeyBinding(Hotkey.From(ModifierKeys.None, Key.Enter), () => search.OnSubmit?.Invoke(bar.SearchText)),
                    new KeyBinding(Hotkey.From(ModifierKeys.None, Key.Escape), () => bar.SetSearchExpanded(false))).Expand();
                row.Add(new Button("Cancel", () => bar.SetSearchExpanded(false)).Variant("ghost"));
            }
            else
            {
                row.Add(new IconButton(Icons.Search, () => bar.SetSearchExpanded(true)).AccessibleLabel("Search"));
            }
        }
        if (!bar.Trailing.IsLayoutEmpty)
        {
            row.Add(bar.Trailing);
        }

        Node content = new Row(spacing: 8f, crossAxisAlignment: CrossAxisAlignment.Center, children: [.. row])
            .Height(BarHeight(bar, t)).Padding(12f, 0f);
        if (bar.LargeTitle && !bar.IsSearchExpanded)
        {
            var large = new List<Node> { content, new Label(bar.Title).FontSize(30f).Bold().Color(t.TitleColor).Padding(16f, 0f) };
            if (bar.Subtitle is { } subtitle)
            {
                large.Add(new Label(subtitle).FontSize(13f).Color(colors.TextMuted).Padding(16f, 0f));
            }
            content = new Column(spacing: 2f, children: [.. large]).Padding(new EdgeInsets(0f, 0f, 0f, 8f));
        }
        return bar.BottomBorder
            ? new Column(children: [content, new Separator(thickness: t.BorderWidth > 0f ? t.BorderWidth : 1f, color: t.BorderBottom)])
            : content;
    }

    private static Node Sections(NavigationBar bar, CascadeTheme theme)
    {
        var t = theme.NavBar;
        string? active = bar.ActiveId;
        bool collapsed = bar.Style == NavBarStyle.Sidebar && bar.IsCollapsedNow;
        foreach (var item in bar.Items)
        {
            item.Owner = bar;
            item.IsActive = item.Id == active;
            item.BarStyle = bar.Style;
            item.Compact = collapsed;
            item.Content = ItemContent(item, bar.Style, collapsed, theme);
        }
        Node[] items = [.. bar.Items];

        switch (bar.Style)
        {
            case NavBarStyle.Rail:
                return new Column(spacing: 4f, crossAxisAlignment: CrossAxisAlignment.Stretch, children: items).Width(t.RailWidth).Padding(6f, 8f);
            case NavBarStyle.Bottom:
            {
                var row = new Row(crossAxisAlignment: CrossAxisAlignment.Stretch, children: [.. items.Select(i => (Node)i.Expand())]).Height(t.BottomHeight);
                return new Column(children: [new Separator(color: t.Border), row]);
            }
            case NavBarStyle.Top:
            {
                // Tabs that do not all fit scroll sideways rather than being cut off.
                Node row = new ScrollView(new Row(spacing: 4f, crossAxisAlignment: CrossAxisAlignment.Stretch, children: items).Height(BarHeight(bar, t)).Padding(8f, 0f))
                    .Direction(ScrollDirection.Horizontal).Scrollbar(ScrollbarMode.Hidden);
                return bar.BottomBorder ? new Column(children: [row, new Separator(color: t.Border)]) : row;
            }
            default:
            {
                float width = bar.IsCollapsible ? (collapsed ? bar.CollapseWidth : bar.ExpandWidth) : t.Width;
                var children = new List<Node>();
                Node? toggle = bar.IsCollapsible && bar.ShowCollapseButton
                    ? new IconButton(collapsed ? Icons.PanelLeftOpen : Icons.PanelLeftClose, bar.ToggleCollapsed)
                        .AccessibleLabel(collapsed ? "Expand sidebar" : "Collapse sidebar")
                    : null;
                if (toggle is not null && bar.CollapseButtonPos == CollapseButtonPosition.Top)
                {
                    children.Add(new Row(mainAxisAlignment: collapsed ? MainAxisAlignment.Center : MainAxisAlignment.End, children: [toggle]));
                }
                children.AddRange(items);
                if (toggle is not null && bar.CollapseButtonPos != CollapseButtonPosition.Top)
                {
                    children.Add(new Spacer());
                    children.Add(new Row(mainAxisAlignment: collapsed ? MainAxisAlignment.Center : MainAxisAlignment.End, children: [toggle]));
                }
                return new Column(spacing: 2f, crossAxisAlignment: CrossAxisAlignment.Stretch, children: [.. children]).Width(width).Padding(8f);
            }
        }
    }

    // An item's icon, label and badge, arranged for the style; the item paints its own highlight.
    private static Node ItemContent(NavItem item, NavBarStyle style, bool collapsed, CascadeTheme theme)
    {
        var t = theme.NavBar;
        var textColor = !item.Enabled ? theme.Colors.TextMuted : item.IsActive ? t.ItemActiveText : t.ItemText;
        if (item.Icon is IconView view)
        {
            view.ColorOverride = !item.Enabled ? theme.Colors.TextMuted : item.IsActive ? t.ItemActiveIcon : t.ItemIcon;
        }
        Node? icon = item.Icon.IsLayoutEmpty ? null : item.Icon;
        Node? badge = item.Badge > 0
            ? new Label(item.Badge > 99 ? "99+" : item.Badge.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .FontSize(10f).Bold().Color(theme.Colors.TextOnPrimary).Padding(5f, 1f).Background(theme.Colors.Danger).CornerRadius(8f)
                .AccessibleLabel($"{item.Badge} new")
            : null;

        switch (style)
        {
            case NavBarStyle.Rail:
            {
                // A rail shows icons; the label is the item's accessible name.
                var parts = new List<Node>();
                if (icon is not null)
                {
                    parts.Add(icon);
                }
                else if (item.Label is { Length: > 0 } initial)
                {
                    parts.Add(new Label(initial[..1].ToUpperInvariant()).Bold().Color(textColor));
                }
                if (badge is not null)
                {
                    parts.Add(badge);
                }
                return new Row(spacing: 2f, mainAxisAlignment: MainAxisAlignment.Center, crossAxisAlignment: CrossAxisAlignment.Center, children: [.. parts]).Height(56f);
            }
            case NavBarStyle.Bottom:
            {
                var top = new List<Node>();
                if (icon is not null)
                {
                    top.Add(icon);
                }
                if (badge is not null)
                {
                    top.Add(badge);
                }
                var parts = new List<Node> { new Row(spacing: 2f, crossAxisAlignment: CrossAxisAlignment.Center, children: [.. top]) };
                if (item.Label is { } small)
                {
                    parts.Add(new Label(small).FontSize(11f).Color(textColor).MaxLines(1).Overflow(TextOverflow.Ellipsis));
                }
                return new Column(spacing: 3f, mainAxisAlignment: MainAxisAlignment.Center, crossAxisAlignment: CrossAxisAlignment.Center, children: [.. parts])
                    .Height(t.BottomHeight);
            }
            case NavBarStyle.Top:
            {
                var parts = new List<Node>();
                if (icon is not null)
                {
                    parts.Add(icon);
                }
                if (item.Label is { } label)
                {
                    parts.Add(new Label(label).Color(textColor).FontWeight(item.IsActive ? FontWeight.SemiBold : FontWeight.Regular));
                }
                if (badge is not null)
                {
                    parts.Add(badge);
                }
                return new Row(spacing: 6f, crossAxisAlignment: CrossAxisAlignment.Center, children: [.. parts]).Padding(12f, 0f);
            }
            default:
            {
                if (collapsed)
                {
                    var compact = new List<Node>();
                    if (icon is not null)
                    {
                        compact.Add(icon);
                    }
                    else if (item.Label is { Length: > 0 } initial)
                    {
                        compact.Add(new Label(initial[..1].ToUpperInvariant()).Bold().Color(textColor));
                    }
                    if (badge is not null)
                    {
                        compact.Add(badge);
                    }
                    return new Row(spacing: 2f, mainAxisAlignment: MainAxisAlignment.Center, crossAxisAlignment: CrossAxisAlignment.Center, children: [.. compact])
                        .Height(t.ItemHeight);
                }
                var parts = new List<Node>();
                if (icon is not null)
                {
                    parts.Add(icon);
                }
                parts.Add(new Label(item.Label ?? item.Id).Color(textColor).FontWeight(item.IsActive ? FontWeight.SemiBold : FontWeight.Regular)
                    .MaxLines(1).Overflow(TextOverflow.Ellipsis).Expand());
                if (badge is not null)
                {
                    parts.Add(badge);
                }
                return new Row(spacing: t.IconLabelGap, crossAxisAlignment: CrossAxisAlignment.Center, children: [.. parts])
                    .Height(t.ItemHeight).Padding(t.ItemPaddingH, 0f);
            }
        }
    }
}
