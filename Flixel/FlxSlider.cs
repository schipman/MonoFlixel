using System;
using Microsoft.Xna.Framework;

namespace MonoFlixel.Flixel;

/// <summary>A small horizontal mouse slider (stands in for flixel-addons' FlxSlider). Drag the handle or click the bar.</summary>
public class FlxSlider: FlxBasic
{
    public float Value;
    public float Min, Max;
    /// <summary>Where it is, and how long it is (across for a horizontal slider, down for a vertical one).</summary>
    public readonly float X, Y, Width;
    /// <summary>A vertical slider (a scroll bar): its Min is at the top, and it has no label or value text.</summary>
    public readonly bool Vertical;
    public readonly bool WholeNumbers;
    public readonly FlxText Label;
    public readonly FlxText ValueText;

    const int BarHeight = 4, HandleW = 6, HandleH = 14;
    bool _dragging;

    public FlxSlider(string name, float x, float y, float width, float min, float max, float value, bool wholeNumbers = false, bool vertical = false)
    {
        Vertical = vertical;
        X = x; Y = y; Width = width; Min = min; Max = max; Value = value; WholeNumbers = wholeNumbers;
        Label = new FlxText(x, y - 13, width, name) { Tint = Color.Black, Alignment = FlxTextAlign.Left };
        ValueText = new FlxText(x, y - 13, width, "") { Tint = new Color(60, 60, 60), Alignment = FlxTextAlign.Right };
        Refresh();
    }

    /// <summary>Shows the current <see cref="Value"/> in the value text (call it after setting Value in code).</summary>
    public void Refresh() => ValueText.Text = WholeNumbers ? ((int)Value).ToString() : Value.ToString("0.00");

    public override void Update(float elapsed)
    {
        var m = FlxG.Mouse;
        bool over = Vertical
            ? m.Y >= Y - HandleW && m.Y <= Y + Width + HandleW && m.X >= X - HandleH / 2f && m.X <= X + HandleH / 2f + BarHeight
            : m.X >= X - HandleW && m.X <= X + Width + HandleW && m.Y >= Y - HandleH / 2f && m.Y <= Y + HandleH / 2f + BarHeight;

        if (m.JustPressed && over) _dragging = true;
        if (!m.Pressed) _dragging = false;

        if (_dragging)
        {
            float t = MathHelper.Clamp(((Vertical ? m.Y : m.X) - (Vertical ? Y : X)) / Width, 0f, 1f);
            Value = Min + (Max - Min) * t;
            if (WholeNumbers) Value = MathF.Round(Value);
            Refresh();
        }
        if (Vertical) return;
        Label.Update(elapsed);
        ValueText.Update(elapsed);
    }

    public override void Draw()
    {
        var sb = FlxG.SpriteBatch;
        var px = FlxG.Pixel;
        float t = Max > Min ? (Value - Min) / (Max - Min) : 0f;

        if (Vertical)
        {
            sb.Draw(px, new Rectangle((int)X, (int)Y, BarHeight, (int)Width), new Color(120, 96, 70));
            sb.Draw(px, new Rectangle((int)(X + BarHeight / 2f - HandleH / 2f + 1), (int)(Y + Width * t - HandleW / 2f), HandleH - 2, HandleW), _dragging ? new Color(60, 36, 20) : new Color(100, 66, 40));
            return;
        }

        sb.Draw(px, new Rectangle((int)X, (int)Y, (int)Width, BarHeight), new Color(120, 120, 120));
        sb.Draw(px, new Rectangle((int)X, (int)Y, (int)(Width * t), BarHeight), new Color(150, 90, 40));
        sb.Draw(px, new Rectangle((int)(X + Width * t - HandleW / 2f), (int)(Y + BarHeight / 2f - HandleH / 2f), HandleW, HandleH),
            _dragging ? new Color(40, 40, 40) : new Color(70, 70, 70));
        Label.Draw();
        ValueText.Draw();
    }
}
