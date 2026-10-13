using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>Number helpers. Port of Flixel's FlxMath.</summary>
public static class FlxMath
{
    public const float MinValue = float.Epsilon;
    public const float MaxValue = float.MaxValue;
    public const float Epsilon = 0.0000001f;

    /// <summary>Clamps a value between min and max.</summary>
    public static float Bound(float value, float min, float max) => value < min ? min : value > max ? max : value;
    public static int Bound(int value, int min, int max) => value < min ? min : value > max ? max : value;

    /// <summary>Linear interpolation: ratio 0 gives a, 1 gives b.</summary>
    public static float Lerp(float a, float b, float ratio) => a + (b - a) * ratio;

    /// <summary>
    /// Frame-rate independent smoothing towards a target: <c>x = FlxMath.SmoothLerp(x, target, elapsed, 0.2f)</c> closes
    /// roughly 63% of the gap every <paramref name="timeConstant"/> seconds, whatever the frame rate.
    /// </summary>
    public static float SmoothLerp(float current, float target, float elapsed, float timeConstant) =>
        timeConstant <= 0 ? target : target + (current - target) * MathF.Exp(-elapsed / timeConstant);

    /// <summary>True if min &lt;= value &lt;= max.</summary>
    public static bool InBounds(float value, float min, float max) => value >= min && value <= max;

    public static bool IsOdd(int n) => (n & 1) != 0;
    public static bool IsEven(int n) => (n & 1) == 0;

    /// <summary>-1, 0 or 1.</summary>
    public static int SignOf(float n) => n < 0 ? -1 : n > 0 ? 1 : 0;

    /// <summary>True if the two numbers are within epsilon of each other.</summary>
    public static bool IsEqual(float a, float b, float epsilon = Epsilon) => MathF.Abs(a - b) <= epsilon;

    /// <summary>Rounds to a number of decimals: RoundDecimal(3.14159f, 2) is 3.14.</summary>
    public static float RoundDecimal(float value, int decimals)
    {
        float mult = MathF.Pow(10, decimals);
        return MathF.Round(value * mult) / mult;
    }

    /// <summary>Maps a value from one range to another: Remap(5, 0, 10, 0, 100) is 50.</summary>
    public static float Remap(float value, float start1, float end1, float start2, float end2) =>
        start2 + (value - start1) * ((end2 - start2) / (end1 - start1));

    /// <summary>Wraps a value into [min, max]: Wrap(11, 0, 10) is 0 and Wrap(-1, 0, 10) is 10.</summary>
    public static int Wrap(int value, int min, int max)
    {
        int range = max - min + 1;
        if (value < min) value += range * ((min - value) / range + 1);
        return min + (value - min) % range;
    }

    public static float Wrap(float value, float min, float max)
    {
        float range = max - min;
        if (range <= 0) return min;
        float m = (value - min) % range;
        if (m < 0) m += range;
        return min + m;
    }

    /// <summary>Adds to a value but never above max (or below min).</summary>
    public static int MaxAdd(int value, int amount, int max, int min = 0) => Bound(value + amount, min, max);

    public static float Distance(float x1, float y1, float x2, float y2)
    {
        float dx = x1 - x2, dy = y1 - y2;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    public static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        float dx = x1 - x2, dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    public static float VectorLength(float dx, float dy) => MathF.Sqrt(dx * dx + dy * dy);

    public static float DotProduct(float ax, float ay, float bx, float by) => ax * bx + ay * by;

    public static bool PointInCoordinates(float px, float py, float rx, float ry, float rw, float rh) =>
        px >= rx && px < rx + rw && py >= ry && py < ry + rh;

    public static bool PointInRect(Vector2 p, Rectangle r) => PointInCoordinates(p.X, p.Y, r.X, r.Y, r.Width, r.Height);

    /// <summary>True if the mouse (in world coordinates) is inside the rectangle.</summary>
    public static bool MouseInRect(Rectangle r) => PointInRect(FlxG.Mouse.WorldPosition, r);

    // --- table based sin / cos: faster than MathF for lots of calls, accurate to about 0.01 ---

    const int TableSize = 3600;
    static readonly float[] SinTable = BuildSinTable();

    static float[] BuildSinTable()
    {
        var t = new float[TableSize];
        for (int i = 0; i < TableSize; i++) t[i] = MathF.Sin(i * MathF.PI * 2 / TableSize);
        return t;
    }

    /// <summary>Approximate sin of an angle in degrees.</summary>
    public static float FastSin(float degrees)
    {
        int i = (int)MathF.Round(degrees * (TableSize / 360f)) % TableSize;
        if (i < 0) i += TableSize;
        return SinTable[i];
    }

    /// <summary>Approximate cos of an angle in degrees.</summary>
    public static float FastCos(float degrees) => FastSin(degrees + 90f);
}

public static class FlxAngle
{
    public const float PiOver180 = MathF.PI / 180f;
    public const float RadToDeg = 180f / MathF.PI;

