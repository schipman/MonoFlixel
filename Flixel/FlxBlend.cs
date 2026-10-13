using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>How a sprite's colors combine with what is already on the screen.</summary>
public enum FlxBlend
{
    /// <summary>Normal: the sprite covers what is below it (by its alpha).</summary>
    Alpha,
    /// <summary>Colors are added: lights, glows, fire, sparks.</summary>
    Add,
    /// <summary>Colors are multiplied: shadows, tinting, darkening.</summary>
    Multiply,
    /// <summary>The inverse of Multiply: brightens without burning out. Soft glows.</summary>
    Screen,
    /// <summary>The sprite's colors are taken away from what is below.</summary>
    Subtract,
    /// <summary>The brighter of the two colors wins.</summary>
    Lighten,
    /// <summary>The darker of the two colors wins.</summary>
    Darken
}

static class FlxBlendStates
{
    // Colors come from the sprite as plain (not premultiplied) values, like the engine's normal blending. The alpha channel
    // is added up or left alone so these also work while drawing into a transparent overlay target.
    public static readonly BlendState Add = new()
    {
        ColorSourceBlend = Blend.SourceAlpha, ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.SourceAlpha, AlphaDestinationBlend = Blend.One
    };

    public static readonly BlendState Multiply = new()
    {
        ColorSourceBlend = Blend.DestinationColor, ColorDestinationBlend = Blend.InverseSourceAlpha,
        AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One
    };

    public static readonly BlendState Screen = new()
    {
        ColorSourceBlend = Blend.SourceAlpha, ColorDestinationBlend = Blend.InverseSourceColor,
        AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One
    };

    public static readonly BlendState Subtract = new()
    {
        ColorBlendFunction = BlendFunction.ReverseSubtract,
        ColorSourceBlend = Blend.SourceAlpha, ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One
    };

    public static readonly BlendState Lighten = new()
    {
        ColorBlendFunction = BlendFunction.Max,
        ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One
    };

    public static readonly BlendState Darken = new()
    {
        ColorBlendFunction = BlendFunction.Min,
        ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One
    };

    /// <summary>The blend state for a mode, or null for Alpha (the batch's normal one).</summary>
    public static BlendState? For(FlxBlend mode) => mode switch
    {
        FlxBlend.Add => Add,
        FlxBlend.Multiply => Multiply,
        FlxBlend.Screen => Screen,
        FlxBlend.Subtract => Subtract,
        FlxBlend.Lighten => Lighten,
        FlxBlend.Darken => Darken,
        _ => null
    };
}
