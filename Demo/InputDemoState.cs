using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// Shows the input system. F1 keyboard and typed text, F2 mouse and FlxMouseEvent (draggable boxes, a ring you can only grab by its pixels),
/// F3 gamepads (four slots, labels for the kind of pad, sticks, triggers, rumble), F4 actions with rebinding saved as JSON,
/// F5 swipes (a mouse drag works like a finger). TAB cycles the pages.
/// </summary>
public class InputDemoState : FlxState
{
    static readonly string[] Titles = { "KEYBOARD", "MOUSE", "GAMEPADS", "ACTIONS", "SWIPES" };

    readonly FlxGroup[] _pages = new FlxGroup[5];
    int _page;
    FlxText _header = null!, _status = null!;

    FlxText Label(int page, float x, float y, string text, Color? color = null, float width = 0)
    {
        var t = new FlxText(x, y, width, text) { Tint = color ?? new Color(210, 215, 230) };
        _pages[page].Add(t);
        return t;
    }

    FlxSprite Rect(int page, float x, float y, int w, int h, Color c)
    {
        var s = new FlxSprite(x, y).MakeGraphic(w, h, c);
        s.Moves = false;
        _pages[page].Add(s);
        return s;
    }

    public override void Create()
    {
        FlxG.Game.Window.Title = "MonoFlixel - input";
        FlxG.Cameras.BgColor = new Color(16, 18, 30);
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = true;
        FlxG.Touches.SimulateWithMouse = true;

        for (int i = 0; i < _pages.Length; i++) { _pages[i] = new FlxGroup(); Add(_pages[i]); }

        _header = new FlxText(6, 4, FlxG.Width - 12, "") { Tint = new Color(216, 235, 162) };
        Add(_header);
        _status = new FlxText(6, FlxG.Height - 10, FlxG.Width - 12, "F1-F5 or TAB: page    ESC: back") { Tint = new Color(110, 120, 145) };
        Add(_status);

        BuildKeyboard();
        BuildMouse();
        BuildPads();
        BuildActions();
        BuildSwipes();
        ShowPage(0);
    }

    void ShowPage(int p)
    {
        _page = p;
        for (int i = 0; i < _pages.Length; i++) _pages[i].Visible = _pages[i].Active = i == p;
        _header.Text = $"INPUT  -  {Titles[p]}   (F1 keyboard  F2 mouse  F3 pads  F4 actions  F5 swipes)";
    }

    // ------------------------------------------------------------------ keyboard

    FlxText _kbHeld = null!, _kbEvents = null!, _kbTyped = null!;
    string _typed = "";
    readonly List<string> _kbLog = new();

    void BuildKeyboard()
    {
        Label(0, 6, 22, "Hold keys: they are listed with their names.", new Color(140, 150, 175));
        _kbHeld = Label(0, 6, 36, "", width: 460);
        Label(0, 6, 70, "JustPressed / JustReleased (latest first):", new Color(140, 150, 175));
        _kbEvents = Label(0, 6, 82, "", new Color(255, 210, 120), 220);
        Label(0, 250, 70, "Typed text (FlxG.Keys.TypedText, backspace works):", new Color(140, 150, 175));
        _kbTyped = Label(0, 250, 82, "", new Color(120, 220, 160), 220);
    }

    void UpdateKeyboard()
    {
        var k = FlxG.Keys;
        _kbHeld.Text = k.AnyKey ? "down: " + string.Join("  ", k.PressedKeys.Select(FlxKey.Name)) : "down: (nothing)";

        foreach (var key in k.JustPressedKeys) _kbLog.Insert(0, "+ " + FlxKey.Name(key));
        foreach (var key in k.JustReleasedKeys) _kbLog.Insert(0, "- " + FlxKey.Name(key));
        if (_kbLog.Count > 12) _kbLog.RemoveRange(12, _kbLog.Count - 12);
        _kbEvents.Text = string.Join("\n", _kbLog);

        foreach (char c in k.TypedText)
        {
            if (c == '\b') { if (_typed.Length > 0) _typed = _typed[..^1]; }
            else if (c == '\r' || c == '\n') _typed += "\n";
            else if (!char.IsControl(c)) _typed += c;
        }
        if (_typed.Length > 200) _typed = _typed[^200..];
        _kbTyped.Text = _typed + "_";
    }

    // ------------------------------------------------------------------ mouse

    FlxText _mouseInfo = null!, _mouseEvents = null!;
    FlxSprite _ring = null!;
    int _ringClicks;
    FlxSprite? _dragging;
    Vector2 _dragOffset;
    readonly List<FlxSprite> _boxes = new();

