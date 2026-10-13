using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// A solid color that fades in, or wipes across the screen with a soft edge. Port of flixel-addons' TransitionFade.
/// The pictures are white with an alpha ramp and get tinted with the transition's color.
/// </summary>
public class TransitionFade : TransitionEffect
{
    static readonly Dictionary<string, Texture2D> Cache = new();

    readonly FlxSprite _back;

    public TransitionFade(TransitionData data) : base(data)
    {
        _back = MakeSprite(data.Direction.X, data.Direction.Y, data.GetRegion());
        _back.ScrollFactor = Vector2.Zero;
        Add(_back);
    }

    public override void Start(TransitionStatus newStatus)
    {
        base.Start(newStatus);

        var end = new Dictionary<string, float>();
        SetTweenValues(newStatus == TransitionStatus.In, Data.Direction.X, Data.Direction.Y, end);

        var o = Data.TweenOptions;
        FlxTween.Tween(_back, end, Data.Duration, new TweenOptions
        {
            Type = o.Type, Ease = o.Ease, StartDelay = o.StartDelay, LoopDelay = o.LoopDelay,
            OnStart = o.OnStart, OnUpdate = o.OnUpdate,
            OnComplete = _ => DelayThenFinish()
        });
    }

    /// <summary>Sets where the picture starts and fills <paramref name="values"/> with where it tweens to.</summary>
    void SetTweenValues(bool isIn, float dirX, float dirY, Dictionary<string, float> values)
    {
        bool isOut = !isIn;
        var s = _back;

        if (dirX == 0 && dirY == 0)
        {
            // no wipe: just fade
            s.Alpha = isIn ? 0 : 1;
            values["Alpha"] = isOut ? 0 : 1;
        }
        else if (dirX != 0 && dirY != 0)
        {
            // diagonal wipe: slides sideways, the picture itself is diagonal
            if (dirX > 0)
            {
                s.X = isIn ? -s.Width : 0;
                values["X"] = isOut ? -s.Width : 0;
            }
            else
            {
                s.X = isIn ? FlxG.Width : FlxG.Width - s.Width;
                values["X"] = isOut ? FlxG.Width : FlxG.Width - s.Width;
            }
        }
        else if (dirX != 0)
        {
            // horizontal wipe
            if (dirX > 0)
            {
                s.X = isIn ? -s.Width : 0;
                values["X"] = isOut ? -s.Width : 0;
            }
            else
            {
                s.X = isIn ? FlxG.Width : -s.Width / 2;
                values["X"] = isOut ? FlxG.Width : -s.Width / 2;
            }
        }
        else
        {
            // vertical wipe
            if (dirY > 0)
            {
                s.Y = isIn ? -s.Height : 0;
                values["Y"] = isOut ? -s.Height : 0;
            }
            else
            {
                s.Y = isIn ? FlxG.Height : -s.Height / 2;
                values["Y"] = isOut ? FlxG.Height : -s.Height / 2;
            }
        }
    }

    FlxSprite MakeSprite(float dirX, float dirY, Rectangle region)
    {
        var sprite = new FlxSprite(region.X, region.Y);

        if (dirX == 0 && dirY == 0)
        {
            // one white pixel stretched over the whole region
            sprite.LoadGraphic(Get("solid", () => Solid(1, 1)));
            sprite.Scale = new Vector2(region.Width, region.Height);
            sprite.UpdateHitbox();
        }
        else if (dirX == 0)
        {
            // vertical wipe: a tall strip, solid on one half and a gradient on the other
            sprite.LoadGraphic(Get($"v{Math.Sign(dirY)}:{region.Height}", () => Strip(1, region.Height, vertical: true, dirY > 0)));
            sprite.Scale = new Vector2(region.Width, 1f);
            sprite.UpdateHitbox();
        }
        else if (dirY == 0)
        {
            // horizontal wipe
            sprite.LoadGraphic(Get($"h{Math.Sign(dirX)}:{region.Width}", () => Strip(region.Width, 1, vertical: false, dirX > 0)));
            sprite.Scale = new Vector2(1f, region.Height);
            sprite.UpdateHitbox();
        }
        else
        {
            // diagonal wipe: a stretched copy of the diagonal gradient picture
            sprite.LoadGraphic(Get($"d:{FlxG.Width}x{FlxG.Height}", () => DiagonalGradient(FlxG.Width, FlxG.Height)));
            sprite.FlipX = dirX < 0;
            sprite.FlipY = dirY < 0;
        }

        sprite.Tint = Data.Color;
        return sprite;
    }

