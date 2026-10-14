using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

// Port of Richard Davey's (Photon Storm) Flixel Power Tools "FlxSpecialFX" plugin: classic demo-scene effects that draw
// into a small pixel canvas shown by a sprite. Make one with the FlxSpecialFX factories, Add() the sprite it gives you to
// a state, and it animates itself every frame:
//
//     var plasma = FlxSpecialFX.Plasma();
//     Add(plasma.Create(0, 0, 160, 120, scaleX: 2, scaleY: 2));
//
// Differences from the Flash original: effects start as soon as they are created (Start(delay) only sets the number of
// frames to skip between redraws), sources are Texture2D / FlxSprite instead of BitmapData, and RevealFX is left out
// (the original is an unfinished copy of GlitchFX).

/// <summary>A software pixel canvas (premultiplied Color pixels) with the few BitmapData operations the effects need.</summary>
public sealed class FxBitmap
{
    public readonly int Width, Height;
    public readonly Color[] Data;

    public FxBitmap(int width, int height, Color? fill = null)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Data = new Color[Width * Height];
        if (fill is { } c && c != Color.Transparent) Array.Fill(Data, c);
    }

    public Rectangle Bounds => new(0, 0, Width, Height);

    public static FxBitmap FromTexture(Texture2D tex, Rectangle? region = null)
    {
        var r = region ?? new Rectangle(0, 0, tex.Width, tex.Height);
        var bmp = new FxBitmap(r.Width, r.Height);
        tex.GetData(0, r, bmp.Data, 0, bmp.Data.Length);
        return bmp;
    }

    /// <summary>The sprite's current frame.</summary>
    public static FxBitmap FromSprite(FlxSprite s)
    {
        if (s.Pixels == null) return new FxBitmap(Math.Max(1, (int)s.Width), Math.Max(1, (int)s.Height));
        Rectangle? r = s.Frames != null && s.Frames.Count > 0 ? s.Frames[Math.Clamp(s.Frame, 0, s.Frames.Count - 1)].Source : null;
        return FromTexture(s.Pixels, r);
    }

    public FxBitmap Clone()
    {
        var b = new FxBitmap(Width, Height);
        Array.Copy(Data, b.Data, Data.Length);
        return b;
    }

    public void Flip(bool x, bool y)
    {
        if (x) for (int r = 0; r < Height; r++) Array.Reverse(Data, r * Width, Width);
        if (y) for (int r = 0; r < Height / 2; r++)
                for (int c = 0; c < Width; c++)
                    (Data[r * Width + c], Data[(Height - 1 - r) * Width + c]) = (Data[(Height - 1 - r) * Width + c], Data[r * Width + c]);
    }

    public void Clear(Color color) => Array.Fill(Data, color);

    public void FillRect(Rectangle rect, Color color)
    {
        rect = Rectangle.Intersect(rect, Bounds);
        for (int y = rect.Top; y < rect.Bottom; y++) Array.Fill(Data, color, y * Width + rect.Left, rect.Width);
    }

    public void SetPixel(int x, int y, Color c)
    {
        if ((uint)x < (uint)Width && (uint)y < (uint)Height) Data[y * Width + x] = c;
    }

    /// <summary>Draws part of another bitmap here (source-over; for opaque pixels that is a plain copy). Clipped on both sides.</summary>
    public void CopyPixels(FxBitmap src, Rectangle srcRect, Point dst) => CopyPixels(src, srcRect, dst, 1f);

    /// <summary>Same, with the source faded by <paramref name="opacity"/> (0..1). With replace the pixels (alpha too) overwrite what is there, like Flash's copyPixels, instead of being laid over it.</summary>
    public void CopyPixels(FxBitmap src, Rectangle srcRect, Point dst, float opacity, bool replace = false)
    {
        // clip the source rectangle to the source, then the destination to this canvas
        var s = Rectangle.Intersect(srcRect, src.Bounds);
        dst.X += s.X - srcRect.X;
        dst.Y += s.Y - srcRect.Y;
        if (dst.X < 0) { s.X -= dst.X; s.Width += dst.X; dst.X = 0; }
        if (dst.Y < 0) { s.Y -= dst.Y; s.Height += dst.Y; dst.Y = 0; }
        s.Width = Math.Min(s.Width, Width - dst.X);
        s.Height = Math.Min(s.Height, Height - dst.Y);
        if (s.Width <= 0 || s.Height <= 0) return;

        for (int y = 0; y < s.Height; y++)
        {
            int si = (s.Y + y) * src.Width + s.X, di = (dst.Y + y) * Width + dst.X;
            for (int x = 0; x < s.Width; x++, si++, di++)
            {
                var p = src.Data[si];
                if (opacity < 1f) p = new Color((int)(p.R * opacity), (int)(p.G * opacity), (int)(p.B * opacity), (int)(p.A * opacity));
                if (replace || p.A == 255) Data[di] = p;
                else if (p.A != 0)
                {
                    var d = Data[di];
                    int inv = 255 - p.A;
                    Data[di] = new Color(p.R + d.R * inv / 255, p.G + d.G * inv / 255, p.B + d.B * inv / 255, p.A + d.A * inv / 255);
                }
            }
        }
    }

    /// <summary>A box blur (like Flash's BlurFilter); <paramref name="quality"/> is the number of passes.</summary>
    public void Blur(float blurX, float blurY, int quality = 1)
    {
        int wx = Math.Max(1, (int)MathF.Round(blurX)), wy = Math.Max(1, (int)MathF.Round(blurY));
        var line = new Color[Math.Max(Width, Height)];
        for (int q = 0; q < Math.Max(1, quality); q++)
        {
            if (wx > 1) for (int y = 0; y < Height; y++) BoxLine(y * Width, 1, Width, wx, line);
            if (wy > 1) for (int x = 0; x < Width; x++) BoxLine(x, Width, Height, wy, line);
        }
    }

    void BoxLine(int start, int step, int count, int w, Color[] tmp)
    {
        int before = w / 2;
        int r = 0, g = 0, b = 0, a = 0;
        // window for output i covers inputs i-before .. i-before+w-1 (outside = transparent)
        void Add(int i, int sign)
        {
            if ((uint)i >= (uint)count) return;
            var c = Data[start + i * step];
            r += sign * c.R; g += sign * c.G; b += sign * c.B; a += sign * c.A;
        }
        for (int i = -before; i < -before + w - 1; i++) Add(i, 1);
        for (int i = 0; i < count; i++)
        {
            Add(i - before + w - 1, 1);
            tmp[i] = new Color(r / w, g / w, b / w, a / w);
            Add(i - before, -1);
        }
        for (int i = 0; i < count; i++) Data[start + i * step] = tmp[i];
    }
}

