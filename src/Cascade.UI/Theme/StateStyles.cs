using System;
using System.Diagnostics.CodeAnalysis;

namespace Cascade.UI;

/// <summary>
/// State style for slider/toggle thumbs and similar drag handles.
/// Properties that are null are unchanged from the base state.
/// </summary>
public record ThumbStateStyle
{
    /// <summary>Horizontal scale factor.</summary>
    public float? ScaleX { get; init; }

    /// <summary>Vertical scale factor.</summary>
    public float? ScaleY { get; init; }

    /// <summary>Opacity override (0.0–1.0).</summary>
    public float? Opacity { get; init; }

    /// <summary>Shadow override.</summary>
    public ShadowSpec? Shadow { get; init; }

    /// <summary>Focus/outline ring color.</summary>
    public ColorValue? OutlineColor { get; init; }

    /// <summary>Focus/outline ring width.</summary>
    public float? OutlineWidth { get; init; }

    /// <summary>Fill brush override.</summary>
    public Brush? Fill { get; init; }

    /// <summary>M3 state layer opacity.</summary>
    public float? StateLayerOpacity { get; init; }

    /// <summary>M3 state layer radius.</summary>
    public float? StateLayerRadius { get; init; }

    /// <summary>Per-state enter transition override. Null = fall back to control default.</summary>
    public Transition? EnterTransition { get; init; }

    /// <summary>Per-state exit transition override. Null = fall back to control default.</summary>
    public Transition? ExitTransition { get; init; }

    /// <summary>Cursor proximity effect — animates a property as the cursor approaches.</summary>
    public CursorProximityEffect? Proximity { get; init; }

    /// <summary>
    /// Binds multiple animated properties to a single physics tick for perfect sync.
    /// </summary>
    public static ThumbStateStyle SpringGroup(Transition spring, params ThumbStateStyle[] layers)
    {
        ArgumentNullException.ThrowIfNull(spring);
        ArgumentNullException.ThrowIfNull(layers);

        var result = new ThumbStateStyle
        {
            EnterTransition = spring,
            ExitTransition = spring,
        };

        foreach (var layer in layers)
        {
            if (layer is null)
            {
                continue;
            }

            result = result with
            {
                ScaleX = layer.ScaleX ?? result.ScaleX,
                ScaleY = layer.ScaleY ?? result.ScaleY,
                Opacity = layer.Opacity ?? result.Opacity,
                Shadow = layer.Shadow ?? result.Shadow,
                OutlineColor = layer.OutlineColor ?? result.OutlineColor,
                OutlineWidth = layer.OutlineWidth ?? result.OutlineWidth,
                Fill = layer.Fill ?? result.Fill,
                StateLayerOpacity = layer.StateLayerOpacity ?? result.StateLayerOpacity,
                StateLayerRadius = layer.StateLayerRadius ?? result.StateLayerRadius,
                EnterTransition = layer.EnterTransition ?? result.EnterTransition,
                ExitTransition = layer.ExitTransition ?? result.ExitTransition,
                Proximity = layer.Proximity ?? result.Proximity,
            };
        }

        return result;
    }
}

/// <summary>
/// State style for buttons. Properties that are null are unchanged from the base state.
/// </summary>
public record ButtonStateStyle
{
    /// <summary>Background brush override.</summary>
    public Brush? Background { get; init; }

    /// <summary>Background color shorthand (creates a solid brush).</summary>
    public ColorValue? BackgroundColor { get; init; }

    /// <summary>Background opacity override (0.0–1.0).</summary>
    public float? BackgroundOpacity { get; init; }

    /// <summary>Border brush override.</summary>
    public Brush? Border { get; init; }

    /// <summary>Border width override.</summary>
    public float? BorderWidth { get; init; }

    /// <summary>Text color override.</summary>
    public ColorValue? TextColor { get; init; }

    /// <summary>Text opacity override (0.0–1.0).</summary>
    public float? TextOpacity { get; init; }

    /// <summary>Shadow override.</summary>
    public ShadowSpec? Shadow { get; init; }

    /// <summary>Uniform scale factor.</summary>
    public float? Scale { get; init; }

    /// <summary>Horizontal scale factor.</summary>
    public float? ScaleX { get; init; }

    /// <summary>Vertical scale factor.</summary>
    public float? ScaleY { get; init; }

    /// <summary>Opacity override.</summary>
    public float? Opacity { get; init; }

    /// <summary>
    /// Brightness multiplier: 1.0 = unchanged, 1.1 = 10% brighter.
    /// Applied post-composition.
    /// </summary>
    public float? Brightness { get; init; }

    /// <summary>
    /// Variable font weight (100–900, fractional). Ignored for non-variable fonts.
    /// </summary>
    public float? FontWeight { get; init; }

    /// <summary>Focus outline color.</summary>
    public ColorValue? OutlineColor { get; init; }

    /// <summary>Focus outline width.</summary>
    public float? OutlineWidth { get; init; }

    /// <summary>Focus outline offset from the control edge.</summary>
    public float? OutlineOffset { get; init; }

