using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

// Port of flixel-addons' FlxEffectSprite and its chainable effects (rainbow, outline, wave, glitch, trail, shake).
//
//     var fx = new FlxEffectSprite(flag);                       // flag is any sprite; it is not added to the state itself
//     fx.Effects.Add(new FlxWaveEffect());
//     fx.Effects.Add(new FlxOutlineEffect(FlxOutlineMode.Fast, Color.Red, 4));
//     Add(fx);
//
// Every frame the target's current picture goes through the active effects in order, and the result is what is drawn.
// Effects work on FxBitmap (see FlxSpecialFX.cs).

/// <summary>One step in an effect sprite's chain.</summary>
public interface IFlxEffect
{
    bool Active { get; set; }
    /// <summary>How far the effect moved the picture (e.g. an outline grows it in every direction); added to the sprite's position when drawn.</summary>
    Vector2 Offset { get; }
    void Update(float elapsed);
    /// <summary>Changes the picture (in place or as a new bitmap) and returns the result.</summary>
    FxBitmap Apply(FxBitmap bitmap);
}

/// <summary>Shows another sprite through a chain of effects. Position it (or tween it) instead of the target.</summary>
public class FlxEffectSprite : FlxSprite
{
    public bool EffectsEnabled = true;
    public FlxSprite Target { get; private set; }
    /// <summary>Advance the target's animation (the target is normally not in a state, so nothing else does).</summary>
    public bool UpdateTargetAnimation = true;
    public List<IFlxEffect> Effects;

    /// <summary>The picture as it is part-way down the chain (an effect like the trail reads it from here).</summary>
    public FxBitmap? Working { get; private set; }

    Vector2 _effectOffset;
    Texture2D? _tex;
    readonly Dictionary<(Texture2D, int), FxBitmap> _frameCache = new();

    public FlxEffectSprite(FlxSprite target, IEnumerable<IFlxEffect>? effects = null) : base(target.X, target.Y)
    {
        Target = target;
        Effects = effects != null ? new List<IFlxEffect>(effects) : new List<IFlxEffect>();
        Moves = false;
    }

    FxBitmap TargetPixels()
    {
        var t = Target;
        if (t.Pixels == null) return new FxBitmap(Math.Max(1, (int)t.Width), Math.Max(1, (int)t.Height));
        var key = (t.Pixels, t.Frame);
        if (!_frameCache.TryGetValue(key, out var bmp)) _frameCache[key] = bmp = FxBitmap.FromSprite(t);
        var copy = bmp.Clone();
        if (t.FlipX || t.FlipY) copy.Flip(t.FlipX, t.FlipY);
        return copy;
    }

    public override void Update(float elapsed)
    {
        if (UpdateTargetAnimation && Target.NumFrames > 1) Target.Animation.Update(elapsed);
        if (EffectsEnabled)
            foreach (var e in Effects)
                if (e.Active) e.Update(elapsed);
        base.Update(elapsed);
    }

    public override void Draw()
    {
        var bmp = TargetPixels();
        _effectOffset = Vector2.Zero;
        Working = bmp;
        if (EffectsEnabled)
            foreach (var e in Effects)
                if (e.Active)
                {
                    bmp = e.Apply(bmp);
                    Working = bmp;
                    _effectOffset += e.Offset;
                }

        if (_tex == null || _tex.Width != bmp.Width || _tex.Height != bmp.Height)
        {
            _tex?.Dispose();
            _tex = new Texture2D(FlxG.GraphicsDevice, bmp.Width, bmp.Height);
            LoadGraphic(_tex);
        }
        _tex.SetData(bmp.Data);

        // the effects' offset moves where the picture is drawn without moving the sprite
        var saved = Offset;
        Offset = saved - _effectOffset;
        Tint = Target.Tint;
        Alpha = Target.Alpha * Alpha;
        Alpha = Alpha > 1 ? 1 : Alpha;
        base.Draw();
        Offset = saved;
    }

    public override void Destroy()
    {
        foreach (var e in Effects) (e as IDisposable)?.Dispose();
        _tex?.Dispose();
        base.Destroy();
    }
}

