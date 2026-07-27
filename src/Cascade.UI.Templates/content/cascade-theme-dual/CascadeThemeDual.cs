using Cascade.UI;

namespace CascadeThemeDual.Namespace;

/// <summary>
/// A dual-mode theme supporting both light and dark color schemes.
/// Override <see cref="LightColors"/> and <see cref="DarkColors"/> to define your palette.
/// </summary>
public class CascadeThemeDual : CascadeTheme
{
    public override string Name => "CascadeThemeDual";

    protected override ColorSet LightColors => new()
    {
        Primary      = new Color(0x00, 0x7A, 0xFF),
        OnPrimary    = Color.White,
        Surface      = new Color(0xFA, 0xFA, 0xFA),
        OnSurface    = new Color(0x1A, 0x1A, 0x1A),
        Background   = Color.White,
        OnBackground = new Color(0x1A, 0x1A, 0x1A),
    };

    protected override ColorSet DarkColors => new()
    {
        Primary      = new Color(0x4D, 0xA6, 0xFF),
        OnPrimary    = new Color(0x1A, 0x1A, 0x1A),
        Surface      = new Color(0x2A, 0x2A, 0x2A),
        OnSurface    = new Color(0xF0, 0xF0, 0xF0),
        Background   = new Color(0x1A, 0x1A, 0x1A),
        OnBackground = new Color(0xF0, 0xF0, 0xF0),
    };
}
