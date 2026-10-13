using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// A picture that repeats across the screen, for scrolling and parallax backgrounds. Port of flixel-addons' FlxBackdrop.
/// Give layers different <c>ScrollFactor</c>s: 0 stays fixed to the screen, 0.2 moves slowly with the camera (far away),
/// 1 moves with the world.
/// <code>
/// var far = new FlxBackdrop(FlxAssets.Get("hills.png"), repeatY: false) { ScrollFactor = new Vector2(0.2f, 0.05f) };
/// far.Y = 120;
/// Add(far);                          // add layers first (back to front), then the world
/// </code>
/// </summary>
public class FlxBackdrop : FlxSprite
{
    /// <summary>Repeat the picture sideways / up and down. Without repeating it is drawn once at X / Y.</summary>
    public bool RepeatX = true, RepeatY = true;

    /// <summary>Extra space between the repeated copies, in pixels.</summary>
    public float SpacingX, SpacingY;

    /// <summary>Makes the picture drift by itself, in pixels per second (clouds, fog, stars).</summary>
    public Vector2 AutoScroll;

    Vector2 _drift;

    public FlxBackdrop(Texture2D texture, bool repeatX = true, bool repeatY = true, float spacingX = 0, float spacingY = 0) : base(0, 0)
    {
        LoadGraphic(texture);
        RepeatX = repeatX; RepeatY = repeatY;
        SpacingX = spacingX; SpacingY = spacingY;
        Moves = false;
    }

    public FlxBackdrop(string path, bool repeatX = true, bool repeatY = true, float spacingX = 0, float spacingY = 0)
        : this(FlxAssets.GetTexture(path), repeatX, repeatY, spacingX, spacingY) { }

    public override void Update(float elapsed)
    {
        _drift += AutoScroll * elapsed;
        base.Update(elapsed);
    }

    public override void Draw()
    {
        if (Pixels == null || Alpha <= 0) return;
        var cam = FlxG.Camera;

        float tw = FrameWidth + SpacingX, th = FrameHeight + SpacingY;
        float px = X + _drift.X - cam.ScrollX * ScrollFactor.X;
        float py = Y + _drift.Y - cam.ScrollY * ScrollFactor.Y;

        // first copy that touches the visible area (copies sit at px + k * tw)
        float startX = RepeatX ? Wrap(px, tw) - tw : px;
        float startY = RepeatY ? Wrap(py, th) - th : py;
        float endX = RepeatX ? cam.ViewWidth : px + 1, endY = RepeatY ? cam.ViewHeight : py + 1;

        var c = Tint;
        c.A = (byte)(c.A * MathHelper.Clamp(Alpha, 0f, 1f));

        var batch = FlxG.SpriteBatch;
        var effects = (FlipX ? SpriteEffects.FlipHorizontally : 0) | (FlipY ? SpriteEffects.FlipVertically : 0);
        var src = new Rectangle(0, 0, FrameWidth, FrameHeight);

        for (float y = startY; RepeatY ? y < endY : y == startY; y += th)
        {
            for (float x = startX; RepeatX ? x < endX : x == startX; x += tw)
            {
                batch.Draw(Pixels, new Vector2(MathF.Round(x), MathF.Round(y)), src, c, 0f, Vector2.Zero, Scale, effects, 0f);
                if (!RepeatX) break;
            }
            if (!RepeatY) break;
        }
    }

    /// <summary>Remainder that is never negative.</summary>
    static float Wrap(float v, float size)
    {
        float m = v % size;
        return m < 0 ? m + size : m;
    }
}