    public static float AsRadians(float degrees) => degrees * PiOver180;
    public static float AsDegrees(float radians) => radians * RadToDeg;

    /// <summary>Wraps an angle into -180..180.</summary>
    public static float WrapAngle(float degrees)
    {
        degrees %= 360f;
        if (degrees > 180f) degrees -= 360f;
        else if (degrees < -180f) degrees += 360f;
        return degrees;
    }

    /// <summary>Angle in degrees from one point to another (0 = right, 90 = down).</summary>
    public static float DegreesTo(Vector2 from, Vector2 to) =>
        MathHelper.ToDegrees(MathF.Atan2(to.Y - from.Y, to.X - from.X));

    /// <summary>Angle in degrees from the middle of one object to the middle of another.</summary>
    public static float AngleBetween(FlxObject from, FlxObject to) => DegreesTo(from.Midpoint, to.Midpoint);

    public static float AngleBetweenPoint(FlxObject from, Vector2 point) => DegreesTo(from.Midpoint, point);

    /// <summary>Angle in degrees from an object to the mouse (world coordinates).</summary>
    public static float AngleBetweenMouse(FlxObject from) => DegreesTo(from.Midpoint, FlxG.Mouse.WorldPosition);

    /// <summary>The smallest turn (-180..180) needed to go from one angle to another.</summary>
    public static float AngleDifference(float from, float to) => WrapAngle(to - from);

    /// <summary>A vector of the given length pointing in the given direction (degrees).</summary>
    public static Vector2 Polar(float length, float degrees)
    {
        float r = MathHelper.ToRadians(degrees);
        return new Vector2(MathF.Cos(r) * length, MathF.Sin(r) * length);
    }

    /// <summary>Rotates a point around a pivot.</summary>
    public static Vector2 RotatePoint(Vector2 point, Vector2 pivot, float degrees)
    {
        float r = MathHelper.ToRadians(degrees);
        float s = MathF.Sin(r), c = MathF.Cos(r);
        var d = point - pivot;
        return new Vector2(pivot.X + d.X * c - d.Y * s, pivot.Y + d.X * s + d.Y * c);
    }

