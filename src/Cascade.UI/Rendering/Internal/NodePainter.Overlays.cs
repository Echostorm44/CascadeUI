namespace Cascade.UI;

/// <summary>
/// Paints the window's overlay layer (<see cref="OverlayManager"/>): dialogs, bottom sheets and
/// popovers, bottom to top, after the page and its own popups and before the context menu and
/// toasts. Each overlay is its backdrop, its surface (shadow, background, hairline border, a
/// sheet's drag handle) and its content tree, all under the overlay's open/close transform.
/// </summary>
/// <remarks>
/// The content is painted with <see cref="PaintRecursive"/> from the panel's window position,
/// so ScrollViews inside a dialog keep their retained layers and a dropdown opened inside it
/// is drawn (by <see cref="PaintDeferredOverlays"/>) above the dialog it belongs to. Nothing
/// is captured into a page ScrollView's layer, and the presenter composites in paint order,
/// so an overlay stays above every cached layer.
/// </remarks>
internal sealed partial class NodePainter
{
    private const float SheetHandleWidth = 36f;
    private const float SheetHandleHeight = 5f;
    private const float SheetHandleTop = 8f;

    /// <summary>The overlays to paint this frame (the window dispatcher's), or null.</summary>
    internal OverlayManager? Overlays { get; set; }

    private void PaintOverlayLayer()
    {
        var overlays = Overlays;
        if (overlays is null || !overlays.HasEntries)
        {
            return;
        }

        var entries = overlays.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            PaintOverlay(entries[i]);
        }
    }

    private void PaintOverlay(OverlayEntry entry)
    {
        float visibility = entry.Visibility;
        if (visibility <= 0f && entry.IsClosing)
        {
            return;
        }

        var dialog = theme.Dialog;
        var viewport = new Rect(0f, 0f, ViewportLogicalWidth, ViewportLogicalHeight);

#if CASCADE_DEVTOOLS
        string? previousProvenance = currentProvenanceId;
        if (DrawProvenance.CaptureEnabled)
        {
            currentProvenanceId = OverlayProvenanceId(entry);
            ctx.SetDrawProvenance(currentProvenanceId);
        }
#endif

        try
        {
            if (entry.ShowBackdrop)
            {
                var backdrop = entry.BackdropOpacity is { } opacity
                    ? dialog.BackdropColor.Opacity(opacity)
                    : dialog.BackdropColor;
                float fade = visibility;
                if (entry.Kind == OverlayKind.Sheet && entry.PanelBounds.Height > 0f)
                {
                    // Dragging a sheet down lifts the dim with it.
                    fade *= 1f - Math.Clamp(entry.DragOffset.Current / entry.PanelBounds.Height, 0f, 1f);
                }

                ctx.DrawRect(viewport, backdrop.ScaleAlpha(fade));
            }

            if (!entry.IsLaidOut || entry.Tree is not { } tree)
            {
                return;
            }

            PaintOverlayPanel(entry, tree, visibility);
        }
        finally
        {
#if CASCADE_DEVTOOLS
            if (DrawProvenance.CaptureEnabled)
            {
                currentProvenanceId = previousProvenance;
                ctx.SetDrawProvenance(previousProvenance);
            }
#endif
        }

        // A dropdown or calendar opened inside the overlay: above the overlay it belongs to.
        PaintDeferredOverlays();
    }

    private void PaintOverlayPanel(OverlayEntry entry, Node tree, float visibility)
    {
        var dialog = theme.Dialog;
        var select = theme.Select;
        var panel = entry.PanelBounds;
        bool isSheet = entry.Kind == OverlayKind.Sheet;
        bool isPopover = entry.Kind == OverlayKind.Popover;
        bool fullScreen = !isPopover && entry.Size.Kind == DialogSizeKind.FullScreen;

        float radius = fullScreen ? 0f : isPopover ? select.DropdownRadius : dialog.Radius;
        var background = isPopover ? select.DropdownBackground : dialog.Background;
        var shadow = isPopover ? select.DropdownShadow : dialog.Shadow;

        // A sheet's surface runs past the window's bottom edge so only its top corners round.
        var surface = isSheet ? new Rect(panel.X, panel.Y, panel.Width, panel.Height + radius) : panel;

        float progress = entry.Presence.Current;
        float dy = isSheet ? Math.Max(0f, entry.DragOffset.Current) : 0f;
        float opacity = visibility;
        float scale = 1f;
        switch (entry.Animation)
        {
            case DialogAnimationKind.Scale:
                scale = dialog.EnterScale + ((1f - dialog.EnterScale) * progress);
                break;

            case DialogAnimationKind.SlideUp:
                dy += (1f - progress) * (isSheet ? surface.Height : OverlayManager.SlideDistance);
                if (isSheet)
                {
                    opacity = 1f;
                }
                break;

            case DialogAnimationKind.SlideDown:
                dy -= (1f - progress) * OverlayManager.SlideDistance;
                break;

            case DialogAnimationKind.None:
                opacity = 1f;
                break;
        }

        if (opacity <= 0f)
        {
            return;
        }

        ScopeGuard translateScope = default;
        ScopeGuard scaleScope = default;
        ScopeGuard opacityScope = default;
        try
        {
            if (dy != 0f)
            {
                translateScope = ctx.PushTranslate(0f, dy);
            }

            if (MathF.Abs(scale - 1f) > 0.0005f)
            {
                scaleScope = ctx.PushScale(scale, scale, ScaleOrigin(entry, panel));
            }

            if (opacity < 0.999f)
            {
                opacityScope = ctx.PushOpacity(opacity);
            }

            if (!fullScreen)
            {
                PaintShadow(shadow, surface, radius);
            }

            ctx.DrawRect(surface, background, radius: radius);
            if (!fullScreen && select.BorderWidth > 0f)
            {
                ctx.DrawRect(surface, stroke: new Stroke(select.BorderColor, 1f / MathF.Max(1f, ctx.PixelRatio)), radius: radius);
            }

            if (isSheet)
            {
                ctx.DrawRect(
                    new Rect(panel.X + ((panel.Width - SheetHandleWidth) / 2f), panel.Y + SheetHandleTop, SheetHandleWidth, SheetHandleHeight),
                    theme.Colors.TextMuted.ScaleAlpha(0.5f),
                    radius: SheetHandleHeight / 2f);
            }

            using (ctx.PushRoundedClip(surface, radius))
            {
                PaintRecursive(tree);
            }
        }
        finally
        {
            opacityScope.Dispose();
            scaleScope.Dispose();
            translateScope.Dispose();
        }
    }

    // Popovers grow out of their anchor; everything else scales about its center.
    private static Point ScaleOrigin(OverlayEntry entry, Rect panel)
    {
        if (entry.Kind != OverlayKind.Popover)
        {
            return panel.Center;
        }

        return entry.ResolvedSide switch
        {
            PopoverSide.Top => new Point(panel.Center.X, panel.Bottom),
            PopoverSide.Left => new Point(panel.Right, panel.Center.Y),
            PopoverSide.Right => new Point(panel.X, panel.Center.Y),
            _ => new Point(panel.Center.X, panel.Y),
        };
    }

#if CASCADE_DEVTOOLS
    private static string OverlayProvenanceId(OverlayEntry entry)
    {
        return entry.Kind switch
        {
            OverlayKind.Popover => "Popover",
            OverlayKind.Sheet => "BottomSheet",
            _ => "Dialog",
        };
    }
#endif
}
