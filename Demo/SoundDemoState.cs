using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// Shows the sound system: streamed OGG music with a position bar, seeking, a loop region, fades, pitch and pan; effects from OGG and
/// WAV files with completion callbacks; two sound groups with their own volumes; a looping engine whose volume and pan follow how far
/// your mouse is from it (proximity); and the volume tray (+ - 0).
/// </summary>
public class SoundDemoState : FlxState
{
    const float EngineX = 110, EngineY = 205, EngineRadius = 95;

    FlxSound _music = null!, _engine = null!;
    FlxSprite _listener = null!;
    FlxText _musicInfo = null!, _fxInfo = null!, _proxInfo = null!, _groupInfo = null!, _status = null!;
    FlxSprite _regionBar = null!;
    bool _region, _engineOn = true;
    int _finished;

    public override void Create()
    {
        FlxG.Game.Window.Title = "MonoFlixel - sound";
        FlxG.Cameras.BgColor = new Color(16, 18, 30);
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = false;

        BuildMusic();
        BuildEffects();
        BuildProximity();

        _status = new FlxText(6, FlxG.Height - 10, FlxG.Width - 12, "") { Tint = new Color(150, 160, 185) };
        Add(_status);
    }

    FlxText Label(float x, float y, string text, Color? color = null, float width = 0)
    {
        var t = new FlxText(x, y, width, text) { Tint = color ?? new Color(140, 150, 175) };
        Add(t);
        return t;
    }

    // ------------------------------------------------------------------ music

    void BuildMusic()
    {
        Label(6, 4, "MUSIC  (streamed OGG)", new Color(216, 235, 162));

        // FlxG.Sound.PlayMusic: loops, replaces earlier music, survives state switches (we stop it ourselves on ESC)
        _music = FlxG.Sound.PlayMusic("sound/mode.ogg", 0.7f)!;

        // the position bar reads the sound's Time (milliseconds)
        var bar = new FlxBar(6, 22, FlxBarFillDirection.LeftToRight, 300, 8, () => (float)_music.Time, 0, (float)_music.Length, showBorder: true)
            .CreateFilledBar(new Color(30, 34, 52), new Color(110, 190, 120), true, new Color(120, 130, 160));
        Add(bar);

        // the region used by the loop demo (L), drawn over the bar
        _regionBar = new FlxSprite(6 + 300f * 2000 / (float)_music.Length, 20).MakeGraphic(Math.Max(1, (int)(300f * 3000 / (float)_music.Length)), 12, new Color(255, 210, 80, 70));
        _regionBar.Visible = false;
        Add(_regionBar);

        _musicInfo = Label(6, 36, "", new Color(210, 215, 230), 300);
        Label(6, 66, "SPACE pause/play   LEFT/RIGHT seek 3 s   L loop 2-5 s", new Color(110, 120, 145));
        Label(6, 76, "F fade out   G fade in   UP/DOWN pitch   Z/X pan", new Color(110, 120, 145));
    }

    // ------------------------------------------------------------------ effects and groups

    void BuildEffects()
    {
        Label(320, 4, "EFFECTS", new Color(216, 235, 162));
        Label(320, 16, "1 jump (OGG)    2 jump (WAV)", new Color(110, 120, 145));
        Label(320, 26, "3 explosion  4 hit", new Color(110, 120, 145));
        _fxInfo = Label(320, 40, "", new Color(210, 215, 230), 150);

        Label(320, 78, "GROUPS (shared volumes)", new Color(216, 235, 162));
        Label(320, 90, "Q/W music    A/S effects", new Color(110, 120, 145));
        _groupInfo = Label(320, 102, "", new Color(210, 215, 230), 150);

        Label(6, 96, "Fade, pitch, pan: this sound only. Groups: all in them.", new Color(90, 100, 125));
        Label(6, 106, "Master volume: + - and 0 (mute), with the tray.", new Color(90, 100, 125));
    }

    // ------------------------------------------------------------------ proximity

    void BuildProximity()
    {
        Label(6, 124, "PROXIMITY  (move the mouse, E switches the engine)", new Color(216, 235, 162));

        // the engine and the area where it can be heard
        var area = new FlxSprite(EngineX - EngineRadius, EngineY - EngineRadius).MakeGraphic((int)EngineRadius * 2, (int)EngineRadius * 2, Color.Transparent);
        var canvas = new FlxCanvas(area);
        canvas.DrawCircle((int)EngineRadius, (int)EngineRadius, (int)EngineRadius - 1, new Color(80, 95, 140), 1);
        canvas.DrawCircle((int)EngineRadius, (int)EngineRadius, (int)EngineRadius / 2, new Color(60, 75, 115), 1);
        canvas.Apply();
        Add(area);

        var engine = new FlxSprite(EngineX - 6, EngineY - 6).MakeGraphic(12, 12, new Color(255, 160, 60));
        Add(engine);
        Label(EngineX - 20, EngineY + 10, "engine", new Color(255, 190, 110));

        _listener = new FlxSprite(0, 0).MakeGraphic(10, 10, new Color(90, 200, 255));
        _listener.Moves = false;
        Add(_listener);

        // a looping sound that gets quieter, and moves between the speakers, with distance from the listener
        _engine = FlxG.Sound.Load("sound/jet.ogg", 0.9f, looped: true).Proximity(EngineX, EngineY, _listener, EngineRadius);
        _engine.Persist = false;
        _engine.Play();

        _proxInfo = Label(230, 150, "", new Color(210, 215, 230), 240);

        var volume = new FlxBar(230, 190, FlxBarFillDirection.LeftToRight, 200, 8, () => _engine.ActualVolume, 0, 1, showBorder: true)
            .CreateFilledBar(new Color(30, 34, 52), new Color(255, 170, 60), true, new Color(120, 130, 160));
        Add(volume);
        Label(230, 180, "volume", new Color(110, 120, 145));

        // pan: -1 (left) to 1 (right), shown as a bar that fills from the middle outwards... the bar's centre is "centred"
        var pan = new FlxBar(230, 216, FlxBarFillDirection.LeftToRight, 200, 8, () => (_engine.VoicePanForDemo() + 1f) / 2f, 0, 1, showBorder: true)
            .CreateFilledBar(new Color(30, 34, 52), new Color(90, 200, 255), true, new Color(120, 130, 160));
        Add(pan);
        Label(230, 206, "pan  (left ... right)", new Color(110, 120, 145));
    }

