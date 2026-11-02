using System.Text;

namespace Cascade.Generators;

/// <summary>
/// Names derived from an icon's file name. Shared by <see cref="IconPackGenerator"/> and
/// <c>tools/LucideImport</c> (which links this file), so a Lucide icon has the same name whether
/// it comes from the built-in pack or from an app's own <c>icons/</c> folder.
/// </summary>
internal static class IconNames
{
    /// <summary>
    /// <c>check-circle</c> becomes <c>CheckCircle</c>. Hyphens, underscores, dots and spaces
    /// separate words; other characters that cannot appear in an identifier are dropped; a
    /// leading digit gets an underscore in front.
    /// </summary>
    public static string ToPascalCase(string name)
    {
        var sb = new StringBuilder(name.Length);
        bool capitalizeNext = true;
        foreach (char c in name)
        {
            if (c == '-' || c == '_' || c == '.' || c == ' ')
            {
                capitalizeNext = true;
                continue;
            }

            if (!char.IsLetterOrDigit(c))
            {
                continue;
            }

            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
            capitalizeNext = false;
        }

        if (sb.Length > 0 && char.IsDigit(sb[0]))
        {
            sb.Insert(0, '_');
        }
        return sb.ToString();
    }

    /// <summary>
    /// The name a screen reader announces when the SVG has no <c>title</c>:
    /// <c>folder-open</c> becomes <c>folder open</c>.
    /// </summary>
    public static string ToAccessibleName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            bool separator = c == '-' || c == '_' || c == '.';
            if (separator)
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                {
                    sb.Append(' ');
                }
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString().Trim();
    }
}
