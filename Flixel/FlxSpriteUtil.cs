using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// Drawing onto a sprite's own pixels (a CPU copy that is written back with Apply). Only use it on sprites made
/// with MakeGraphic: pictures loaded from files are shared between sprites, so drawing on them changes all of them.
/// <code>
/// var canvas = new FlxCanvas(sprite);
/// canvas.FillRect(0, 0, 32, 32, Color.Black);
/// canvas.DrawLine(0, 0, 31, 31, Color.Red, 2);
/// canvas.FillCircle(16, 16, 6, Color.Yellow);
/// canvas.Apply();
/// </code>
/// </summary>
public sealed class FlxCanvas
{
    readonly Texture2D _texture;
    readonly Color[] _pixels;

    public int Width { get; }
    public int Height { get; }

    public FlxCanvas(FlxSprite sprite)
    {
        _texture = sprite.Pixels ?? throw new InvalidOperationException("The sprite has no graphic yet (call MakeGraphic first).");
        Width = _texture.Width;
        Height = _texture.Height;
        _pixels = new Color[Width * Height];
        _texture.GetData(_pixels);
    }

    /// <summary>Writes the changes to the sprite.</summary>
    public void Apply() => _texture.SetData(_pixels);

    public Color GetPixel(int x, int y) => InBounds(x, y) ? _pixels[y * Width + x] : Color.Transparent;

    bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>Sets a pixel, replacing what was there.</summary>
    public void SetPixel(int x, int y, Color color)
    {
        if (InBounds(x, y)) _pixels[y * Width + x] = color;
    }

    /// <summary>Draws a pixel, blending a semi-transparent color over what is there.</summary>
    public void BlendPixel(int x, int y, Color color)
    {
        if (!InBounds(x, y)) return;
        if (color.A == 255) { _pixels[y * Width + x] = color; return; }
        if (color.A == 0) return;

        var below = _pixels[y * Width + x];
        float a = color.A / 255f;
        _pixels[y * Width + x] = new Color(
            (int)(color.R * a + below.R * (1 - a)),
            (int)(color.G * a + below.G * (1 - a)),
            (int)(color.B * a + below.B * (1 - a)),
            (int)Math.Max(color.A, (int)below.A));
    }

    public void Clear(Color color) => Array.Fill(_pixels, color);

    /// <summary>
    /// Copies the current frame of another sprite onto this one at (x, y) (its Tint and Alpha are applied, transparent
    /// parts blend). Like Flixel's stamp: use it to build a picture out of other pictures.
    /// </summary>
    public void Stamp(FlxSprite source, int x, int y)
    {
        if (source.Pixels == null || source.Frames == null) return;
        var frame = source.Frames[Math.Clamp(source.Frame, 0, source.Frames.Count - 1)];
        var r = frame.Source;

        var data = new Color[r.Width * r.Height];
        source.Pixels.GetData(0, r, data, 0, data.Length);

        var tint = source.Tint;
        float alpha = Math.Clamp(source.Alpha, 0f, 1f);
        for (int py = 0; py < r.Height; py++)
            for (int px = 0; px < r.Width; px++)
            {
                var c = data[py * r.Width + px];
                if (c.A == 0) continue;
                BlendPixel(x + frame.Offset.X + px, y + frame.Offset.Y + py,
                    new Color(c.R * tint.R / 255, c.G * tint.G / 255, c.B * tint.B / 255, (int)(c.A * tint.A / 255 * alpha)));
            }
    }

    public void FillRect(int x, int y, int width, int height, Color color)
    {
        for (int py = Math.Max(0, y); py < Math.Min(Height, y + height); py++)
            for (int px = Math.Max(0, x); px < Math.Min(Width, x + width); px++)
                BlendPixel(px, py, color);
    }

    public void DrawRect(int x, int y, int width, int height, Color color, int thickness = 1)
    {
        FillRect(x, y, width, thickness, color);
        FillRect(x, y + height - thickness, width, thickness, color);
        FillRect(x, y + thickness, thickness, height - 2 * thickness, color);
        FillRect(x + width - thickness, y + thickness, thickness, height - 2 * thickness, color);
    }

