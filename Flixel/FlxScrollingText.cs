using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// A horizontal text scroller, the kind from demo scene intros: a sprite that shows a message crawling in from the right,
/// made from a bitmap font. Port of Richard Davey's (Photon Storm) Flixel Power Tools <c>FlxScrollingText</c> (see
/// NOTICE-flixel-power-tools.txt).
/// <code>
/// var ticker = new FlxScrollingText(FlxBitmapFont.Default, new Rectangle(0, 100, 320, 16), pixels: 2, text: "GREETINGS TO ALL FLIXEL FANS      ");
/// Add(ticker.Sprite);        // an ordinary sprite: position it, rotate it, tint it, collide with it
/// Add(ticker);               // it scrolls while it is in the state
/// ticker.AddText("MORE TEXT ...");
/// </code>
/// Each step the picture moves left by <see cref="Pixels"/> and the next columns of the message come in on the right. Differences from the
/// original: it works with proportional fonts (the original needed every character the same size), it is an object you add to a state
/// (not a static plugin), and <see cref="Tint"/> colors the text.
/// </summary>
public class FlxScrollingText : FlxBasic
{
    readonly FlxBitmapFont _font;
    readonly FxBitmap _canvas;
    readonly Texture2D _texture;
    readonly Dictionary<char, FxBitmap> _cells = new();
    readonly int _clearDistance;
    string _text;
    int _charIndex, _column, _wait, _clearCount;

    /// <summary>The sprite that shows the scroller.</summary>
    public FlxSprite Sprite { get; }

    /// <summary>Pixels the text moves each step. Small values are smooth but slow.</summary>
    public int Pixels;

    /// <summary>Steps (frames) to wait between two moves; 0 = move every frame. Higher values slow it down.</summary>
    public int Steps;

    /// <summary>Only scroll while the sprite is on screen (default true).</summary>
    public bool OnlyScrollOnScreen;

    /// <summary>At the end of the message, go back to its start (default true). Otherwise the text scrolls off and the scroller stops.</summary>
    public bool LoopOnWrap;

    /// <summary>False when stopped (also set once a non-looping message has scrolled off).</summary>
    public bool Scrolling = true;

    /// <summary>The color the text is drawn in (white = the font's own colors).</summary>
    public Color Tint = Color.White;

    /// <summary>True once a non-looping message has gone by.</summary>
    public bool Complete { get; private set; }

    public string Text => _text;

    public FlxScrollingText(FlxBitmapFont font, Rectangle region, int pixels = 1, int steps = 0, string text = "FLIXEL ROCKS!", bool onlyScrollOnScreen = true, bool loopOnWrap = true)
    {
        _font = font;
        Pixels = Math.Max(1, pixels);
        Steps = steps;
        OnlyScrollOnScreen = onlyScrollOnScreen;
        LoopOnWrap = loopOnWrap;
        _text = text.Length == 0 ? " " : text;

        _canvas = new FxBitmap(region.Width, region.Height);
        _texture = new Texture2D(FlxG.GraphicsDevice, _canvas.Width, _canvas.Height);
        _texture.SetData(_canvas.Data);
        Sprite = new FlxSprite(region.X, region.Y).LoadGraphic(_texture);
        _clearDistance = region.Width - Pixels;

        Step();   // the first columns are in at once
    }

    /// <summary>Appends text to the message, or with <paramref name="overwrite"/> replaces it. Can be called while it scrolls.</summary>
    public void AddText(string text, bool overwrite = false)
    {
        if (overwrite)
        {
            _text = text.Length == 0 ? " " : text;
            _charIndex = 0;
            _column = 0;
        }
        else _text += text;
        if (Complete) { Complete = false; Scrolling = true; _clearCount = 0; }
    }

    public void StartScrolling() => Scrolling = true;
    public void StopScrolling() => Scrolling = false;

    public override void Update(float elapsed)
    {
        if (!Scrolling || !Sprite.Exists) return;
        if (OnlyScrollOnScreen && !Sprite.IsOnScreen()) return;

        if (Steps > 0 && _wait < Steps) { _wait++; return; }
        _wait = 0;
        Step();
        _texture.SetData(_canvas.Data);
    }

    void Step()
    {
        int w = _canvas.Width, h = _canvas.Height;
        int shift = Math.Min(Pixels, w);

        // move everything left
        for (int y = 0; y < h; y++)
        {
            Array.Copy(_canvas.Data, y * w + shift, _canvas.Data, y * w, w - shift);
            Array.Fill(_canvas.Data, Color.Transparent, y * w + w - shift, shift);
        }

        if (!Complete)
        {
            // the next columns of the message come in on the right
            for (int k = 0; k < shift && !Complete; k++)
            {
                var cell = Cell(_text[_charIndex]);
                int dx = w - shift + k;
                for (int y = 0; y < Math.Min(h, cell.Height); y++)
                    _canvas.Data[y * w + dx] = cell.Data[y * cell.Width + _column];

                if (++_column >= cell.Width)
                {
                    _column = 0;
                    if (++_charIndex >= _text.Length)
                    {
                        _charIndex = 0;
                        if (!LoopOnWrap) { Complete = true; _clearCount = 0; }
                    }
                }
            }
        }
        else
        {
            // nothing new comes in: wait until the last letters have left, then stop
            _clearCount += shift;
            if (_clearCount >= _clearDistance) Scrolling = false;
        }
    }

    /// <summary>One character as a picture: as wide as the font advances after it, as high as the font's lines.</summary>
    FxBitmap Cell(char c)
    {
        if (_cells.TryGetValue(c, out var cell)) return cell;

        var glyph = _font.GetGlyph(c) ?? _font.GetGlyph('?');
        int width = Math.Max(1, glyph?.XAdvance ?? _font.SpaceWidth);
        cell = new FxBitmap(width, Math.Max(1, _font.LineHeight));
        if (glyph != null && glyph.Source.Width > 0)
        {
            var page = FxBitmap.FromTexture(_font.Pages[glyph.Page], glyph.Source);
            for (int i = 0; i < page.Data.Length; i++)
                page.Data[i] = FlxColor.Multiply(page.Data[i], Tint);
            cell.CopyPixels(page, page.Bounds, new Point(glyph.XOffset, glyph.YOffset), 1f, replace: false);
        }
        _cells[c] = cell;
        return cell;
    }

    public override void Destroy()
    {
        Sprite.Kill();
        base.Destroy();
    }
}
