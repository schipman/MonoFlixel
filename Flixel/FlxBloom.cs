using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// A bloom (glow) effect that works at the size of the window, not at the size of the low-resolution game screen, so the glow is
/// smooth while the pixel art underneath stays crisp:
/// <list type="number">
/// <item>the bright parts of the picture are cut out (<see cref="Threshold"/>),</item>
/// <item>they are blurred with a separable gaussian on a texture of the window's size divided by <see cref="Downsample"/>, <see cref="Passes"/>
/// times, each time wider than the last,</item>
/// <item>all those blurs are added up (a tight core with a wide halo around it) and laid on top of the picture (<see cref="Intensity"/>).</item>
/// </list>
/// <code>
/// FlxG.PostProcess.Set(FlxBloom.Create(threshold: 0.65f, intensity: 0.6f));
/// </code>
/// Use it as the last effect in the chain (earlier effects run at the size of the game screen).
/// </summary>
public sealed class FlxBloom : FlxPostProcess
{
    readonly Effect _blur;
    readonly EffectParameter? _threshold, _direction;
    RenderTarget2D? _a, _b, _sum;

    /// <summary>Only parts brighter than this (0 to 1, judged by the brightest color channel) glow.</summary>
    public float Threshold = 0.65f;

    /// <summary>How strong the glow is laid on the picture (default 0.6). The blur levels are added together, so more Passes also means more glow.</summary>
    public float Intensity = 0.6f;

    /// <summary>
    /// Distance between the blur's taps in glow-texture pixels for the first, tightest blur (default 1.5). Every further pass doubles it,
    /// so the halo grows quickly; too big a value shows banding.
    /// </summary>
    public float Spread = 1.5f;

    /// <summary>How many blur levels are added up (sideways and up and down each). 1 is a tight glow, 4 a wide soft halo. Default 3.</summary>
    public int Passes = 3;

    /// <summary>The glow texture is the window size divided by this (default 2). 1 is the sharpest (and slowest), 4 gives a very soft, cheap glow.</summary>
    public int Downsample = 2;

    public FlxBloom(Effect bright, Effect blur) : base(bright, "Bloom")
    {
        _blur = blur;
        _threshold = bright.Parameters["Threshold"];
        _direction = blur.Parameters["Direction"];
    }

    /// <summary>Loads the two bloom shaders (Content/Shaders/BloomBright and BloomBlur) and sets up the effect.</summary>
    public static FlxBloom Create(float threshold = 0.65f, float intensity = 0.6f, float spread = 1.5f, int passes = 3, int downsample = 2)
    {
        var content = FlxG.Game.Content;
        return new FlxBloom(content.Load<Effect>("Shaders/BloomBright"), content.Load<Effect>("Shaders/BloomBlur"))
        {
            Threshold = threshold, Intensity = intensity, Spread = spread, Passes = passes, Downsample = downsample
        };
    }

    internal override void Draw(SpriteBatch batch, Texture2D source, Rectangle dest)
    {
        var gd = batch.GraphicsDevice;
        int w = Math.Max(1, dest.Width / Math.Max(1, Downsample)), h = Math.Max(1, dest.Height / Math.Max(1, Downsample));
        EnsureTargets(gd, w, h);

        var previous = gd.GetRenderTargets();       // where the finished picture has to go

        // 1. the bright parts, smoothly scaled down to the glow texture
        _threshold?.SetValue(Threshold);
        gd.SetRenderTarget(_a);
        gd.Clear(Color.Black);
        batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, effect: Effect);
        batch.Draw(source, new Rectangle(0, 0, w, h), Color.White);
        batch.End();

        // 2. blur: sideways into B, up and down back into A, each time with wider taps; every level is added to the sum
        gd.SetRenderTarget(_sum);
        gd.Clear(Color.Black);
        for (int i = 0; i < Math.Max(1, Passes); i++)
        {
            float spread = Spread * (1 << Math.Min(i, 6));
            Blur(batch, _a!, _b!, new Vector2(spread / w, 0), w, h);
            Blur(batch, _b!, _a!, new Vector2(0, spread / h), w, h);

            gd.SetRenderTarget(_sum);
            batch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp);
            batch.Draw(_a!, new Rectangle(0, 0, w, h), Color.White);
            batch.End();
        }

        // 3. back to the real target: the crisp picture, then the glow added on top of it
        if (previous.Length > 0) gd.SetRenderTargets(previous);
        else gd.SetRenderTarget(null);

        batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp);
        batch.Draw(source, dest, Color.White);
        batch.End();

        batch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp);
        float left = Math.Max(0f, Intensity);
        while (left > 0.001f)
        {
            float part = Math.Min(1f, left);                  // the additive blend multiplies by the color's alpha: 1 at most per draw
            batch.Draw(_sum!, dest, new Color(255, 255, 255, (int)MathF.Round(part * 255f)));
            left -= part;
        }
        batch.End();
    }

    void Blur(SpriteBatch batch, RenderTarget2D from, RenderTarget2D to, Vector2 step, int w, int h)
    {
        _direction?.SetValue(step);
        batch.GraphicsDevice.SetRenderTarget(to);
        batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, effect: _blur);
        batch.Draw(from, new Rectangle(0, 0, w, h), Color.White);
        batch.End();
    }

    void EnsureTargets(GraphicsDevice gd, int w, int h)
    {
        if (_a != null && _a.Width == w && _a.Height == h) return;

        _a?.Dispose();
        _b?.Dispose();
        _sum?.Dispose();
        _a = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
        _b = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
        // the sum is drawn into between blur passes (which switch to other targets), so it must keep what it holds
        _sum = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
    }
}
