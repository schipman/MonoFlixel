using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// Fills a Width x Height region by repeating a texture, with scrolling. Good for parallax
/// backgrounds, water, conveyor belts. Scale/rotation/animation are not supported.
/// Use ScrollFactor for parallax: new Vector2(0.5f, 0.5f) etc.
/// </summary>
public class FlxTiledSprite : FlxSprite
{
    public float ScrollX, ScrollY;        // pattern offset in pixels (positive moves the image right / down)
    public bool RepeatX = true, RepeatY = true;

    public FlxTiledSprite(Texture2D? texture, float width, float height, bool repeatX = true, bool repeatY = true)
        : base(0, 0)
    {
        if (texture != null) LoadGraphic(texture);
        Width = width; Height = height;
        RepeatX = repeatX; RepeatY = repeatY;
        Moves = false;
    }

    static int Mod(int a, int m) => ((a % m) + m) % m;

    public override void Draw()
    {
        if (Pixels == null || Alpha <= 0 || Width <= 0 || Height <= 0) return;
        var cam = FlxG.Camera;

        int left = (int)MathF.Round(X - cam.ScrollX * ScrollFactor.X);
        int top = (int)MathF.Round(Y - cam.ScrollY * ScrollFactor.Y);
        int w = (int)Width, h = (int)Height;
        int tw = Pixels.Width, th = Pixels.Height;

        int sx = (int)MathF.Round(ScrollX), sy = (int)MathF.Round(ScrollY);
        int offX = RepeatX ? Mod(sx, tw) - tw : sx;
        int offY = RepeatY ? Mod(sy, th) - th : sy;

        var c = Tint;
        c.A = (byte)(c.A * MathHelper.Clamp(Alpha, 0f, 1f));
        var batch = FlxG.SpriteBatch;

        for (int y = offY; y < h; y += th)
        {
            int dy0 = Math.Max(y, 0), dy1 = Math.Min(y + th, h);
            if (dy1 > dy0)
            {
                for (int x = offX; x < w; x += tw)
                {
                    int dx0 = Math.Max(x, 0), dx1 = Math.Min(x + tw, w);
                    if (dx1 > dx0)
                        batch.Draw(Pixels,
                            new Rectangle(left + dx0, top + dy0, dx1 - dx0, dy1 - dy0),
                            new Rectangle(dx0 - x, dy0 - y, dx1 - dx0, dy1 - dy0), c);
                    if (!RepeatX) break;
                }
            }
            if (!RepeatY) break;
        }
    }
}
