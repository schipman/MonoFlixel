using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoFlixel.Mode;

namespace MonoFlixel.Demo;

/// <summary>
/// Main menu: the demos as tiles over a slowly changing gradient with an aurora in it, glowing dust drifting up, and a title whose
/// letters ride a wave and cycle through the colors. It is made of engine parts: a sprite drawn by code each frame, an FlxEmitter
/// with additive blending, FlxMouseEvent for the tiles, FlxColor.FromHsb, and the effects menu (F) on top of it all.
/// </summary>
public class LauncherState : FlxState
{
    /// <summary>The size the launcher runs at (the demos change it when they start).</summary>
    public const int Width = 640, Height = 360, Zoom = 2;

    /// <summary>Back to the launcher, from a demo.</summary>
    public static void Return() => FlxG.SwitchState(new LauncherState(), Width, Height, Zoom);

    // name, one line about it, launcher action.
    static readonly (string Name, string Blurb, Action Launch)[] Demos =
    {
        ("MODE", "Classic platformer", () => FlxG.SwitchState(new Mode.MenuState(), 320, 240, 2)),
        ("MEGA MAN", "Tiles, coins, lifts", () => FlxG.SwitchState(new MegaManState(), 480, 270, 2)),
        ("TILED", "TMX maps, objects", () => FlxG.SwitchState(new TiledDemoState(), 320, 240, 2)),
        ("QUAD TREE", "Fast collisions", () => FlxG.SwitchState(new QuadTreeDemoState(), 480, 270, 2)),
        ("CAVE GEN", "Cellular caves", () => FlxG.SwitchState(new CaveDemoState(), 640, 480, 1)),
        ("BSP MAP", "Dungeons and rooms", () => FlxG.SwitchState(new BspGenerateState(), 640, 480, 1)),
        ("ENGINE", "Tweens, saves, bars", () => FlxG.SwitchState(new EngineDemoState(), 480, 270, 2)),
        ("PATHFIND", "A* and steering", () => FlxG.SwitchState(new PathfindDemoState(), 480, 270, 2)),
        ("TRANSITIONS", "Screen wipes", () => FlxG.SwitchState(new TransitionDemoState(), 640, 480, 1)),
        ("BITMAP TEXT", "Fonts and borders", () => FlxG.SwitchState(new BitmapTextDemoState(), 480, 270, 2)),
        ("SPRITES", "Atlases, blend modes", () => FlxG.SwitchState(new SpritesDemoState(), 480, 270, 2)),
        ("BIG CAVE", "Worlds from noise", () => FlxG.SwitchState(new BigCaveDemoState(), 480, 270, 2)),
        ("SOUND", "OGG, WAV, fades", () => FlxG.SwitchState(new SoundDemoState(), 480, 270, 2)),
        ("INPUT", "Keys, pads, actions", () => FlxG.SwitchState(new InputDemoState(), 480, 270, 2)),
        ("DROWN", "Dive, mine, craft", () => Drown.DrownMenuState.Open()),
        ("POST APOC", "Walk the road", () => PostApoc.PostApocMenuState.Open()),
        ("ROGUELIKE", "FOV, A*, Dijkstra", () => FlxG.SwitchState(new RoguelikeDemoState(), 640, 360, 2)),
        ("SPECIAL FX", "Plasma, stars, waves", () => FlxG.SwitchState(new SpecialFxDemoState(), 640, 360, 2)),
        ("EFFECT SPRITE", "Sprite Effects", () => FlxG.SwitchState(new EffectSpriteDemoState(), 640, 480, 1)),
        ("FSM", "State machine slime", () => FlxG.SwitchState(new FsmDemoState(), 320, 240, 2)),
        ("WEAPON", "Bullets and guns", () => FlxG.SwitchState(new WeaponDemoState(), 480, 270, 2)),
    };

    const int Columns = 4, TileW = 124, TileH = 34, Gap = 8, RowGap = 4;

    readonly List<LauncherTile> _tiles = new();
    readonly List<FlxText> _letters = new();
    FlxTypeText _subtitle = null!;
    FlxText _ticker = null!, _hint = null!;
    FlxButton _fx = null!;
    int _selected;
    float _time;