/// <summary>Color helpers the effects use (Flixel's FlxColor / FlxGradient array functions).</summary>
public static class FxColor
{
    /// <summary>A random int from 0 up to (not including) n.</summary>
    internal static int Below(int n) => FlxG.Random.Int(0, Math.Max(1, n) - 1);

    /// <summary>0xAARRGGBB to a Color.</summary>
    public static Color Argb(uint v) => new((int)((v >> 16) & 255), (int)((v >> 8) & 255), (int)(v & 255), (int)((v >> 24) & 255));

    /// <summary>A color from hue (0-360), saturation and brightness (0-1).</summary>
    public static Color FromHsb(float hue, float saturation, float brightness)
    {
        float h = (hue % 360f) / 60f, f = h - MathF.Floor(h);
        float v = brightness, p = v * (1 - saturation), q = v * (1 - saturation * f), t = v * (1 - saturation * (1 - f));
        (float r, float g, float b) = (int)h switch { 0 => (v, t, p), 1 => (q, v, p), 2 => (p, v, t), 3 => (p, q, v), 4 => (t, p, v), _ => (v, p, q) };
        return new Color(r, g, b);
    }

    /// <summary>A hue wheel of <paramref name="count"/> colors (360 by default) at full saturation and brightness.</summary>
    public static Color[] HsvColorWheel(int count = 360, int alpha = 255)
    {
        var wheel = new Color[count];
        for (int i = 0; i < count; i++)
        {
            float h = i * 6f / count;
            int sector = (int)h;
            float f = h - sector;
            int q = (int)(255 * (1 - f)), t = (int)(255 * f);
            (int r, int g, int b) = sector switch
            {
                0 => (255, t, 0), 1 => (q, 255, 0), 2 => (0, 255, t), 3 => (0, q, 255), 4 => (t, 0, 255), _ => (255, 0, q),
            };
            wheel[i] = new Color(r, g, b, alpha);
        }
        return wheel;
    }

    /// <summary>A strip of <paramref name="length"/> colors running through the given colors.</summary>
    public static Color[] Gradient(int length, params Color[] colors) => FlxGradient.CreatePixels(1, length, colors);
}

/// <summary>The sprite an effect draws into: it runs the effect every frame.</summary>
public sealed class FxSprite : FlxSprite
{
    readonly FlxSpecialFXBase _fx;
    public FxSprite(FlxSpecialFXBase fx, float x, float y) : base(x, y) { _fx = fx; Moves = false; }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);
        if (_fx.Active) _fx.Step(elapsed);
    }
}

/// <summary>What every effect has: a canvas, the sprite that shows it, and a start/stop switch.</summary>
public abstract class FlxSpecialFXBase
{
    /// <summary>Runs the effect (it still shows when stopped, it just stops changing).</summary>
    public bool Active;
    public FlxSprite Sprite = null!;

    protected FxBitmap Canvas = null!;
    protected FxBitmap? Image;
    protected FlxSprite? SourceRef;
    protected bool UpdateFromSource;
    protected Color ClearColor = Color.Transparent;
    protected int UpdateLimit, LastUpdate;
    Texture2D _tex = null!;

