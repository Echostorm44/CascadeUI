using Cascade.UI;

namespace CascadeThemeItem.Namespace;

/// <summary>
/// A custom theme. Override properties from <see cref="CascadeTheme"/>
/// to define your brand colors, typography, and spacing.
/// </summary>
public class CascadeThemeItem : CascadeTheme
{
    public override string Name => "CascadeThemeItem";

    public override Color Primary => new(0x00, 0x7A, 0xFF);
    public override Color OnPrimary => Color.White;
    public override Color Surface => new(0xFA, 0xFA, 0xFA);
    public override Color OnSurface => new(0x1A, 0x1A, 0x1A);
    public override Color Background => Color.White;
    public override Color OnBackground => new(0x1A, 0x1A, 0x1A);
}
