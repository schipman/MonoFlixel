using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel.Demo;

/// <summary>Small art generated in code so the hazard layer needs no extra file.</summary>
public static class TilesetFactory
{
    /// <summary>One tile (frame 0) of spikes.</summary>
    public static Texture2D MakeSpikeTile(GraphicsDevice gd, int size)
    {
        var tex = new Texture2D(gd, size, size);
        var px = new Color[size * size];                  // transparent

        int period = Math.Max(2, size / 2);
        float half = (period - 1) / 2f;
        for (int x = 0; x < size; x++)
        {
            float tri = 1f - MathF.Abs((x % period) - half) / half;
            int h = (int)MathF.Round(tri * size * 0.75f) + 1;
            for (int y = size - h; y < size; y++)
                if (y >= 0) px[y * size + x] = new Color(210, 210, 225);
        }

        tex.SetData(px);
        return tex;
    }
}
