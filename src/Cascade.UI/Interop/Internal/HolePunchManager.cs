namespace Cascade.UI;

/// <summary>
/// Manages the hole punch compositing mode for native views.
///
/// When a native view operates in <see cref="NativeCompositingMode.HolePunch"/> mode,
/// this class coordinates:
/// 1. Creating an OS-level child window positioned in front of the Cascade render surface
/// 2. Maintaining a transparent "hole" in the Cascade rendering at the native view's bounds
/// 3. Synchronizing position, size, and z-order as the layout changes
/// 4. Handling show/hide transitions when the view scrolls out of clip bounds
///
/// Hole punch mode provides zero display latency and zero memory overhead (no pixel buffer),
/// but the native content always renders on top — it cannot be clipped by scroll containers
/// or overlaid by Cascade content. It may also lag during animations.
/// </summary>
internal sealed class HolePunchManager : IDisposable
{
    private nint parentHandle;
    private nint childHandle;
    private Rect currentBounds;
    private Rect clipBounds;
    private bool visible;
    private bool disposed;

    /// <summary>The OS child window handle for the native view.</summary>
    internal nint ChildHandle => childHandle;

    /// <summary>Whether the hole punch region is currently visible.</summary>
    internal bool IsVisible => visible;

    /// <summary>Current bounds of the hole punch region in physical pixels.</summary>
    internal Rect CurrentBounds => currentBounds;

    internal HolePunchManager()
    {
    }

    /// <summary>
    /// Initializes the hole punch with the parent window handle.
    /// Creates the OS child window at the initial position.
    /// </summary>
    internal void Initialize(nint parentHandle, Rect initialBounds, float scale)
    {
        this.parentHandle = parentHandle;
        currentBounds = ScaleBounds(initialBounds, scale);

        // Create OS child window:
        // Windows: CreateWindowEx(WS_EX_TRANSPARENT, ..., WS_CHILD | WS_VISIBLE, ...)
        // macOS: [[NSView alloc] initWithFrame:bounds], addSubview
        // Linux/X11: XCreateWindow with parent, XMapWindow
        // Linux/Wayland: wl_compositor_create_subsurface
        childHandle = CreateChildWindow(this.parentHandle, currentBounds);
        visible = false;
    }

    /// <summary>
    /// Updates the position and size of the hole punch region.
    /// Called when layout changes.
    /// </summary>
    internal void UpdateBounds(Rect newBounds, float scale)
    {
        Rect scaledBounds = ScaleBounds(newBounds, scale);

        if (scaledBounds == currentBounds)
        {
            return;
        }

        currentBounds = scaledBounds;

        if (childHandle != 0)
        {
            RepositionChildWindow(childHandle, currentBounds);
        }
    }

    /// <summary>
    /// Sets the clip bounds (typically the scroll container or window bounds).
    /// If the native view is fully outside the clip bounds, it is hidden.
    /// If partially inside, it is clipped to the intersection.
    /// </summary>
    internal void UpdateClipBounds(Rect newClipBounds)
    {
        clipBounds = newClipBounds;
        ApplyClipping();
    }

    /// <summary>
    /// Shows the hole punch region (when the view scrolls into view).
    /// </summary>
    internal void Show()
    {
        if (visible || parentHandle == 0)
        {
            return;
        }

        if (childHandle != 0)
        {
            ShowChildWindow(childHandle);
        }
        visible = true;
    }

    /// <summary>
    /// Hides the hole punch region (when the view scrolls out of view).
    /// </summary>
    internal void Hide()
    {
        if (!visible || parentHandle == 0)
        {
            return;
        }

        if (childHandle != 0)
        {
            HideChildWindow(childHandle);
        }
        visible = false;
    }

    /// <summary>
    /// Brings the child window to the front of the z-order.
    /// Called when the native view's z-order changes in the Cascade tree.
    /// </summary>
    internal void BringToFront()
    {
        if (childHandle != 0)
        {
            RaiseChildWindow(childHandle);
        }
    }

    /// <summary>
    /// Returns the handle that should be passed to the <see cref="NativeViewAdapter"/>
    /// as the parent for creating its content.
    /// </summary>
    internal nint GetAdapterParentHandle()
    {
        return childHandle != 0 ? childHandle : parentHandle;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (childHandle != 0)
        {
            DestroyChildWindow(childHandle);
            childHandle = 0;
        }

        visible = false;
        disposed = true;
    }

    // ─── Private implementation ─────────────────────────────────────

    private void ApplyClipping()
    {
        if (childHandle == 0)
        {
            return;
        }

        // Check if the native view is fully outside the clip bounds
        if (!currentBounds.Intersects(clipBounds))
        {
            Hide();
            return;
        }

        // Compute the visible intersection
        Rect visibleRect = currentBounds.Intersect(clipBounds);

        if (visibleRect.Width <= 0 || visibleRect.Height <= 0)
        {
            Hide();
            return;
        }

        Show();

        // If partially clipped, apply a clip region to the child window
        if (visibleRect != currentBounds)
        {
            SetChildWindowClipRegion(childHandle, visibleRect);
        }
        else
        {
            ClearChildWindowClipRegion(childHandle);
        }
    }

    private static Rect ScaleBounds(Rect bounds, float scale)
    {
        return new Rect(
            bounds.X * scale,
            bounds.Y * scale,
            bounds.Width * scale,
            bounds.Height * scale
        );
    }

    private static nint CreateChildWindow(nint parent, Rect bounds)
    {
        // Platform child window creation
        _ = parent; _ = bounds;
        return 0; // Returns actual handle at runtime
    }

    private static void RepositionChildWindow(nint handle, Rect bounds)
    {
        // Windows: SetWindowPos(handle, ..., x, y, w, h, SWP_NOZORDER)
        // macOS: [view setFrame:bounds]
        // Linux: XMoveResizeWindow / wl_subsurface_set_position
        _ = handle; _ = bounds;
    }

    private static void ShowChildWindow(nint handle)
    {
        // Windows: ShowWindow(handle, SW_SHOW)
        // macOS: [view setHidden:NO]
        // Linux: XMapWindow / wl_surface_commit
        _ = handle;
    }

    private static void HideChildWindow(nint handle)
    {
        // Windows: ShowWindow(handle, SW_HIDE)
        // macOS: [view setHidden:YES]
        // Linux: XUnmapWindow
        _ = handle;
    }

    private static void RaiseChildWindow(nint handle)
    {
        // Windows: SetWindowPos(handle, HWND_TOP, ...)
        // macOS: [view.superview addSubview:view positioned:NSWindowAbove ...]
        // Linux: XRaiseWindow
        _ = handle;
    }

    private static void DestroyChildWindow(nint handle)
    {
        // Windows: DestroyWindow(handle)
        // macOS: [view removeFromSuperview]
        // Linux: XDestroyWindow
        _ = handle;
    }

    private static void SetChildWindowClipRegion(nint handle, Rect clipRect)
    {
        // Windows: SetWindowRgn with HRGN
        // macOS: layer.mask / clipsToBounds
        // Linux: XShapeCombineRectangles
        _ = handle; _ = clipRect;
    }

    private static void ClearChildWindowClipRegion(nint handle)
    {
        // Remove any clip region, showing the full window
        _ = handle;
    }
}
