using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// Shows the tweens, FlxBar, FlxSave and substates: ease lanes, motion tweens, color / shake tweens, health and
/// meter bars, a bar that follows an object, a counter saved between runs, and a pause substate (P).
/// </summary>
public class EngineDemoState : FlxState
{
    static readonly (string Name, Func<float, float> Ease)[] Eases =
    {
        ("Linear", FlxEase.Linear),
        ("QuadInOut", FlxEase.QuadInOut),
        ("BounceOut", FlxEase.BounceOut),
        ("ElasticOut", FlxEase.ElasticOut),
        ("BackInOut", FlxEase.BackInOut),
        ("ExpoInOut", FlxEase.ExpoInOut),
    };

    float _health = 100, _meter;
    FlxTween? _healthTween;
    FlxSprite _shakeBox = null!;
    FlxText _status = null!, _saveText = null!;
    int _launches, _score, _best;

    public override void Create()
    {
        FlxG.Game.Window.Title = "MonoFlixel - tweens, bars, save, substates";
        FlxG.Cameras.BgColor = new Color(20, 22, 34);
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = true;

        BuildEaseLanes();
        BuildMotion();
        BuildBars();
        BuildSave();

        Label(4, 2, "TWEENS", new Color(216, 235, 162));
        Label(4, 122, "BARS", new Color(216, 235, 162));
        Label(4, 232, "SAVE", new Color(216, 235, 162));

        _status = new FlxText(4, FlxG.Height - 11, FlxG.Width - 8, "");
        _status.Tint = new Color(180, 190, 200);
        Add(_status);
        SetStatus("SPACE: hurt / shake   UP: heal   ENTER: +10 score   P: pause (substate)   ESC: back");
    }

    FlxText Label(float x, float y, string text, Color? color = null, float width = 0)
    {
        var t = new FlxText(x, y, width, text) { Tint = color ?? new Color(140, 150, 170) };
        Add(t);
        return t;
    }

    void SetStatus(string s) => _status.Text = s;

    // ------------------------------------------------------------------ tweens

    void BuildEaseLanes()
    {
        const float x0 = 78, x1 = 300;
        for (int i = 0; i < Eases.Length; i++)
        {
            float y = 16 + i * 17;
            Label(4, y, Eases[i].Name);
            Add(new FlxSprite(x0, y + 5).MakeGraphic((int)(x1 - x0 + 8), 1, new Color(60, 66, 90)));

            // FlxTween.Tween(target, values, duration, options): a box moving along X with a different ease per lane
            var box = new FlxSprite(x0, y + 1).MakeGraphic(9, 9, new Color(250, 200, 70));
            Add(box);
            FlxTween.Tween(box, new { X = x1 }, 1.6f, new TweenOptions { Type = FlxTweenType.PingPong, Ease = Eases[i].Ease, LoopDelay = 0.2f });
        }
    }

    void BuildMotion()
    {
        // circular motion
        Label(322, 8, "Circular / Quad / Color / Shake");
        var dot = new FlxSprite(0, 0).MakeGraphic(6, 6, new Color(120, 220, 120));
        Add(dot);
        FlxTween.CircularMotion(dot, 392, 62, 26, 0, true, 3f, true, new TweenOptions { Type = FlxTweenType.Looping });

        // quad (bezier) motion, back and forth
        var curve = new FlxSprite(0, 0).MakeGraphic(6, 6, new Color(230, 120, 220));
        Add(curve);
        FlxTween.QuadMotion(curve, 330, 108, 392, 18, 456, 108, 2.4f, true,
            new TweenOptions { Type = FlxTweenType.PingPong, Ease = FlxEase.SineInOut });

        // color tween
        var colorBox = new FlxSprite(326, 22).MakeGraphic(14, 14, Color.White);
        Add(colorBox);
        FlxTween.Color(1.5f, new Color(230, 60, 60), new Color(60, 120, 240), c => colorBox.Tint = c,
            new TweenOptions { Type = FlxTweenType.PingPong });

        // shake tween (SPACE)
        _shakeBox = new FlxSprite(448, 22).MakeGraphic(14, 14, new Color(240, 240, 240));
        Add(_shakeBox);
    }

    // ------------------------------------------------------------------ bars