    /// <summary>Fluent compound focus ring inner color.</summary>
    public ColorValue? InnerRingColor { get; init; }

    /// <summary>Fluent compound focus ring inner width.</summary>
    public float? InnerRingWidth { get; init; }

    /// <summary>Overlay color applied over the background (Fluent hover pattern).</summary>
    public ColorValue? OverlayColor { get; init; }

    /// <summary>M3 state layer opacity.</summary>
    public float? StateLayerOpacity { get; init; }

    /// <summary>M3 state layer color (defaults to text/icon color).</summary>
    public ColorValue? StateLayerColor { get; init; }

    /// <summary>Per-state enter transition. Null = fall back to control default.</summary>
    public Transition? EnterTransition { get; init; }

    /// <summary>Per-state exit transition. Null = fall back to control default.</summary>
    public Transition? ExitTransition { get; init; }

    /// <summary>Cursor proximity effect.</summary>
    public CursorProximityEffect? Proximity { get; init; }

    /// <summary>
    /// Binds multiple animated properties to a single physics tick for perfect sync.
    /// </summary>
    public static ButtonStateStyle SpringGroup(Transition spring, params ButtonStateStyle[] layers)
    {
        ArgumentNullException.ThrowIfNull(spring);
        ArgumentNullException.ThrowIfNull(layers);

        var result = new ButtonStateStyle
        {
            EnterTransition = spring,
            ExitTransition = spring,
        };

        foreach (var layer in layers)
        {
            if (layer is null)
            {
                continue;
            }

            result = result with
            {
                Background = layer.Background ?? result.Background,
                BackgroundColor = layer.BackgroundColor ?? result.BackgroundColor,
                BackgroundOpacity = layer.BackgroundOpacity ?? result.BackgroundOpacity,
                Border = layer.Border ?? result.Border,
                BorderWidth = layer.BorderWidth ?? result.BorderWidth,
                TextColor = layer.TextColor ?? result.TextColor,
                TextOpacity = layer.TextOpacity ?? result.TextOpacity,
                Shadow = layer.Shadow ?? result.Shadow,
                Scale = layer.Scale ?? result.Scale,
                ScaleX = layer.ScaleX ?? result.ScaleX,
                ScaleY = layer.ScaleY ?? result.ScaleY,
                Opacity = layer.Opacity ?? result.Opacity,
                Brightness = layer.Brightness ?? result.Brightness,
                FontWeight = layer.FontWeight ?? result.FontWeight,
                OutlineColor = layer.OutlineColor ?? result.OutlineColor,
                OutlineWidth = layer.OutlineWidth ?? result.OutlineWidth,
                OutlineOffset = layer.OutlineOffset ?? result.OutlineOffset,
                InnerRingColor = layer.InnerRingColor ?? result.InnerRingColor,
                InnerRingWidth = layer.InnerRingWidth ?? result.InnerRingWidth,
                OverlayColor = layer.OverlayColor ?? result.OverlayColor,
                StateLayerOpacity = layer.StateLayerOpacity ?? result.StateLayerOpacity,
                StateLayerColor = layer.StateLayerColor ?? result.StateLayerColor,
                EnterTransition = layer.EnterTransition ?? result.EnterTransition,
                ExitTransition = layer.ExitTransition ?? result.ExitTransition,
                Proximity = layer.Proximity ?? result.Proximity,
            };
        }

        return result;
    }
}

/// <summary>
/// Configures a continuous property animation driven by cursor distance.
/// The effect begins at <see cref="Radius"/> logical pixels and reaches
/// full strength at contact (distance = 0).
/// </summary>
public record CursorProximityEffect
{
    /// <summary>Distance in logical pixels at which the effect begins.</summary>
    public required float Radius { get; init; }

    /// <summary>Which animatable property to drive.</summary>
    public required ProximityProperty Property { get; init; }

    /// <summary>Value range: far (at Radius) to near (at contact).</summary>
    public required FloatRange Range { get; init; }

    /// <summary>Transition used to follow cursor movement.</summary>
    public Transition Follow { get; init; } = new(AnimationModel.Ease(Duration.Ms(60)));

    /// <summary>Constructor for positional parameters.</summary>
    [SetsRequiredMembers]
    public CursorProximityEffect(float radius, ProximityProperty property, FloatRange range)
    {
        Radius = radius;
        Property = property;
        Range = range;
    }
}

/// <summary>
/// Properties that can be driven by cursor proximity.
/// </summary>
public enum ProximityProperty
{
    /// <summary>Shadow spread radius.</summary>
    ShadowSpread,

    /// <summary>Shadow blur radius.</summary>
    ShadowBlur,

    /// <summary>Shadow opacity.</summary>
    ShadowOpacity,

    /// <summary>Brightness multiplier.</summary>
    Brightness,

    /// <summary>Uniform scale factor.</summary>
    Scale,

    /// <summary>Outline/ring width.</summary>
    OutlineWidth,

    /// <summary>Overall opacity.</summary>
    Opacity,

    /// <summary>Emissive glow radius — Etch compositor pass.</summary>
    GlowRadius,
}