    void BuildMouse()
    {
        _mouseInfo = Label(1, 6, 22, "", width: 220);
        _mouseEvents = Label(1, 6, 130, "", new Color(255, 210, 120), 220);

        // three boxes you can drag; the one on top is grabbed first (it was added last)
        var colors = new[] { new Color(200, 80, 80), new Color(80, 170, 90), new Color(80, 120, 210) };
        for (int i = 0; i < 3; i++)
        {
            var box = Rect(1, 250 + i * 30, 40 + i * 12, 60, 60, colors[i]);
            _boxes.Add(box);
            FlxMouseEvent.Add(box,
                onMouseDown: b => { _dragging = b; _dragOffset = new Vector2(FlxG.Mouse.X - b.X, FlxG.Mouse.Y - b.Y); b.Alpha = 0.7f; Log($"down on box {_boxes.IndexOf(b)}"); },
                onMouseUp: b => { _dragging = null; b.Alpha = 1f; },
                onMouseOver: b => b.Tint = Color.White,
                onMouseOut: b => b.Tint = Color.Lerp(Color.White, Color.Gray, 0.2f));
            FlxMouseEvent.SetDoubleClickCallback(box, b => { b.Angle += 45; Log("double click: turned"); });
        }

        // a ring made of pixels: you can only grab it on the ring itself, the middle is transparent
        _ring = Rect(1, 270, 140, 70, 70, Color.Transparent);
        var canvas = new FlxCanvas(_ring);
        for (int r = 28; r <= 34; r++) canvas.DrawCircle(35, 35, r, new Color(255, 200, 70), 1);
        canvas.Apply();
        FlxMouseEvent.Add(_ring, onMouseOver: r => r.Tint = Color.Cyan, onMouseOut: r => r.Tint = Color.White);
        FlxMouseEvent.SetClickCallback(_ring, r => { _ringClicks++; Log("ring clicked"); });
        Label(1, 250, 125, "pixel perfect ring (click the ring, not the hole)", new Color(140, 150, 175), 220);
    }

    readonly List<string> _mouseLog = new();
    void Log(string s) { _mouseLog.Insert(0, s); if (_mouseLog.Count > 8) _mouseLog.RemoveAt(8); }

    void UpdateMouse()
    {
        var m = FlxG.Mouse;
        _mouseInfo.Text =
            $"game {m.X:0},{m.Y:0}   window {m.ScreenX:0},{m.ScreenY:0}\n" +
            $"world {m.WorldPosition.X:0},{m.WorldPosition.Y:0}   delta {m.Delta.X:0.#},{m.Delta.Y:0.#}\n" +
            $"left {(m.Left.Pressed ? "DOWN" : "-")}  middle {(m.Middle.Pressed ? "DOWN" : "-")}  right {(m.Right.Pressed ? "DOWN" : "-")}\n" +
            $"clicks {m.Left.ClickCount}  wheel {m.Wheel:0.#}\n" +
            $"under mouse: {(FlxMouseEvent.ObjectUnderMouse is { } o ? (o == _ring ? "ring" : "box " + _boxes.IndexOf((FlxSprite)o)) : "none")}\n" +
            $"ring clicks {_ringClicks}";

        if (m.JustScrolled) Log($"wheel {m.Wheel:0.#}");
        if (m.Middle.JustPressed) Log("middle button");
        if (m.Right.JustPressed) Log("right button");

        if (_dragging != null)
        {
            _dragging.X = m.X - _dragOffset.X;
            _dragging.Y = m.Y - _dragOffset.Y;
        }
        _mouseEvents.Text = string.Join("\n", _mouseLog);
    }

    // ------------------------------------------------------------------ gamepads

    readonly FlxText[] _padInfo = new FlxText[4];
    readonly FlxSprite[] _leftDot = new FlxSprite[4], _rightDot = new FlxSprite[4];

    void BuildPads()
    {
        for (int i = 0; i < 4; i++)
        {
            float y = 22 + i * 56;
            Label(2, 6, y, $"PAD {i + 1}", new Color(216, 235, 162));
            _padInfo[i] = Label(2, 60, y, "", width: 330);

            Rect(2, 400, y, 32, 32, new Color(36, 40, 60));
            Rect(2, 438, y, 32, 32, new Color(36, 40, 60));
            _leftDot[i] = Rect(2, 414, y + 14, 4, 4, new Color(110, 190, 255));
            _rightDot[i] = Rect(2, 452, y + 14, 4, 4, new Color(255, 170, 90));
        }
    }

