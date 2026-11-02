using System.Reflection;

namespace Cascade.UI.Tests;

/// <summary>
/// Guards the built-in Lucide pack (CONTROLS-020): every icon draws, shapes that came from
/// <c>circle</c>/<c>rect</c> elements are really there, former names equal the icon they were
/// renamed to, and the class keeps the properties that make it cheap: no static constructor
/// and no allocation once an icon has been used.
/// </summary>
public class LucideIconsTests
{
    private static readonly ColorValue White = new("#FFFFFF");

    private static List<(string Name, Icon Icon)> AllIcons()
    {
        return typeof(Icons)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(Icon))
            .Select(p => (p.Name, (Icon)p.GetValue(null)!))
            .ToList();
    }

    [Test]
    public async Task EveryIcon_HasPaths_AndDrawsAt24px()
    {
        var icons = AllIcons();
        var blank = new List<string>();
        foreach (var (name, icon) in icons)
        {
            if (icon.Paths.Length == 0 || icon.ViewBox.Width != 24f || icon.ViewBox.Height != 24f)
            {
                blank.Add(name);
                continue;
            }

            byte[] rgba = IconRasterizer.Rasterize(icon.Paths, 24, 24, 24, 2f, White, marginPx: 2);
            if (CountCovered(rgba) == 0)
            {
                blank.Add(name);
            }
        }

        // 1,891 current icons plus 264 former names (lucide-static 1.59.0).
        await Assert.That(icons.Count).IsGreaterThanOrEqualTo(2155);
        await Assert.That(string.Join(", ", blank)).IsEqualTo("");
    }

    [Test]
    public async Task Icons_HasNoStaticConstructor()
    {
        // A static constructor would build every icon on first touch of any one, and keep all
        // of them alive through NativeAOT trimming.
        await Assert.That(typeof(Icons).TypeInitializer is null).IsTrue();
    }

    [Test]
    public async Task RepeatedAccess_DoesNotAllocate()
    {
        _ = Icons.Play;
        _ = Icons.Settings;

        long before = GC.GetAllocatedBytesForCurrentThread();
        int total = 0;
        for (int i = 0; i < 1000; i++)
        {
            total += Icons.Play.Paths.Length + Icons.Settings.Paths.Length;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(total).IsGreaterThan(0);
        await Assert.That(allocated).IsEqualTo(0L);
    }

    [Test]
    public async Task FormerName_EqualsCurrentIcon()
    {
        await Assert.That(Icons.AlertTriangle == Icons.TriangleAlert).IsTrue();
        await Assert.That(Icons.CheckCircle == Icons.CircleCheckBig).IsTrue();
        await Assert.That(Icons.AlertTriangle.AccessibleName).IsEqualTo("triangle alert");
    }

    [Test]
    public async Task Settings_KeepsItsCircle()
    {
        // lucide settings.svg is a <path> (the cog) and a <circle> (the hub). Before shape
        // conversion the hub was dropped.
        await Assert.That(Icons.Settings.Paths.Length).IsEqualTo(2);
        await Assert.That(Icons.Settings.Paths[1]).IsEqualTo("M9 12A3 3 0 1 0 15 12A3 3 0 1 0 9 12Z");
    }

    [Test]
    public async Task Circle_DrawsARing()
    {
        // lucide circle.svg is a single <circle cx=12 cy=12 r=10>. At 48px the ring is 20px
        // from the centre: ink on the ring, none in the middle.
        const int px = 48;
        byte[] rgba = IconRasterizer.Rasterize(Icons.Circle.Paths, 24, 24, px, 4f, White, marginPx: 0);

        await Assert.That(AlphaAt(rgba, px, 24, 24)).IsEqualTo((byte)0);
        await Assert.That(AlphaAt(rgba, px, 44, 24)).IsGreaterThan((byte)200);
        await Assert.That(AlphaAt(rgba, px, 24, 4)).IsGreaterThan((byte)200);
    }

    [Test]
    public async Task Square_FromRect_DrawsItsEdges()
    {
        // lucide square.svg is a single <rect x=3 y=3 width=18 height=18 rx=2>.
        const int px = 48;
        byte[] rgba = IconRasterizer.Rasterize(Icons.Square.Paths, 24, 24, px, 4f, White, marginPx: 0);

        await Assert.That(AlphaAt(rgba, px, 24, 6)).IsGreaterThan((byte)200);   // top edge, y = 3 units
        await Assert.That(AlphaAt(rgba, px, 42, 24)).IsGreaterThan((byte)200);  // right edge, x = 21 units
        await Assert.That(AlphaAt(rgba, px, 24, 24)).IsEqualTo((byte)0);        // hollow
    }

    private static byte AlphaAt(byte[] rgba, int size, int x, int y)
    {
        return rgba[(((y * size) + x) * 4) + 3];
    }

    private static int CountCovered(byte[] rgba)
    {
        int covered = 0;
        for (int i = 3; i < rgba.Length; i += 4)
        {
            if (rgba[i] > 0)
            {
                covered++;
            }
        }
        return covered;
    }
}
