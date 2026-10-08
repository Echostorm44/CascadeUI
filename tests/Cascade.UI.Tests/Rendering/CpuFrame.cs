using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// Renders backend draw calls through the real CPU path: the provider records the frame
/// (<see cref="EtchRecorder"/>) and <see cref="EtchCpuRenderer"/> composites it at native resolution.
/// </summary>
internal static class CpuFrame
{
    /// <summary>The Inter font the app ships, as tests load it.</summary>
    public static byte[] InterRegular()
    {
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Test font not found: " + path);
        }
        return File.ReadAllBytes(path);
    }

    /// <summary>
    /// Draws with <paramref name="draw"/> on a <paramref name="width"/> × <paramref name="height"/>
    /// frame over <paramref name="background"/> and returns the CPU-rendered RGBA pixels.
    /// </summary>
    public static byte[] Render(int width, int height, ColorValue background, Action<EtchBackend> draw,
        float textGamma = 1.5f, float lightWeight = 1f)
    {
        using var provider = new EtchBackendProvider();
        provider.BeginFrame((uint)width, (uint)height);
        draw(provider.Backend);
        provider.RecordFrame(background);
        using var renderer = new EtchCpuRenderer();
        var parameters = new global::Etch.Compose.ComposeParameters { TextGamma = textGamma, LightWeight = lightWeight };
        renderer.Render(provider.MainRecording, parameters, (uint)width, (uint)height, null, null);
        var image = renderer.CaptureFrame() ?? throw new InvalidOperationException("No CPU frame");
        return image.Pixels;
    }
}