    /// <summary>Redraw only every <paramref name="delay"/>+1 frames (0 = every frame).</summary>
    public void Start(int delay = 0) { UpdateLimit = Math.Max(0, delay); LastUpdate = 0; Active = true; }
    public void Stop() => Active = false;

    /// <summary>Called by the sprite each frame while Active.</summary>
    public abstract void Step(float elapsed);

    protected FlxSprite MakeSprite(float x, float y, int width, int height, Color? fill = null)
    {
        Canvas = new FxBitmap(width, height, fill);
        _tex = new Texture2D(FlxG.GraphicsDevice, Canvas.Width, Canvas.Height);
        _tex.SetData(Canvas.Data);
        Sprite = new FxSprite(this, x, y).LoadGraphic(_tex);
        Active = true;
        return Sprite;
    }

    protected void Upload() => _tex.SetData(Canvas.Data);

    /// <summary>True when this frame should be skipped because of the Start(delay) setting.</summary>
    protected bool Skip()
    {
        if (LastUpdate != UpdateLimit) { LastUpdate++; return true; }
        LastUpdate = 0;
        return false;
    }

    protected FxBitmap Source(FlxSprite s) => FxBitmap.FromSprite(s);

    public virtual void Destroy()
    {
        Active = false;
        Sprite?.Kill();
        _tex?.Dispose();
        SourceRef = null;
    }
}

/// <summary>Classic plasma, drawn from sine tables. Runs without a source image.</summary>
public sealed class PlasmaFX : FlxSpecialFXBase
{
    public int Pos1, Pos2 = 5, Pos3, Pos4;
    public int Depth = 128;
    Color[] _colors = Array.Empty<Color>();
    readonly int[] _sin = new int[512];
    int _span;

    /// <summary>Scale makes the plasma chunkier (2 = every plasma pixel is 2x2); (x, y) is the top-left of the scaled result.</summary>
    public FlxSprite Create(int x, int y, int width, int height, int scaleX = 1, int scaleY = 1, Color[]? palette = null)
    {
        MakeSprite(x, y, width, height);
        if (scaleX != 1 || scaleY != 1) { Sprite.Origin = Vector2.Zero; Sprite.Scale = new Vector2(scaleX, scaleY); }
        _colors = palette ?? FxColor.HsvColorWheel();
        _span = _colors.Length - 1;
        for (int i = 0; i < 512; i++) _sin[i] = (int)(MathF.Sin(i * 0.703125f * 0.0174532f) * 1024);
        return Sprite;
    }

    public override void Step(float elapsed)
    {
        int tpos3 = Pos3, tpos4 = Pos4;
        for (int y = 0; y < Canvas.Height; y++)
        {
            int tpos1 = Pos1 + 5, tpos2 = (Pos2 + 3) & 511;
            tpos3 &= 511;
            for (int x = 0; x < Canvas.Width; x++)
            {
                tpos1 &= 511; tpos2 &= 511;
                int x2 = _sin[tpos1] + _sin[tpos2] + _sin[tpos3] + _sin[tpos4 & 511];
                int index = Depth + (x2 >> 4);
                if (index <= 0) index += _span;
                if (index >= _span) index -= _span;
                Canvas.Data[y * Canvas.Width + x] = _colors[Math.Clamp(index, 0, _span)];
                tpos1 += 5; tpos2 += 3;
            }
            tpos3 += 1; tpos4 += 3;
        }
        Upload();
        Pos1 += 4;   // horizontal shift
        Pos3 += 2;   // vertical shift
    }
}

/// <summary>A scrolling line of cycling colors (a raster bar / rainbow line).</summary>
public sealed class RainbowLineFX : FlxSpecialFXBase
{
    Color[] _line = Array.Empty<Color>();
    int _max, _current, _speed, _chunk;
    bool _reverse;

    /// <param name="colors">Colors to run through (null: the full hue wheel).</param>
    /// <param name="colorWidth">How many pixels the colors are stretched over.</param>
    /// <param name="stepSize">Pixel width of each color block.</param>
    /// <param name="fadeWidth">Length of the fade to/from the fade color at each end (0: none).</param>
    public FlxSprite Create(int x, int y, int width, int height = 1, Color[]? colors = null, int colorWidth = 360, int colorSpeed = 1,
        int stepSize = 1, int fadeWidth = 128, Color? fadeColor = null)
    {
        MakeSprite(x, y, width, height);
        SetColors(colors, colorWidth, true, fadeWidth, fadeColor ?? Color.Black);
        _speed = colorSpeed;
        _chunk = Math.Max(1, stepSize);
        return Sprite;
    }

    public void SetColors(Color[]? colors, int colorWidth = 360, bool resetCurrent = false, int fadeWidth = 128, Color? fadeColor = null)
    {
        var line = colors != null ? FxColor.Gradient(colorWidth, colors) : FxColor.HsvColorWheel();
        if (fadeWidth != 0)
        {
            var fade = fadeColor ?? Color.Black;
            var a = FxColor.Gradient(fadeWidth, fade, fade, fade, line[0]);
            var b = FxColor.Gradient(fadeWidth, line[^1], fade, fade, fade, fade);
            var all = new Color[a.Length + line.Length + b.Length];
            a.CopyTo(all, 0); line.CopyTo(all, a.Length); b.CopyTo(all, a.Length + line.Length);
            line = all;
        }
        _line = line;
        _max = line.Length - 1;
        if (_current > _max || resetCurrent) _current = 0;
    }

