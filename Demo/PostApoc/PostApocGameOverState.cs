using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo.PostApoc;

/// <summary>
/// The end of a run: "You Failed to Survive.", how it ended, what was achieved (miles, meals, drinks, kills, rests, counted up one
/// line at a time), the score and the high score. It arrives through the tiles transition that closes over the road.
/// </summary>
public class PostApocGameOverState : FlxTransitionableState
{
    readonly RunStats _stats;
    readonly string _cause;
    readonly bool _female;
    readonly int _score, _previousBest;
    readonly bool _newBest;

    readonly FlxText[] _lines = new FlxText[5];
    readonly string[] _final = new string[5];
    FlxTypeText _title = null!;
    FlxText _scoreText = null!, _best = null!, _hint = null!;
    bool _titleDone;
    float _time;
    int _shownLines;
    bool _scoreDone, _leaving;

    // what each line shows: the number counts up to its value
    readonly (string Before, float Value, bool Decimal, string One, string Many)[] _facts;

    public PostApocGameOverState(RunStats stats, string cause, bool female)
    {
        _stats = stats;
        _cause = cause;
        _female = female;
        _score = stats.Score;
        _previousBest = PostApocData.HighScore;
        _newBest = _score > _previousBest;
        if (_newBest) PostApocData.HighScore = _score;

        TransIn = new TransitionData(TransitionType.Tiles, new Color(110, 8, 8), 1.1f, new Vector2(-1, -1), new TransitionTileData(FlxTransitionAssets.Diamond, 32, 32));
        TransOut = null;

        _facts = new[]
        {
            ("You travelled ", stats.Miles, true, " mile", " miles"),
            ("You ate ", (float)stats.Eaten, false, " food item", " food items"),
            ("You drank ", (float)stats.Drank, false, " time", " times"),
            ("You killed ", (float)stats.Kills, false, " enemy", " enemies"),
            ("You rested ", (float)stats.Rests, false, " time", " times"),
        };
    }

    static string Format((string Before, float Value, bool Decimal, string One, string Many) f, float shown)
    {
        string number = f.Decimal ? shown.ToString("0.0") : ((int)MathF.Round(shown)).ToString();
        bool one = f.Decimal ? MathF.Abs(f.Value - 1f) < 0.05f : (int)MathF.Round(f.Value) == 1;
        return f.Before + number + (one ? f.One : f.Many);
    }

    public override void Create()
    {
        base.Create();                      // starts the transition in

        FlxG.Game.Window.Title = "POST APOC";
        FlxG.Cameras.BgColor = Color.Black;
        FlxG.Mouse.Visible = true;
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.LoadCursor("postapoc/cursor.png");

        var sky = PostApocMenuState.Sky();
        sky.Tint = new Color(255, 130, 120);
        Add(sky);

        // the title is typed, slowly, and the rest follows when it is done
        const string title = "You Failed to Survive.";
        _title = new FlxTypeText(0, 26, 0, title, 32) { Tint = new Color(215, 60, 45), ShowCursor = true, UseDefaultSound = true, DefaultSoundVolume = 0.3f, Delay = 0.07f };
        _title.SetBorderStyle(FlxTextBorderStyle.Outline, new Color(20, 4, 4), 2);
        _title.X = (FlxG.Width - _title.MeasureLine(title)) / 2f;
        _title.SetTypingVariation(0.5f);
        _title.SkipKeys = new[] { Keys.Enter, Keys.Space, Keys.Z };
        Add(_title);
        _title.Start(callback: () => { _titleDone = true; new FlxTimer().Start(1f, _ => _title.ShowCursor = false); });
        Add(new FlxText(0, 74, FlxG.Width, _cause) { Alignment = FlxTextAlign.Center, Tint = new Color(180, 130, 110) });

        for (int i = 0; i < _lines.Length; i++)
        {
            _lines[i] = new FlxText(0, 112 + i * 22, FlxG.Width, "", 16) { Alignment = FlxTextAlign.Center, Tint = new Color(225, 205, 175) };
            _lines[i].Visible = false;
            Add(_lines[i]);
        }

        _scoreText = new FlxText(0, 242, FlxG.Width, "", 32) { Alignment = FlxTextAlign.Center, Tint = new Color(255, 215, 90) };
        _scoreText.SetBorderStyle(FlxTextBorderStyle.Outline, new Color(30, 14, 4), 2);
        _scoreText.Visible = false;
        Add(_scoreText);

        _best = new FlxText(0, 284, FlxG.Width, "", 16) { Alignment = FlxTextAlign.Center, Tint = new Color(190, 150, 110) };
        _best.Visible = false;
        Add(_best);

        _hint = new FlxText(0, FlxG.Height - 26, FlxG.Width, "ENTER / Z: try again        ESC: menu") { Alignment = FlxTextAlign.Center, Tint = new Color(120, 90, 80) };
        _hint.Visible = false;
        Add(_hint);
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);          // (the transition is a substate: this only runs when it is over)
        if (!_titleDone) return;       // the stats wait for the title
        _time += elapsed;

        // the lines come one after another, each counting up
        const float start = 0.4f, gap = 0.4f, count = 0.4f;
        for (int i = 0; i < _lines.Length; i++)
        {
            float t = (_time - (start + i * gap)) / count;
            if (t <= 0) continue;
            if (!_lines[i].Visible) { _lines[i].Visible = true; PostApocData.Sfx("pickup", 0.35f); _shownLines = i + 1; }
            _lines[i].Text = Format(_facts[i], _facts[i].Value * FlxEase.QuadOut(Math.Clamp(t, 0f, 1f)));
        }

        // then the score, counting up, and the best
        float scoreAt = start + _lines.Length * gap + 0.3f;
        if (_time > scoreAt)
        {
            _scoreText.Visible = true;
            float t = Math.Clamp((_time - scoreAt) / 1.0f, 0f, 1f);
            _scoreText.Text = "SCORE  " + (int)(_score * FlxEase.QuadOut(t));

            if (t >= 1f && !_scoreDone)
            {
                _scoreDone = true;
                _best.Visible = _hint.Visible = true;
                _best.Text = _newBest ? "NEW HIGH SCORE!" : "High score  " + _previousBest;
                PostApocData.Sfx(_newBest ? "pistol_gunshot" : "pickup", 0.4f);
                if (_newBest) FlxG.Camera.Flash(new Color(255, 230, 120), 0.4f);
            }
        }

        if (_scoreDone && _newBest) _best.Tint = FlxColor.FromHsb(_time * 200f, 0.5f, 1f);          // the new best flashes through the colors

        // keys skip the counting; once it is done they leave
        if (FlxG.Keys.AnyJustPressed(Keys.Enter, Keys.Space, Keys.Z) || FlxG.Gamepad.JustPressed(FlxGamepadInputID.A))
        {
            if (!_scoreDone) _time = 100f;
            else TryAgain();
        }
        if (_scoreDone && FlxG.Keys.JustPressed(Keys.Escape)) Menu();
    }

    void TryAgain()
    {
        if (_leaving) return;
        _leaving = true;
        FlxG.Camera.Fade(Color.Black, 0.5f, onComplete: () =>
            FlxG.SwitchState(new PostApocPlayState(false, _female), PostApocMenuState.Width, PostApocMenuState.Height, PostApocMenuState.Zoom));
    }

    void Menu()
    {
        if (_leaving) return;
        _leaving = true;
        FlxG.Camera.Fade(Color.Black, 0.5f, onComplete: PostApocMenuState.Open);
    }
}