    public override void Create()
    {
        FlxG.Game.Window.Title = "MonoFlixel";
        FlxG.Cameras.BgColor = new Color(10, 10, 24);
        FlxG.Sound.Destroy(true);
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = true;

        Add(new AnimatedBackdrop());
        Add(MakeDust());

        BuildTitle();
        BuildTiles();

        _fx = new FlxButton(Width - 88, 8, "", OpenEffects);
        Add(_fx);

        _hint = new FlxText(0, Height - 28, Width, "Click, arrows + ENTER, or 1-9      F: effects      ESC: quit") { Alignment = FlxTextAlign.Center };
        Add(_hint);

        _ticker = new FlxText(Width, Height - 13, 0,
            "tilemaps  *  tweens  *  sound  *  gamepads and actions  *  post-processing stack  *  pathfinding  *  procedural levels  *  " +
            "blend modes  *  bitmap fonts  *  tiled maps  *  noise  *  particles  *  save games  *  transitions  *  quad trees  *  ");
        Add(_ticker);

        FlxG.Camera.Fade(Color.Black, 0.6f, fadeIn: true);
    }

    void OpenEffects() => OpenSubState(new PostProcessMenuState());

    // ------------------------------------------------------------------ the parts

    FlxEmitter MakeDust()
    {
        // a soft round dot, made here
        const int size = 16;
        var dot = new Texture2D(FlxG.GraphicsDevice, size, size);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = MathF.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f)) / 8f;
                float a = Math.Clamp(1f - d, 0f, 1f);
                pixels[y * size + x] = new Color(255, 255, 255, (int)(255 * a * a));
            }
        dot.SetData(pixels);

        const int count = 90;
        var emitter = new FlxEmitter(0, Height + 6, count) { Width = Width };
        emitter.Velocity.Set(-14, -48, 14, -16);
        emitter.Lifespan.Set(6, 11);
        for (int i = 0; i < count; i++)
        {
            var p = new GlowParticle();
            p.LoadGraphic(dot);
            p.Blend = FlxBlend.Add;
            p.Tint = FlxColor.FromHsb(FlxG.Random.Float(170, 330), 0.45f, 1f);
            float s = FlxG.Random.Float(0.4f, 1.6f);
            p.Scale = new Vector2(s, s);
            emitter.Add(p);
        }
        emitter.Start(explode: false, frequency: 0.11f);
        return emitter;
    }

    void BuildTitle()
    {
        const string text = "MONOFLIXEL";
        var letters = new List<FlxText>();
        float total = 0;
        foreach (char c in text)
        {
            var t = new FlxText(0, 0, 0, c.ToString(), 32);
            t.SetBorderStyle(FlxTextBorderStyle.Outline, new Color(12, 12, 30), 2);
            letters.Add(t);
            total += t.TextWidth + 3;
        }

        float x = (Width - total) / 2f;
        foreach (var t in letters)
        {
            t.X = x;
            t.Y = 22;
            x += t.TextWidth + 3;
            _letters.Add(t);
            Add(t);
        }

        // typed out, at an uneven pace, with a blinking cursor that goes away a moment after it is done
        const string subtitle = "HaxeFlixel, ported to MonoGame";
        _subtitle = new FlxTypeText(0, 70, 0, subtitle) { ShowCursor = true, UseDefaultSound = true, DefaultSoundVolume = 0.18f, Delay = 0.045f };
        _subtitle.X = (Width - _subtitle.MeasureLine(subtitle)) / 2f;
        _subtitle.SetTypingVariation(0.6f);
        _subtitle.SkipOnClick = true;
        Add(_subtitle);
        new FlxTimer().Start(0.9f, _ => _subtitle.Start(callback: () => new FlxTimer().Start(1.6f, __ => _subtitle.ShowCursor = false)));
    }

    void BuildTiles()
    {
        float totalW = Columns * TileW + (Columns - 1) * Gap;
        float startX = (Width - totalW) / 2f, startY = 90;

        for (int i = 0; i < Demos.Length; i++)
        {
            int col = i % Columns, row = i / Columns;
            var tile = new LauncherTile(startX + col * (TileW + Gap), startY + row * (TileH + RowGap), TileW, TileH, Demos[i].Name, Demos[i].Blurb,
                i < 9 ? (i + 1).ToString() : "", FlxColor.FromHsb(i * 24f + 10f, 0.65f, 1f), i * 0.045f);
            _tiles.Add(tile);
            tile.AddTo(this);

            int index = i;
            var launch = Demos[i].Launch;
            FlxMouseEvent.Add(tile.Body, onMouseOver: _ => _selected = index, pixelPerfect: false);
            FlxMouseEvent.SetClickCallback(tile.Body, _ => launch());
        }
    }

    // ------------------------------------------------------------------ every frame

    public override void Update(float elapsed)
    {
        _time += elapsed;

        // the title: a wave, and every letter its own color moving through the rainbow
        for (int i = 0; i < _letters.Count; i++)
        {
            _letters[i].Y = 22 + MathF.Sin(_time * 2.6f + i * 0.55f) * 5f;
            _letters[i].Tint = FlxColor.FromHsb(_time * 40f + i * 28f, 0.42f, 1f);
        }
        _subtitle.Tint = FlxColor.FromHsb(_time * 22f + 160f, 0.35f, 0.95f);
        _hint.Tint = new Color(150, 160, 195);

        // the ticker crawls to the left and comes round again
        _ticker.X -= 34f * elapsed;
        if (_ticker.X < -_ticker.TextWidth) _ticker.X += _ticker.TextWidth + Width;
        _ticker.Tint = FlxColor.FromHsb(_time * 14f + 200f, 0.3f, 0.6f);

        _fx.Label.Text = FlxG.PostProcess.Global.Count == 0 ? "EFFECTS (F)" : $"EFFECTS: {FlxG.PostProcess.Global.Count} (F)";

        MoveSelection();
        for (int i = 0; i < _tiles.Count; i++) _tiles[i].Animate(_time, i == _selected, elapsed);

        base.Update(elapsed);

        if (FlxG.Keys.JustPressed(Keys.F)) { OpenEffects(); return; }
        if (FlxG.Keys.AnyJustPressed(Keys.Enter, Keys.Space, Keys.X) || FlxG.Gamepad.JustPressed(FlxGamepadInputID.A)) { Demos[_selected].Launch(); return; }

        for (int i = 0; i < Math.Min(9, Demos.Length); i++)
            if (FlxG.Keys.JustPressed(Keys.D1 + i)) { Demos[i].Launch(); return; }   // 1-9 (0 mutes the sound; the rest: click or arrows + ENTER)

        if (FlxG.Keys.JustPressed(Keys.Escape)) FlxG.Game.Exit();
    }

    void MoveSelection()
    {
        int n = Demos.Length, rows = (n + Columns - 1) / Columns;
        int col = _selected % Columns, row = _selected / Columns;
        var k = FlxG.Keys;
        var pad = FlxG.Gamepad;

        if (k.AnyJustPressed(Keys.Right, Keys.D) || pad.JustPressed(FlxGamepadInputID.DPadRight)) col = (col + 1) % Columns;
        if (k.AnyJustPressed(Keys.Left, Keys.A) || pad.JustPressed(FlxGamepadInputID.DPadLeft)) col = (col + Columns - 1) % Columns;
        if (k.AnyJustPressed(Keys.Down, Keys.S) || pad.JustPressed(FlxGamepadInputID.DPadDown)) row = (row + 1) % rows;
        if (k.AnyJustPressed(Keys.Up, Keys.W) || pad.JustPressed(FlxGamepadInputID.DPadUp)) row = (row + rows - 1) % rows;

        int target = row * Columns + col;
        if (target >= n) target = n - 1;            // the last row is not full
        _selected = target;
    }
}

