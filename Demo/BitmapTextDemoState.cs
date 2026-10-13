using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// Shows FlxBitmapFont / FlxBitmapText: a font loaded from a BMFont .fnt (with kerning), from XML, from a monospace grid and the
/// default one; scaling, borders, alignment, spacing. T switches whether text is
/// drawn after it (text stays steady) or before it (the shader bends the text too).
/// </summary>
public class BitmapTextDemoState : FlxState
{

    readonly List<FlxBitmapText> _texts = new();
    FlxBitmapText _paragraph = null!, _status = null!;
    FlxTypeText _typed = null!;
    bool _after = true;

    static readonly FlxTextBorderStyle[] Borders =
        { FlxTextBorderStyle.None, FlxTextBorderStyle.Outline, FlxTextBorderStyle.OutlineFast, FlxTextBorderStyle.Shadow };
    int _border;       // index into Borders: starts with no border

    const string TypedMessage = "Incoming transmission. The road ahead is clear, but the water is rising. Keep moving, and do not stop to rest.";

    const string Lorem = "Bitmap fonts draw every character from a picture. This box wraps its words at the field width, " +
                         "and can line them up left, center, right or justified. Try the arrow keys to change the spacing!";

    public override void Create()
    {
        FlxG.Game.Window.Title = "MonoFlixel - bitmap text";
        FlxG.Cameras.BgColor = new Color(18, 22, 40);
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = true;

        BuildBackdrop();

        // ---- four fonts, same text
        var defaultFont = FlxBitmapFont.Default;
        var bmFont = FlxBitmapFont.FromBMFont(FlxAssets.Get("fonts/nokia8.fnt"));            // .fnt text format, with kerning pairs
        var xmlFont = FlxBitmapFont.FromBMFont(FlxAssets.Get("fonts/nokia8_xml.fnt"));        // .fnt XML format

        // a monospace grid: every cell of the Nokia atlas is 10x10, in ASCII order from the space
        var letters = new StringBuilder();
        for (int c = 32; c < 127; c++) letters.Append((char)c);
        var monoFont = FlxBitmapFont.FromMonospace(FlxBitmapFont.Default.Atlas, letters.ToString(), 10, 10);

        const string sample = "AVATAR TAT LTY Tay";
        float y = 22;
        AddSample("Default font", defaultFont, sample, ref y);
        AddSample("BMFont .fnt (kerning)", bmFont, sample, ref y);
        AddSample("BMFont XML", xmlFont, sample, ref y);
        AddSample("FromMonospace", monoFont, sample, ref y);

        // ---- scaled, bordered, rotated
        Label(10, 128, "Scale 2 + outline");
        var big = new FlxBitmapText(10, 140, "Big text", bmFont) { TextScale = 2, Tint = new Color(255, 230, 120) };
        big.SetBorderStyle(FlxTextBorderStyle.Outline, new Color(120, 40, 20));
        Track(big);

        Label(10, 168, "Letter spacing / shadow");
        var spaced = new FlxBitmapText(10, 180, "S P A C E D", bmFont) { LetterSpacing = 2, Tint = new Color(150, 230, 255) };
        spaced.SetBorderStyle(FlxTextBorderStyle.Shadow, new Color(0, 40, 80));
        Track(spaced);

        Label(10, 198, "Rotated");
        var rot = new FlxBitmapText(40, 214, "Spin", bmFont) { TextScale = 2, Angle = -12, Tint = new Color(180, 255, 160) };
        Track(rot);

        // ---- wrapped paragraph
        Label(245, 22, "Wrapped text (A: alignment)");
        _paragraph = new FlxBitmapText(245, 36, Lorem, bmFont, 220)
        {
            Background = true,
            BackgroundColor = new Color(0, 0, 0, 140),
            Tint = Color.White
        };
        Track(_paragraph);

        // ---- typed text (FlxTypeText): a message that types itself, with a cursor, a tick for every letter and an uneven rhythm
        Label(245, 140, "Typed text (R: again, SPACE: skip)");
        _typed = new FlxTypeText(245, 154, 220, TypedMessage)
        {
            Background = true,
            BackgroundColor = new Color(0, 0, 0, 140),
            Tint = new Color(150, 255, 170),
            ShowCursor = true,
            UseDefaultSound = true,
            DefaultSoundVolume = 0.2f,
            Prefix = "> ",
            SkipKeys = new[] { Keys.Space }
        };
        _typed.SetTypingVariation(0.6f);
        Track(_typed);
        _typed.Start(delay: 0.04f);

        _status = new FlxBitmapText(4, FlxG.Height - 24, "", defaultFont, FlxG.Width - 8) { Tint = new Color(255, 230, 120) };
        _status.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black);
        Track(_status);

