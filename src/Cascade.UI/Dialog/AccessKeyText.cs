namespace Cascade.UI;

/// <summary>
/// Menu labels with Win32-style access keys: an <c>&amp;</c> before a character marks it as the
/// access key (<c>"&amp;Open"</c> shows "Open" with O as its key) and <c>"&amp;&amp;"</c> is a literal
/// ampersand.
/// </summary>
internal static class AccessKeyText
{
    /// <summary>
    /// Splits <paramref name="label"/> into the text shown and the index of its access key in that
    /// text (-1 for none). With <paramref name="firstLetterFallback"/>, a label without a marker
    /// takes its first letter or digit as the access key.
    /// </summary>
    internal static (string Display, int AccessKey) Parse(string? label, bool firstLetterFallback)
    {
        if (string.IsNullOrEmpty(label))
        {
            return ("", -1);
        }

        if (label.IndexOf('&', StringComparison.Ordinal) < 0)
        {
            return (label, firstLetterFallback ? FirstLetterOrDigit(label) : -1);
        }

        var sb = new System.Text.StringBuilder(label.Length);
        int accessKey = -1;
        for (int i = 0; i < label.Length; i++)
        {
            char ch = label[i];
            if (ch == '&' && i + 1 < label.Length)
            {
                i++;
                if (label[i] != '&' && accessKey < 0)
                {
                    accessKey = sb.Length;
                }

                sb.Append(label[i]);
                continue;
            }

            sb.Append(ch);
        }

        string display = sb.ToString();
        if (accessKey < 0 && firstLetterFallback)
        {
            accessKey = FirstLetterOrDigit(display);
        }

        return (display, accessKey);
    }

    private static int FirstLetterOrDigit(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsLetterOrDigit(text[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
