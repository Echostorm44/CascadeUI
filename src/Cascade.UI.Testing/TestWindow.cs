namespace Cascade.UI.Testing;

/// <summary>
/// A fake window implementation for testing. Does not create any platform
/// window — provides a programmable surface for testing window-dependent behavior.
/// </summary>
public sealed class TestWindow
{
    /// <summary>Creates a test window with the specified dimensions.</summary>
    public TestWindow(float width = 1280, float height = 720, string title = "Test Window")
    {
        Width = width;
        Height = height;
        Title = title;
    }

    /// <summary>Window width in logical pixels.</summary>
    public float Width { get; set; }

    /// <summary>Window height in logical pixels.</summary>
    public float Height { get; set; }

    /// <summary>Window title.</summary>
    public string Title { get; set; }

    /// <summary>Whether the window is considered visible.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Whether the window is focused.</summary>
    public bool IsFocused { get; set; } = true;

    /// <summary>The display scale factor (1.0 = 96 DPI, 2.0 = 192 DPI).</summary>
    public float DisplayScale { get; set; } = 1.0f;

    /// <summary>Simulates a resize event.</summary>
    public void Resize(float width, float height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>Simulates focus gain.</summary>
    public void SimulateFocus()
    {
        IsFocused = true;
    }

    /// <summary>Simulates focus loss.</summary>
    public void SimulateBlur()
    {
        IsFocused = false;
    }
}