    public void DrawLine(int x0, int y0, int x1, int y1, Color color, int thickness = 1)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        int half = thickness / 2;

        while (true)
        {
            FillRect(x0 - half, y0 - half, thickness, thickness, color);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    public void FillCircle(int cx, int cy, int radius, Color color)
    {
        int r2 = radius * radius;
        for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
                if (x * x + y * y <= r2) BlendPixel(cx + x, cy + y, color);
    }

    public void DrawCircle(int cx, int cy, int radius, Color color, int thickness = 1)
    {
        int outer = radius * radius, inner = (radius - thickness) * (radius - thickness);
        for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                int d = x * x + y * y;
                if (d <= outer && d > inner) BlendPixel(cx + x, cy + y, color);
            }
    }
}

/// <summary>Small helpers for sprites and objects. Port of the useful parts of Flixel's FlxSpriteUtil.</summary>
public static class FlxSpriteUtil
{
    /// <summary>When the object leaves one side of the screen it comes back in on the opposite side.</summary>
    public static void ScreenWrap(FlxObject obj, bool left = true, bool right = true, bool top = true, bool bottom = true)
    {
        if (left && obj.X + obj.Width < 0) obj.X = FlxG.Width;
        else if (right && obj.X > FlxG.Width) obj.X = -obj.Width;

        if (top && obj.Y + obj.Height < 0) obj.Y = FlxG.Height;
        else if (bottom && obj.Y > FlxG.Height) obj.Y = -obj.Height;
    }

    /// <summary>Keeps the object inside a rectangle (the whole screen by default).</summary>
    public static void Bound(FlxObject obj, float? minX = null, float? minY = null, float? maxX = null, float? maxY = null)
    {
        float x0 = minX ?? 0, y0 = minY ?? 0, x1 = maxX ?? FlxG.Width, y1 = maxY ?? FlxG.Height;

        if (obj.X < x0) obj.X = x0;
        else if (obj.X + obj.Width > x1) obj.X = x1 - obj.Width;

        if (obj.Y < y0) obj.Y = y0;
        else if (obj.Y + obj.Height > y1) obj.Y = y1 - obj.Height;
    }

    /// <summary>Fades the sprite's Alpha from 0 to 1.</summary>
    public static FlxTween FadeIn(FlxSprite sprite, float duration = 1f, Action? onComplete = null, Func<float, float>? ease = null)
    {
        sprite.Alpha = 0;
        return FlxTween.Num(0, 1, duration, v => sprite.Alpha = v,
            new TweenOptions { Ease = ease, OnComplete = onComplete == null ? null : _ => onComplete() });
    }

    /// <summary>Fades the sprite's Alpha from where it is to 0.</summary>
    public static FlxTween FadeOut(FlxSprite sprite, float duration = 1f, Action? onComplete = null, Func<float, float>? ease = null) =>
        FlxTween.Num(sprite.Alpha, 0, duration, v => sprite.Alpha = v,
            new TweenOptions { Ease = ease, OnComplete = onComplete == null ? null : _ => onComplete() });

    /// <summary>Makes the sprite a solid color of the given size (new texture).</summary>
    public static FlxSprite Fill(FlxSprite sprite, Color color)
    {
        if (sprite.Pixels == null) return sprite;
        var canvas = new FlxCanvas(sprite);
        canvas.Clear(color);
        canvas.Apply();
        return sprite;
    }

    /// <summary>Where the object's top-left corner is on screen (world position minus camera scroll).</summary>
    public static Vector2 ScreenPosition(FlxObject obj, FlxCamera? cam = null)
    {
        cam ??= FlxG.Camera;
        return new Vector2(obj.X - cam.ScrollX * obj.ScrollFactor.X, obj.Y - cam.ScrollY * obj.ScrollFactor.Y);
    }
}