        var help = new FlxBitmapText(4, FlxG.Height - 12, "A align  B border  arrows spacing  R retype  T text after fx  ESC back",
            defaultFont, FlxG.Width - 8) { Tint = new Color(180, 190, 210) };
        help.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black);
        Track(help);

        Refresh();
    }

    // a striped, gradient backdrop so a shader's distortion is easy to see
    void BuildBackdrop()
    {
        var sky = FlxGradient.CreateSprite(FlxG.Width, FlxG.Height, new[] { new Color(20, 30, 70), new Color(60, 30, 80), new Color(15, 20, 40) });
        Add(sky);

        var grid = new FlxSprite().MakeGraphic(FlxG.Width, FlxG.Height, Color.Transparent);
        var canvas = new FlxCanvas(grid);
        for (int x = 0; x < FlxG.Width; x += 16) canvas.FillRect(x, 0, 1, FlxG.Height, new Color(255, 255, 255, 40));
        for (int yy = 0; yy < FlxG.Height; yy += 16) canvas.FillRect(0, yy, FlxG.Width, 1, new Color(255, 255, 255, 40));
        canvas.Apply();
        Add(grid);
    }

    FlxBitmapText Label(float x, float y, string text)
    {
        var t = new FlxBitmapText(x, y, text) { Tint = new Color(140, 150, 175) };
        Track(t);
        return t;
    }

    void AddSample(string name, FlxBitmapFont font, string text, ref float y)
    {
        Label(10, y, name);
        var t = new FlxBitmapText(10, y + 11, text, font) { TextScale = 1, Tint = Color.White };
        Track(t);
        y += 26;
    }

    void Track(FlxBitmapText t)
    {
        _texts.Add(t);
        Add(t);
    }

    // ------------------------------------------------------------------ controls

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        if (FlxG.Keys.JustPressed(Keys.A)) { _paragraph.Alignment = (FlxTextAlign)(((int)_paragraph.Alignment + 1) % 4); Refresh(); }
        if (FlxG.Keys.JustPressed(Keys.B))
        {
            _border = (_border + 1) % Borders.Length;
            _paragraph.SetBorderStyle(Borders[_border], new Color(0, 0, 0));
            Refresh();
        }
        if (FlxG.Keys.JustPressed(Keys.Right)) { _paragraph.LetterSpacing += 1; Refresh(); }
        if (FlxG.Keys.JustPressed(Keys.Left)) { _paragraph.LetterSpacing -= 1; Refresh(); }
        if (FlxG.Keys.JustPressed(Keys.Up)) { _paragraph.LineSpacing += 1; Refresh(); }
        if (FlxG.Keys.JustPressed(Keys.Down)) { _paragraph.LineSpacing -= 1; Refresh(); }

        if (FlxG.Keys.JustPressed(Keys.R)) { _typed.ResetText(TypedMessage); _typed.Start(delay: 0.04f, forceRestart: true); }
        if (FlxG.Keys.JustPressed(Keys.T)) { _after = !_after; foreach (var t in _texts) t.DrawAfterPostProcess = _after; Refresh(); }

        if (FlxG.Keys.JustPressed(Keys.Escape))
        {
            FlxG.PostProcess.Clear();
            LauncherState.Return();
        }
    }

    void Refresh()
    {
        _status.Text = $"alignment {_paragraph.Alignment}  border {Borders[_border]}  letter {_paragraph.LetterSpacing:0}  line {_paragraph.LineSpacing:0}   " +
                       $"text drawn {(_after ? "AFTER" : "BEFORE")} the shader";
    }
}
