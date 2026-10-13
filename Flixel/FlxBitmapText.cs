using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

public enum FlxTextAlign { Left, Center, Right, Justify }

public enum FlxTextBorderStyle
{
    None,
    /// <summary>The text drawn 8 times around itself in the border color.</summary>
    Outline,
    /// <summary>Like Outline but only up, down, left and right (cheaper, looks the same on small text).</summary>
    OutlineFast,
    /// <summary>One copy of the text shifted down and right.</summary>
    Shadow
}

/// <summary>
/// Text drawn from a <see cref="FlxBitmapFont"/>. Port of Flixel's FlxBitmapText: any font (monospace, XNA or BMFont
/// with kerning), word wrapping, alignment (left, center, right, justify), letter and line spacing, scaling, outline
/// or shadow, a background color, rotation and tint. Like Flixel, Moves is false by default.
/// <code>
/// var text = new FlxBitmapText(20, 20, "Hello world", FlxBitmapFont.FromBMFont(FlxAssets.Get("fonts/nokia8.fnt")), fieldWidth: 200);
/// text.Alignment = FlxTextAlign.Justify;
/// text.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black);
/// </code>
/// By default text is drawn after post-processing shaders (see <see cref="FlxBasic.DrawAfterPostProcess"/>), so effects like
/// Wiggle or Bloom change the game but not the text.
/// </summary>
public class FlxBitmapText : FlxSprite
{
    /// <summary>New text objects get this value for DrawAfterPostProcess (default true).</summary>
    public static bool DefaultDrawAfterPostProcess = true;

    struct Quad
    {
        public int Page;
        public Rectangle Source;
        public float X, Y;
    }

    sealed class Line
    {
        public string Text = "";
        public bool EndOfParagraph;
    }

    FlxBitmapFont _font;
    string _text;
    FlxTextAlign _align = FlxTextAlign.Left;
    float _fieldWidth, _letterSpacing, _lineSpacing, _textScale = 1f;
    bool _wordWrap = true, _multiLine = true;
    readonly List<Quad> _quads = new();

    public FlxTextBorderStyle BorderStyle = FlxTextBorderStyle.None;
    public Color BorderColor = Color.Black;
    public float BorderSize = 1f;

    /// <summary>Fill the text's box with BackgroundColor.</summary>
    public bool Background;
    public Color BackgroundColor = Color.Black;

    /// <summary>Round the position to whole pixels (keeps pixel fonts sharp; ignored when rotated).</summary>
    public bool SnapToPixels = true;

    /// <summary>Width / height of the text itself (Width is the field width when one is set).</summary>
    public float TextWidth { get; private set; }
    public float TextHeight { get; private set; }
    public int NumLines { get; private set; }

    public FlxBitmapText(float x = 0, float y = 0, string text = "", FlxBitmapFont? font = null, float fieldWidth = 0) : base(x, y)
    {
        _font = font ?? FlxBitmapFont.Default;
        _text = text ?? "";
        _fieldWidth = fieldWidth;
        Moves = false;
        DrawAfterPostProcess = DefaultDrawAfterPostProcess;
        Layout();
    }

    // ------------------------------------------------------------------ properties

    public string Text { get => _text; set { value ??= ""; if (_text != value) { _text = value; Layout(); } } }

    public FlxBitmapFont Font
    {
        get => _font;
        set { if (!ReferenceEquals(_font, value)) { _font = value ?? FlxBitmapFont.Default; FontChanged(); Layout(); } }
    }

    /// <summary>Fixed width of the text box: text wraps inside it and Alignment is relative to it. 0 = as wide as the longest line.</summary>
    public float FieldWidth { get => _fieldWidth; set { if (_fieldWidth != value) { _fieldWidth = value; Layout(); } } }

    public FlxTextAlign Alignment { get => _align; set { if (_align != value) { _align = value; Layout(); } } }

    /// <summary>Multiplies the font's size (2 = twice as big). Whole numbers keep pixel fonts crisp.</summary>
    public float TextScale { get => _textScale; set { if (_textScale != value) { _textScale = Math.Max(0.01f, value); Layout(); } } }

    /// <summary>Extra pixels between characters (can be negative), before TextScale.</summary>
    public float LetterSpacing { get => _letterSpacing; set { if (_letterSpacing != value) { _letterSpacing = value; Layout(); } } }

