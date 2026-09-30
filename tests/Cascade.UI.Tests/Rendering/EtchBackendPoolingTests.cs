using System.Reflection;
using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// Guards the per-frame allocation fixes in <see cref="EtchBackend"/>: scene ops come from a
/// frame arena and path builders write into reused scratch arrays.
/// </summary>
public class EtchBackendPoolingTests
{
    [Test]
    public async Task SceneOpClear_ResetsEveryField()
    {
        var op = new EtchBackend.SceneOp();
        var fields = typeof(EtchBackend.SceneOp).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var field in fields)
        {
            field.SetValue(op, NonDefault(field.FieldType));
        }

        op.Clear();

        foreach (var field in fields)
        {
            object? expected = DefaultOf(field.FieldType);
            await Assert.That(Equals(field.GetValue(op), expected)).IsTrue().Because($"{field.Name} was not cleared");
        }
    }

    [Test]
    public async Task Reset_RecyclesOpsWithoutLeakingFields()
    {
        using var backend = new EtchBackend();
        backend.DrawCircle(0, 10, 10, 5, ColorValue.FromRgba(1, 0, 0), ColorValue.FromRgba(0, 0, 1), 2, default, default);
        var first = backend.Commands[0];

        backend.Reset();
        backend.DrawRect(0, 1, 2, 3, 4, 0, null, null, 0);
        var second = backend.Commands[0];

        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(second.Kind).IsEqualTo(EtchBackend.OpKind.DrawRect);
        await Assert.That(second.Fill).IsNull();
        await Assert.That(second.StrokeColor).IsNull();
        await Assert.That(second.Radius).IsEqualTo(0f);
    }

    [Test]
    public async Task CirclePath_RebuiltFromScratch_HasCurrentCoordinates()
    {
        var (aMin, aMax) = EndPointExtent(EtchBackend.BuildCirclePath(0, 0, 10));
        var (bMin, bMax) = EndPointExtent(EtchBackend.BuildCirclePath(100, 100, 1));

        await Assert.That(aMax - aMin).IsEqualTo(20.0);
        await Assert.That(bMin).IsEqualTo(99.0);
        await Assert.That(bMax - bMin).IsEqualTo(2.0);
    }

    // Horizontal extent of the on-curve points (the circle's four cardinal points).
    private static (double Min, double Max) EndPointExtent(Etch.Geometry.BezPath path)
    {
        double min = double.MaxValue;
        double max = double.MinValue;
        foreach (var seg in path.Iterate())
        {
            if (seg.Verb == Etch.Geometry.PathVerb.Close)
            {
                continue;
            }
            min = Math.Min(min, seg.End.X);
            max = Math.Max(max, seg.End.X);
        }
        return (min, max);
    }

    private static object? DefaultOf(Type type)
    {
        if (type == typeof(float)) { return 0f; }
        if (type == typeof(int)) { return 0; }
        if (type == typeof(ulong)) { return 0UL; }
        if (type == typeof(bool)) { return false; }
        if (type == typeof(System.Numerics.Matrix3x2)) { return default(System.Numerics.Matrix3x2); }
        if (type == typeof(Rect)) { return default(Rect); }
        if (type == typeof(EtchBackend.OpKind)) { return default(EtchBackend.OpKind); }
        return null;
    }

    private static object? NonDefault(Type type)
    {
        if (type == typeof(float)) { return 1f; }
        if (type == typeof(int)) { return 1; }
        if (type == typeof(ulong)) { return 1UL; }
        if (type == typeof(bool)) { return true; }
        if (type == typeof(string)) { return "x"; }
        if (type == typeof(ColorValue?)) { return ColorValue.FromRgba(1, 1, 1); }
        if (type == typeof(GradientStop[])) { return Array.Empty<GradientStop>(); }
        if (type == typeof(System.Numerics.Matrix3x2)) { return System.Numerics.Matrix3x2.CreateScale(2); }
        if (type == typeof(Rect)) { return new Rect(1, 1, 1, 1); }
        if (type == typeof(EtchBackend.OpKind)) { return EtchBackend.OpKind.DrawCircle; }
        throw new InvalidOperationException($"Add a non-default value for {type} so Clear() stays covered.");
    }
}
