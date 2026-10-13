using System;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>
/// Text drawn from a bitmap font (see FlxBitmapText for everything it can do). Size 8 is the default font's native size;
/// 16, 24, 32 scale it up crisply. Supports a fixed field width (word wrap + alignment), outline / shadow border, tint and
/// rotation. With another Font, Size is that font's size in pixels (the font's own native size is Size 1x).
/// </summary>
public class FlxText : FlxBitmapText
{
    int _size = 8;

    public int Size
    {
        get => _size;
        set { _size = Math.Max(1, value); TextScale = _size / (float)Math.Max(1, Font.Size); }
    }

    public FlxText(float x = 0, float y = 0, float fieldWidth = 0, string text = "", int size = 8) : base(x, y, text, null, fieldWidth)
    {
        _size = Math.Max(1, size);
        TextScale = _size / (float)Math.Max(1, Font.Size);
        Layout();
    }

    protected override void FontChanged() => TextScale = _size / (float)Math.Max(1, Font.Size);

    public FlxText SetFormat(int size, Color color, FlxTextAlign align = FlxTextAlign.Left)
    {
        Size = size; Tint = color; Alignment = align;
        return this;
    }

    public new FlxText SetBorderStyle(FlxTextBorderStyle style, Color color, float size = 1f)
    {
        base.SetBorderStyle(style, color, size);
        return this;
    }
}