    /// <summary>Extra pixels between lines, before TextScale.</summary>
    public float LineSpacing { get => _lineSpacing; set { if (_lineSpacing != value) { _lineSpacing = value; Layout(); } } }

    /// <summary>Wrap long lines at FieldWidth (default true; needs a FieldWidth).</summary>
    public bool WordWrap { get => _wordWrap; set { if (_wordWrap != value) { _wordWrap = value; Layout(); } } }

    /// <summary>Allow several lines: false turns line breaks into spaces.</summary>
    public bool MultiLine { get => _multiLine; set { if (_multiLine != value) { _multiLine = value; Layout(); } } }

    /// <summary>Called when the font changed (FlxText uses it to keep its Size).</summary>
    protected virtual void FontChanged() { }

    /// <summary>Sets font, scale, color, alignment and border in one go.</summary>
    public FlxBitmapText SetFormat(FlxBitmapFont? font = null, float scale = 1f, Color? color = null, FlxTextAlign alignment = FlxTextAlign.Left,
        FlxTextBorderStyle border = FlxTextBorderStyle.None, Color? borderColor = null)
    {
        if (font != null) Font = font;
        TextScale = scale;
        if (color.HasValue) Tint = color.Value;
        Alignment = alignment;
        BorderStyle = border;
        if (borderColor.HasValue) BorderColor = borderColor.Value;
        return this;
    }

    public FlxBitmapText SetBorderStyle(FlxTextBorderStyle style, Color color, float size = 1f)
    {
        BorderStyle = style; BorderColor = color; BorderSize = size;
        return this;
    }

    // ------------------------------------------------------------------ layout

    /// <summary>Width of a single line in pixels, with TextScale, letter spacing and kerning.</summary>
    public float MeasureLine(string line)
    {
        if (line.Length == 0) return 0;
        string s = Prepare(line);
        float w = 0;
        for (int i = 0; i < s.Length; i++)
        {
            w += _font.Advance(s[i]);
            if (i + 1 < s.Length) w += _font.GetKerning(s[i], s[i + 1]) + _letterSpacing;
        }
        return w * _textScale;
    }

    string Prepare(string s) => s.Replace("\t", "    ");

    /// <summary>
    /// The given text with a line break where this field would wrap it (at its current width, font and scale): the same words, so that
    /// showing it a few letters at a time never moves a word to the next line half way through.
    /// </summary>
    public string WrapText(string text) => string.Join("\n", BreakIntoLines(text).ConvertAll(l => l.Text));

    List<Line> BreakIntoLines() => BreakIntoLines(_text);

    List<Line> BreakIntoLines(string source)
    {
        var lines = new List<Line>();
        string text = source.Replace("\r\n", "\n").Replace('\r', '\n');
        if (!_multiLine) text = text.Replace('\n', ' ');

        foreach (var paragraph in text.Split('\n'))
        {
            if (!_wordWrap || _fieldWidth <= 0)
            {
                lines.Add(new Line { Text = paragraph, EndOfParagraph = true });
                continue;
            }

            string current = "";
            foreach (var word in paragraph.Split(' '))
            {
                string candidate = current.Length == 0 ? word : current + " " + word;

                if (MeasureLine(candidate) <= _fieldWidth)
                {
                    current = candidate;
                    continue;
                }

                if (current.Length > 0)
                {
                    lines.Add(new Line { Text = current });
                    current = "";
                }

                if (MeasureLine(word) <= _fieldWidth) { current = word; continue; }

                // a single word wider than the field: break it between characters
                string piece = "";
                foreach (char c in word)
                {
                    if (piece.Length > 0 && MeasureLine(piece + c) > _fieldWidth)
                    {
                        lines.Add(new Line { Text = piece });
                        piece = "";
                    }
                    piece += c;
                }
                current = piece;
            }
            lines.Add(new Line { Text = current, EndOfParagraph = true });
        }
        return lines;
    }

