namespace Cascade.UI;

/// <summary>
/// Compositing mode for native views within the Cascade GPU scene.
/// </summary>
public enum NativeCompositingMode
{
    /// <summary>
    /// The native view renders to an offscreen buffer. The framework uploads
    /// the buffer as a GPU texture each frame. Cascade content can overlay
    /// the native view (tooltips, dialogs, menus). The native view participates
    /// in scroll containers, animations, and z-ordering. One frame of display
    /// latency. This is the default and recommended mode.
    /// </summary>
    TextureBridge,

    /// <summary>
    /// The native view is placed as an OS-level child window directly in front
    /// of Cascade's render surface. Zero display latency and zero memory
    /// overhead. However, the native view is always on top of the Cascade scene,
    /// does not clip to scroll containers, and may lag during animations.
    /// </summary>
    HolePunch
}
