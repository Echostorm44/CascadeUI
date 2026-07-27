namespace Cascade.UI.GoldenTextFixture;

/// <summary>
/// One specimen page of the golden-text suite: a font weight, a polarity
/// (white-on-black or black-on-white), and a layout scale factor. The page id
/// format is <c>{weight}-{theme}-{scale}</c>, e.g. <c>regular-dark-125</c>.
/// The test suite (tests/Cascade.UI.GoldenText) builds the same ids — keep
/// the two sides in sync.
/// </summary>
internal sealed record PageSpec(
    string Weight,
    bool DarkBackground,
    float Scale)
{
    /// <summary>
    /// Specimen sheet design size, in specimen units. One specimen unit maps
    /// to exactly <see cref="Scale"/> device pixels regardless of OS DPI (the
    /// draw callback divides the page scale by the window's pixel ratio).
    /// The capture region for a page is (SheetWidth × Scale, SheetHeight × Scale)
    /// device pixels — sized to stay inside the screenshot pipeline's
    /// no-resize budget (≤ 1568 px max edge, ≤ 1.15 MP) at Scale 2.0.
    /// </summary>
    public const int SheetWidth = 640;

    /// <summary>See <see cref="SheetWidth"/>.</summary>
    public const int SheetHeight = 440;

    /// <summary>
    /// Absolute path to the page's font file. Anchored to the app base
    /// directory (not the working directory) and using the framework's
    /// lowercase <c>fonts</c> LinkBase so the fixture renders identically no
    /// matter where it is launched from.
    /// </summary>
    public string FontPath => System.IO.Path.Combine(AppContext.BaseDirectory, "fonts", Weight switch
    {
        "regular" => "Inter-Regular.ttf",
        "medium" => "Inter-Medium.ttf",
        "semibold" => "Inter-SemiBold.ttf",
        // The emoji page uses Inter as the primary font; emoji codepoints have no
        // Inter glyph and fall back to the system color-emoji font (WP-3520).
        "emoji" => "Inter-Regular.ttf",
        // The cjk page likewise uses Inter; CJK codepoints fall back to the system
        // Han font, exercising glyph-atlas density (WP-3522).
        "cjk" => "Inter-Regular.ttf",
        _ => throw new ArgumentException($"Unknown weight '{Weight}'. Use: regular, medium, semibold, emoji, cjk."),
    });

    public ColorValue Background => DarkBackground ? new ColorValue("#000000") : new ColorValue("#FFFFFF");

    public ColorValue Foreground => DarkBackground ? new ColorValue("#FFFFFF") : new ColorValue("#000000");

    /// <summary>
    /// Parses a page id (<c>{weight}-{theme}-{scale}</c>). Throws on any
    /// malformed input — a fixture silently rendering the wrong page would
    /// poison goldens.
    /// </summary>
    public static PageSpec Parse(string id)
    {
        string[] parts = id.Split('-');
        if (parts.Length != 3)
        {
            throw new ArgumentException($"Invalid page id '{id}'. Expected: weight-theme-scale (e.g. regular-dark-100).");
        }

        string weight = parts[0] switch
        {
            "regular" or "medium" or "semibold" or "emoji" or "cjk" => parts[0],
            _ => throw new ArgumentException($"Invalid weight '{parts[0]}' in page id '{id}'."),
        };

        bool dark = parts[1] switch
        {
            "dark" => true,
            "light" => false,
            _ => throw new ArgumentException($"Invalid theme '{parts[1]}' in page id '{id}'."),
        };

        float scale = parts[2] switch
        {
            "100" => 1.0f,
            "125" => 1.25f,
            "150" => 1.5f,
            "200" => 2.0f,
            _ => throw new ArgumentException($"Invalid scale '{parts[2]}' in page id '{id}'. Use: 100, 125, 150, 200."),
        };

        return new PageSpec(weight, dark, scale);
    }

    /// <summary>
    /// The specimen lines, per font size: pangrams, digits, and the known
    /// 2026-06-10 regression strings ("Donut Gauges", "45%", "91%"). Larger
    /// sizes carry shorter strings so every line fits the sheet width.
    /// </summary>
    public static readonly IReadOnlyList<(float FontSize, string Text)> Lines =
    [
        (8f, "The quick brown fox jumps over the lazy dog 0123456789 Donut Gauges 45% 91%"),
        (11f, "The quick brown fox jumps over the lazy dog 0123456789 Donut Gauges 45% 91%"),
        (13f, "The quick brown fox jumps over the lazy dog 0123456789 Donut Gauges 45% 91%"),
        (16f, "The quick brown fox jumps over the lazy dog"),
        (16f, "0123456789 Donut Gauges 45% 91%"),
        (24f, "Pack my box with five dozen liquor jugs"),
        (24f, "0123456789 Donut Gauges 45% 91%"),
        (40f, "Donut Gauges 45% 91%"),
        (72f, "Donut Gauges"),
        (72f, "45% 91%"),
    ];

    /// <summary>
    /// Effective raster size (fontSize × scale) above which lines are omitted
    /// from the sheet: the glyph atlas silently drops glyphs taller than its
    /// 127 px shelf cap (known defect, WP-3509 — Inter caps at fontSize 144
    /// rasterize to 140 px of ink). A golden must encode correct rendering,
    /// not a known bug, so 72 px lines are skipped at scale 2.0 until
    /// WP-3509 lands — then remove this filter and regenerate the goldens.
    /// </summary>
    public const float MaxEffectiveRasterSize = 130f;

    /// <summary>
    /// COLR/CPAL color-emoji specimen lines (WP-3520). Rendered only on the
    /// "emoji" page: these codepoints are absent from Inter and itemize to the
    /// system color-emoji font, exercising the color-glyph atlas + shader. Not
    /// pixel-golden'd — emoji artwork is system/version-dependent (see the
    /// suite README); the tests assert color is present and survives an atlas
    /// reset instead.
    /// </summary>
    public static readonly IReadOnlyList<(float FontSize, string Text)> EmojiLines =
    [
        (24f, "\U0001F600\U0001F601\U0001F602\U0001F923\U0001F60A\U0001F60D\U0001F60E\U0001F973\U0001F680\U0001F389"),
        (40f, "\U0001F600\U0001F389\U0001F680\U0001F525\U0001F44D"),
        (64f, "\U0001F600\U0001F389\U0001F680"),
    ];

    /// <summary>
    /// Dense distinct-Han CJK lines (WP-3522): a working set of ~200 unique CJK
    /// glyphs at 16 px to exercise glyph-atlas density and the WP-3509 reset under
    /// CJK. Codepoints are absent from Inter and itemize to the system Han font.
    /// </summary>
    public static readonly IReadOnlyList<(float FontSize, string Text)> CjkLines =
    [
        (16f, "一二三四五六七八九十百千万亿东西南北中外上下左右前后内"),
        (16f, "文字体测试渲染图形界面布局排版字形轮廓笔画结构密度压力"),
        (16f, "你我他她它们的是不了在有人这那些什么时候地方因为所以但"),
        (16f, "天地日月星辰山川河流海洋森林花草树木鸟兽虫鱼风雨雷电云"),
        (16f, "春夏秋冬温度湿度气候变化季节交替循环往复岁月流逝时光荏"),
        (16f, "国家城市乡村道路桥梁建筑房屋门窗墙壁屋顶地板楼梯电梯间"),
        (16f, "学习工作生活休息睡眠饮食运动健康快乐幸福平安吉祥如意顺"),
    ];

    /// <summary>The lines rendered on this page (see <see cref="MaxEffectiveRasterSize"/>).</summary>
    public IEnumerable<(float FontSize, string Text)> LinesForPage()
    {
        var source = Weight switch
        {
            "emoji" => EmojiLines,
            "cjk" => CjkLines,
            _ => Lines,
        };
        foreach ((float fontSize, string text) in source)
        {
            if (fontSize * Scale <= MaxEffectiveRasterSize)
            {
                yield return (fontSize, text);
            }
        }
    }
}