/// <summary>A button of the launcher: a panel with an accent color, a name, a line about the demo and a glow when it is chosen.</summary>
sealed class LauncherTile
{
    public readonly FlxSprite Body, Accent, Glow;
    readonly FlxText _name, _blurb, _key;
    readonly Texture2D _normal, _lit;
    readonly Color _color;
    readonly float _x, _y, _delay;
    float _t, _pulse;
    bool _wasLit;

    public LauncherTile(float x, float y, int w, int h, string name, string blurb, string key, Color color, float delay)
    {
        _x = x; _y = y; _color = color; _delay = delay;

        _normal = Panel(w, h, color, false);
        _lit = Panel(w, h, color, true);

        Glow = new FlxSprite(x - 8, y - 8).LoadGraphic(GlowTexture(w + 16, h + 16, 8));
        Glow.Blend = FlxBlend.Add;
        Glow.Tint = color;
        Glow.Alpha = 0;

        Body = new FlxSprite(x, y).LoadGraphic(_normal);
        Accent = new FlxSprite(x + 5, y + 5).MakeGraphic(3, h - 10, color);
        _name = new FlxText(x + 14, y + 7, w - 20, name) { Tint = Color.White };
        _blurb = new FlxText(x + 14, y + 20, w - 20, blurb) { Tint = new Color(140, 150, 185) };
        _key = new FlxText(x + w - 14, y + 5, 10, key) { Tint = color * 0.8f };

        foreach (var s in new FlxSprite[] { Glow, Body, Accent }) s.Moves = false;
    }