    void UpdatePads()
    {
        for (int i = 0; i < 4; i++)
        {
            var p = FlxG.Gamepads[i];
            _leftDot[i].Visible = _rightDot[i].Visible = p.Connected;
            if (!p.Connected) { _padInfo[i].Text = "not connected"; _padInfo[i].Tint = new Color(100, 108, 130); continue; }

            _padInfo[i].Tint = new Color(210, 215, 230);
            var held = Enum.GetValues<FlxGamepadInputID>()
                .Where(id => id is not (FlxGamepadInputID.None or FlxGamepadInputID.LeftAnalogStick or FlxGamepadInputID.RightAnalogStick) && p.Pressed(id))
                .Select(p.GetInputLabel);
            _padInfo[i].Text =
                $"{p.Name}  ({p.Model})\n" +
                $"pressed: {string.Join(" ", held)}\n" +
                $"LT {p.LeftTrigger:0.00}  RT {p.RightTrigger:0.00}  press {p.GetInputLabel(FlxGamepadInputID.A)} to rumble";

            _leftDot[i].X = 414 + p.LeftStick.X * 14; _leftDot[i].Y = 22 + i * 56 + 14 + p.LeftStick.Y * 14;
            _rightDot[i].X = 452 + p.RightStick.X * 14; _rightDot[i].Y = 22 + i * 56 + 14 + p.RightStick.Y * 14;

            if (p.JustPressed(FlxGamepadInputID.A)) p.Rumble(0.4f, 0.8f, 0.4f);
        }

        _status.Text = FlxG.Gamepads.NumActive == 0 ? "no gamepad found: plug one in (they show up on their own)" : "F1-F5 or TAB: page    ESC: back";
    }

    // ------------------------------------------------------------------ actions

    FlxActionSet _set = null!;
    FlxActionDigital _jump = null!, _fire = null!;
    FlxActionAnalog _move = null!;
    FlxSprite _player = null!;
    FlxText _actionInfo = null!;
    float _jumpTime;
    bool _rebinding;
    readonly List<string> _shots = new();
    readonly List<FlxSprite> _bullets = new();

    void BuildActions()
    {
        // the player does not know which device asks: it only knows the actions
        _jump = new FlxActionDigital("jump", _ => _jumpTime = 0.35f).AddKey(Keys.Space).AddGamepad(FlxGamepadInputID.A);
        _fire = new FlxActionDigital("fire", _ => Fire()).AddMouse(FlxMouseButtonID.Left).AddGamepad(FlxGamepadInputID.X);
        _move = new FlxActionAnalog("move").AddKeys(Keys.W, Keys.S, Keys.A, Keys.D).AddKeys(Keys.Up, Keys.Down, Keys.Left, Keys.Right).AddGamepad(FlxGamepadInputID.LeftAnalogStick);
        _set = new FlxActionSet("player", _jump, _fire, _move);
        FlxG.Actions.Activate(_set);

        // saved bindings (made with R below) come back here
        if (FlxG.Save.Get<string>("bindings", "") is { Length: > 0 } json)
        {
            try { FlxG.Actions.ApplyBindings(json); } catch (Exception) { /* a broken save is ignored */ }
        }

        _player = Rect(3, 230, 130, 16, 16, new Color(90, 200, 255));
        _actionInfo = Label(3, 6, 22, "", width: 440);
        Label(3, 6, 90, "Move: WASD / arrows / left stick.  Jump: Space / A (bigger while jumping).\nFire: left click / X.  R: rebind jump (press a key, ESC cancels).  S: save bindings   C: clear save",
            new Color(140, 150, 175), 460);
    }

    void Fire()
    {
        if (_page != 3) return;
        var b = Rect(3, _player.X + 5, _player.Y + 5, 6, 6, new Color(255, 210, 80));
        b.Velocity = Vector2.Normalize(FlxG.Mouse.Position - _player.Midpoint + new Vector2(0.001f, 0)) * 200;
        b.Moves = true;
        _bullets.Add(b);
    }

