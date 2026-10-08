using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// A retained layer (ScrollView content) inside an opacity scope fades once: its content is
/// captured at full opacity and the composite takes the scope's opacity. Capturing it faded as
/// well rendered a 0.5 scope at 0.25.
/// </summary>
public class LayerOpacityTests
{
    private static readonly ColorValue White = ColorValue.FromRgba(1, 1, 1);
    private static readonly ColorValue Red = ColorValue.FromRgba(1, 0, 0);

    [Test]
    public async Task LayerInsideAnOpacityScope_FadesOnce()
    {
        byte[] layered = CpuFrame.Render(64, 64, White, backend =>
        {
            backend.PushLayer(0, 0.5f, BlendMode.Normal);
            ulong handle = backend.NextLayerHandle();
            backend.PushLayerTexture(0, handle, 64, 64);
            backend.DrawRect(0, 8, 8, 48, 48, 0, Red, null, 0);
            backend.PopLayerTexture(0, handle);
            backend.DrawLayerTexture(0, handle, 0, 0, 1);
            backend.PopLayer(0);
        });
        byte[] direct = CpuFrame.Render(64, 64, White, backend =>
        {
            backend.PushLayer(0, 0.5f, BlendMode.Normal);
            backend.DrawRect(0, 8, 8, 48, 48, 0, Red, null, 0);
            backend.PopLayer(0);
        });

        int centre = (32 * 64 + 32) * 4;
        // Half red over white: green and blue at the sRGB encoding of linear 0.5 (188).
        await Assert.That((int)direct[centre + 1]).IsEqualTo(188);
        await Assert.That((int)layered[centre + 1]).IsEqualTo((int)direct[centre + 1]);
        await Assert.That((int)layered[centre]).IsEqualTo((int)direct[centre]);
    }
}