    public void AddTo(FlxState state)
    {
        state.Add(Glow);
        state.Add(Body);
        state.Add(Accent);
        state.Add(_name);
        state.Add(_blurb);
        state.Add(_key);
    }

    public void Animate(float time, bool lit, float elapsed)
    {
        // coming in: slides up and fades in, one tile after the other
        _t += elapsed;
        float e = Math.Clamp((_t - _delay) / 0.45f, 0f, 1f);
        e = FlxEase.QuadOut(e);
        float y = _y + (1f - e) * 26f;

        _pulse += elapsed * (lit ? 4.5f : 0f);
        float glow = lit ? 0.22f + 0.1f * MathF.Sin(_pulse) : 0f;

        if (lit != _wasLit) { Body.LoadGraphic(lit ? _lit : _normal); _wasLit = lit; }

        // a lit tile lifts a little
        float lift = lit ? -1.5f : 0f;
        Glow.Y = y - 8 + lift;
        Body.Y = y + lift;
        Accent.Y = y + 5 + lift;
        _name.Y = y + 7 + lift;
        _blurb.Y = y + 20 + lift;
        _key.Y = y + 5 + lift;

        Glow.Alpha = glow * e;
        Body.Alpha = e;
        Accent.Alpha = e;
        Accent.Tint = lit ? FlxColor.FromHsb(time * 120f + _x, 0.5f, 1f) : _color;

        int a = (int)(255 * e);
        _name.Tint = new Color(255, 255, 255, a);
        _blurb.Tint = lit ? new Color(210, 220, 245, a) : new Color(140, 150, 185, a);
        _key.Tint = new Color(_color.R, _color.G, _color.B, a);
    }

    static Texture2D Panel(int w, int h, Color accent, bool lit)
    {
        var tex = new Texture2D(FlxG.GraphicsDevice, w, h);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float g = y / (float)h;                              // a little lighter at the top
                float r = 12 + 14 * (1 - g), gg = 15 + 14 * (1 - g), b = 32 + 18 * (1 - g);
                float a = 205;
                if (lit)
                {
                    r += accent.R * 0.14f; gg += accent.G * 0.14f; b += accent.B * 0.14f;
                    a = 235;
                }

                bool border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                bool corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                if (corner) { px[y * w + x] = Color.Transparent; continue; }
                if (border)
                {
                    float k = lit ? 0.95f : 0.4f;
                    px[y * w + x] = new Color((int)(accent.R * k), (int)(accent.G * k), (int)(accent.B * k), 255);
                    continue;
                }
                px[y * w + x] = new Color((int)Math.Min(255, r), (int)Math.Min(255, gg), (int)Math.Min(255, b), (int)a);
            }
        tex.SetData(px);
        return tex;
    }

    /// <summary>A white rectangle with soft edges: fully bright inside, fading out over <paramref name="pad"/> pixels.</summary>
    static Texture2D GlowTexture(int w, int h, int pad)
    {
        var tex = new Texture2D(FlxG.GraphicsDevice, w, h);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = Math.Max(0, Math.Max(pad - x, x - (w - 1 - pad)));
                float dy = Math.Max(0, Math.Max(pad - y, y - (h - 1 - pad)));
                float d = MathF.Sqrt(dx * dx + dy * dy) / pad;
                float a = Math.Clamp(1f - d, 0f, 1f);
                px[y * w + x] = new Color(255, 255, 255, (int)(255 * a * a));
            }
        tex.SetData(px);
        return tex;
    }
}