    public int ColorSpeed { get => _speed; set { if (value < _max) _speed = value; } }
    public int StepSize { get => _chunk; set { if (value > 0 && value < Canvas.Width) { Canvas.Clear(Color.Transparent); _chunk = value; } } }
    /// <summary>0 scrolls forward, 1 backward.</summary>
    public void SetDirection(int direction) => _reverse = direction == 1;

    public override void Step(float elapsed)
    {
        for (int x = 0; x < Canvas.Width; x += _chunk)
            Canvas.FillRect(new Rectangle(x, 0, _chunk, Canvas.Height), _line[FlxMath.Wrap(_current + x, 1, _max)]);
        if (!_reverse) { _current += _speed; if (_current >= _max) _current = 0; }
        else { _current -= _speed; if (_current < 0) _current = _max; }
        Upload();
    }
}

/// <summary>Fills a picture in by repeating its bottom rows downwards (the "flood fill" demo effect).</summary>
public sealed class FloodFillFX : FlxSpecialFXBase
{
    public bool Complete { get; private set; }
    int _offset, _dropY;
    Rectangle _dropRect;

    public FlxSprite Create(Texture2D source, int x, int y, int width, int height, int pixels = 1, Color? backgroundColor = null) =>
        Create(FxBitmap.FromTexture(source), x, y, width, height, pixels, backgroundColor);

    public FlxSprite Create(FlxSprite source, int x, int y, int width, int height, int pixels = 1, Color? backgroundColor = null) =>
        Create(FxBitmap.FromSprite(source), x, y, width, height, pixels, backgroundColor);

    FlxSprite Create(FxBitmap source, int x, int y, int width, int height, int pixels, Color? bg)
    {
        ClearColor = bg ?? Color.Transparent;
        MakeSprite(x, y, width, height, ClearColor);
        if (source.Width != width || source.Height != height)
        {
            // a smaller picture sits at the bottom
            Image = new FxBitmap(width, height, ClearColor);
            Image.CopyPixels(source, source.Bounds, new Point(0, height - source.Height), 1f, replace: true);
        }
        else Image = source;
        _offset = Math.Max(1, pixels);
        _dropRect = new Rectangle(0, height - _offset, width, _offset);
        _dropY = height;
        return Sprite;
    }

    public override void Step(float elapsed)
    {
        if (Complete || Skip()) return;
        for (int y = 0; y < _dropY; y += _offset) Canvas.CopyPixels(Image!, _dropRect, new Point(0, y), 1f, replace: true);   // a straight copy: laying soft edge pixels over themselves again and again would turn them solid black
        _dropY -= _offset;
        _dropRect.Y -= _offset;
        if (_dropY <= 0) Complete = true;
        Upload();
    }
}

/// <summary>Draws a picture one row at a time, each shifted sideways by a random amount (a broken-TV glitch).</summary>
public sealed class GlitchFX : FlxSpecialFXBase
{
    int _glitchSize, _glitchSkip;

    /// <param name="maxGlitch">Most pixels a row may shift sideways (the sprite is this much wider than the source).</param>
    /// <param name="maxSkip">Most rows glitched together as one band.</param>
    /// <param name="autoUpdate">Re-read the source sprite's current frame every redraw (for animated sources).</param>
    public FlxSprite CreateFromFlxSprite(FlxSprite source, int maxGlitch, int maxSkip, bool autoUpdate = false, Color? backgroundColor = null)
    {
        Image = Source(source);
        return Create(source.X, source.Y, maxGlitch, maxSkip, backgroundColor, autoUpdate ? source : null);
    }

    public FlxSprite CreateFromTexture(Texture2D source, int x, int y, int maxGlitch, int maxSkip, Color? backgroundColor = null)
    {
        Image = FxBitmap.FromTexture(source);
        return Create(x, y, maxGlitch, maxSkip, backgroundColor, null);
    }

    FlxSprite Create(float x, float y, int maxGlitch, int maxSkip, Color? bg, FlxSprite? autoSource)
    {
        ClearColor = bg ?? Color.Transparent;
        MakeSprite(x, y, Image!.Width + maxGlitch, Image.Height, ClearColor);
        SourceRef = autoSource;
        UpdateFromSource = autoSource != null;
        _glitchSize = maxGlitch;
        _glitchSkip = maxSkip;
        return Sprite;
    }

    public void ChangeGlitchValues(int maxGlitch, int maxSkip) { _glitchSize = maxGlitch; _glitchSkip = maxSkip; }

