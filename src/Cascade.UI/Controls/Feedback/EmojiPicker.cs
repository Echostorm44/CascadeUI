namespace Cascade.UI;

/// <summary>
/// Emoji categories for filtering the emoji grid.
/// </summary>
public enum EmojiCategory
{
    /// <summary>Smileys, hand gestures, and people.</summary>
    SmileysAndPeople,

    /// <summary>Animals, plants, and nature scenes.</summary>
    AnimalsAndNature,

    /// <summary>Food, beverages, and cooking.</summary>
    FoodAndDrink,

    /// <summary>Sports, games, and hobbies.</summary>
    Activities,

    /// <summary>Travel destinations, vehicles, and places.</summary>
    TravelAndPlaces,

    /// <summary>Everyday objects.</summary>
    Objects,

    /// <summary>Mathematical, currency, and other symbols.</summary>
    Symbols,

    /// <summary>Country and regional flags.</summary>
    Flags
}

/// <summary>
/// Skin tone modifier for people emoji.
/// </summary>
public enum SkinTone
{
    /// <summary>Default yellow skin tone.</summary>
    Default,

    /// <summary>Light skin tone (Fitzpatrick Type I-II).</summary>
    Light,

    /// <summary>Medium-light skin tone (Fitzpatrick Type III).</summary>
    MediumLight,

    /// <summary>Medium skin tone (Fitzpatrick Type IV).</summary>
    Medium,

    /// <summary>Medium-dark skin tone (Fitzpatrick Type V).</summary>
    MediumDark,

    /// <summary>Dark skin tone (Fitzpatrick Type VI).</summary>
    Dark
}

/// <summary>
/// Emoji grid control with category navigation, search, skin tone selection,
/// and recent emoji tracking.
/// </summary>
public sealed class EmojiPicker : Node
{
    public EmojiPicker(Action<string> onSelect)
    {
        ArgumentNullException.ThrowIfNull(onSelect);
        OnSelect = onSelect;
    }

    /// <summary>Callback invoked with the selected emoji character string.</summary>
    public Action<string> OnSelect { get; }

    // ── Internal state for extension methods ──────────────────────

    /// <summary>Filtered emoji categories to display.</summary>
    internal IReadOnlyList<EmojiCategory>? Categories { get; set; }

    /// <summary>Two-way binding to persistent recent emoji list.</summary>
    internal Bindable<IReadOnlyList<string>>? RecentEmojiBind { get; set; }

    /// <summary>Maximum number of recent emoji to track.</summary>
    internal int MaxRecentCount { get; set; } = 24;

    /// <summary>Two-way binding to the selected skin tone preference.</summary>
    internal Bindable<SkinTone>? SkinToneBind { get; set; }

    /// <summary>Whether the control is disabled.</summary>
    internal bool IsDisabled { get; set; }

    /// <summary>Accessible label for screen readers.</summary>
    internal LocKey AccessibleLabelValue { get; set; }

    // ── Runtime state for layout/paint/input ──────────────────────────

    /// <summary>Currently selected category tab index.</summary>
    internal int SelectedCategoryIndex { get; set; }

    /// <summary>Index of the currently hovered emoji cell (-1 for none, -100-N for tab hover).</summary>
    internal int HoveredIndex { get; set; } = -1;

