namespace Cascade.UI;

/// <summary>
/// Builds an immutable <see cref="Path"/> from a sequence of drawing commands.
/// Methods return <c>this</c> for fluent chaining.
/// </summary>
/// <remarks>
/// Implementation note: uses directly-grown arrays (not <c>List&lt;T&gt;</c>)
/// so that <see cref="Build"/> can slice them into the resulting <see cref="Path"/>
/// without an extra copy. One <see cref="Path"/> allocation per build.
///
/// For paint-time code that builds a path every frame, prefer
/// <see cref="PathBuilder.Rent"/> / <see cref="Reset"/> which pools a
/// thread-local builder and reuses its backing arrays across frames.
/// In that mode steady-state path construction is zero-managed-allocation
/// aside from the single <see cref="Path"/> object itself.
/// </remarks>
public sealed class PathBuilder
{
    [ThreadStatic]
    private static PathBuilder? pooled;

    private byte[] commands;
    private float[] data;
    private int commandCount;
    private int dataCount;
    private bool built;
    private bool buffersHandedOff;

    /// <summary>Creates a new PathBuilder with default initial capacity.</summary>
    public PathBuilder() : this(initialCommandCapacity: 16, initialDataCapacity: 32)
    {
    }

    /// <summary>
    /// Creates a PathBuilder with pre-sized backing arrays. Useful when the
    /// approximate path size is known to avoid a grow cycle.
    /// </summary>
    public PathBuilder(int initialCommandCapacity, int initialDataCapacity)
    {
        commands = new byte[Math.Max(1, initialCommandCapacity)];
        data = new float[Math.Max(1, initialDataCapacity)];
    }

    /// <summary>
    /// Rents a pooled, thread-local <see cref="PathBuilder"/>. Call
    /// <see cref="Build"/> when finished; the builder's backing arrays are
    /// retained for the next rent. The pool holds exactly one builder per
    /// thread so callers must not nest rent/build across the same frame.
    /// </summary>
    /// <remarks>
    /// This is the recommended entry point for paint-time code. The returned
    /// builder has its command and data buffers already cleared and ready
    /// for appending.
    /// </remarks>
    public static PathBuilder Rent()
    {
        var builder = pooled;
        if (builder == null)
        {
            builder = new PathBuilder(initialCommandCapacity: 64, initialDataCapacity: 256);
            pooled = builder;
        }
        else
        {
            builder.Reset();
        }
        return builder;
    }

    /// <summary>
    /// Resets the builder so it can be reused. Backing arrays are retained
    /// unless the last <see cref="Build"/> handed them off to a Path, in
    /// which case fresh buffers of the same capacity are allocated here.
    /// After Reset, the builder is in the same logical state as a freshly
    /// constructed one.
    /// </summary>
    public void Reset()
    {
        if (buffersHandedOff)
        {
            commands = new byte[commands.Length];
            data = new float[data.Length];
            buffersHandedOff = false;
        }
        commandCount = 0;
        dataCount = 0;
        built = false;
    }

    /// <summary>Moves the current point to the given position without drawing.</summary>
    public PathBuilder MoveTo(Point point)
    {
        ThrowIfBuilt();
        AppendCommand(Path.CmdMoveTo);
        AppendData2(point.X, point.Y);
        return this;
    }

    /// <summary>Draws a straight line from the current point to the given position.</summary>
    public PathBuilder LineTo(Point point)
    {
        ThrowIfBuilt();
        AppendCommand(Path.CmdLineTo);
        AppendData2(point.X, point.Y);
        return this;
    }

    /// <summary>Draws a cubic Bézier curve from the current point.</summary>
    public PathBuilder CubicTo(Point cp1, Point cp2, Point end)
    {
        ThrowIfBuilt();
        AppendCommand(Path.CmdCubicTo);
        EnsureDataCapacity(6);
        data[dataCount++] = cp1.X;
        data[dataCount++] = cp1.Y;
        data[dataCount++] = cp2.X;
        data[dataCount++] = cp2.Y;
        data[dataCount++] = end.X;
        data[dataCount++] = end.Y;
        return this;
    }