/// <summary>Cycles the sprite's silhouette through a rainbow, blended over the picture.</summary>
public sealed class FlxRainbowEffect : IFlxEffect
{
    public bool Active { get; set; } = true;
    public Vector2 Offset => Vector2.Zero;
    /// <summary>Degrees of hue per frame.</summary>
    public float Speed;
    public float Alpha, Brightness;
    float _time;
    int _hue;

    public FlxRainbowEffect(float alpha = 1f, float brightness = 1f, float speed = 5f, int startHue = 0)
    {
        Alpha = alpha; Brightness = brightness; Speed = speed;
        _hue = Math.Clamp(startHue, 0, 360);
        _time = _hue;
    }

    public void Update(float elapsed)
    {
        _time += Speed;
        _hue = (int)_time;
        if (_hue > 360) { _hue = 0; _time -= 360; }
    }

    public FxBitmap Apply(FxBitmap bitmap)
    {
        var c = FxColor.FromHsb(_hue, 1f, Brightness);
        float a = Alpha;
        var d = bitmap.Data;
        for (int i = 0; i < d.Length; i++)
        {
            var p = d[i];
            if (p.A == 0) continue;
            // the hue colour, with the sprite's own alpha as a mask, over the picture
            float k = a * p.A / 255f;
            float inv = 1f - k;
            d[i] = new Color((int)(c.R * k + p.R * inv), (int)(c.G * k + p.G * inv), (int)(c.B * k + p.B * inv), p.A);
        }
        return bitmap;
    }
}

public enum FlxOutlineMode { Fast, Normal, PixelByPixel }

/// <summary>Draws a colored outline around the sprite.</summary>
public sealed class FlxOutlineEffect : IFlxEffect
{
    public bool Active { get; set; } = true;
    public Vector2 Offset { get; private set; }
    /// <summary>The outline is built once; set this to rebuild it (e.g. after changing color or thickness).</summary>
    public bool Dirty = true;
    public FlxOutlineMode Mode;
    public Color Color;
    public int Thickness, Threshold;
    public float Quality;

    FxBitmap? _pixels, _border;
    float _mx, _my;

    public FlxOutlineEffect(FlxOutlineMode mode = FlxOutlineMode.Fast, Color? color = null, int thickness = 1, int threshold = 0, float quality = 1f)
    {
        Mode = mode; Color = color ?? Color.White; Thickness = thickness; Threshold = threshold; Quality = quality;
    }

    public void Update(float elapsed) { }

    public FxBitmap Apply(FxBitmap bitmap)
    {
        if (Dirty)
        {
            int brush = Thickness * 2;
            _pixels = new FxBitmap(bitmap.Width + brush, bitmap.Height + brush);
            if (Mode == FlxOutlineMode.PixelByPixel)
            {
                for (int y = 0; y < bitmap.Height; y++)
                    for (int x = 0; x < bitmap.Width; x++)
                        if (bitmap.Data[y * bitmap.Width + x].A > Threshold) _pixels.FillRect(new Rectangle(x, y, brush, brush), Color);
            }
            else
            {
                // the sprite's silhouette in the outline color, stamped around the picture
                _border = new FxBitmap(bitmap.Width, bitmap.Height);
                for (int i = 0; i < _border.Data.Length; i++)
                {
                    float k = bitmap.Data[i].A / 255f;
                    _border.Data[i] = new Color((int)(Color.R * k), (int)(Color.G * k), (int)(Color.B * k), (int)(Color.A * k));
                }
                _mx = _my = Thickness;
                int iterations = Math.Max(1, (int)(Thickness * Quality));
                float delta = (float)Thickness / iterations, cur = delta;
                for (int i = 0; i < iterations; i++)
                {
                    if (Mode == FlxOutlineMode.Normal)
                    {
                        Stamp(-cur, -cur); Stamp(cur, 0); Stamp(cur, 0); Stamp(0, cur);
                        Stamp(0, cur); Stamp(-cur, 0); Stamp(-cur, 0); Stamp(0, -cur);
                        _mx += cur;
                    }
                    else
                    {
                        Stamp(-cur, -cur); Stamp(cur * 2, 0); Stamp(0, cur * 2); Stamp(-cur * 2, 0);
                        _mx += cur; _my -= cur;
                    }
                    cur += delta;
                }
            }
            Dirty = false;
        }
        if (_pixels == null) return bitmap;

        Offset = new Vector2(-Thickness, -Thickness);
        // the picture on top of the outline
        var result = _pixels.Clone();
        result.CopyPixels(bitmap, bitmap.Bounds, new Point(Thickness, Thickness));
        return result;
    }