    // ------------------------------------------------------------------ pictures (cached: they don't depend on the color)

    static Texture2D Get(string key, Func<Texture2D> make)
    {
        if (!Cache.TryGetValue(key, out var tex)) Cache[key] = tex = make();
        return tex;
    }

    static Texture2D Solid(int w, int h)
    {
        var t = new Texture2D(FlxG.GraphicsDevice, w, h);
        var d = new Color[w * h];
        Array.Fill(d, Color.White);
        t.SetData(d);
        return t;
    }

    /// <summary>
    /// A strip twice the region's size along the wipe direction: one half solid white, the other half fading to
    /// transparent, so the soft edge leads the wipe in. <paramref name="positive"/> is a wipe towards +x / +y.
    /// </summary>
    static Texture2D Strip(int w, int h, bool vertical, bool positive)
    {
        int length = vertical ? h : w;
        int total = length * 2;
        var tex = new Texture2D(FlxG.GraphicsDevice, vertical ? w : total, vertical ? total : h);
        var data = new Color[tex.Width * tex.Height];

        for (int i = 0; i < total; i++)
        {
            byte alpha;
            if (positive)
            {
                // solid first, then a gradient from solid to transparent
                alpha = i < length ? (byte)255 : (byte)(255 - 255 * (i - length) / Math.Max(1, length - 1));
            }
            else
            {
                // gradient from transparent to solid first, then solid
                alpha = i < length ? (byte)(255 * i / Math.Max(1, length - 1)) : (byte)255;
            }

            var c = new Color(255, 255, 255, (int)alpha);
            if (vertical) for (int x = 0; x < tex.Width; x++) data[i * tex.Width + x] = c;
            else for (int y = 0; y < tex.Height; y++) data[y * tex.Width + i] = c;
        }
        tex.SetData(data);
        return tex;
    }

    /// <summary>The diagonal gradient picture stretched to 2.5 screen widths by 1 screen height (smoothly).</summary>
    static Texture2D DiagonalGradient(int gameWidth, int gameHeight)
    {
        var source = FlxTransitionAssets.DiagonalGradient;
        var src = new Color[source.Width * source.Height];
        source.GetData(src);

        int dw = (int)MathF.Floor(gameWidth * 2.5f), dh = gameHeight;
        var dst = new Color[dw * dh];

        for (int y = 0; y < dh; y++)
        {
            float fy = (y + 0.5f) * source.Height / dh - 0.5f;
            int y0 = Math.Clamp((int)MathF.Floor(fy), 0, source.Height - 1), y1 = Math.Min(y0 + 1, source.Height - 1);
            float ty = Math.Clamp(fy - y0, 0f, 1f);

            for (int x = 0; x < dw; x++)
            {
                float fx = (x + 0.5f) * source.Width / dw - 0.5f;
                int x0 = Math.Clamp((int)MathF.Floor(fx), 0, source.Width - 1), x1 = Math.Min(x0 + 1, source.Width - 1);
                float tx = Math.Clamp(fx - x0, 0f, 1f);

                var top = Color.Lerp(src[y0 * source.Width + x0], src[y0 * source.Width + x1], tx);
                var bottom = Color.Lerp(src[y1 * source.Width + x0], src[y1 * source.Width + x1], tx);
                dst[y * dw + x] = Color.Lerp(top, bottom, ty);
            }
        }

        var tex = new Texture2D(FlxG.GraphicsDevice, dw, dh);
        tex.SetData(dst);
        return tex;
    }
}