    public override void Step(float elapsed)
    {
        if (Skip()) return;
        if (UpdateFromSource && SourceRef is { Exists: true }) Image = Source(SourceRef);
        Canvas.Clear(ClearColor);
        int skip = 1 + FxColor.Below(Math.Max(1, _glitchSkip));
        for (int y = 0; y < Canvas.Height; y += skip)
            Canvas.CopyPixels(Image!, new Rectangle(0, y, Image!.Width, skip), new Point(FxColor.Below(Math.Max(1, _glitchSize)), y));
        Upload();
    }
}

public enum SineWaveType { VerticalSine, VerticalCosine, HorizontalSine, HorizontalCosine }

/// <summary>Waves a picture: every column (or row) is shifted by a sine, and the wave scrolls.</summary>
public sealed class SineWaveFX : FlxSpecialFXBase
{
    bool _vertical;
    int _waveSize, _chunk;
    float[] _data = Array.Empty<float>();
    int _shift, _counter;
    public Action? LoopComplete;

    public FlxSprite CreateFromFlxSprite(FlxSprite source, SineWaveType type, int size, int length, int frequency = 2, int pixelsPerChunk = 1,
        bool updateFrame = false, Color? backgroundColor = null)
    {
        Image = Source(source);
        var s = Create(source.X, source.Y, type, size, length, frequency, pixelsPerChunk, backgroundColor);
        if (updateFrame) { SourceRef = source; UpdateFromSource = true; }
        return s;
    }

    public FlxSprite CreateFromTexture(Texture2D source, int x, int y, SineWaveType type, int size, int length, int frequency = 2,
        int pixelsPerChunk = 1, Color? backgroundColor = null)
    {
        Image = FxBitmap.FromTexture(source);
        return Create(x, y, type, size, length, frequency, pixelsPerChunk, backgroundColor);
    }

    FlxSprite Create(float x, float y, SineWaveType type, int size, int length, int frequency, int chunk, Color? bg)
    {
        _vertical = type is SineWaveType.VerticalSine or SineWaveType.VerticalCosine;
        if (_vertical && chunk >= Image!.Width) throw new ArgumentException("pixelsPerChunk cannot be >= the width for a vertical wave.");
        if (!_vertical && chunk >= Image!.Height) throw new ArgumentException("pixelsPerChunk cannot be >= the height for a horizontal wave.");
        UpdateWaveData(type, size, length, frequency, chunk);
        ClearColor = bg ?? Color.Transparent;
        MakeSprite(x, y, _vertical ? Image!.Width : Image!.Width + _waveSize * 3, _vertical ? Image.Height + _waveSize * 3 : Image.Height, ClearColor);
        return Sprite;
    }

    /// <summary>Change the wave. Frequency must be even.</summary>
    public void UpdateWaveData(SineWaveType type, int size, int length, int frequency = 2, int pixelsPerChunk = 1)
    {
        if (frequency % 2 != 0) throw new ArgumentException("frequency must be an even number");
        _waveSize = size / 2;
        _chunk = Math.Max(1, pixelsPerChunk);
        int n = Math.Max(2, length / _chunk);
        // the same sine/cosine recurrence the original FlxMath.sinCosGenerator uses
        var sin = new float[n];
        var cos = new float[n];
        float s = _waveSize, c = _waveSize, frq = frequency * MathF.PI / n;
        for (int i = 0; i < n; i++)
        {
            s -= c * frq;
            c += s * frq;
            sin[i] = s; cos[i] = c;
        }
        _data = type is SineWaveType.VerticalCosine or SineWaveType.HorizontalCosine ? cos : sin;
        _shift = 0;
    }

    public override void Step(float elapsed)
    {
        if (Skip()) return;
        if (UpdateFromSource && SourceRef is { Exists: true }) Image = Source(SourceRef);
        Canvas.Clear(ClearColor);
        int s = 0;
        int baseOffset = _waveSize + _waveSize / 2;
        if (_vertical)
            for (int x = 0; x < Image!.Width; x += _chunk, s++)
                Canvas.CopyPixels(Image, new Rectangle(x, 0, _chunk, Image.Height), new Point(x, baseOffset + (int)Wave(s)));
        else
            for (int y = 0; y < Image!.Height; y += _chunk, s++)
                Canvas.CopyPixels(Image, new Rectangle(0, y, Image.Width, _chunk), new Point(baseOffset + (int)Wave(s), y));

        // the wave scrolls by rotating the table
        _shift = (_shift + 1) % _data.Length;
        if (++_counter == _data.Length) { _counter = 0; LoopComplete?.Invoke(); }
        Upload();
    }

    float Wave(int i) => _data[(i + _shift) % _data.Length];
}

public enum StarfieldType { Scroll2D = 1, Warp3D = 2 }

/// <summary>A field of stars: 2D scrolling at different speeds (parallax), or flying outwards from the center.</summary>
public sealed class StarfieldFX : FlxSpecialFXBase
{
    sealed class Star { public float X, Y, D = 1, Speed, R; public int Layer; }

