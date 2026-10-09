namespace Cascade.UI;

/// <summary>
/// The framework component every overlay's content is mounted under. It renders the parts of
/// the panel that are nodes — the optional title and the room for a sheet's drag handle — and
/// carries the panel's accessibility role and name. The surface (background, border, shadow),
/// the backdrop and the drag-handle pill are painted around it by the overlay painter, so they
/// animate with the panel without being part of the content's layout.
/// </summary>
internal sealed class OverlayChrome : Component
{
    /// <summary>Height reserved at the top of a bottom sheet for its drag handle (and the area it can be dragged by).</summary>
    internal const float SheetHandleArea = 20f;

    private readonly OverlayEntry entry;

    internal OverlayChrome(OverlayEntry entry)
    {
        this.entry = entry;
        OverlayEntry = entry;
    }

    protected override Node Render()
    {
        var dialog = ThemeSwitcher.Current.Dialog;

        // A content-sized panel hugs its content; a fixed-width one (and a sheet) stretches it
        // to the panel width. A stretching column under loose constraints would grow to the
        // whole window.
        var cross = StretchesContent(entry) ? CrossAxisAlignment.Stretch : CrossAxisAlignment.Start;
        Column panel = entry.Title is { Length: > 0 } title
            ? new Column(spacing: 0, crossAxisAlignment: cross, children:
            [
                new Label(title)
                    .Style(dialog.TitleStyle)
                    .Color(dialog.TitleColor)
                    .Padding(EdgeInsets.Only(top: dialog.PaddingV, right: dialog.PaddingH, left: dialog.PaddingH)),
                entry.Content,
            ])
            : new Column(spacing: 0, crossAxisAlignment: cross, children: [entry.Content]);

        if (entry.Kind == OverlayKind.Sheet)
        {
            panel = panel.Padding(EdgeInsets.Only(top: SheetHandleArea));
        }

        panel = panel.AccessibleRole(entry.Role);
        if (entry.AccessibleLabel is { Length: > 0 } label)
        {
            panel = panel.AccessibleLabel(label);
        }

        return panel;
    }

    private static bool StretchesContent(OverlayEntry entry)
    {
        return entry.Kind == OverlayKind.Sheet
            || (entry.Kind == OverlayKind.Dialog && entry.Size.Kind != DialogSizeKind.Auto);
    }
}