    /// <summary>Angle in degrees for a facing direction (Right = 0, Down = 90, Left = 180, Up = -90).</summary>
    public static float AngleFromFacing(FlxDirection facing) => facing switch
    {
        FlxDirection.Right => 0f,
        FlxDirection.Down => 90f,
        FlxDirection.Left => 180f,
        FlxDirection.Up => -90f,
        _ => 0f
    };
}

public static class FlxColor
{
    /// <summary>0xRRGGBB, e.g. FlxColor.Rgb(0xd8eba2).</summary>
    public static Color Rgb(int rgb) => new((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

    /// <summary>0xAARRGGBB.</summary>
    public static Color Argb(uint argb) => new((int)((argb >> 16) & 0xFF), (int)((argb >> 8) & 0xFF), (int)(argb & 0xFF), (int)(argb >> 24));

    /// <summary>
    /// Parses "#RRGGBB", "#AARRGGBB", "0xRRGGBB", "RRGGBB" or a color name such as "CornflowerBlue".
    /// Throws FormatException if it can't.
    /// </summary>
    public static Color FromString(string s)
    {
        string t = s.Trim();
        string hex = t.StartsWith('#') ? t[1..] : t.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? t[2..] : t;

        if ((hex.Length == 6 || hex.Length == 8) && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
            return hex.Length == 6 ? Rgb((int)v) : Argb(v);

        var prop = typeof(Color).GetProperty(t, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
        if (prop?.GetValue(null) is Color named) return named;

        throw new FormatException($"Can't read '{s}' as a color.");
    }

    /// <summary>"#RRGGBB", or "#AARRGGBB" with includeAlpha.</summary>
    public static string ToHex(Color c, bool includeAlpha = false) =>
        includeAlpha ? $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>Color from hue (0-360), saturation (0-1) and brightness (0-1).</summary>
    public static Color FromHsb(float hue, float saturation, float brightness, float alpha = 1f)
    {
        hue = ((hue % 360f) + 360f) % 360f;
        float c = brightness * saturation;
        float x = c * (1 - MathF.Abs(hue / 60f % 2 - 1));
        float m = brightness - c;

        (float r, float g, float b) = (int)(hue / 60f) switch
        {
            0 => (c, x, 0f),
            1 => (x, c, 0f),
            2 => (0f, c, x),
            3 => (0f, x, c),
            4 => (x, 0f, c),
            _ => (c, 0f, x)
        };
        return new Color(r + m, g + m, b + m, alpha);
    }

    /// <summary>Hue (0-360), saturation (0-1) and brightness (0-1) of a color.</summary>
    public static (float Hue, float Saturation, float Brightness) ToHsb(Color color)
    {
        float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f;
        float max = MathF.Max(r, MathF.Max(g, b)), min = MathF.Min(r, MathF.Min(g, b));
        float d = max - min;

        float h = d == 0 ? 0
            : max == r ? 60f * (((g - b) / d) % 6)
            : max == g ? 60f * ((b - r) / d + 2)
            : 60f * ((r - g) / d + 4);
        if (h < 0) h += 360f;
        return (h, max == 0 ? 0 : d / max, max);
    }

    public static Color Interpolate(Color a, Color b, float t) => Color.Lerp(a, b, MathHelper.Clamp(t, 0, 1));

    /// <summary>A list of colors blending from a to b (steps colors, both ends included).</summary>
    public static Color[] Gradient(Color a, Color b, int steps)
    {
        if (steps < 2) return new[] { a };
        var list = new Color[steps];
        for (int i = 0; i < steps; i++) list[i] = Color.Lerp(a, b, i / (float)(steps - 1));
        return list;
    }

    public static Color Lighten(Color c, float amount) => Color.Lerp(c, new Color(255, 255, 255, (int)c.A), MathHelper.Clamp(amount, 0, 1));
    public static Color Darken(Color c, float amount) => Color.Lerp(c, new Color(0, 0, 0, (int)c.A), MathHelper.Clamp(amount, 0, 1));
    public static Color Invert(Color c) => new(255 - c.R, 255 - c.G, 255 - c.B, (int)c.A);
    public static Color Multiply(Color a, Color b) => new(a.R * b.R / 255, a.G * b.G / 255, a.B * b.B / 255, (int)(a.A * b.A / 255));

    /// <summary>Same color with a different alpha (0-1).</summary>
    public static Color WithAlpha(Color c, float alpha) => new(c.R, c.G, c.B, (int)(MathHelper.Clamp(alpha, 0, 1) * 255));
}

public sealed class FlxRandom
{
    readonly Random _r;

    public FlxRandom(int? seed = null) { _r = seed.HasValue ? new Random(seed.Value) : new Random(); }

    public float Float() => _r.NextSingle();
    public float Float(float min, float max) => min + (max - min) * _r.NextSingle();

    /// <summary>Whole number from min to max, both included (like Flixel).</summary>
    public int Int(int min, int max) => min >= max ? min : _r.Next(min, max + 1);

    public bool Bool(float chancePercent = 50f) => _r.NextSingle() * 100f < chancePercent;

    /// <summary>A random element of the list.</summary>
    public T GetObject<T>(IReadOnlyList<T> list) => list[Int(0, list.Count - 1)];

    /// <summary>-1 or 1.</summary>
    public int Sign(float chancePercentPositive = 50f) => Bool(chancePercentPositive) ? 1 : -1;

    /// <summary>Shuffles a list in place.</summary>
    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _r.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