    public int CenterX, CenterY;
    /// <summary>Scroll direction for the 2D field (pixels per frame at speed 1). Default: left.</summary>
    public float StarXOffset = -1, StarYOffset;
    /// <summary>Milliseconds between steps (0: every frame).</summary>
    public int UpdateInterval;

    readonly List<Star> _stars = new();
    StarfieldType _type;
    Color[] _depth = Array.Empty<Color>();
    float _sinceStep;

    public FlxSprite Create(int x, int y, int width, int height, int quantity = 200, StarfieldType type = StarfieldType.Scroll2D, int updateInterval = 20)
    {
        ClearColor = Color.Black;
        MakeSprite(x, y, width, height, ClearColor);
        _type = type;
        UpdateInterval = updateInterval;
        CenterX = width >> 1;
        CenterY = height >> 1;
        for (int i = 0; i < quantity; i++)
            _stars.Add(new Star
            {
                X = FxColor.Below(width), Y = FxColor.Below(height),
                Layer = type == StarfieldType.Scroll2D ? 1 + FxColor.Below(5) : 0,
                Speed = type == StarfieldType.Scroll2D ? 0 : FlxG.Random.Float(),
                R = (float)(FlxG.Random.Float() * Math.PI * 2),
            });
        _depth = type == StarfieldType.Scroll2D
            ? FxColor.Gradient(5, FxColor.Argb(0xff585858), FxColor.Argb(0xffF4F4F4))
            : FxColor.Gradient(300, FxColor.Argb(0xff292929), Color.White);
        return Sprite;
    }

    public void SetBackgroundColor(Color color) => ClearColor = color;

    /// <summary>Brightness range and number of speed layers for the 2D field.</summary>
    public void SetStarDepthColors(int depth, Color? lowest = null, Color? highest = null)
    {
        _depth = FxColor.Gradient(Math.Max(2, depth), lowest ?? FxColor.Argb(0xff585858), highest ?? FxColor.Argb(0xffF4F4F4));
        foreach (var s in _stars) s.Layer = 1 + FxColor.Below(depth);
    }

    public void SetStarSpeed(float xShift, float yShift) { StarXOffset = xShift; StarYOffset = yShift; }

    public override void Step(float elapsed)
    {
        _sinceStep += elapsed * 1000f;
        if (_sinceStep < UpdateInterval) return;
        _sinceStep = 0;

        Canvas.Clear(ClearColor);
        int w = Canvas.Width, h = Canvas.Height;
        foreach (var s in _stars)
        {
            if (_type == StarfieldType.Scroll2D)
            {
                s.X += StarXOffset * s.Layer;
                s.Y += StarYOffset * s.Layer;
                Canvas.SetPixel((int)s.X, (int)s.Y, _depth[Math.Clamp(s.Layer - 1, 0, _depth.Length - 1)]);
                if (s.X > w) s.X = 0; else if (s.X < 0) s.X = w;
                if (s.Y > h) s.Y = 0; else if (s.Y < 0) s.Y = h;
            }
            else
            {
                s.D *= 1.1f;
                s.X = CenterX + MathF.Cos(s.R) * s.D * s.Speed;
                s.Y = CenterY + MathF.Sin(s.R) * s.D * s.Speed;
                Canvas.SetPixel((int)s.X, (int)s.Y, Color.White);
                if (s.X < 0 || s.X > w || s.Y < 0 || s.Y > h)
                {
                    s.D = 1;
                    s.R = (float)(FlxG.Random.Float() * Math.PI * 2);
                    s.Speed = FlxG.Random.Float();
                }
            }
        }
        Upload();
    }
}

public enum CenterSlideMode { RevealVertical, RevealHorizontal, HideVertical, HideHorizontal }

/// <summary>Opens a picture from its middle outwards (or closes it to the middle), a few pixels per frame.</summary>
public sealed class CenterSlideFX : FlxSpecialFXBase
{
    public bool Complete { get; private set; }
    public Action? CompleteCallback;

    CenterSlideMode _mode;
    int _pixels;
    Rectangle _sideA, _sideB;
    Point _pointA, _pointB;

    public FlxSprite CreateFromFlxSprite(FlxSprite source, CenterSlideMode mode = CenterSlideMode.RevealVertical, int pixels = 1, Color? backgroundColor = null) =>
        Create(FxBitmap.FromSprite(source), source.X, source.Y, mode, pixels, backgroundColor);

    public FlxSprite CreateFromTexture(Texture2D source, int x, int y, CenterSlideMode mode = CenterSlideMode.RevealVertical, int pixels = 1, Color? backgroundColor = null) =>
        Create(FxBitmap.FromTexture(source), x, y, mode, pixels, backgroundColor);

