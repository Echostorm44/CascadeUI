using System.Collections.Concurrent;
using System.Diagnostics;
using SharpImageFrame = SharpImage.Image.ImageFrame;

namespace Cascade.UI.Imaging;

/// <summary>Where an <see cref="Image"/>'s picture is: not asked for yet, on its way, on show, or failed.</summary>
internal enum ImageLoadState
{
    NotStarted,
    Loading,
    Loaded,
    Failed,
}

/// <summary>How an <see cref="Image"/> wants its picture decoded; the same options share one decode.</summary>
internal sealed record ImageDecodeOptions(
    bool AutoOrient,
    ColorValue? SvgColor,
    IReadOnlyDictionary<string, ColorValue>? SvgColorMap,
    IReadOnlyList<Func<SharpImageFrame, SharpImageFrame>> Steps,
    float CornerRadius,
    string StepsKey)
{
    /// <summary>A cache key for the options.</summary>
    internal string Key
    {
        get
        {
            var key = new System.Text.StringBuilder();
            key.Append(AutoOrient ? 'o' : '-');
            if (SvgColor is { } tint)
            {
                key.Append("|t").Append(ImageCodec.ToHex(tint)).Append(tint.A.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            }
            if (SvgColorMap is { Count: > 0 } map)
            {
                foreach (var (from, to) in map.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    key.Append("|m").Append(from).Append('>').Append(ImageCodec.ToHex(to));
                }
            }
            key.Append('|').Append(StepsKey).Append("|r").Append(CornerRadius.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return key.ToString();
        }
    }
}

/// <summary>
/// Loads and decodes <see cref="Image"/> pictures off the UI thread (a large photo decoding on it froze
/// the app), caches them process-wide by source and options, and remembers when each arrived (for the
/// fade-in). Files, byte arrays and URLs all come through here. A source that fails is remembered as
/// failed, so it isn't fetched again every frame.
/// </summary>
internal static class ImageLoader
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, long> ArrivedAt = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> InFlight = new(StringComparer.Ordinal);
    private static readonly Lazy<HttpClient> Http = new(() => new HttpClient { Timeout = TimeSpan.FromSeconds(30) });

    /// <summary>The HTTP client for downloads (images, Markdown sources).</summary>
    internal static HttpClient SharedHttp => Http.Value;

    /// <summary>
    /// The picture for <paramref name="sourceKey"/> decoded with <paramref name="options"/>, or null while
    /// it loads (or when it failed). <paramref name="start"/> false only looks (a lazy image off screen).
    /// </summary>
    internal static ImageSource? Get(string sourceKey, Func<Task<byte[]>> fetch, ImageDecodeOptions options, bool start,
        out ImageLoadState state, out long arrivedTimestamp)
    {
        string key = sourceKey + "\n" + options.Key;
        if (Cache.TryGetValue(key, out var cached))
        {
            state = cached is null ? ImageLoadState.Failed : ImageLoadState.Loaded;
            arrivedTimestamp = ArrivedAt.GetValueOrDefault(key);
            return cached;
        }

        arrivedTimestamp = 0;
        if (!start && !InFlight.ContainsKey(key))
        {
            state = ImageLoadState.NotStarted;
            return null;
        }

        state = ImageLoadState.Loading;
        if (InFlight.TryAdd(key, 0))
        {
            var uiContext = SynchronizationContext.Current;
            _ = Task.Run(async () =>
            {
                ImageSource? decoded;
                try
                {
                    decoded = Decode(await fetch().ConfigureAwait(false), options);
                }
                catch (Exception ex) when (ex is IOException or NotSupportedException or FormatException
                    or UnauthorizedAccessException or HttpRequestException or TaskCanceledException or InvalidOperationException)
                {
                    decoded = null;
                }

                Cache[key] = decoded;
                ArrivedAt[key] = Stopwatch.GetTimestamp();
                InFlight.TryRemove(key, out _);

                // Frame scheduling is UI-thread affine: marshal the repaint request back.
                if (uiContext is not null)
                {
                    uiContext.Post(static _ => SharedScheduler.Instance.RequestFrame(), null);
                }
                else
                {
                    SharedScheduler.Instance.RequestFrame();
                }
            });
        }
        return null;
    }

    /// <summary>Decodes on the calling thread (a byte array the app already holds).</summary>
    internal static ImageSource? GetNow(string sourceKey, byte[] data, ImageDecodeOptions options, out ImageLoadState state, out long arrivedTimestamp)
    {
        string key = sourceKey + "\n" + options.Key;
        if (!Cache.TryGetValue(key, out var source))
        {
            try
            {
                source = Decode(data, options);
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or FormatException)
            {
                source = null;
            }
            Cache[key] = source;
            ArrivedAt[key] = Stopwatch.GetTimestamp();
        }
        state = source is null ? ImageLoadState.Failed : ImageLoadState.Loaded;
        arrivedTimestamp = ArrivedAt.GetValueOrDefault(key);
        return source;
    }

    /// <summary>Fetches a URL's bytes.</summary>
    internal static Task<byte[]> FetchUrl(string url) => Http.Value.GetByteArrayAsync(new Uri(url));

    private static ImageSource Decode(byte[] data, ImageDecodeOptions options)
    {
        (byte[] rgba, int width, int height) = ImageCodec.DecodeToRgba(data, options.AutoOrient, options.SvgColorMap, options.Steps);

        // .SvgColor: every pixel takes the colour, keeping its coverage (a one-colour icon recoloured).
        if (options.SvgColor is { } tint && FormatRegistryIsSvg(data))
        {
            (byte r, byte g, byte b) = tint.ToSrgbBytes();
            for (int i = 0; i < rgba.Length; i += 4)
            {
                rgba[i] = r;
                rgba[i + 1] = g;
                rgba[i + 2] = b;
                rgba[i + 3] = (byte)Math.Round(rgba[i + 3] * Math.Clamp(tint.A, 0f, 1f));
            }
        }

        if (options.CornerRadius > 0f)
        {
            RoundCorners(rgba, width, height, options.CornerRadius);
        }
        return ImageSource.FromBytes(rgba, width, height);
    }

    private static bool FormatRegistryIsSvg(byte[] data) =>
        SharpImage.Formats.FormatRegistry.DetectFormat(data) == SharpImage.Formats.ImageFileFormat.Svg;

    // Clears the pixels outside a rounded rectangle, anti-aliasing its edge.
    private static void RoundCorners(byte[] rgba, int width, int height, float radius)
    {
        float r = Math.Min(radius, Math.Min(width, height) / 2f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float cx = Math.Clamp(px, r, width - r);
                float cy = Math.Clamp(py, r, height - r);
                float dx = px - cx, dy = py - cy;
                if (dx == 0f && dy == 0f)
                {
                    continue;
                }
                float coverage = Math.Clamp(r - MathF.Sqrt(dx * dx + dy * dy) + 0.5f, 0f, 1f);
                int a = ((y * width) + x) * 4 + 3;
                rgba[a] = (byte)Math.Round(rgba[a] * coverage);
            }
        }
    }
}