    void Stamp(float dx, float dy)
    {
        _mx += dx; _my += dy;
        _pixels!.CopyPixels(_border!, _border!.Bounds, new Point((int)MathF.Round(_mx), (int)MathF.Round(_my)));
    }
}

public enum FlxWaveMode { All, Start, End }
public enum FlxWaveDirection { Horizontal, Vertical }

/// <summary>Ripples the picture: each row (or column) is shifted by a sine that moves over time.</summary>
public sealed class FlxWaveEffect : IFlxEffect
{
    public bool Active { get; set; } = true;
    public Vector2 Offset { get; private set; }
    /// <summary>All: the whole picture waves. Start / End: the wave fades out towards the end / start (relative to Center).</summary>
    public FlxWaveMode Mode;
    public float Speed, Center;
    public FlxWaveDirection Direction;
    public int Wavelength, Strength;
    float _interlace;
    /// <summary>0..1: shifts every other row/column out of phase.</summary>
    public float InterlaceOffset { get => _interlace; set => _interlace = Math.Clamp(value, 0f, 1f); }

    float _time;

    public FlxWaveEffect(FlxWaveMode mode = FlxWaveMode.All, int strength = 10, float center = 0.5f, float speed = 3f, int wavelength = 5,
        FlxWaveDirection direction = FlxWaveDirection.Horizontal, float interlaceOffset = 0f)
    {
        Strength = strength; Mode = mode; Speed = speed; Wavelength = wavelength; Direction = direction; Center = center; InterlaceOffset = interlaceOffset;
    }

    public void Update(float elapsed) => _time += elapsed * Speed;

    public FxBitmap Apply(FxBitmap bitmap)
    {
        bool horizontal = Direction == FlxWaveDirection.Horizontal;
        int hs = horizontal ? Strength : 0, vs = horizontal ? 0 : Strength;
        Offset = new Vector2(-hs, -vs);
        var result = new FxBitmap(bitmap.Width + hs * 2, bitmap.Height + vs * 2);

        int length = horizontal ? bitmap.Height : bitmap.Width;
        int p = 0;
        while (p < length)
        {
            float offsetP = length * Center;
            int size = 1;
            switch (Mode)
            {
                case FlxWaveMode.All:
                    offsetP = Strength;
                    break;
                case FlxWaveMode.Start:
                    if (p <= offsetP) offsetP = (1 - p / offsetP) * Strength;
                    else { size = length - p; offsetP = 0; }
                    break;
                case FlxWaveMode.End:
                    if (p >= offsetP) offsetP = (1 - (1 - (float)p / length) / (1 - Center)) * Strength;
                    else { size = (int)MathF.Ceiling(offsetP); offsetP = 0; }
                    break;
            }
            size = Math.Max(1, size);
            float pixelOffset = offsetP * Calc(p);
            if (horizontal) result.CopyPixels(bitmap, new Rectangle(0, p, bitmap.Width, size), new Point((int)(Strength + pixelOffset), p));
            else result.CopyPixels(bitmap, new Rectangle(p, 0, size, bitmap.Height), new Point(p, (int)(Strength + pixelOffset)));
            p += size;
        }
        return result;
    }

    float Calc(int p)
    {
        float phase = 0;
        if (_interlace != 0 && _interlace != 1 && (p & 1) != 0) phase = MathF.PI * 2 * _interlace;
        return MathF.Sin(p / (float)Wavelength + _time + phase);
    }
}

