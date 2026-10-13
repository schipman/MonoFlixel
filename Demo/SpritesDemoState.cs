using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// Shows atlases (Sparrow XML, TexturePacker JSON, Aseprite with tags), animation control (reverse, ping-pong, speed, frame
/// callbacks), blend modes, ClipRect, baked rotation, FlxStrip, FlxTileblock, FULL (47 tile) auto-tiling and FlxCanvas.Stamp.
/// </summary>
public class SpritesDemoState : FlxState
{
    static readonly string[] WalkerAnims = { "idle", "walk", "jump", "run_back" };

    FlxSprite _walker = null!, _flag = null!, _arrowSmooth = null!, _arrowBaked = null!;
    FlxStrip _wave = null!;
    FlxTilemap _blob = null!;
    FlxText _walkerInfo = null!, _status = null!;
    Vector2[] _waveBase = Array.Empty<Vector2>();
    float _time, _clip = 1f;
    bool _reversed;
    string _lastEvent = "";

    string A(string file) => FlxAssets.Get("atlas/" + file);

    public override void Create()
    {
        FlxG.Game.Window.Title = "MonoFlixel - sprites, atlases, blend modes";
        FlxG.Cameras.BgColor = new Color(18, 20, 34);
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = true;

        BuildAtlases();
        BuildBlendModes();
        BuildEffects();
        BuildTiles();

        _status = new FlxText(4, FlxG.Height - 11, FlxG.Width - 8, "1-4: walker   R: reverse   SPACE: pause   UP/DOWN: speed   click tiles   ESC: back")
            { Tint = new Color(170, 180, 200) };
        Add(_status);
    }

    FlxText Label(float x, float y, string text, float width = 0)
    {
        var t = new FlxText(x, y, width, text) { Tint = new Color(140, 150, 175) };
        Add(t);
        return t;
    }

    // ------------------------------------------------------------------ atlases and animation

    void BuildAtlases()
    {
        Label(8, 4, "Sparrow XML");
        var shapes = FlxAtlasFrames.FromSparrow(A("shapes.png"), A("shapes.xml"));
        var orb = new FlxSprite(24, 18).LoadFrames(shapes);
        orb.Animation.AddByPrefix("pulse", "orb", fps: 12, pingPong: true);     // frames orb0001..orb0008, trimmed in the sheet
        orb.Animation.Play("pulse");
        Add(orb);

        Label(78, 4, "TP JSON");
        var star = new FlxSprite(96, 18).LoadFrames(FlxAtlasFrames.FromTexturePackerJson(A("shapes.png"), A("shapes.json")));
        star.Animation.AddByPrefix("spin", "star", fps: 10);
        star.Animation.Play("spin");
        Add(star);

        // Aseprite: the tags in the JSON become animations, with the frame durations set in Aseprite
        Label(172, 4, "Aseprite tags");
        _walker = new FlxSprite().LoadAseprite(A("walker.png"), A("walker.json"));
        _walker.Scale = new Vector2(4, 4);
        _walker.UpdateHitbox();
        _walker.X = 190; _walker.Y = 14;
        _walker.Animation.OnFrame = (name, step, frame) => _lastEvent = $"{name} step {step} (frame {frame})";
        _walker.Animation.Play("walk");
        Add(_walker);

        _walkerInfo = Label(172, 80, "");

        // the same arrow, rotated by the renderer and baked into 16 pictures
        Label(336, 4, "Rotate: smooth / baked 16");
        _arrowSmooth = new FlxSprite(350, 26).LoadGraphic(A("arrow.png"));
        _arrowSmooth.Scale = new Vector2(2, 2);
        Add(_arrowSmooth);
        _arrowBaked = new FlxSprite(412, 26).LoadRotatedGraphic(A("arrow.png"), 16);
        _arrowBaked.Scale = new Vector2(2, 2);
        Add(_arrowBaked);
    }

    // ------------------------------------------------------------------ blend modes

    void BuildBlendModes()
    {
        Label(8, 98, "Blend modes: the same orange glow over a rainbow");
        var rainbow = FlxGradient.CreateSprite(290, 44, new[]
        {
            new Color(230, 60, 60), new Color(240, 200, 60), new Color(70, 200, 90), new Color(60, 160, 230), new Color(150, 80, 220)
        }, 1, 0);
        rainbow.X = 8; rainbow.Y = 110;
        Add(rainbow);

        FlxBlend[] modes = { FlxBlend.Alpha, FlxBlend.Add, FlxBlend.Screen, FlxBlend.Multiply, FlxBlend.Subtract, FlxBlend.Lighten };
        for (int i = 0; i < modes.Length; i++)
        {
            float x = 8 + i * 48.5f;
            var glow = new FlxSprite(x, 112).LoadGraphic(A("glow.png"));
            glow.Tint = new Color(255, 150, 40);
            glow.Blend = modes[i];
            Add(glow);

            // a little motion so you can see how they combine
            FlxTween.Tween(glow, new { Y = 122f }, 1f, new TweenOptions { Type = FlxTweenType.PingPong, Ease = FlxEase.SineInOut, StartDelay = i * 0.1f });
            Label(x + 4, 158, modes[i].ToString());
        }
    }