    /// <summary>Works out where every glyph goes. Runs whenever a property changes, so Width, Height and NumLines are always current.</summary>
    protected void Layout()
    {
        _quads.Clear();

        var lines = BreakIntoLines();
        NumLines = lines.Count;

        float maxWidth = 0;
        var widths = new float[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            widths[i] = MeasureLine(lines[i].Text);
            maxWidth = MathF.Max(maxWidth, widths[i]);
        }

        float fieldW = _fieldWidth > 0 ? _fieldWidth : maxWidth;
        float lineHeight = (_font.LineHeight + _lineSpacing) * _textScale;

        for (int li = 0; li < lines.Count; li++)
        {
            string s = Prepare(lines[li].Text);
            float lw = widths[li];

            float x = _align switch
            {
                FlxTextAlign.Center => (fieldW - lw) * 0.5f,
                FlxTextAlign.Right => fieldW - lw,
                _ => 0f
            };

            // justify: share the leftover width among the spaces (not on the last line of a paragraph)
            float extraPerSpace = 0;
            if (_align == FlxTextAlign.Justify && !lines[li].EndOfParagraph)
            {
                int spaces = 0;
                foreach (char c in s) if (c == ' ') spaces++;
                if (spaces > 0) extraPerSpace = Math.Max(0, fieldW - lw) / spaces;
            }

            float y = li * lineHeight;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                var g = _font.GetGlyph(c);

                if (g != null && g.Source.Width > 0 && g.Source.Height > 0 && c != ' ')
                    _quads.Add(new Quad { Page = g.Page, Source = g.Source, X = x + g.XOffset * _textScale, Y = y + g.YOffset * _textScale });

                float advance = g?.XAdvance ?? _font.SpaceWidth;
                if (c == ' ') advance = _font.HasGlyph(' ') ? _font.GetGlyph(' ')!.XAdvance : _font.SpaceWidth;

                if (i + 1 < s.Length) advance += _font.GetKerning(c, s[i + 1]) + _letterSpacing;
                x += advance * _textScale;
                if (c == ' ') x += extraPerSpace;
            }
        }

        TextWidth = maxWidth;
        TextHeight = lines.Count * lineHeight;
        Width = fieldW;
        Height = TextHeight;
    }

    // ------------------------------------------------------------------ update / draw

    public override void Draw()
    {
        if (Alpha <= 0) return;

        var cam = FlxG.Camera;
        float px = X - cam.ScrollX * ScrollFactor.X;
        float py = Y - cam.ScrollY * ScrollFactor.Y;
        float a = MathHelper.Clamp(Alpha, 0f, 1f);

        if (Background)
        {
            var bg = BackgroundColor; bg.A = (byte)(bg.A * a);
            FlxG.SpriteBatch.Draw(FlxG.Pixel, new Rectangle((int)MathF.Round(px), (int)MathF.Round(py), (int)MathF.Ceiling(Width), (int)MathF.Ceiling(Height)), bg);
        }

        if (_quads.Count == 0) return;

        if (BorderSize > 0 && BorderStyle != FlxTextBorderStyle.None)
        {
            var bc = BorderColor; bc.A = (byte)(bc.A * a);
            switch (BorderStyle)
            {
                case FlxTextBorderStyle.Outline:
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if (dx != 0 || dy != 0) DrawGlyphs(px + dx * BorderSize, py + dy * BorderSize, bc);
                    break;
                case FlxTextBorderStyle.OutlineFast:
                    DrawGlyphs(px - BorderSize, py, bc);
                    DrawGlyphs(px + BorderSize, py, bc);
                    DrawGlyphs(px, py - BorderSize, bc);
                    DrawGlyphs(px, py + BorderSize, bc);
                    break;
                case FlxTextBorderStyle.Shadow:
                    DrawGlyphs(px + BorderSize, py + BorderSize, bc);
                    break;
            }
        }

        var c = Tint; c.A = (byte)(c.A * a);
        DrawGlyphs(px, py, c);
    }

    void DrawGlyphs(float px, float py, Color color)
    {
        var batch = FlxG.SpriteBatch;
        float rot = MathHelper.ToRadians(Angle);
        float cos = MathF.Cos(rot), sin = MathF.Sin(rot);
        float cx = Width * 0.5f, cy = Height * 0.5f;
        bool rotated = Angle != 0;

        foreach (var q in _quads)
        {
            float lx = q.X - cx, ly = q.Y - cy;
            if (rotated) { float rx = lx * cos - ly * sin; ly = lx * sin + ly * cos; lx = rx; }

            var pos = new Vector2(px + cx + lx, py + cy + ly);
            if (!rotated && SnapToPixels) pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));

            batch.Draw(_font.Pages[q.Page], pos, q.Source, color, rot, Vector2.Zero, _textScale, SpriteEffects.None, 0f);
        }
    }
}