    /// <summary>Draws a quadratic Bézier curve from the current point.</summary>
    public PathBuilder QuadTo(Point control, Point end)
    {
        ThrowIfBuilt();
        AppendCommand(Path.CmdQuadTo);
        EnsureDataCapacity(4);
        data[dataCount++] = control.X;
        data[dataCount++] = control.Y;
        data[dataCount++] = end.X;
        data[dataCount++] = end.Y;
        return this;
    }

    /// <summary>Closes the current contour by drawing a line back to the start.</summary>
    public PathBuilder Close()
    {
        ThrowIfBuilt();
        AppendCommand(Path.CmdClose);
        return this;
    }

    /// <summary>
    /// Builds an immutable <see cref="Path"/> from the accumulated commands.
    /// The path is safe to cache and reuse across frames.
    /// After calling Build(), the builder cannot be reused until <see cref="Reset"/>
    /// is called — doing so would mutate a live Path's buffers.
    /// </summary>
    /// <remarks>
    /// The resulting Path's memory slices into the builder's backing arrays
    /// (zero-copy). The Path takes a logical snapshot at the current count;
    /// subsequent <see cref="Reset"/> + re-append produces a new, independent
    /// Path only because Reset allocates fresh backing arrays when the
    /// previous buffers are owned by a live Path.
    /// </remarks>
    public Path Build()
    {
        ThrowIfBuilt();
        built = true;
        buffersHandedOff = true;

        // Zero-copy hand-off: the Path slices directly into our backing
        // arrays. Reset() will allocate fresh replacements before the next
        // use. This cuts Build() cost from 2x ToArray() + Path alloc
        // (3 allocations) to just the Path object (1 allocation).
        return new Path(
            new ReadOnlyMemory<byte>(commands, 0, commandCount),
            new ReadOnlyMemory<float>(data, 0, dataCount));
    }

    /// <summary>
    /// Builds a transient <see cref="Path"/> that aliases this builder's
    /// backing arrays without copying. The returned Path is only valid until
    /// the next call to <see cref="Reset"/> on this builder — after Reset,
    /// the buffers may be overwritten. Use only for frame-scoped paths that
    /// are drawn immediately and not retained.
    /// </summary>
    /// <remarks>
    /// This is the allocation-free path-construction primitive: combined
    /// with <see cref="Rent"/> and <see cref="Reset"/>, steady-state per-paint
    /// path building allocates only the <see cref="Path"/> object itself
    /// (no backing arrays, no List&lt;T&gt;, no ToArray copies).
    /// </remarks>
    public Path BuildTransient()
    {
        ThrowIfBuilt();
        built = true;
        // buffersHandedOff stays false — Reset() will just truncate counts
        // and reuse the existing buffers in place. The returned Path's
        // memory becomes stale after the next Reset.
        return new Path(
            new ReadOnlyMemory<byte>(commands, 0, commandCount),
            new ReadOnlyMemory<float>(data, 0, dataCount));
    }

    private void AppendCommand(byte cmd)
    {
        if (commandCount == commands.Length)
        {
            Array.Resize(ref commands, commands.Length * 2);
        }
        commands[commandCount++] = cmd;
    }

    private void AppendData2(float a, float b)
    {
        EnsureDataCapacity(2);
        data[dataCount++] = a;
        data[dataCount++] = b;
    }

    private void EnsureDataCapacity(int additional)
    {
        int required = dataCount + additional;
        if (required > data.Length)
        {
            int newLen = data.Length * 2;
            while (newLen < required)
            {
                newLen *= 2;
            }
            Array.Resize(ref data, newLen);
        }
    }

    private void ThrowIfBuilt()
    {
        if (built)
        {
            throw new InvalidOperationException("This PathBuilder has already been built. Call Reset() before reusing.");
        }
    }
}
