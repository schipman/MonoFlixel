using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// A gaussian blur of the whole picture: a sideways blur, then an up and down blur (a 15 tap gaussian each, so it costs 30 reads
/// however wide it is). <see cref="Radius"/> is the sigma of the blur in game pixels; more <see cref="Passes"/> make it wider and smoother.
/// <code>FlxG.PostProcess.Add(FlxGaussianBlur.Create(radius: 1.5f));</code>
/// </summary>
public sealed class FlxGaussianBlur : FlxPostProcess
{
    readonly EffectParameter? _direction;
    RenderTarget2D? _a, _b;

    /// <summary>The blur's sigma, in game pixels (the colors spread about this far). 0 turns the blur off. Default 1.5.</summary>
    public float Radius = 1.5f;

    /// <summary>How many times the blur is run on its own result (1 to 4 or so). Default 1.</summary>
    public int Passes = 1;

    public FlxGaussianBlur(Effect blur) : base(blur, "GaussianBlur")
    {
        _direction = blur.Parameters["Direction"];
        Sampler = SamplerState.LinearClamp;
    }

    /// <summary>Loads the blur shader (Content/Shaders/BloomBlur, which FlxBloom uses too).</summary>
    public static FlxGaussianBlur Create(float radius = 1.5f, int passes = 1) =>
        new(FlxG.Game.Content.Load<Effect>("Shaders/BloomBlur")) { Radius = radius, Passes = passes };

    internal override void Draw(SpriteBatch batch, Texture2D source, Rectangle dest)
    {
        var gd = batch.GraphicsDevice;
        int w = source.Width, h = source.Height;
        var previous = gd.GetRenderTargets();
        Texture2D current = source;

        if (Radius > 0.001f)
        {
            EnsureTargets(gd, w, h);

            // the taps of the shader are 3 per sigma: one tap is Radius / 3 pixels
            float step = Radius / 3f;
            for (int pass = 0; pass < Math.Max(1, Passes); pass++)
            {
                Blur(batch, current, _a!, new Vector2(step / w, 0), w, h);
                Blur(batch, _a!, _b!, new Vector2(0, step / h), w, h);
                current = _b!;
            }
        }

        if (previous.Length > 0) gd.SetRenderTargets(previous);
        else gd.SetRenderTarget(null);

        batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp);
        batch.Draw(current, dest, Color.White);
        batch.End();
    }

    void Blur(SpriteBatch batch, Texture2D from, RenderTarget2D to, Vector2 step, int w, int h)
    {
        _direction?.SetValue(step);
        batch.GraphicsDevice.SetRenderTarget(to);
        batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, effect: Effect);
        batch.Draw(from, new Rectangle(0, 0, w, h), Color.White);
        batch.End();
    }

    void EnsureTargets(GraphicsDevice gd, int w, int h)
    {
        if (_a != null && _a.Width == w && _a.Height == h) return;
        _a?.Dispose();
        _b?.Dispose();
        _a = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
        _b = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
    }
}