public enum FlxGlitchDirection { Horizontal, Vertical }

/// <summary>Shifts rows (or columns) of the picture by random amounts, refreshing every <see cref="Delay"/> seconds.</summary>
public sealed class FlxGlitchEffect : IFlxEffect
{
    public bool Active { get; set; } = true;
    public Vector2 Offset { get; private set; }
    /// <summary>Rows (or columns) per glitch band.</summary>
    public int Size;
    public float Delay;
    public FlxGlitchDirection Direction;
    /// <summary>Most pixels a band is shifted.</summary>
    public int Strength;

    float _time;
    FxBitmap? _pixels;

    public FlxGlitchEffect(int strength = 4, int size = 1, float delay = 0.05f, FlxGlitchDirection direction = FlxGlitchDirection.Horizontal)
    {
        Strength = strength; Size = size; Delay = delay; Direction = direction;
    }

    public void Update(float elapsed)
    {
        if (_time > Delay) _time = 0;
        else _time += elapsed;
    }

    public FxBitmap Apply(FxBitmap bitmap)
    {
        if (_time == 0)
        {
            bool horizontal = Direction == FlxGlitchDirection.Horizontal;
            int hs = horizontal ? Strength : 0, vs = horizontal ? 0 : Strength;
            Offset = new Vector2(-hs, -vs);
            _pixels = new FxBitmap(bitmap.Width + hs * 2, bitmap.Height + vs * 2);
            int step = Math.Max(1, Size);
            if (horizontal)
                for (int p = 0; p < bitmap.Height; p += step)
                    _pixels.CopyPixels(bitmap, new Rectangle(0, p, bitmap.Width, Math.Min(step, bitmap.Height - p)), new Point(FlxG.Random.Int(-Strength, Strength) + Strength, p));
            else
                for (int p = 0; p < bitmap.Width; p += step)
                    _pixels.CopyPixels(bitmap, new Rectangle(p, 0, Math.Min(step, bitmap.Width - p), bitmap.Height), new Point(p, FlxG.Random.Int(-Strength, Strength) + Strength));
        }
        // between refreshes the last glitched picture is shown, as in the original
        return _pixels != null ? _pixels.Clone() : bitmap;
    }
}

/// <summary>Shakes the sprite for a while after <see cref="Start"/> (it moves where the sprite is drawn, not the pixels).</summary>
public sealed class FlxShakeEffect : IFlxEffect
{
    // a shake rests until Start() is called; active is on by default like the others
    public bool Active { get; set; } = true;
    public Vector2 Offset { get; private set; }
    public float Intensity;
    public Action? OnComplete;
    public FlxAxes Axes;

    float _duration, _time;

    public FlxShakeEffect(float intensity = 5f, float duration = 0.5f, Action? onComplete = null, FlxAxes axes = FlxAxes.XY)
    {
        Intensity = intensity; _duration = duration; OnComplete = onComplete; Axes = axes;
    }

    public void Update(float elapsed)
    {
        if (_time <= 0) return;
        _time -= elapsed;
        if (_time > 0)
        {
            float x = Offset.X, y = Offset.Y;
            if (Axes != FlxAxes.Y) x = FlxG.Random.Float(-Intensity, Intensity);
            if (Axes != FlxAxes.X) y = FlxG.Random.Float(-Intensity, Intensity);
            Offset = new Vector2(x, y);
        }
        else
        {
            Offset = Vector2.Zero;
            OnComplete?.Invoke();
        }
    }

    public FxBitmap Apply(FxBitmap bitmap) => bitmap;

    /// <summary>Begin shaking (ignored while already shaking unless <paramref name="force"/>).</summary>
    public void Start(bool force = false)
    {
        if (!force && _time > 0) return;
        Reset(Intensity, _duration, OnComplete, Axes);
    }

    public void Reset(float intensity = 5f, float duration = 0.5f, Action? onComplete = null, FlxAxes axes = FlxAxes.XY)
    {
        Intensity = intensity; _duration = duration; _time = duration; OnComplete = onComplete; Axes = axes;
    }
}