/// <summary>Dust that fades in and out over its life while it drifts up.</summary>
sealed class GlowParticle : FlxParticle
{
    public override void Update(float elapsed)
    {
        base.Update(elapsed);
        if (Lifespan > 0) Alpha = MathF.Sin(MathF.PI * Math.Clamp(Age / Lifespan, 0f, 1f)) * 0.7f;
    }
}

/// <summary>
/// The background: a vertical gradient whose two colors drift around the color wheel, with a slow aurora laid over it. A small picture
/// (half the screen) is worked out each frame and stretched; sines of the column and the row are worked out once per line, so a frame
/// costs a few multiplications per pixel.
/// </summary>
sealed class AnimatedBackdrop : FlxSprite
{
    const int Divisor = 2;

    readonly int _w, _h;
    readonly Color[] _buffer;
    readonly float[] _col1, _col2, _row1, _row2;
    float _time;

    public AnimatedBackdrop()
    {
        _w = FlxG.Width / Divisor;
        _h = FlxG.Height / Divisor;
        _buffer = new Color[_w * _h];
        _col1 = new float[_w]; _col2 = new float[_w];
        _row1 = new float[_h]; _row2 = new float[_h];

        MakeGraphic(_w, _h, Color.Black);
        Origin = Vector2.Zero;
        Scale = new Vector2(Divisor, Divisor);
        Moves = false;
        Fill();
    }

    public override void Update(float elapsed)
    {
        _time += elapsed;
        Fill();
        base.Update(elapsed);
    }

    void Fill()
    {
        float t = _time;
        var top = FlxColor.FromHsb(245f + 28f * MathF.Sin(t * 0.20f), 0.80f, 0.20f);
        var bottom = FlxColor.FromHsb(310f + 45f * MathF.Sin(t * 0.27f + 1f), 0.75f, 0.34f);
        var aurora = FlxColor.FromHsb(165f + 70f * MathF.Sin(t * 0.15f), 0.8f, 1f);

        for (int x = 0; x < _w; x++) { _col1[x] = MathF.Sin(x * 0.035f + t * 0.55f); _col2[x] = MathF.Sin(x * 0.081f - t * 0.40f); }
        for (int y = 0; y < _h; y++) { _row1[y] = MathF.Sin(y * 0.052f - t * 0.35f); _row2[y] = MathF.Cos(y * 0.047f + t * 0.5f); }

        for (int y = 0; y < _h; y++)
        {
            float k = y / (float)(_h - 1);
            float br = top.R + (bottom.R - top.R) * k, bg = top.G + (bottom.G - top.G) * k, bb = top.B + (bottom.B - top.B) * k;
            int row = y * _w;

            for (int x = 0; x < _w; x++)
            {
                float p = _col1[x] + _row1[y] + _col2[x] * _row2[y];        // -3 to 3
                float a = (p + 3f) / 6f;
                a = a * a * 0.42f;

                _buffer[row + x] = new Color(
                    (int)Math.Min(255, br + aurora.R * a),
                    (int)Math.Min(255, bg + aurora.G * a),
                    (int)Math.Min(255, bb + aurora.B * a));
            }
        }
        Pixels!.SetData(_buffer);
    }
}
