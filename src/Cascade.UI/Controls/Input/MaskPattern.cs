namespace Cascade.UI;

/// <summary>
/// A named input mask pattern that constrains what can be typed and how
/// the value is displayed. Pattern characters: # = digit, A = letter,
/// * = alphanumeric, ? = optional.
/// </summary>
public sealed class MaskPattern
{
    private static readonly Dictionary<string, MaskPattern> registeredMasks = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a custom mask pattern from a format string.
    /// </summary>
    /// <param name="pattern">
    /// The mask format string. # = digit, A = letter, * = alphanumeric, ? = optional.
    /// </param>
    /// <param name="placeholderChar">Character displayed for unfilled mask slots. Default is '_'.</param>
    public MaskPattern(string pattern, char placeholderChar = '_')
    {
        Pattern = pattern;
        PlaceholderChar = placeholderChar;
    }

    /// <summary>The mask format string.</summary>
    public string Pattern { get; }

    /// <summary>Character displayed for unfilled mask slots.</summary>
    public char PlaceholderChar { get; }

    /// <summary>###-##-#### (Social Security Number).</summary>
    public static MaskPattern SSN { get; } = new("###-##-####");

    /// <summary>##-####### (Employer Identification Number).</summary>
    public static MaskPattern EIN { get; } = new("##-#######");

    /// <summary>#### #### #### #### (standard credit card).</summary>
    public static MaskPattern CreditCard { get; } = new("#### #### #### ####");

    /// <summary>#### ###### ##### (American Express).</summary>
    public static MaskPattern CreditCardAmex { get; } = new("#### ###### #####");

    /// <summary>### or #### (card verification value).</summary>
    public static MaskPattern CVV { get; } = new("####");

    /// <summary>MM/DD/YYYY.</summary>
    public static MaskPattern Date { get; } = new("##/##/####");

    /// <summary>YYYY-MM-DD.</summary>
    public static MaskPattern DateIso { get; } = new("####-##-##");

    /// <summary>HH:MM AM/PM.</summary>
    public static MaskPattern Time12 { get; } = new("##:## AA");

    /// <summary>HH:MM (24-hour).</summary>
    public static MaskPattern Time24 { get; } = new("##:##");

    /// <summary>MM/DD/YYYY HH:MM AM/PM.</summary>
    public static MaskPattern DateTime { get; } = new("##/##/#### ##:## AA");

    /// <summary>(###) ###-#### (US phone).</summary>
    public static MaskPattern PhoneUs { get; } = new("(###) ###-####");

    /// <summary>(###) ###-#### x##### (US phone with extension).</summary>
    public static MaskPattern PhoneUsExt { get; } = new("(###) ###-#### x#####");

    /// <summary>+## (###) ###-#### (international phone).</summary>
    public static MaskPattern PhoneIntl { get; } = new("+## (###) ###-####");

    /// <summary>##### or #####-#### (US postal code).</summary>
    public static MaskPattern PostalCodeUs { get; } = new("#####-####");

    /// <summary>A#A #A# (Canadian postal code).</summary>
    public static MaskPattern PostalCodeCa { get; } = new("A#A #A#");

    /// <summary>Variable-length UK postal code (regex-based).</summary>
    public static MaskPattern PostalCodeUk { get; } = new("****  ***");

    /// <summary>###.###.###.### (IPv4 address).</summary>
    public static MaskPattern IPv4 { get; } = new("###.###.###.###");

    /// <summary>##:##:##:##:##:## (MAC address).</summary>
    public static MaskPattern MacAddress { get; } = new("##:##:##:##:##:##");

    /// <summary>17 alphanumeric characters (Vehicle Identification Number, no I/O/Q).</summary>
    public static MaskPattern Vin { get; } = new("*****************");

    /// <summary>########## (10-digit National Provider Identifier).</summary>
    public static MaskPattern NpiUs { get; } = new("##########");

    /// <summary>#####-####-## (National Drug Code).</summary>
    public static MaskPattern Ndc { get; } = new("#####-####-##");

    /// <summary>
    /// Registers a custom named mask pattern for use across the application.
    /// </summary>
    public static void Register(string name, MaskPattern mask)
    {
        registeredMasks[name] = mask;
    }