    void BuildBars()
    {
        // the meter every demo bar listens to
        FlxTween.Num(0, 100, 3f, v => _meter = v, new TweenOptions { Type = FlxTweenType.PingPong, Ease = FlxEase.SineInOut });

        // health bar: its value is read from a function every frame
        Label(10, 134, "Health (SPACE hurts, UP heals)");
        var health = new FlxBar(10, 146, FlxBarFillDirection.LeftToRight, 160, 12, () => _health, 0, 100, showBorder: true)
            .CreateFilledBar(new Color(90, 20, 20), new Color(70, 200, 90), showBorder: true, border: Color.White);
        health.OnEmpty = () => SetStatus("K.O.!  press UP to heal");
        health.OnFilled = () => SetStatus("Fully healed");
        Add(health);

        // other directions, all driven by the same meter
        AddBar("RightToLeft", 10, 172, FlxBarFillDirection.RightToLeft);
        AddBar("HorizontalInsideOut", 10, 192, FlxBarFillDirection.HorizontalInsideOut);
        AddBar("HorizontalOutsideIn", 10, 212, FlxBarFillDirection.HorizontalOutsideIn);

        // gradient bar
        Label(262, 134, "Gradient");
        var grad = new FlxBar(262, 146, FlxBarFillDirection.LeftToRight, 130, 12, () => _meter, 0, 100, showBorder: true)
            .CreateGradientBar(new[] { new Color(220, 50, 50), new Color(240, 210, 60), new Color(70, 210, 90) }, new Color(30, 30, 40), true, Color.White);
        Add(grad);

        // vertical bars
        string[] names = { "B>T", "T>B", "In-Out", "Out-In" };
        var dirs = new[] { FlxBarFillDirection.BottomToTop, FlxBarFillDirection.TopToBottom, FlxBarFillDirection.VerticalInsideOut, FlxBarFillDirection.VerticalOutsideIn };
        for (int i = 0; i < dirs.Length; i++)
        {
            float x = 266 + i * 34;
            Label(x - 3, 164, names[i]);
            Add(new FlxBar(x, 176, dirs[i], 16, 48, () => _meter, 0, 100, showBorder: true)
                .CreateFilledBar(new Color(30, 30, 40), new Color(90, 170, 240), true, new Color(200, 200, 210)));
        }

        // a bar that follows an object around
        Label(408, 134, "TrackParent");
        var enemy = new FlxSprite(410, 190).MakeGraphic(20, 20, new Color(220, 120, 40));
        Add(enemy);
        FlxTween.LinearMotion(enemy, 410, 192, 446, 192, 1.6f, true,
            new TweenOptions { Type = FlxTweenType.PingPong, Ease = FlxEase.QuadInOut });
        var follow = new FlxBar(0, 0, FlxBarFillDirection.LeftToRight, 28, 4, () => 100 - _meter, 0, 100, showBorder: true)
            .CreateFilledBar(new Color(60, 20, 20), new Color(230, 70, 70), true, Color.Black);
        follow.TrackParent(enemy, -4, -9);
        Add(follow);
    }

    void AddBar(string name, float x, float y, FlxBarFillDirection dir)
    {
        Label(x + 126, y + 1, name);
        Add(new FlxBar(x, y, dir, 120, 10, () => _meter, 0, 100, showBorder: true)
            .CreateFilledBar(new Color(30, 30, 40), new Color(90, 170, 240), true, new Color(200, 200, 210)));
    }

    // ------------------------------------------------------------------ save

    void BuildSave()
    {
        // FlxG.Save is bound for us and written when the game exits
        _launches = FlxG.Save.Get("engineDemo.launches", 0) + 1;
        _best = FlxG.Save.Get("engineDemo.best", 0);
        FlxG.Save.Set("engineDemo.launches", _launches);
        FlxG.Save.Flush();

        _saveText = new FlxText(4, 243, FlxG.Width - 8, "") { Tint = new Color(160, 200, 160) };
        Add(_saveText);
        RefreshSaveText();
    }

    void RefreshSaveText() =>
        _saveText.Text = $"Opened {_launches}x   score {_score}   best {_best}   file {System.IO.Path.GetFileName(FlxG.Save.FilePath)} in AppData";

    // ------------------------------------------------------------------ update

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        if (FlxG.Keys.JustPressed(Keys.Space))
        {
            FlxTween.Shake(_shakeBox, 0.35f, 0.5f);
            ChangeHealth(-15);
        }
        if (FlxG.Keys.JustPressed(Keys.Up)) ChangeHealth(+25);

        if (FlxG.Keys.JustPressed(Keys.Enter))
        {
            _score += 10;
            if (_score > _best)
            {
                _best = _score;
                FlxG.Save.Set("engineDemo.best", _best);      // kept in memory; written on Flush() or when the game exits
            }
            RefreshSaveText();
        }

        if (FlxG.Keys.JustPressed(Keys.P)) OpenSubState(new PauseSubState());

        if (FlxG.Keys.JustPressed(Keys.Escape))
        {
            FlxG.Save.Flush();
            LauncherState.Return();
        }
    }

    void ChangeHealth(float delta)
    {
        float target = Math.Clamp(_health + delta, 0, 100);
        _healthTween?.Cancel();
        _healthTween = FlxTween.Num(_health, target, 0.35f, v => _health = v, new TweenOptions { Ease = FlxEase.QuadOut });
    }
}

/// <summary>A pause menu drawn over the demo. The demo underneath is not updated while this is open.</summary>
public class PauseSubState : FlxSubState
{
    public PauseSubState() : base(new Color(0, 0, 0, 170)) { }

    public override void Create()
    {
        var title = new FlxText(0, 90, FlxG.Width, "PAUSED", 32) { Alignment = FlxTextAlign.Center, Tint = Color.White };
        title.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black, 2);
        Add(title);

        Add(new FlxButton(FlxG.Width / 2f - 90, 140, "RESUME (P)", Close));
        Add(new FlxButton(FlxG.Width / 2f + 10, 140, "LAUNCHER (Q)", Quit));

        var hint = new FlxText(0, 168, FlxG.Width, "Global tweens and timers are frozen with FlxG.TimeScale = 0")
            { Alignment = FlxTextAlign.Center, Tint = new Color(170, 170, 180) };
        Add(hint);

        // tweens and timers belong to the game, not the state, so stop their clock while paused
        OnOpen = () => FlxG.TimeScale = 0f;
        OnClose = () => FlxG.TimeScale = 1f;
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);
        if (FlxG.Keys.AnyJustPressed(Keys.P, Keys.Escape)) Close();
        if (FlxG.Keys.JustPressed(Keys.Q)) Quit();
    }

    static void Quit() => LauncherState.Return();
}
