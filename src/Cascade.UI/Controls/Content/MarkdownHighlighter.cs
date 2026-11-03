namespace Cascade.UI;

/// <summary>
/// Syntax colouring for Markdown code blocks: keywords, strings, comments (line and block, carried
/// across lines), numbers and type-like names, for the common languages by their usual fence names.
/// An unknown language is drawn plain.
/// </summary>
internal static class MarkdownHighlighter
{
    /// <summary>The colours of one code theme (a dark one on dark themes, a light one on light).</summary>
    internal sealed record Palette(ColorValue Background, ColorValue Plain, ColorValue Keyword, ColorValue String, ColorValue Comment, ColorValue Number, ColorValue TypeName);

    internal static readonly Palette Dark = new(new("#161b22"), new("#c9d1d9"), new("#ff7b72"), new("#a5d6ff"), new("#8b949e"), new("#79c0ff"), new("#d2a8ff"));
    internal static readonly Palette Light = new(new("#f6f8fa"), new("#1f2328"), new("#cf222e"), new("#0a3069"), new("#6e7781"), new("#0550ae"), new("#8250df"));

    /// <summary>The code palette for a theme, by how dark its background is.</summary>
    internal static Palette For(CascadeTheme theme)
    {
        var bg = theme.Colors.Background;
        return bg.R * 0.299f + bg.G * 0.587f + bg.B * 0.114f < 0.5f ? Dark : Light;
    }

    private sealed record Language(HashSet<string> Keywords, string[] LineComments, (string Open, string Close)? BlockComment, string Quotes, bool CaseInsensitive = false, bool Markup = false);