    // ------------------------------------------------------------------ clip rect, strip

    void BuildEffects()
    {
        Label(310, 98, "ClipRect");
        _flag = new FlxSprite(310, 110).LoadGraphic(A("flag.png"));
        Add(_flag);
        FlxTween.Num(0, 1, 2.2f, v => _clip = v, new TweenOptions { Type = FlxTweenType.PingPong, Ease = FlxEase.QuadInOut });

        Label(390, 98, "FlxStrip");
        _wave = FlxStrip.CreateGrid(FlxAssets.GetTexture(A("flag.png")), 16, 4, 64, 32);
        _wave.X = 392; _wave.Y = 112;
        _waveBase = (Vector2[])_wave.Vertices.Clone();
        Add(_wave);
    }

    // ------------------------------------------------------------------ tiles

    void BuildTiles()
    {
        Label(8, 172, "FlxAutoTiling.Full (click)");
        var rnd = new Random(3);
        var map = new int[12 * 4];
        for (int i = 0; i < map.Length; i++) map[i] = rnd.NextDouble() < 0.65 ? 1 : 0;
        _blob = new FlxTilemap().LoadMapFromArray(map, 12, 4, A("full47.png"), 16, 16, FlxAutoTiling.Full);
        _blob.X = 8; _blob.Y = 184;
        Add(_blob);

        Label(222, 172, "FlxTileblock");
        Add(new FlxTileblock(222, 184, 96, 32).LoadTiles(A("bricks.png"), 16, 16));

        // FlxCanvas.Stamp: build a picture out of other sprites' frames
        Label(336, 172, "FlxCanvas.Stamp");
        var shapes = FlxAtlasFrames.FromSparrow(A("shapes.png"), A("shapes.xml"));
        var board = new FlxSprite(336, 184).MakeGraphic(128, 48, new Color(40, 44, 70));
        var canvas = new FlxCanvas(board);
        canvas.DrawRect(0, 0, 128, 48, new Color(90, 100, 150));
        var brush = new FlxSprite().LoadFrames(shapes);
        for (int i = 0; i < 4; i++)
        {
            brush.Frame = 1 + i * 2;                       // orbs of different sizes, stamped at their trimmed position
            canvas.Stamp(brush, 6 + i * 30, 8);
        }
        brush.Frame = 11;
        brush.Tint = new Color(255, 255, 255, 140);
        canvas.Stamp(brush, 50, 20);
        canvas.Apply();
        Add(board);
    }

    // ------------------------------------------------------------------ update

    public override void Update(float elapsed)
    {
        base.Update(elapsed);
        _time += elapsed;

        // walker controls
        for (int i = 0; i < WalkerAnims.Length; i++)
            if (FlxG.Keys.JustPressed(Keys.D1 + i)) { _walker.Animation.Play(WalkerAnims[i], force: true, reversed: _reversed); }
        if (FlxG.Keys.JustPressed(Keys.R)) { _reversed = !_reversed; _walker.Animation.Play(_walker.Animation.CurrentName ?? "walk", force: true, reversed: _reversed); }
        if (FlxG.Keys.JustPressed(Keys.Space)) _walker.Animation.Paused = !_walker.Animation.Paused;
        if (FlxG.Keys.JustPressed(Keys.Up)) _walker.Animation.SpeedScale = Math.Min(4f, _walker.Animation.SpeedScale + 0.5f);
        if (FlxG.Keys.JustPressed(Keys.Down)) _walker.Animation.SpeedScale = Math.Max(0.25f, _walker.Animation.SpeedScale - 0.5f);

        var anim = _walker.Animation;
        _walkerInfo.Text = $"{anim.CurrentName}{(anim.Reversed ? " (reversed)" : "")} x{anim.SpeedScale:0.0}{(anim.Paused ? " paused" : "")}\n{_lastEvent}";

        // rotation: the same angle for both arrows
        _arrowSmooth.Angle = _arrowBaked.Angle = _time * 45f;

        // clip rect reveals the flag from the left
        int w = Math.Max(1, (int)(64 * _clip));
        _flag.ClipRect = new Rectangle(0, 0, w, 32);

        // wave the strip's vertices
        for (int i = 0; i < _wave.Vertices.Length; i++)
        {
            var b = _waveBase[i];
            _wave.Vertices[i] = new Vector2(b.X, b.Y + MathF.Sin(_time * 4f + b.X * 0.18f) * 3f * (b.X / 64f));
        }

        // click a tile to toggle it: FULL auto-tiling picks the right one of the 47 pictures for every neighbour
        if (FlxG.Mouse.JustPressed)
        {
            int i = _blob.GetMapIndexAt(FlxG.Mouse.X, FlxG.Mouse.Y);
            if (i >= 0) _blob.SetTileByIndex(i, _blob.GetTileByIndex(i) > 0 ? 0 : 1);
        }

        if (FlxG.Keys.JustPressed(Keys.Escape)) LauncherState.Return();
    }
}