    void UpdateActions(float dt)
    {
        _player.X = Math.Clamp(_player.X + _move.X * 120 * dt, 0, FlxG.Width - 16);
        _player.Y = Math.Clamp(_player.Y + _move.Y * 120 * dt, 130, FlxG.Height - 30);

        _jumpTime = Math.Max(0, _jumpTime - dt);
        _player.Scale = _jumpTime > 0 ? new Vector2(1.6f, 1.6f) : Vector2.One;

        for (int i = _bullets.Count - 1; i >= 0; i--)
            if (_bullets[i].X < -10 || _bullets[i].X > FlxG.Width || _bullets[i].Y < -10 || _bullets[i].Y > FlxG.Height)
            {
                _pages[3].Remove(_bullets[i]);
                _bullets.RemoveAt(i);
            }

        if (_rebinding)
        {
            if (FlxG.Keys.JustPressed(Keys.Escape)) _rebinding = false;
            else if (FlxG.Keys.FirstJustPressed is { } key)
            {
                // replace the keyboard binding of jump, keep the pad one
                foreach (var old in _jump.Inputs.OfType<FlxKeyInput>().ToArray()) _jump.RemoveInput(old);
                _jump.AddKey(key);
                _rebinding = false;
            }
        }
        else
        {
            if (FlxG.Keys.JustPressed(Keys.R)) _rebinding = true;
            if (FlxG.Keys.JustPressed(Keys.S)) { FlxG.Save.Set("bindings", FlxG.Actions.ExportBindings()); FlxG.Save.Flush(); }
            if (FlxG.Keys.JustPressed(Keys.C)) { FlxG.Save.Set("bindings", ""); FlxG.Save.Flush(); }
        }

        _actionInfo.Text =
            $"move   {_move.X:0.00}, {_move.Y:0.00}   ({_move.Inputs.Count} inputs)\n" +
            $"jump   {(_jump.Triggered ? "TRIGGERED" : "-")}   bound to: {Describe(_jump)}\n" +
            $"fire   {(_fire.Triggered ? "TRIGGERED" : "-")}   bound to: {Describe(_fire)}\n" +
            (_rebinding ? "PRESS THE NEW KEY FOR JUMP..." : "saved: " + (FlxG.Save.Get<string>("bindings", "").Length > 0 ? "yes" : "no"));
    }

    static string Describe(FlxActionDigital a) => string.Join(", ", a.Inputs.Select(i => i switch
    {
        FlxKeyInput k => FlxKey.Name(k.Key),
        FlxMouseButtonInput m => "mouse " + m.Button,
        FlxGamepadButtonInput g => "pad " + g.Id,
        _ => i.GetType().Name
    }));

    // ------------------------------------------------------------------ swipes

    FlxText _swipeInfo = null!;
    FlxSprite _ball = null!;
    readonly List<string> _swipeLog = new();

    void BuildSwipes()
    {
        Label(4, 6, 22, "Drag with the mouse (or a finger): a drag longer than 8 px is a swipe.\nThe ball is flicked in the direction you swiped.", new Color(140, 150, 175), 460);
        _swipeInfo = Label(4, 6, 60, "", new Color(255, 210, 120), 220);
        _ball = Rect(4, 330, 150, 14, 14, new Color(255, 120, 120));
        _ball.Moves = true;
    }

    void UpdateSwipes()
    {
        foreach (var s in FlxG.Touches.Swipes)
        {
            _swipeLog.Insert(0, $"{s.Direction}  {s.Distance:0} px  {s.Degrees:0} deg  {s.Speed:0} px/s");
            if (_swipeLog.Count > 10) _swipeLog.RemoveAt(10);
            _ball.Velocity = new Vector2(MathF.Cos(s.Radians), MathF.Sin(s.Radians)) * Math.Min(300, s.Speed);
        }

        _ball.Velocity *= 0.985f;
        if (_ball.X < 0 || _ball.X > FlxG.Width - 14) { _ball.Velocity = new Vector2(-_ball.Velocity.X, _ball.Velocity.Y); _ball.X = Math.Clamp(_ball.X, 0, FlxG.Width - 14); }
        if (_ball.Y < 30 || _ball.Y > FlxG.Height - 24) { _ball.Velocity = new Vector2(_ball.Velocity.X, -_ball.Velocity.Y); _ball.Y = Math.Clamp(_ball.Y, 30, FlxG.Height - 24); }

        var t = FlxG.Touches.List;
        _swipeInfo.Text = (t.Count > 0 ? $"finger at {t[0].X:0},{t[0].Y:0}\n" : "no finger\n") + string.Join("\n", _swipeLog);
    }

    // ------------------------------------------------------------------ update

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        // while rebinding, the keyboard belongs to the rebind: no page switching, no leaving
        if (!_rebinding)
        {
            for (int i = 0; i < 5; i++) if (FlxG.Keys.JustPressed(Keys.F1 + i)) ShowPage(i);
            if (FlxG.Keys.JustPressed(Keys.Tab)) ShowPage((_page + 1) % 5);
        }

        // boxes and ring only react while their page is shown
        bool mousePage = _page == 1;
        foreach (var b in _boxes) FlxMouseEvent.SetMouseEnabled(b, mousePage);
        FlxMouseEvent.SetMouseEnabled(_ring, mousePage);

        switch (_page)
        {
            case 0: UpdateKeyboard(); break;
            case 1: UpdateMouse(); break;
            case 2: UpdatePads(); break;
            case 4: UpdateSwipes(); break;
        }
        UpdateActions(elapsed);                  // the action set always runs; only its drawing is on page 4
        if (_page != 2) _status.Text = "F1-F5 or TAB: page    ESC: back";

        if (!_rebinding && FlxG.Keys.JustPressed(Keys.Escape))
        {
            FlxG.Touches.SimulateWithMouse = false;
            LauncherState.Return();
        }
    }
}
