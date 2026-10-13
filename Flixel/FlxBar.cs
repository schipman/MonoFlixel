using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>Which way a bar fills up as its value grows.</summary>
public enum FlxBarFillDirection
{
    LeftToRight, RightToLeft, TopToBottom, BottomToTop,
    /// <summary>Grows from the centre towards both left and right edges.</summary>
    HorizontalInsideOut,
    /// <summary>Grows from both left and right edges towards the centre.</summary>
    HorizontalOutsideIn,
    VerticalInsideOut, VerticalOutsideIn
}

/// <summary>
/// A health / progress bar. Port of Flixel's FlxBar: set <see cref="Value"/> (between Min and Max) or give it a
/// function to read the value from every frame.
/// <code>
/// var bar = new FlxBar(10, 10, FlxBarFillDirection.LeftToRight, 100, 8, () => player.Health, 0, 100);
/// bar.CreateFilledBar(Color.DarkRed, Color.LimeGreen, showBorder: true);
/// bar.TrackParent(player, 0, -12);        // follow the player around
/// bar.OnEmpty = () => ...;
/// </code>
/// </summary>
public class FlxBar : FlxSprite
{
    public FlxBarFillDirection FillDirection;
    public float Min { get; private set; }
    public float Max { get; private set; }

    /// <summary>The bar draws in this many steps (default 100), so a huge bar still fills smoothly but a short one doesn't flicker.</summary>
    public int NumDivisions = 100;

    public bool ShowBorder;
    public Color BorderColor = Color.White;
    public Color EmptyColor = new(40, 40, 40);
    public Color FillColor = Color.LimeGreen;

    /// <summary>Called when the value reaches Min / Max.</summary>
    public Action? OnEmpty, OnFilled;

    /// <summary>If set, the value is read from this function every update.</summary>
    public Func<float>? ValueSource;

    Texture2D? _emptyTex, _fillTex;
    float _value;
    bool _wasEmpty, _wasFilled;
    FlxObject? _parent;
    Vector2 _parentOffset;