    FlxSprite Create(FxBitmap source, float x, float y, CenterSlideMode mode, int pixels, Color? bg)
    {
        ClearColor = bg ?? Color.Transparent;
        MakeSprite(x, y, source.Width, source.Height, ClearColor);
        Image = source;
        _mode = mode;
        _pixels = Math.Max(1, pixels);
        int w = source.Width, h = source.Height;
        switch (mode)
        {
            case CenterSlideMode.RevealVertical:
                _sideA = new Rectangle(0, 0, w, _pixels); _sideB = new Rectangle(0, h - _pixels, w, _pixels);
                _pointA = _pointB = new Point(0, h / 2);
                break;
            case CenterSlideMode.RevealHorizontal:
                _sideA = new Rectangle(0, 0, _pixels, h); _sideB = new Rectangle(w - _pixels, 0, _pixels, h);
                _pointA = _pointB = new Point(w / 2, 0);
                break;
            case CenterSlideMode.HideVertical:
                ShowImage();
                _sideA = new Rectangle(0, 0, w, h / 2); _sideB = new Rectangle(0, h / 2, w, h - h / 2);
                _pointA = new Point(0, 0); _pointB = new Point(0, h / 2);
                break;
            case CenterSlideMode.HideHorizontal:
                ShowImage();
                _sideA = new Rectangle(0, 0, w / 2, h); _sideB = new Rectangle(w / 2, 0, w - w / 2, h);
                _pointA = new Point(0, 0); _pointB = new Point(w / 2, 0);
                break;
        }
        return Sprite;
    }

    void ShowImage() { Array.Copy(Image!.Data, Canvas.Data, Canvas.Data.Length); Upload(); }

    /// <summary>Turn a finished reveal around so the picture closes again.</summary>
    public void Reverse()
    {
        if (_mode == CenterSlideMode.RevealVertical) { _mode = CenterSlideMode.HideVertical; Complete = false; }
        else if (_mode == CenterSlideMode.RevealHorizontal) { _mode = CenterSlideMode.HideHorizontal; Complete = false; }
        else return;
        int w = Image!.Width, h = Image.Height;
        // restart from fully open
        if (_mode == CenterSlideMode.HideVertical)
        { _sideA = new Rectangle(0, 0, w, h / 2); _sideB = new Rectangle(0, h / 2, w, h - h / 2); _pointA = new Point(0, 0); _pointB = new Point(0, h / 2); }
        else
        { _sideA = new Rectangle(0, 0, w / 2, h); _sideB = new Rectangle(w / 2, 0, w - w / 2, h); _pointA = new Point(0, 0); _pointB = new Point(w / 2, 0); }
    }

    public override void Step(float elapsed)
    {
        if (Complete || Skip()) return;
        Canvas.Clear(ClearColor);
        Canvas.CopyPixels(Image!, _sideA, _pointA);
        Canvas.CopyPixels(Image!, _sideB, _pointB);

        switch (_mode)
        {
            case CenterSlideMode.RevealVertical:
                _sideA.Height += _pixels; _pointA.Y -= _pixels; _sideB.Height += _pixels; _sideB.Y -= _pixels; break;
            case CenterSlideMode.RevealHorizontal:
                _sideA.Width += _pixels; _pointA.X -= _pixels; _sideB.Width += _pixels; _sideB.X -= _pixels; break;
            case CenterSlideMode.HideVertical:
                _sideA.Height -= _pixels; _pointA.Y += _pixels; _sideB.Height -= _pixels; _sideB.Y += _pixels; break;
            case CenterSlideMode.HideHorizontal:
                _sideA.Width -= _pixels; _pointA.X += _pixels; _sideB.Width -= _pixels; _sideB.X += _pixels; break;
        }

        if ((_mode == CenterSlideMode.RevealVertical && _pointA.Y < 0) || (_mode == CenterSlideMode.RevealHorizontal && _pointA.X < 0))
        {
            Array.Copy(Image!.Data, Canvas.Data, Canvas.Data.Length);
            Complete = true;
        }
        else if ((_mode == CenterSlideMode.HideVertical && _sideA.Height <= 0) || (_mode == CenterSlideMode.HideHorizontal && _sideA.Width <= 0))
        {
            Canvas.Clear(ClearColor);
            Complete = true;
        }
        Upload();
        if (Complete) CompleteCallback?.Invoke();
    }
}

/// <summary>
/// A blur canvas: sprites you add are stamped onto it every frame and the whole canvas is blurred, so they leave soft trails.
/// Stamped sprites should keep their normal world position (the canvas sits at 0,0 of the screen).
/// </summary>
public sealed class BlurFX : FlxSpecialFXBase
{
    readonly List<(FlxSprite Sprite, bool AutoRemove)> _objects = new();
    float _blurX, _blurY;
    int _quality;

    public FlxSprite Create(int width, int height, float blurX, float blurY, int blurQuality = 1)
    {
        MakeSprite(0, 0, width, height);
        _blurX = blurX; _blurY = blurY; _quality = blurQuality;
        Active = false;   // nothing to do until a sprite is added
        return Sprite;
    }

    public void AddSprite(FlxSprite source, bool autoRemove = true)
    {
        _objects.Add((source, autoRemove));
        Active = true;
    }

    public void RemoveSprite(FlxSprite source) => _objects.RemoveAll(o => o.Sprite == source);