    private static HashSet<string> Words(string words, bool caseInsensitive = false) =>
        new(words.Split(' ', StringSplitOptions.RemoveEmptyEntries), caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private const string CFamily = "if else for while do switch case default break continue return goto sizeof typedef struct union enum const static extern volatile register void char short int long float double signed unsigned bool true false NULL nullptr inline auto";

    private static readonly Language CSharp = new(Words("abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while var async await get set init record required when yield nameof not and or with global partial where dynamic file scoped"), ["//"], ("/*", "*/"), "\"'");
    private static readonly Language JavaScript = new(Words("break case catch class const continue debugger default delete do else export extends false finally for from function if import in instanceof let new null of return static super switch this throw true try typeof undefined var void while with yield async await interface type enum implements private protected public readonly abstract as declare namespace keyof"), ["//"], ("/*", "*/"), "\"'`");
    private static readonly Language Python = new(Words("False None True and as assert async await break class continue def del elif else except finally for from global if import in is lambda nonlocal not or pass raise return try while with yield self match case"), ["#"], null, "\"'");
    private static readonly Language Rust = new(Words("as async await break const continue crate dyn else enum extern false fn for if impl in let loop match mod move mut pub ref return self Self static struct super trait true type unsafe use where while"), ["//"], ("/*", "*/"), "\"");
    private static readonly Language Go = new(Words("break case chan const continue default defer else fallthrough for func go goto if import interface map package range return select struct switch type var true false nil"), ["//"], ("/*", "*/"), "\"'`");
    private static readonly Language Java = new(Words("abstract assert boolean break byte case catch char class const continue default do double else enum extends final finally float for goto if implements import instanceof int interface long native new package private protected public return short static strictfp super switch synchronized this throw throws transient try void volatile while true false null var record fun val when object companion data sealed"), ["//"], ("/*", "*/"), "\"'");
    private static readonly Language C = new(Words(CFamily + " class public private protected virtual override template typename namespace using new delete this throw try catch operator friend constexpr noexcept"), ["//"], ("/*", "*/"), "\"'");
    private static readonly Language Shell = new(Words("if then else elif fi for while do done case esac in function return local export echo exit set unset readonly shift until select"), ["#"], null, "\"'");
    private static readonly Language PowerShell = new(Words("begin break catch class continue data do dynamicparam else elseif end exit filter finally for foreach from function if in param process return switch throw trap try until using while", caseInsensitive: true), ["#"], ("<#", "#>"), "\"'", CaseInsensitive: true);
    private static readonly Language Sql = new(Words("select from where and or not insert into values update set delete create table drop alter index view join inner left right outer on group by order having limit offset as distinct union all null is in like between case when then else end primary key foreign references default exists", caseInsensitive: true), ["--"], ("/*", "*/"), "'\"", CaseInsensitive: true);
    private static readonly Language Json = new(Words("true false null"), [], null, "\"");
    private static readonly Language Yaml = new(Words("true false null yes no on off"), ["#"], null, "\"'");
    private static readonly Language Css = new(Words("important inherit initial unset none auto"), [], ("/*", "*/"), "\"'");
    private static readonly Language Markup = new(Words(""), [], ("<!--", "-->"), "\"'", Markup: true);

    private static Language? Find(string? language) => language?.Trim().ToUpperInvariant() switch
    {
        "C#" or "CS" or "CSHARP" => CSharp,
        "JS" or "JAVASCRIPT" or "JSX" or "TS" or "TYPESCRIPT" or "TSX" or "MJS" => JavaScript,
        "PY" or "PYTHON" => Python,
        "RS" or "RUST" => Rust,
        "GO" or "GOLANG" => Go,
        "JAVA" or "KOTLIN" or "KT" or "SCALA" or "SWIFT" => Java,
        "C" or "H" or "CPP" or "C++" or "CC" or "HPP" or "OBJC" => C,
        "SH" or "BASH" or "ZSH" or "SHELL" or "CONSOLE" => Shell,
        "PS" or "PS1" or "POWERSHELL" or "PWSH" => PowerShell,
        "SQL" => Sql,
        "JSON" or "JSONC" => Json,
        "YAML" or "YML" or "TOML" or "INI" => Yaml,
        "CSS" or "SCSS" or "LESS" => Css,
        "XML" or "HTML" or "XAML" or "SVG" or "CSPROJ" or "AXAML" => Markup,
        _ => null,
    };

    /// <summary>Whether a fence language is one that is coloured.</summary>
    internal static bool Knows(string? language) => Find(language) is not null;

    /// <summary>The code as lines of coloured spans.</summary>
    internal static List<List<MarkdownSpan>> Highlight(string code, string? language, bool enabled, Palette palette)
    {
        var lines = code.Split('\n');
        var result = new List<List<MarkdownSpan>>(lines.Length);
        var lang = enabled ? Find(language) : null;
        bool inBlock = false;
        foreach (string raw in lines)
        {
            string line = raw.TrimEnd('\r');
            result.Add(lang is null ? [new MarkdownSpan(line, Color: palette.Plain)] : Line(line, lang, palette, ref inBlock));
        }
        return result;
    }

    private static List<MarkdownSpan> Line(string line, Language lang, Palette palette, ref bool inBlock)
    {
        var (Plain, Keyword, String, Comment, Number, TypeName) = (palette.Plain, palette.Keyword, palette.String, palette.Comment, palette.Number, palette.TypeName);
        var spans = new List<MarkdownSpan>();
        void Add(string text, ColorValue color)
        {
            if (text.Length == 0)
            {
                return;
            }
            if (spans.Count > 0 && spans[^1].Color == color)
            {
                spans[^1] = spans[^1] with { Text = spans[^1].Text + text };
            }
            else
            {
                spans.Add(new MarkdownSpan(text, Color: color));
            }
        }

        int i = 0;
        while (i < line.Length)
        {
            if (inBlock)
            {
                int close = line.IndexOf(lang.BlockComment!.Value.Close, i, StringComparison.Ordinal);
                int end = close < 0 ? line.Length : close + lang.BlockComment.Value.Close.Length;
                Add(line[i..end], Comment);
                i = end;
                inBlock = close < 0;
                continue;
            }

            if (lang.BlockComment is { } block && string.CompareOrdinal(line, i, block.Open, 0, block.Open.Length) == 0)
            {
                inBlock = true;
                Add(block.Open, Comment);
                i += block.Open.Length;
                continue;
            }

            bool comment = false;
            foreach (string marker in lang.LineComments)
            {
                if (string.CompareOrdinal(line, i, marker, 0, marker.Length) == 0)
                {
                    Add(line[i..], Comment);
                    i = line.Length;
                    comment = true;
                    break;
                }
            }
            if (comment)
            {
                break;
            }

            char c = line[i];
            if (lang.Markup && c == '<')
            {
                // A tag name; attributes and values follow as ordinary tokens.
                int start = i;
                i++;
                if (i < line.Length && line[i] is '/' or '?' or '!')
                {
                    i++;
                }
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] is ':' or '-' or '.' or '_'))
                {
                    i++;
                }
                Add(line[start..i], Keyword);
                continue;
            }

            if (lang.Quotes.Contains(c, StringComparison.Ordinal))
            {
                int start = i;
                i++;
                while (i < line.Length && line[i] != c)
                {
                    i += line[i] == '\\' ? 2 : 1;
                }
                i = Math.Min(line.Length, i + 1);
                Add(line[start..i], String);
                continue;
            }

            if (char.IsDigit(c) && (i == 0 || !char.IsLetterOrDigit(line[i - 1])))
            {
                int start = i;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] is '.' or '_'))
                {
                    i++;
                }
                Add(line[start..i], Number);
                continue;
            }

            if (char.IsLetter(c) || c is '_' or '$' or '@')
            {
                int start = i;
                i++;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_' || (line[i] == '-' && lang.CaseInsensitive)))
                {
                    i++;
                }
                string word = line[start..i];
                var color = lang.Keywords.Contains(word) ? Keyword
                    : !lang.Markup && char.IsUpper(word[0]) && word.Length > 1 ? TypeName
                    : Plain;
                Add(word, color);
                continue;
            }

            Add(c.ToString(), Plain);
            i++;
        }

        if (spans.Count == 0)
        {
            spans.Add(new MarkdownSpan(string.Empty, Color: Plain));
        }
        return spans;
    }
}