    // ------------------------------------------------------------------ update

    void PlayEffect(string path)
    {
        // Play: fire and forget; the sound is recycled when it ends, onComplete runs then
        FlxG.Sound.Play(path, 0.8f, onComplete: () => _finished++);
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        // the listener is the mouse
        _listener.X = FlxG.Mouse.X - 5;
        _listener.Y = FlxG.Mouse.Y - 5;

        var k = FlxG.Keys;
        if (k.JustPressed(Keys.D1)) PlayEffect("sound/jump.ogg");
        if (k.JustPressed(Keys.D2)) PlayEffect("mode/sounds/jump.wav");
        if (k.JustPressed(Keys.D3)) PlayEffect("sound/asplode.ogg");
        if (k.JustPressed(Keys.D4)) PlayEffect("sound/hit.ogg");

        if (k.JustPressed(Keys.Space)) { if (_music.Playing) _music.Pause(); else _music.Play(); }
        if (k.JustPressed(Keys.Left)) _music.Time = Math.Max(0, _music.Time - 3000);
        if (k.JustPressed(Keys.Right)) _music.Time = Math.Min(_music.Length - 100, _music.Time + 3000);
        if (k.JustPressed(Keys.F)) _music.FadeOut(1.5f);
        if (k.JustPressed(Keys.G)) _music.FadeIn(1.5f, 0f, 0.7f);
        if (k.JustPressed(Keys.Up)) _music.Pitch += 0.1f;
        if (k.JustPressed(Keys.Down)) _music.Pitch -= 0.1f;
        if (k.JustPressed(Keys.Z)) _music.Pan -= 0.25f;
        if (k.JustPressed(Keys.X)) _music.Pan += 0.25f;

        if (k.JustPressed(Keys.L))
        {
            // play only 2 s to 5 s, over and over: LoopTime is where it starts over, EndTime where it ends
            _region = !_region;
            _regionBar.Visible = _region;
            _music.LoopTime = _region ? 2000 : 0;
            _music.Play(forceRestart: true, startTime: _region ? 2000 : 0, endTime: _region ? 5000 : null);
        }

        if (k.JustPressed(Keys.Q)) FlxG.Sound.DefaultMusicGroup.Volume -= 0.1f;
        if (k.JustPressed(Keys.W)) FlxG.Sound.DefaultMusicGroup.Volume += 0.1f;
        if (k.JustPressed(Keys.A)) FlxG.Sound.DefaultSoundGroup.Volume -= 0.1f;
        if (k.JustPressed(Keys.S)) FlxG.Sound.DefaultSoundGroup.Volume += 0.1f;

        if (k.JustPressed(Keys.E))
        {
            _engineOn = !_engineOn;
            if (_engineOn) _engine.Play(); else _engine.Pause();
        }

        UpdateTexts();

        if (k.JustPressed(Keys.Escape))
        {
            FlxG.Sound.DefaultMusicGroup.Volume = 1f;
            FlxG.Sound.DefaultSoundGroup.Volume = 1f;
            FlxG.Sound.Destroy(true);
            LauncherState.Return();
        }
    }

    void UpdateTexts()
    {
        _musicInfo.Text =
            $"{(_music.Playing ? "playing" : _music.Paused ? "paused" : "stopped")}  {_music.Time / 1000:0.0} / {_music.Length / 1000:0.0} s   loops {_music.LoopCount}\n" +
            $"volume {_music.Volume:0.00}{(_music.Fading ? " (fading)" : "")}   pitch {_music.Pitch:0.0}   pan {_music.Pan:0.00}";

        _fxInfo.Text = $"finished {_finished}\nvoices in the pool {FlxG.Sound.List.Length}";
        _groupInfo.Text = $"music {FlxG.Sound.DefaultMusicGroup.Volume:0.0}   effects {FlxG.Sound.DefaultSoundGroup.Volume:0.0}\n" +
                          $"master {(FlxG.Sound.Muted ? "muted" : FlxG.Sound.Volume.ToString("0.0"))}";

        float dist = Vector2.Distance(_listener.Midpoint, new Vector2(EngineX, EngineY));
        _proxInfo.Text = $"distance {dist:0} px of {EngineRadius:0}\nengine volume {_engine.ActualVolume:0.00}";

        _status.Text = FlxG.Sound.NoDevice ? "no audio device: playing silently" : "+ / - volume   0 mute   ESC back";
    }
}

static class SoundDemoExtensions
{
    /// <summary>The pan the engine sound is using (set by proximity), for the display.</summary>
    public static float VoicePanForDemo(this FlxSound s) => s.Pan;
}