    public override void Step(float elapsed)
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            var (s, auto) = _objects[i];
            if (!s.Exists) { if (auto) _objects.RemoveAt(i); continue; }
            if (!s.Visible) continue;
            var img = FxBitmap.FromSprite(s);
            Canvas.CopyPixels(img, img.Bounds, new Point((int)(s.X - s.Offset.X), (int)(s.Y - s.Offset.Y)));
        }
        // the delay controls how often the blur runs, not how often sprites are drawn
        if (Skip()) { Upload(); return; }
        Canvas.Blur(_blurX, _blurY, _quality);
        Upload();
    }
}

/// <summary>The copper-bar effect from the Amiga/Atari scene: colored bars that slide up and down the screen.</summary>
public sealed class WowCopperFX : FlxSpecialFXBase
{
    static readonly Color[] Palette = BuildPalette();

    readonly Color[] _strip = new Color[64];
    int _tab, _delay;
    /// <summary>Height of each bar band, and how far down the bands go (default: the whole sprite).</summary>
    public int Amount = 8, Max;

    static Color[] BuildPalette()
    {
        // ramps up and down in magenta, yellow, cyan and white, each followed by a gap of near-black
        var list = new List<Color>();
        void Ramp(int rm, int gm, int bm)
        {
            for (int i = 1; i <= 15; i++) list.Add(new Color(rm * i * 17, gm * i * 17, bm * i * 17));
            for (int i = 15; i >= 1; i--) list.Add(new Color(rm * i * 17, gm * i * 17, bm * i * 17));
            // note the original lists the peak twice and then 10 near-black entries
            for (int i = 0; i < 10; i++) list.Add(new Color(0, 0, 1));
        }
        Ramp(1, 0, 1); Ramp(1, 1, 0); Ramp(0, 1, 1); Ramp(1, 1, 1);
        return list.ToArray();
    }

    public FlxSprite Create(int x, int y, int width, int height)
    {
        MakeSprite(x, y, width, height);
        Max = height;
        Array.Fill(_strip, new Color(0, 1, 0));
        return Sprite;
    }

    public override void Step(float elapsed)
    {
        _strip[31] = Palette[_tab];
        if (++_delay >= 2)
        {
            // scroll the strip up one row (the bottom fills with near-black)
            Array.Copy(_strip, 1, _strip, 0, _strip.Length - 1);
            _strip[^1] = new Color(0, 1, 0);
            _tab++;
            _delay = 0;
        }
        if (_tab >= Palette.Length) _tab = 0;

        // each band is filled from the strip, shifted 7 rows further than the one above it
        int band = 0;
        for (int i = 0; i < Max; i += Amount, band++)
            for (int y = i; y < Math.Min(i + Amount, Canvas.Height); y++)
            {
                var c = _strip[FlxMath.Wrap(y - 7 * band, 0, 63) % 64];
                Array.Fill(Canvas.Data, c, y * Canvas.Width, Canvas.Width);
            }
        Upload();
    }
}

/// <summary>
/// Entry point: make effects with the factories, Add() the sprite each one's Create gives you to a state, and control them all
/// from here. Call <see cref="Clear"/> when you leave a state that used them.
/// </summary>
public static class FlxSpecialFX
{
    static readonly List<FlxSpecialFXBase> Members = new();

    static T Register<T>(T fx) where T : FlxSpecialFXBase { Members.Add(fx); return fx; }

    public static PlasmaFX Plasma() => Register(new PlasmaFX());
    public static RainbowLineFX RainbowLine() => Register(new RainbowLineFX());
    public static FloodFillFX FloodFill() => Register(new FloodFillFX());
    public static SineWaveFX SineWave() => Register(new SineWaveFX());
    public static GlitchFX Glitch() => Register(new GlitchFX());
    public static StarfieldFX Starfield() => Register(new StarfieldFX());
    public static BlurFX Blur() => Register(new BlurFX());
    public static CenterSlideFX CenterSlide() => Register(new CenterSlideFX());
    public static WowCopperFX WowCopper() => Register(new WowCopperFX());

    /// <summary>Start every effect, or only those of type T.</summary>
    public static void StartFX<T>() where T : FlxSpecialFXBase { foreach (var m in Members) if (m is T) m.Active = true; }
    public static void StartFX() { foreach (var m in Members) m.Active = true; }
    public static void StopFX<T>() where T : FlxSpecialFXBase { foreach (var m in Members) if (m is T) m.Active = false; }
    public static void StopFX() { foreach (var m in Members) m.Active = false; }

    /// <summary>True if any effect of type T is running.</summary>
    public static bool IsActive<T>() where T : FlxSpecialFXBase => Members.Exists(m => m is T && m.Active);

    public static bool Remove(FlxSpecialFXBase fx)
    {
        if (!Members.Remove(fx)) return false;
        fx.Destroy();
        return true;
    }

    /// <summary>Destroy every effect (and kill their sprites).</summary>
    public static void Clear()
    {
        foreach (var m in Members.ToArray()) m.Destroy();
        Members.Clear();
    }
}
