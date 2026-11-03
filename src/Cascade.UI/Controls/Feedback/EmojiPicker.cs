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
public sealed class EmojiPicker : Node, IPumpedNode
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

    // ── Runtime state for layout/paint/input ──────────────────────────

    /// <summary>Currently selected category tab index.</summary>
    internal int SelectedCategoryIndex { get; set; }

    /// <summary>Index of the currently hovered emoji cell (-1 for none, -100-N for tab hover).</summary>
    internal int HoveredIndex { get; set; } = -1;

    /// <summary>Absolute bounds of the picker in viewport coordinates.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>Built-in emoji, one array per <see cref="EmojiCategory"/>, in that order.</summary>
    internal static readonly string[][] EmojiData =
    [
        // SmileysAndPeople (faces, then hands and gestures, which take a skin tone)
        ["😀", "😃", "😄", "😁", "😆", "😅", "🤣", "😂", "🙂", "😊",
         "😇", "🥰", "😍", "🤩", "😘", "😗", "😚", "😙", "🥲", "😋",
         "😛", "😜", "🤪", "😝", "🤑", "🤗", "🤭", "🤫", "🤔", "🫡",
         "😐", "😑", "😶", "🫥", "😏", "😒", "🙄", "😬", "🤥", "😌",
         "😔", "😪", "🤤", "😴", "😷", "🤒", "🤕", "🤢", "🤮", "🥵",
         "🥶", "🥴", "😵", "🤯", "🤠", "🥳", "🥸", "😎", "🤓", "🧐",
         "😕", "🫤", "😟", "🙁", "😮", "😯", "😲", "😳", "🥺", "🥹",
         "😦", "😧", "😨", "😰", "😥", "😢", "😭", "😱", "😖", "😣",
         "👋", "🤚", "✋", "🖖", "👌", "🤌", "🤏", "✌", "🤞", "🤟",
         "🤘", "🤙", "👈", "👉", "👆", "👇", "☝", "👍", "👎", "✊",
         "👊", "🤛", "🤜", "👏", "🙌", "👐", "🤲", "🙏", "💪", "✍"],
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
         "🥕", "🌽", "🌶️", "🥒", "🥬", "🥦", "🧄", "🧅", "🍄", "🥜",
         "🍞", "🥐", "🥖", "🧀", "🥚", "🍳", "🥞", "🥓", "🍔", "🍟",
         "🍕", "🌭", "🌮", "🌯", "🍝", "🍜", "🍣", "🍩", "🍪", "🎂",
         "🍰", "🍫", "🍿", "☕", "🍵", "🥤", "🍺", "🍷", "🍸", "🥂"],
        // Activities
        ["⚽", "🏀", "🏈", "⚾", "🥎", "🎾", "🏐", "🏉", "🥏", "🎱",
         "🏓", "🏸", "🏒", "🏑", "🥍", "🏏", "⛳", "🏹", "🎣", "🥊",
         "🥋", "⛸️", "🎿", "🛷", "🏂", "🏋️", "🤸", "🧘", "🏄", "🚴",
         "🏆", "🥇", "🥈", "🥉", "🏅", "🎖️", "🎫", "🎟️", "🎪", "🎭",
         "🎨", "🎬", "🎤", "🎧", "🎼", "🎹", "🥁", "🎷", "🎺", "🎸",
         "🎻", "🎲", "♟️", "🎯", "🎳", "🎮", "🎰", "🧩", "🪀", "🪁"],
        // TravelAndPlaces
        ["🚗", "🚕", "🚙", "🚌", "🚎", "🏎️", "🚓", "🚑", "🚒", "🚐",
         "🛻", "🚚", "🚛", "🚜", "🏍️", "🛵", "🚲", "🛴", "🚂", "🚆",
         "🚇", "🚊", "🚝", "🚄", "✈️", "🛫", "🛬", "🚁", "🚀", "🛸",
         "⛵", "🚤", "🛳️", "⛴️", "🚢", "⚓", "🗺️", "🧭", "🏔️", "⛰️",
         "🌋", "🏕️", "🏖️", "🏜️", "🏝️", "🏟️", "🏛️", "🏠", "🏡", "🏢",
         "🏰", "🗼", "🗽", "⛪", "🌉", "🌃", "🌅", "🌄", "🎡", "🎢"],
        // Objects
        ["💻", "🖥️", "🖨️", "⌨️", "🖱️", "💽", "💾", "💿", "📱", "☎️",
         "📞", "📟", "📠", "📺", "📻", "🎙️", "⏰", "⌛", "⏳", "📡",
         "🔋", "🔌", "💡", "🔦", "🕯️", "🧯", "🛢️", "💸", "💵", "💳",
         "💎", "⚖️", "🧰", "🔧", "🔨", "⚒️", "🛠️", "⛏️", "🔩", "⚙️",
         "🧱", "⛓️", "🧲", "🔫", "💣", "🧨", "🪓", "🔪", "🗡️", "🛡️",
         "📷", "📸", "📹", "🎥", "📽️", "🔍", "🔎", "📚", "📖", "✏️"],
        // Symbols
        ["❤️", "🧡", "💛", "💚", "💙", "💜", "🖤", "🤍", "🤎", "💔",
         "❣️", "💕", "💞", "💓", "💗", "💖", "💘", "💝", "☮️", "✝️",
         "☯️", "🕉️", "☸️", "✡️", "🔯", "☪️", "♈", "♉", "♊", "♋",
         "♌", "♍", "♎", "♏", "♐", "♑", "♒", "♓", "⛎", "🔀",
         "🔁", "▶️", "⏸️", "⏹️", "⏺️", "⏭️", "⏮️", "✅", "☑️", "✔️",
         "❌", "❎", "➕", "➖", "➗", "✖️", "💯", "⚠️", "⛔", "🚫"],
        // Flags
        ["🏁", "🚩", "🎌", "🏴", "🏳️", "🏳️‍🌈", "🏳️‍⚧️", "🏴‍☠️", "🇺🇳", "🇺🇸",
         "🇨🇦", "🇲🇽", "🇧🇷", "🇦🇷", "🇬🇧", "🇮🇪", "🇫🇷", "🇩🇪", "🇮🇹", "🇪🇸",
         "🇵🇹", "🇳🇱", "🇧🇪", "🇨🇭", "🇦🇹", "🇸🇪", "🇳🇴", "🇩🇰", "🇫🇮", "🇮🇸",
         "🇵🇱", "🇨🇿", "🇬🇷", "🇹🇷", "🇺🇦", "🇮🇳", "🇨🇳", "🇯🇵", "🇰🇷", "🇦🇺",
         "🇳🇿", "🇿🇦", "🇪🇬", "🇳🇬", "🇰🇪", "🇸🇦", "🇮🇱", "🇸🇬", "🇹🇭", "🇻🇳"],
    ];

    /// <summary>Each category's tab icon, by <see cref="EmojiCategory"/>.</summary>
    internal static readonly string[] CategoryIcons =
        ["😀", "🐶", "🍎", "⚽", "🚗", "💻", "❤️", "🏁"];

    // ── Geometry, shared by layout, paint, hover and click ─────────────

    internal const float CellSize = 36f;
    internal const float Spacing = 2f;
    internal const int Columns = 8;
    internal const float TabHeight = 36f;
    internal const float Pad = 8f;
    internal const int VisibleRows = 5;

    /// <summary>The Recent tab's icon.</summary>
    internal const string RecentIcon = "🕘";

    /// <summary>Hover code for the skin-tone button (tabs are -(100 + tab), cells their index).</summary>
    internal const int ToneButtonHover = -2;

    /// <summary>How far the grid is scrolled (pixels). Kept across re-renders.</summary>
    internal float ScrollOffset { get; set; }

    private bool HasRecentTab => RecentEmojiBind is { Value.Count: > 0 } && MaxRecentCount > 0;

    /// <summary>The tabs, in order: Recent (when there are recent emoji), then the chosen categories.</summary>
    internal IReadOnlyList<EmojiCategory?> Tabs
    {
        get
        {
            var tabs = new List<EmojiCategory?>();
            if (HasRecentTab)
            {
                tabs.Add(null);
            }
            foreach (var category in Categories ?? Enum.GetValues<EmojiCategory>())
            {
                if (!tabs.Contains(category))
                {
                    tabs.Add(category);
                }
            }
            return tabs;
        }
    }

    /// <summary>The selected tab, kept within range.</summary>
    internal int CurrentTab => Math.Clamp(SelectedCategoryIndex, 0, Math.Max(0, Tabs.Count - 1));

    /// <summary>A tab's icon.</summary>
    internal static string TabIcon(EmojiCategory? category) => category is { } c ? CategoryIcons[(int)c] : RecentIcon;

    /// <summary>The emoji on the current tab, with the chosen skin tone on those that take one.</summary>
    internal IReadOnlyList<string> CurrentEmoji
    {
        get
        {
            var tabs = Tabs;
            if (tabs.Count == 0)
            {
                return [];
            }
            var tone = SkinToneBind?.Value ?? UI.SkinTone.Default;
            return tabs[CurrentTab] is { } category
                ? EmojiData[(int)category].Select(e => WithTone(e, tone)).ToList()
                : RecentEmojiBind!.Value.Value.Take(MaxRecentCount).ToList();
        }
    }

    /// <summary>The width of a tab, with the skin-tone button (when there is one) taking a slot at the end.</summary>
    internal float TabWidth(float width) => (width - Pad * 2f) / Math.Max(1, Tabs.Count + (SkinToneBind is null ? 0 : 1));

    /// <summary>The furthest the grid can scroll.</summary>
    internal float MaxScroll(float height)
    {
        int rows = (CurrentEmoji.Count + Columns - 1) / Columns;
        float content = rows * (CellSize + Spacing);
        float view = height - TabHeight - 4f - Pad;
        return Math.Max(0f, content - view);
    }

    /// <summary>
    /// What is at (<paramref name="x"/>, <paramref name="y"/>) in the picker's coordinates: a cell's index,
    /// -(100 + tab) for a tab, <see cref="ToneButtonHover"/> for the skin-tone button, or -1.
    /// </summary>
    internal int HitTest(float x, float y, float width, float height)
    {
        if (x < 0 || x > width || y < 0 || y > height)
        {
            return -1;
        }
        if (y < TabHeight)
        {
            float tabW = TabWidth(width);
            int slot = (int)MathF.Floor((x - Pad) / tabW);
            if (slot >= 0 && slot < Tabs.Count)
            {
                return -(100 + slot);
            }
            return SkinToneBind is not null && slot == Tabs.Count ? ToneButtonHover : -1;
        }

        float gridY = TabHeight + 4f - ScrollOffset;
        int col = (int)MathF.Floor((x - Pad) / (CellSize + Spacing));
        int row = (int)MathF.Floor((y - gridY) / (CellSize + Spacing));
        if (col < 0 || col >= Columns || row < 0)
        {
            return -1;
        }
        int index = row * Columns + col;
        return index < CurrentEmoji.Count ? index : -1;
    }

    /// <summary>Picks an emoji: tells the app, and puts it first in the recent list (up to its limit).</summary>
    internal void Choose(string emoji)
    {
        OnSelect(emoji);
        if (RecentEmojiBind is { } recent && MaxRecentCount > 0)
        {
            var list = new List<string> { emoji };
            list.AddRange(recent.Value.Where(e => e != emoji));
            recent.OnChange([.. list.Take(MaxRecentCount)]);
        }
    }

    /// <summary>Moves the skin tone on to the next one.</summary>
    internal void NextSkinTone()
    {
        if (SkinToneBind is { } tone)
        {
            tone.OnChange((SkinTone)(((int)tone.Value + 1) % Enum.GetValues<SkinTone>().Length));
        }
    }

    // Hands and gestures take a skin tone; faces and the rest don't.
    private static readonly HashSet<string> ToneCapable =
    [
        "👋", "🤚", "✋", "🖖", "👌", "🤌", "🤏", "✌", "🤞", "🤟", "🤘", "🤙", "👈", "👉", "👆", "👇",
        "☝", "👍", "👎", "✊", "👊", "🤛", "🤜", "👏", "🙌", "👐", "🤲", "🙏", "💪", "✍",
    ];

    /// <summary>The emoji with <paramref name="tone"/>'s modifier when it takes one.</summary>
    internal static string WithTone(string emoji, SkinTone tone) =>
        tone == UI.SkinTone.Default || !ToneCapable.Contains(emoji)
            ? emoji
            : emoji + char.ConvertFromUtf32(0x1F3FB + (int)tone - 1);

    void IPumpedNode.Pump()
    {
    }

    void IPumpedNode.AdoptFrom(Node previous)
    {
        // The tab and scroll position used to reset on every re-render.
        if (previous is EmojiPicker old)
        {
            SelectedCategoryIndex = old.SelectedCategoryIndex;
            ScrollOffset = old.ScrollOffset;
        }
    }
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
        picker.LayoutData.A11yLabel = label.Resolve();
        return picker;
    }
}
