using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel.Demo.Drown;

/// <summary>
/// The deep gets dark. A picture the size of the screen (drawn at half size and stretched) is multiplied over the world: dark blue
/// everywhere except near lights, where it opens up to full brightness. The original did this with a lighting shader and a light
/// mask render target; with FlxSprite.Blend = Multiply it is a sprite whose pixels are worked out each frame.
/// </summary>
public sealed class DarknessLayer : FlxSprite
{
    public readonly record struct Light(Vector2 World, float Radius, Color Color);

    const int Divisor = 2;

    readonly int _w, _h;
    readonly Color[] _buffer;
    public readonly List<Light> Lights = new();

    /// <summary>The light that is everywhere, per channel (0 to 1).</summary>
    public Vector3 Ambient = new(0.2f, 0.3f, 0.45f);

    public DarknessLayer()
    {
        _w = FlxG.Width / Divisor;
        _h = FlxG.Height / Divisor;
        _buffer = new Color[_w * _h];

        MakeGraphic(_w, _h, Color.White);
        Origin = Vector2.Zero;
        Scale = new Vector2(Divisor, Divisor);
        Blend = FlxBlend.Multiply;
        ScrollFactor = Vector2.Zero;
        Moves = false;
        X = Y = 0;
    }

    public override void Draw()
    {
        var cam = FlxG.Camera;
        float scrollX = cam.ScrollX, scrollY = cam.ScrollY;

        byte ar = (byte)(Math.Clamp(Ambient.X, 0, 1) * 255), ag = (byte)(Math.Clamp(Ambient.Y, 0, 1) * 255), ab = (byte)(Math.Clamp(Ambient.Z, 0, 1) * 255);
        Array.Fill(_buffer, new Color(ar, ag, ab, (byte)255));

        foreach (var light in Lights)
        {
            // where it is on the screen, in the small picture
            float cx = (light.World.X - scrollX) / Divisor, cy = (light.World.Y - scrollY) / Divisor;
            float r = light.Radius / Divisor;
            if (r < 1) continue;

            int x0 = Math.Max(0, (int)(cx - r)), x1 = Math.Min(_w - 1, (int)(cx + r));
            int y0 = Math.Max(0, (int)(cy - r)), y1 = Math.Min(_h - 1, (int)(cy + r));
            float r2 = r * r;

            for (int y = y0; y <= y1; y++)
            {
                float dy = y - cy;
                int row = y * _w;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx;
                    float t = 1f - (dx * dx + dy * dy) / r2;
                    if (t <= 0) continue;
                    float k = t * t * 1.25f;                // brighter in the middle, soft edge

                    ref Color c = ref _buffer[row + x];
                    c.R = (byte)Math.Min(255, c.R + light.Color.R * k);
                    c.G = (byte)Math.Min(255, c.G + light.Color.G * k);
                    c.B = (byte)Math.Min(255, c.B + light.Color.B * k);
                }
            }
        }

        Pixels!.SetData(_buffer);
        base.Draw();
    }
}
