using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>Makes gradient pictures. Port of Flixel's FlxGradient.</summary>
public static class FlxGradient
{
    /// <summary>
    /// A gradient through the given colors. <paramref name="rotation"/> is the direction in degrees: 90 (default) goes
    /// from top to bottom, 0 from left to right. <paramref name="chunkSize"/> makes it blocky (colors change every N pixels).
    /// </summary>
    public static Color[] CreatePixels(int width, int height, Color[] colors, int chunkSize = 1, float rotation = 90f)
    {
        if (colors.Length == 0) throw new ArgumentException("Give at least one color.", nameof(colors));
        chunkSize = Math.Max(1, chunkSize);

        float r = MathHelper.ToRadians(rotation);
        float cos = MathF.Cos(r), sin = MathF.Sin(r);
        float length = MathF.Abs(width * cos) + MathF.Abs(height * sin);   // how far the gradient runs along its direction
        float cx = width / 2f, cy = height / 2f;

        var data = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                // quantise to chunks, then find how far along the gradient this pixel is
                float px = (x / chunkSize) * chunkSize + chunkSize / 2f;
                float py = (y / chunkSize) * chunkSize + chunkSize / 2f;
                float t = length <= 0 ? 0 : ((px - cx) * cos + (py - cy) * sin) / length + 0.5f;
                data[y * width + x] = Sample(colors, t);
            }
        return data;
    }

    public static Texture2D CreateTexture(int width, int height, Color[] colors, int chunkSize = 1, float rotation = 90f)
    {
        var tex = new Texture2D(FlxG.GraphicsDevice, width, height);
        tex.SetData(CreatePixels(width, height, colors, chunkSize, rotation));
        return tex;
    }

    /// <summary>A sprite showing a gradient, e.g. a sky: <c>FlxGradient.CreateSprite(FlxG.Width, FlxG.Height, new[] { Color.Black, Color.DarkBlue })</c>.</summary>
    public static FlxSprite CreateSprite(int width, int height, Color[] colors, int chunkSize = 1, float rotation = 90f) =>
        new FlxSprite().LoadGraphic(CreateTexture(width, height, colors, chunkSize, rotation));

    static Color Sample(Color[] colors, float t)
    {
        if (colors.Length == 1) return colors[0];
        float f = MathHelper.Clamp(t, 0f, 1f) * (colors.Length - 1);
        int i = Math.Min((int)f, colors.Length - 2);
        return Color.Lerp(colors[i], colors[i + 1], f - i);
    }
}
