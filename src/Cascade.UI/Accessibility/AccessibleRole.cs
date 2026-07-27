namespace Cascade.UI;

/// <summary>
/// Semantic role for accessible elements in the component tree. Maps to
/// platform-native accessibility roles (UIA on Windows, NSAccessibility
/// on macOS, AT-SPI2 on Linux).
/// </summary>
public enum AccessibleRole
{
    /// <summary>
    /// No semantic role assigned. Used as the default when a node has not
    /// been given an explicit accessibility role.
    /// </summary>
    None,

    /// <summary>A push button.</summary>
    Button,

    /// <summary>A checkbox control.</summary>
    Checkbox,

    /// <summary>A hyperlink.</summary>
    Link,

    /// <summary>A heading (with level). Used for section headings.</summary>
    Heading,

    /// <summary>Static text content.</summary>
    Text,

    /// <summary>A text input field.</summary>
    TextBox,

    /// <summary>A radio button within a group.</summary>
    Radio,

    /// <summary>A group of radio buttons.</summary>
    RadioGroup,

    /// <summary>A combobox / dropdown select.</summary>
    ComboBox,

    /// <summary>A range slider.</summary>
    Slider,

    /// <summary>An on/off toggle switch.</summary>
    Switch,

    /// <summary>A list container.</summary>
    List,

    /// <summary>An item within a list.</summary>
    ListItem,

    /// <summary>A data table.</summary>
    Table,

    /// <summary>A row within a table.</summary>
    Row,

    /// <summary>A column header in a table.</summary>
    ColumnHeader,

    /// <summary>A cell within a table row.</summary>
    Cell,

    /// <summary>A tab list container.</summary>
    TabList,

    /// <summary>An individual tab.</summary>
    Tab,

    /// <summary>The content panel associated with a tab.</summary>
    TabPanel,

    /// <summary>A menu bar.</summary>
    MenuBar,

    /// <summary>An item within a menu.</summary>
    MenuItem,

    /// <summary>A dialog window.</summary>
    Dialog,

    /// <summary>A destructive or critical dialog.</summary>
    AlertDialog,

    /// <summary>A progress bar.</summary>
    ProgressBar,

    /// <summary>A scrollbar region.</summary>
    ScrollBar,

    /// <summary>An image.</summary>
    Image,

    /// <summary>A navigation landmark.</summary>
    Navigation,

    /// <summary>The main content landmark.</summary>
    Main,

    /// <summary>A tree view container.</summary>
    Tree,

    /// <summary>An item within a tree view.</summary>
    TreeItem,

    /// <summary>A generic named region.</summary>
    Region,

    /// <summary>
    /// A decorative/presentational element hidden from the accessibility tree.
    /// </summary>
    Presentation
}
