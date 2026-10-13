using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

public enum TransitionType
{
    /// <summary>No transition at all.</summary>
    None,
    /// <summary>A grid of animated tiles (diamonds, circles, squares...) grows to cover the screen, or shrinks away.</summary>
    Tiles,
    /// <summary>A solid color fades in, or wipes across the screen in a direction.</summary>
    Fade
}

/// <summary>Which camera a transition is drawn on.</summary>
public enum TransitionCameraMode
{
    /// <summary>The top-most camera at the time (default).</summary>
    Top,
    /// <summary>A new camera made for the transition, above all others.</summary>
    New,
    /// <summary>Every camera the state is drawn on.</summary>
    Default
}

/// <summary>The picture used for a Tiles transition: a strip of square frames from empty to full.</summary>
public class TransitionTileData
{
    /// <summary>The frame strip (null = the built-in diamonds).</summary>
    public Texture2D? Asset;

    public int Width = 32, Height = 32;

    /// <summary>Frames per second of the grow / shrink animation of one tile.</summary>
    public int FrameRate = 40;

    public TransitionTileData(Texture2D? asset = null, int width = 32, int height = 32, int frameRate = 40)
    {
        Asset = asset; Width = width; Height = height; FrameRate = frameRate;
    }
}

/// <summary>
/// Describes one transition (see FlxTransitionableState). Port of flixel-addons' TransitionData.
/// <code>
/// new TransitionData(TransitionType.Tiles, Color.Black, 0.6f, new Vector2(1, 1), new TransitionTileData(FlxTransitionAssets.Circle));
/// new TransitionData(TransitionType.Fade, Color.White, 1f);
/// </code>
/// </summary>
public class TransitionData
{
    public TransitionType Type;

    /// <summary>The picture for Tiles transitions.</summary>
    public TransitionTileData? TileData;

    /// <summary>The color of the transition.</summary>
    public Color Color;

    /// <summary>How long the transition takes, in seconds.</summary>
    public float Duration = 1f;

    Vector2 _direction;

    /// <summary>
    /// Adds a "wipe": each axis is -1, 0 or 1 (for example (1, 0) wipes from the left to the right, (1, 1) from the top left corner).
    /// (0, 0) means no wipe: everything fades or grows at the same time.
    /// </summary>
    public Vector2 Direction
    {
        get => _direction;
        set => _direction = new Vector2(MathHelper.Clamp(value.X, -1, 1), MathHelper.Clamp(value.Y, -1, 1));
    }

    /// <summary>Easing and other options of the tween that drives a Fade transition (its OnComplete is not used).</summary>
    public TweenOptions TweenOptions = new();

    /// <summary>The part of the screen the transition covers (null = the whole game screen).</summary>
    public Rectangle? Region;

    public TransitionCameraMode CameraMode = TransitionCameraMode.Top;

    public TransitionData(TransitionType type = TransitionType.Fade, Color? color = null, float duration = 1f, Vector2? direction = null,
        TransitionTileData? tileData = null, Rectangle? region = null, TransitionCameraMode cameraMode = TransitionCameraMode.Top)
    {
        Type = type;
        Color = color ?? Color.White;
        Duration = duration;
        Direction = direction ?? Vector2.Zero;
        TileData = tileData;
        Region = region;
        CameraMode = cameraMode;
    }

    /// <summary>The area to cover right now.</summary>
    internal Rectangle GetRegion() => Region ?? new Rectangle(0, 0, FlxG.Width, FlxG.Height);
}