    public FlxBar(float x = 0, float y = 0, FlxBarFillDirection direction = FlxBarFillDirection.LeftToRight,
        int width = 100, int height = 10, Func<float>? valueSource = null, float min = 0, float max = 100, bool showBorder = false)
        : base(x, y)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("A bar needs a size.");
        FillDirection = direction;
        Width = width;
        Height = height;
        Min = min;
        Max = max;
        ShowBorder = showBorder;
        ValueSource = valueSource;
        Moves = false;
        ScrollFactor = Vector2.One;
        _value = min;
        if (valueSource != null) _value = Math.Clamp(valueSource(), Math.Min(min, max), Math.Max(min, max));
        _wasEmpty = _value <= Min;
        _wasFilled = _value >= Max;
    }

    // ------------------------------------------------------------------ value

    public float Value
    {
        get => _value;
        set
        {
            float lo = Math.Min(Min, Max), hi = Math.Max(Min, Max);
            _value = Math.Clamp(value, lo, hi);

            bool empty = _value <= Min, filled = _value >= Max;
            if (empty && !_wasEmpty) OnEmpty?.Invoke();
            if (filled && !_wasFilled) OnFilled?.Invoke();
            _wasEmpty = empty;
            _wasFilled = filled;
        }
    }

    /// <summary>The value as a percentage of the range, 0 to 100.</summary>
    public float Percent
    {
        get => Max == Min ? 0 : (_value - Min) / (Max - Min) * 100f;
        set => Value = Min + (Max - Min) * (value / 100f);
    }

    /// <summary>Changes the range; the value is clamped into it.</summary>
    public void SetRange(float min, float max)
    {
        if (max == min) throw new ArgumentException("A bar's range can't be empty.");
        Min = min;
        Max = max;
        Value = _value;
    }

    // ------------------------------------------------------------------ tracking

    /// <summary>Keeps the bar at the parent's position plus an offset (e.g. a health bar over an enemy's head).</summary>
    public void TrackParent(FlxObject parent, float offsetX = 0, float offsetY = 0)
    {
        _parent = parent;
        _parentOffset = new Vector2(offsetX, offsetY);
        ScrollFactor = parent.ScrollFactor;
    }

    public void StopTrackingParent() => _parent = null;

    // ------------------------------------------------------------------ look

    /// <summary>Plain colors for the empty and the filled part.</summary>
    public FlxBar CreateFilledBar(Color empty, Color fill, bool showBorder = false, Color? border = null)
    {
        EmptyColor = empty; FillColor = fill; ShowBorder = showBorder;
        if (border.HasValue) BorderColor = border.Value;
        _emptyTex = _fillTex = null;
        return this;
    }

    /// <summary>The filled part blends through the given colors along the bar's full length (revealed as the bar fills).</summary>
    public FlxBar CreateGradientBar(Color[] fillColors, Color empty, bool showBorder = false, Color? border = null)
    {
        if (fillColors.Length == 0) throw new ArgumentException("Give at least one color.", nameof(fillColors));
        EmptyColor = empty; ShowBorder = showBorder;
        if (border.HasValue) BorderColor = border.Value;
        _emptyTex = null;

        bool horizontal = IsHorizontal;
        int w = Math.Max(1, (int)Width - (ShowBorder ? 2 : 0)), h = Math.Max(1, (int)Height - (ShowBorder ? 2 : 0));
        var data = new Color[w * h];
        int len = horizontal ? w : h;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float t = len <= 1 ? 0 : (horizontal ? x : y) / (float)(len - 1);
                data[y * w + x] = Sample(fillColors, t);
            }
        _fillTex = new Texture2D(FlxG.GraphicsDevice, w, h);
        _fillTex.SetData(data);
        return this;
    }

    /// <summary>Images for the empty and the filled part (each is stretched/cropped to the bar's size; null keeps the color).</summary>
    public FlxBar CreateImageBar(Texture2D? empty, Texture2D? fill, Color? emptyBackground = null, Color? fillBackground = null)
    {
        _emptyTex = empty;
        _fillTex = fill;
        if (emptyBackground.HasValue) EmptyColor = emptyBackground.Value;
        if (fillBackground.HasValue) FillColor = fillBackground.Value;
        return this;
    }

    bool IsHorizontal => FillDirection is FlxBarFillDirection.LeftToRight or FlxBarFillDirection.RightToLeft
        or FlxBarFillDirection.HorizontalInsideOut or FlxBarFillDirection.HorizontalOutsideIn;

    static Color Sample(Color[] colors, float t)
    {
        if (colors.Length == 1) return colors[0];
        float f = Math.Clamp(t, 0f, 1f) * (colors.Length - 1);
        int i = Math.Min((int)f, colors.Length - 2);
        return Color.Lerp(colors[i], colors[i + 1], f - i);
    }

    // ------------------------------------------------------------------ update / draw

    public override void Update(float elapsed)
    {
        if (ValueSource != null) Value = ValueSource();

        if (_parent != null)
        {
            X = _parent.X + _parentOffset.X;
            Y = _parent.Y + _parentOffset.Y;
        }
        base.Update(elapsed);
    }

    public override void Draw()
    {
        if (Alpha <= 0) return;
        var cam = FlxG.Camera;
        var sb = FlxG.SpriteBatch;
        var px = FlxG.Pixel;

        int x = (int)MathF.Round(X - cam.ScrollX * ScrollFactor.X);
        int y = (int)MathF.Round(Y - cam.ScrollY * ScrollFactor.Y);
        int w = (int)Width, h = (int)Height;

        Color Fade(Color c) { c.A = (byte)(c.A * Math.Clamp(Alpha, 0f, 1f)); return c; }

        int b = ShowBorder ? 1 : 0;
        if (ShowBorder) sb.Draw(px, new Rectangle(x, y, w, h), Fade(BorderColor));

        // interior (inside the border)
        var inner = new Rectangle(x + b, y + b, Math.Max(1, w - 2 * b), Math.Max(1, h - 2 * b));

        if (_emptyTex != null) sb.Draw(_emptyTex, inner, Fade(Color.White));
        else sb.Draw(px, inner, Fade(EmptyColor));

        float frac = Max == Min ? 0 : Math.Clamp((_value - Min) / (Max - Min), 0f, 1f);
        int steps = Math.Max(1, NumDivisions);
        frac = MathF.Round(frac * steps) / steps;
        if (frac <= 0) return;

        foreach (var part in FillParts(inner, frac))
        {
            if (part.Width <= 0 || part.Height <= 0) continue;
            if (_fillTex != null)
            {
                // the matching piece of the fill image, scaled the same way the image fits the interior
                var src = new Rectangle(
                    (int)MathF.Round((part.X - inner.X) * _fillTex.Width / (float)inner.Width),
                    (int)MathF.Round((part.Y - inner.Y) * _fillTex.Height / (float)inner.Height),
                    Math.Max(1, (int)MathF.Round(part.Width * _fillTex.Width / (float)inner.Width)),
                    Math.Max(1, (int)MathF.Round(part.Height * _fillTex.Height / (float)inner.Height)));
                src.Width = Math.Min(src.Width, _fillTex.Width - src.X);
                src.Height = Math.Min(src.Height, _fillTex.Height - src.Y);
                sb.Draw(_fillTex, part, src, Fade(Color.White));
            }
            else sb.Draw(px, part, Fade(FillColor));
        }
    }

    /// <summary>The rectangles that make up the filled part, for a fill fraction of 0..1.</summary>
    Rectangle[] FillParts(Rectangle r, float frac)
    {
        int fw = (int)MathF.Round(r.Width * frac), fh = (int)MathF.Round(r.Height * frac);
        switch (FillDirection)
        {
            case FlxBarFillDirection.LeftToRight: return new[] { new Rectangle(r.X, r.Y, fw, r.Height) };
            case FlxBarFillDirection.RightToLeft: return new[] { new Rectangle(r.Right - fw, r.Y, fw, r.Height) };
            case FlxBarFillDirection.TopToBottom: return new[] { new Rectangle(r.X, r.Y, r.Width, fh) };
            case FlxBarFillDirection.BottomToTop: return new[] { new Rectangle(r.X, r.Bottom - fh, r.Width, fh) };
            case FlxBarFillDirection.HorizontalInsideOut: return new[] { new Rectangle(r.X + (r.Width - fw) / 2, r.Y, fw, r.Height) };
            case FlxBarFillDirection.VerticalInsideOut: return new[] { new Rectangle(r.X, r.Y + (r.Height - fh) / 2, r.Width, fh) };
            case FlxBarFillDirection.HorizontalOutsideIn:
            {
                int half = fw / 2;
                return new[] { new Rectangle(r.X, r.Y, half, r.Height), new Rectangle(r.Right - (fw - half), r.Y, fw - half, r.Height) };
            }
            default: // VerticalOutsideIn
            {
                int half = fh / 2;
                return new[] { new Rectangle(r.X, r.Y, r.Width, half), new Rectangle(r.X, r.Bottom - (fh - half), r.Width, fh - half) };
            }
        }
    }
}
