namespace Cascade.UI;

/// <summary>
/// Marks a <b>virtual</b> theme token (property or Create* factory) on a base
/// <see cref="CascadeTheme"/> as one that concrete theme subclasses are required to
/// override. Leaving it at the base default triggers the CASCADETHEME001 warning.
/// </summary>
/// <remarks>
/// Cascade's own global tokens are already <c>abstract</c> (the compiler forces every
/// theme to implement them), so this attribute is for theme <em>authors</em> building a
/// design system: put it on the virtual tokens your own base theme defaults, so each
/// variant subclass is reminded to customise them instead of silently inheriting.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, Inherited = false)]
public sealed class RequiredThemeTokenAttribute : Attribute
{
}
