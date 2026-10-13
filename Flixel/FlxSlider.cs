using System;
using Microsoft.Xna.Framework;

namespace MonoFlixel.Flixel;

/// <summary>A small horizontal mouse slider (stands in for flixel-addons' FlxSlider). Drag the handle or click the bar.</summary>
public class FlxSlider: FlxBasic
{
    public float Value;
    public readonly float Min, Max;
    public readonly float X, Y, Width;
    public readonly bool WholeNumbers;
    public readonly FlxText Label;
    public readonly FlxText ValueText;

    const int BarHeight = 4, HandleW = 6, HandleH = 14;
    bool _dragging;

    public FlxSlider(string name, float x, float y, float width, float min, float max, float value, bool wholeNumbers = false)
    {
        X = x; Y = y; Width = width; Min = min; Max = max; Value = value; WholeNumbers = wholeNumbers;
        Label = new FlxText(x, y - 13, width, name) { Tint = Color.Black, Alignment = FlxTextAlign.Left };
        ValueText = new FlxText(x, y - 13, width, "") { Tint = new Color(60, 60, 60), Alignment = FlxTextAlign.Right };
        Refresh();
    }

    void Refresh() => ValueText.Text = WholeNumbers ? ((int)Value).ToString() : Value.ToString("0.00");

    public override void Update(float elapsed)
    {
        var m = FlxG.Mouse;
        bool over = m.X >= X - HandleW && m.X <= X + Width + HandleW && m.Y >= Y - HandleH / 2f && m.Y <= Y + HandleH / 2f + BarHeight;

        if (m.JustPressed && over) _dragging = true;
        if (!m.Pressed) _dragging = false;

        if (_dragging)
        {
            float t = MathHelper.Clamp((m.X - X) / Width, 0f, 1f);
            Value = Min + (Max - Min) * t;
            if (WholeNumbers) Value = MathF.Round(Value);
            Refresh();
        }
        Label.Update(elapsed);
        ValueText.Update(elapsed);
    }

    public override void Draw()
    {
        var sb = FlxG.SpriteBatch;
        var px = FlxG.Pixel;
        float t = (Value - Min) / (Max - Min);

        sb.Draw(px, new Rectangle((int)X, (int)Y, (int)Width, BarHeight), new Color(120, 120, 120));
        sb.Draw(px, new Rectangle((int)X, (int)Y, (int)(Width * t), BarHeight), new Color(150, 90, 40));
        sb.Draw(px, new Rectangle((int)(X + Width * t - HandleW / 2f), (int)(Y + BarHeight / 2f - HandleH / 2f), HandleW, HandleH),
            _dragging ? new Color(40, 40, 40) : new Color(70, 70, 70));
        Label.Draw();
        ValueText.Draw();
    }
}