    /// <summary>
    /// Retrieves a registered custom mask pattern by name.
    /// Returns null if the name is not registered.
    /// </summary>
    internal static MaskPattern? GetRegistered(string name)
    {
        return registeredMasks.GetValueOrDefault(name);
    }

    /// <summary>
    /// Returns the number of input slots (non-literal characters) in the pattern.
    /// </summary>
    internal int InputSlotCount
    {
        get
        {
            int count = 0;
            foreach (char c in Pattern)
            {
                if (c is '#' or 'A' or '*' or '?')
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Determines whether a character matches the mask slot at the given pattern position.
    /// </summary>
    internal static bool IsSlotMatch(char maskChar, char inputChar)
    {
        return maskChar switch
        {
            '#' => char.IsAsciiDigit(inputChar),
            'A' => char.IsLetter(inputChar),
            '*' => char.IsLetterOrDigit(inputChar),
            '?' => true,
            _ => maskChar == inputChar
        };
    }

    /// <summary>
    /// Applies the mask to a raw input string, producing the formatted display value.
    /// Literal characters from the pattern are inserted automatically.
    /// </summary>
    internal string Apply(string rawInput)
    {
        var result = new char[Pattern.Length];
        int inputIndex = 0;

        for (int i = 0; i < Pattern.Length; i++)
        {
            char maskChar = Pattern[i];

            if (maskChar is '#' or 'A' or '*' or '?')
            {
                if (inputIndex < rawInput.Length && IsSlotMatch(maskChar, rawInput[inputIndex]))
                {
                    result[i] = rawInput[inputIndex];
                    inputIndex++;
                }
                else
                {
                    result[i] = PlaceholderChar;
                }
            }
            else
            {
                result[i] = maskChar;
            }
        }

        return new string(result);
    }

    /// <summary>
    /// Strips literal mask characters from a formatted value, returning only user input.
    /// </summary>
    internal string StripLiterals(string formattedValue)
    {
        var result = new List<char>();
        int len = Math.Min(formattedValue.Length, Pattern.Length);

        for (int i = 0; i < len; i++)
        {
            char maskChar = Pattern[i];
            char inputChar = formattedValue[i];

            if (maskChar is '#' or 'A' or '*' or '?' && inputChar != PlaceholderChar)
            {
                result.Add(inputChar);
            }
        }

        return new string([.. result]);
    }
}

/// <summary>
/// A fully custom mask with format/parse functions and allowed character set.
/// </summary>
public sealed class CustomMask
{
    /// <summary>
    /// Creates a custom mask with full control over formatting and parsing.
    /// </summary>
    /// <param name="format">Formats the raw value for display.</param>
    /// <param name="parse">Parses the display value back to the raw value.</param>
    /// <param name="allowedChars">The set of characters allowed in the input.</param>
    /// <param name="placeholder">Placeholder character for unfilled positions.</param>
    public CustomMask(
        Func<string, string> format,
        Func<string, string> parse,
        CharSet allowedChars = CharSet.AlphaNumeric,
        char placeholder = '_')
    {
        Format = format;
        Parse = parse;
        AllowedChars = allowedChars;
        Placeholder = placeholder;
    }

    /// <summary>Formats the raw value for display.</summary>
    public Func<string, string> Format { get; }

    /// <summary>Parses the display value back to the raw value.</summary>
    public Func<string, string> Parse { get; }

    /// <summary>The set of characters allowed in the input.</summary>
    public CharSet AllowedChars { get; }

    /// <summary>Placeholder character for unfilled positions.</summary>
    public char Placeholder { get; }

    /// <summary>
    /// Determines whether a character is allowed by the character set.
    /// </summary>
    internal bool IsCharAllowed(char c)
    {
        return AllowedChars switch
        {
            CharSet.Numeric => char.IsAsciiDigit(c),
            CharSet.Alpha => char.IsLetter(c),
            CharSet.AlphaNumeric => char.IsLetterOrDigit(c),
            _ => false
        };
    }
}

/// <summary>
/// Defines a set of allowed characters for custom mask input.
/// </summary>
public enum CharSet
{
    /// <summary>Digits only (0-9).</summary>
    Numeric,

    /// <summary>Letters only (a-z, A-Z).</summary>
    Alpha,

    /// <summary>Letters and digits.</summary>
    AlphaNumeric
}