    /// <summary>Absolute bounds of the picker in viewport coordinates.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>Built-in emoji data organized by category.</summary>
    internal static readonly string[][] EmojiData =
    [
        // SmileysAndPeople
        ["😀", "😃", "😄", "😁", "😆", "😅", "🤣", "😂", "🙂", "😊",
         "😇", "🥰", "😍", "🤩", "😘", "😗", "😚", "😙", "🥲", "😋",
         "😛", "😜", "🤪", "😝", "🤑", "🤗", "🤭", "🤫", "🤔", "🫡",
         "😐", "😑", "😶", "🫥", "😏", "😒", "🙄", "😬", "🤥", "😌",
         "😔", "😪", "🤤", "😴", "😷", "🤒", "🤕", "🤢", "🤮", "🥵",
         "🥶", "🥴", "😵", "🤯", "🤠", "🥳", "🥸", "😎", "🤓", "🧐",
         "😕", "🫤", "😟", "🙁", "😮", "😯", "😲", "😳", "🥺", "🥹",
         "😦", "😧", "😨", "😰", "😥", "😢", "😭", "😱", "😖", "😣"],
        // AnimalsAndNature
        ["🐶", "🐱", "🐭", "🐹", "🐰", "🦊", "🐻", "🐼", "🐨", "🐯",
         "🦁", "🐮", "🐷", "🐸", "🐵", "🐔", "🐧", "🐦", "🐤", "🦆",
         "🦅", "🦉", "🦇", "🐺", "🐗", "🐴", "🦄", "🐝", "🐛", "🦋",
         "🐌", "🐞", "🐜", "🪲", "🪳", "🕷️", "🦂", "🐢", "🐍", "🦎",
         "🌸", "💐", "🌹", "🌺", "🌻", "🌼", "🌷", "🪻", "🌱", "🌲",
         "🌳", "🌴", "🌵", "🌾", "🌿", "☘️", "🍀", "🍁", "🍂", "🍃"],
        // FoodAndDrink
        ["🍎", "🍐", "🍊", "🍋", "🍌", "🍉", "🍇", "🍓", "🫐", "🍈",
         "🍒", "🍑", "🥭", "🍍", "🥥", "🥝", "🍅", "🥑", "🍆", "🥔",
         "🥕", "🌽", "🌶️", "🫑", "🥒", "🥬", "🥦", "🧄", "🧅", "🍄",
         "🍞", "🥐", "🥖", "🫓", "🥨", "🥯", "🥞", "🧇", "🧀", "🍖",
         "🍕", "🌮", "🌯", "🥗", "🍿", "🧂", "🥤", "🧋", "🍺", "🍷"],
        // Activities
        ["⚽", "🏀", "🏈", "⚾", "🥎", "🎾", "🏐", "🏉", "🥏", "🎱",
         "🏓", "🏸", "🏒", "🥊", "🥋", "🥅", "⛳", "⛸️", "🎿", "🛷",
         "🎯", "🪀", "🪁", "🎮", "🕹️", "🎲", "🧩", "♟️", "🎭", "🎨",
         "🎬", "🎤", "🎧", "🎼", "🎹", "🥁", "🪘", "🎷", "🎺", "🪗"],
        // TravelAndPlaces
        ["🚗", "🚕", "🚙", "🚌", "🚎", "🏎️", "🚓", "🚑", "🚒", "🚐",
         "🛻", "🚚", "🚛", "🚜", "🏍️", "🛵", "🚲", "🛴", "✈️", "🚀",
         "🛸", "🚁", "⛵", "🚢", "🏠", "🏡", "🏢", "🏣", "🏥", "🏦",
         "🌍", "🌎", "🌏", "🗺️", "🗻", "🏔️", "⛰️", "🏕️", "🏖️", "🏜️"],
        // Objects
        ["⌚", "📱", "💻", "⌨️", "🖥️", "🖨️", "🖱️", "💾", "💿", "📀",
         "📷", "📹", "🎥", "📽️", "📺", "📻", "🔋", "🔌", "💡", "🔦",
         "🕯️", "💰", "💳", "💎", "🔧", "🔨", "🪛", "🔩", "⚙️", "🧲",
         "📌", "📎", "🔑", "🗝️", "🔒", "📦", "📫", "📝", "📁", "📂"],
        // Symbols
        ["❤️", "🧡", "💛", "💚", "💙", "💜", "🖤", "🤍", "🤎", "💔",
         "❤️‍🔥", "💕", "💞", "💓", "💗", "💖", "💘", "💝", "💟", "☮️",
         "✝️", "☪️", "🕉️", "☸️", "✡️", "🔯", "🕎", "☯️", "☦️", "🛐",
         "⛎", "♈", "♉", "♊", "♋", "♌", "♍", "♎", "♏", "♐"],
        // Flags
        ["🏁", "🚩", "🎌", "🏴", "🏳️", "🏳️‍🌈", "🏳️‍⚧️", "🏴‍☠️", "🇺🇸", "🇬🇧",
         "🇫🇷", "🇩🇪", "🇯🇵", "🇰🇷", "🇨🇳", "🇮🇳", "🇧🇷", "🇨🇦", "🇦🇺", "🇲🇽"],
    ];

    /// <summary>Category tab labels (emoji icons).</summary>
    internal static readonly string[] CategoryIcons =
        ["😀", "🐶", "🍎", "⚽", "🚗", "💻", "❤️", "🏁"];
}

/// <summary>
/// Fluent extension methods for <see cref="EmojiPicker"/>.
/// </summary>
public static class EmojiPickerExtensions
{
    /// <summary>Filters the emoji grid to specific categories.</summary>
    public static EmojiPicker Categories(this EmojiPicker picker, IReadOnlyList<EmojiCategory> categories)
    {
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(categories);
        picker.Categories = categories;
        return picker;
    }

    /// <summary>Binds persistent recent emoji and sets the maximum count.</summary>
    public static EmojiPicker RecentEmoji(this EmojiPicker picker, Bindable<IReadOnlyList<string>> recent, int maxCount = 24)
    {
        ArgumentNullException.ThrowIfNull(picker);
        if (maxCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Max recent count must be non-negative.");
        }

        picker.RecentEmojiBind = recent;
        picker.MaxRecentCount = maxCount;
        return picker;
    }

    /// <summary>Binds the skin tone preference.</summary>
    public static EmojiPicker SkinTone(this EmojiPicker picker, Bindable<SkinTone> skinTone)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.SkinToneBind = skinTone;
        return picker;
    }

    /// <summary>Disables or enables the control.</summary>
    public static EmojiPicker Disabled(this EmojiPicker picker, bool disabled = true)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.IsDisabled = disabled;
        return picker;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static EmojiPicker AccessibleLabel(this EmojiPicker picker, LocKey label)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.AccessibleLabelValue = label;
        return picker;
    }
}