/// <summary>Leaves a fading trail of the sprite's recent positions (the sprite has to move, e.g. by a tween).</summary>
public sealed class FlxTrailEffect : IFlxEffect, IDisposable
{
    public bool Active { get; set; } = true;
    public Vector2 Offset { get; private set; }
    public FlxEffectSprite Target { get; }
    int _length;
    public int Length
    {
        get => _length;
        set
        {
            _length = Math.Max(1, value);
            while (_positions.Count > _length)
            {
                _positions.RemoveAt(0);
                if (_pixels.Count > _positions.Count) _pixels.RemoveAt(0);
            }
        }
    }
    /// <summary>Opacity of the newest trail image.</summary>
    public float Alpha;
    /// <summary>Frames between recorded positions.</summary>
    public int FramesDelay;
    bool _cache;
    /// <summary>Remember what the sprite looked like at each position (otherwise the trail uses the current picture).</summary>
    public bool CachePixels { get => _cache; set { _cache = value; if (!value) _pixels.Clear(); } }

    readonly List<Vector2> _positions = new();
    readonly List<FxBitmap> _pixels = new();
    int _frames;

    public FlxTrailEffect(FlxEffectSprite target, int length = 10, float alpha = 0.5f, int framesDelay = 2, bool cachePixels = true)
    {
        Target = target;
        Length = length;
        Alpha = alpha;
        FramesDelay = framesDelay;
        CachePixels = cachePixels;
    }

    public void Update(float elapsed) { }

    void UpdateRecents()
    {
        if (++_frames < FramesDelay) return;
        if (_positions.Count >= _length) _positions.RemoveAt(0);
        _positions.Add(new Vector2(Target.X, Target.Y));
        if (_cache && Target.Working != null)
        {
            if (_pixels.Count >= _length) _pixels.RemoveAt(0);
            _pixels.Add(Target.Working.Clone());
        }
        _frames = 0;
    }

    public FxBitmap Apply(FxBitmap bitmap)
    {
        UpdateRecents();
        if (_positions.Count < 1) return bitmap;

        float minX = 0, maxX = 0, minY = 0, maxY = 0;
        int ww = bitmap.Width, hh = bitmap.Height;
        for (int i = 0; i < _positions.Count; i++)
        {
            if (_cache && i < _pixels.Count) { ww = _pixels[i].Width; hh = _pixels[i].Height; }
            float dx = _positions[i].X - Target.X, dy = _positions[i].Y - Target.Y;
            minX = Math.Min(dx, Math.Min(minX, 0));
            minY = Math.Min(dy, Math.Min(minY, 0));
            maxX = Math.Max(dx + ww, Math.Max(maxX, 0));
            maxY = Math.Max(dy + hh, Math.Max(maxY, 0));
        }
        Offset = new Vector2(minX, minY);
        if (!_cache && minX == 0 && minY == 0 && maxX == 0 && maxY == 0) return bitmap;

        var result = new FxBitmap(Math.Max(bitmap.Width, (int)(maxX - minX)), Math.Max(bitmap.Height, (int)(maxY - minY)));
        float alphaStep = Alpha / _positions.Count;
        for (int i = 0; i < _positions.Count; i++)
        {
            int tx = (int)(_positions[i].X - Target.X - Offset.X), ty = (int)(_positions[i].Y - Target.Y - Offset.Y);
            if (!_cache && tx == 0 && ty == 0) continue;
            var img = _cache && i < _pixels.Count ? _pixels[i] : bitmap;
            result.CopyPixels(img, img.Bounds, new Point(tx, ty), alphaStep * i);
        }
        result.CopyPixels(bitmap, bitmap.Bounds, new Point((int)-Offset.X, (int)-Offset.Y));
        return result;
    }

    /// <summary>Forget the recorded positions (e.g. after teleporting the sprite).</summary>
    public void Clear() { _positions.Clear(); _pixels.Clear(); }

    public void Dispose() => Clear();
}
