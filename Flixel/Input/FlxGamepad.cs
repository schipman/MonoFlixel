using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel;

/// <summary>The buttons, triggers and sticks of a gamepad, by position (A is the bottom button whatever the pad calls it).</summary>
public enum FlxGamepadInputID
{
    None,
    A, B, X, Y,
    Back, Guide, Start,
    LeftStickClick, RightStickClick,
    LeftShoulder, RightShoulder,
    DPadUp, DPadDown, DPadLeft, DPadRight,
    LeftTrigger, RightTrigger,
    /// <summary>The left stick pushed in a direction (counts as pressed past <see cref="FlxGamepad.AnalogThreshold"/>).</summary>
    LeftStickUp, LeftStickDown, LeftStickLeft, LeftStickRight,
    RightStickUp, RightStickDown, RightStickLeft, RightStickRight,
    /// <summary>The sticks as a whole: pressed when pushed in any direction.</summary>
    LeftAnalogStick, RightAnalogStick
}

public enum FlxGamepadModel { Xbox360, XboxOne, XboxSeries, PS3, PS4, PS5, Switch, Generic }

/// <summary>How the dead zone of a stick is applied.</summary>
public enum FlxGamepadDeadZoneMode
{
    /// <summary>Each axis on its own: small movement is zero on that axis.</summary>
    Independent,
    /// <summary>The stick as a whole: inside the circle it is zero, outside it is rescaled so it starts from zero at the edge.</summary>
    Circular
}

public static class FlxGamepadModels
{
    /// <summary>Works out what kind of pad it is from the name the system reports.</summary>
    public static FlxGamepadModel Detect(string? name)
    {
        if (string.IsNullOrEmpty(name)) return FlxGamepadModel.Generic;
        var n = name.ToLowerInvariant();
        if (n.Contains("dualsense") || n.Contains("ps5")) return FlxGamepadModel.PS5;
        if (n.Contains("dualshock 4") || n.Contains("dualshock4") || n.Contains("ps4") || n.Contains("wireless controller")) return FlxGamepadModel.PS4;
        if (n.Contains("dualshock") || n.Contains("ps3") || n.Contains("playstation(r)3")) return FlxGamepadModel.PS3;
        if (n.Contains("switch") || n.Contains("pro controller") || n.Contains("joy-con") || n.Contains("joycon")) return FlxGamepadModel.Switch;
        if (n.Contains("series")) return FlxGamepadModel.XboxSeries;
        if (n.Contains("xbox one") || n.Contains("xboxone")) return FlxGamepadModel.XboxOne;
        if (n.Contains("xbox 360") || n.Contains("xbox360") || n.Contains("x360") || n.Contains("xinput") || n.Contains("xbox")) return FlxGamepadModel.Xbox360;
        return FlxGamepadModel.Generic;
    }

    /// <summary>What this button is printed as on that kind of pad: "Cross" on a PlayStation pad, "B" on a Switch pad for the bottom button...</summary>
    public static string Label(FlxGamepadModel model, FlxGamepadInputID id)
    {
        bool ps = model is FlxGamepadModel.PS3 or FlxGamepadModel.PS4 or FlxGamepadModel.PS5;
        bool nin = model == FlxGamepadModel.Switch;

        switch (id)
        {
            case FlxGamepadInputID.A: return ps ? "Cross" : nin ? "B" : "A";
            case FlxGamepadInputID.B: return ps ? "Circle" : nin ? "A" : "B";
            case FlxGamepadInputID.X: return ps ? "Square" : nin ? "Y" : "X";
            case FlxGamepadInputID.Y: return ps ? "Triangle" : nin ? "X" : "Y";
            case FlxGamepadInputID.Back:
                return model switch
                {
                    FlxGamepadModel.PS3 => "Select",
                    FlxGamepadModel.PS4 => "Share",
                    FlxGamepadModel.PS5 => "Create",
                    FlxGamepadModel.Switch => "-",
                    FlxGamepadModel.XboxOne or FlxGamepadModel.XboxSeries => "View",
                    _ => "Back"
                };
            case FlxGamepadInputID.Start:
                return model switch
                {
                    FlxGamepadModel.PS4 or FlxGamepadModel.PS5 => "Options",
                    FlxGamepadModel.Switch => "+",
                    FlxGamepadModel.XboxOne or FlxGamepadModel.XboxSeries => "Menu",
                    _ => "Start"
                };
            case FlxGamepadInputID.Guide:
                return model switch
                {
                    FlxGamepadModel.PS3 or FlxGamepadModel.PS4 or FlxGamepadModel.PS5 => "PS",
                    FlxGamepadModel.Switch => "Home",
                    _ => "Guide"
                };
            case FlxGamepadInputID.LeftStickClick: return ps ? "L3" : "LS";
            case FlxGamepadInputID.RightStickClick: return ps ? "R3" : "RS";
            case FlxGamepadInputID.LeftShoulder: return ps ? "L1" : nin ? "L" : "LB";
            case FlxGamepadInputID.RightShoulder: return ps ? "R1" : nin ? "R" : "RB";
            case FlxGamepadInputID.LeftTrigger: return ps ? "L2" : nin ? "ZL" : "LT";
            case FlxGamepadInputID.RightTrigger: return ps ? "R2" : nin ? "ZR" : "RT";
            case FlxGamepadInputID.DPadUp: return "D-Pad Up";
            case FlxGamepadInputID.DPadDown: return "D-Pad Down";
            case FlxGamepadInputID.DPadLeft: return "D-Pad Left";
            case FlxGamepadInputID.DPadRight: return "D-Pad Right";
            case FlxGamepadInputID.LeftStickUp: return "Left Stick Up";
            case FlxGamepadInputID.LeftStickDown: return "Left Stick Down";
            case FlxGamepadInputID.LeftStickLeft: return "Left Stick Left";
            case FlxGamepadInputID.LeftStickRight: return "Left Stick Right";
            case FlxGamepadInputID.RightStickUp: return "Right Stick Up";
            case FlxGamepadInputID.RightStickDown: return "Right Stick Down";
            case FlxGamepadInputID.RightStickLeft: return "Right Stick Left";
            case FlxGamepadInputID.RightStickRight: return "Right Stick Right";
            case FlxGamepadInputID.LeftAnalogStick: return "Left Stick";
            case FlxGamepadInputID.RightAnalogStick: return "Right Stick";
            default: return "";
        }
    }
}

/// <summary>
/// One gamepad. Get them from <c>FlxG.Gamepads</c> (or <c>FlxG.Gamepad</c>: the first one in use, never null). Ask by position with
/// <see cref="FlxGamepadInputID"/>, or with MonoGame's <see cref="Buttons"/> if you like those better.
/// <code>
/// var pad = FlxG.Gamepad;
/// if (pad.JustPressed(FlxGamepadInputID.A)) Jump();
/// player.Velocity.X = pad.LeftStick.X * 100;
/// pad.Rumble(0.3f, 0.8f);
/// </code>
/// </summary>
public class FlxGamepad
{
    public const float AnalogThreshold = 0.5f;

    GamePadState _cur, _prev;
    bool _connected;
    float _rumbleLeft;

    internal FlxGamepadManager Manager = null!;

    /// <summary>The slot, 0 to 3.</summary>
    public int Id { get; }
    public string Name { get; internal set; } = "";
    public FlxGamepadModel Model { get; set; } = FlxGamepadModel.Generic;
    public bool Connected => _connected;

    /// <summary>Stick values below this are zero (0 to 1).</summary>
    public float DeadZone = 0.15f;
    public FlxGamepadDeadZoneMode DeadZoneMode = FlxGamepadDeadZoneMode.Circular;

    public FlxGamepad(int id) { Id = id; }

    internal void Update(GamePadState state, bool connected, string name, float dt)
    {
        _prev = _cur;
        _cur = connected ? state : default;
        if (connected != _connected)
        {
            _connected = connected;
            Name = connected ? name : "";
            Model = connected ? FlxGamepadModels.Detect(name) : FlxGamepadModel.Generic;
            if (!connected) { _prev = default; StopRumble(); }
        }

        if (_rumbleLeft > 0)
        {
            _rumbleLeft -= dt;
            if (_rumbleLeft <= 0) StopRumble();
        }
    }

    // ------------------------------------------------------------------ sticks and triggers

    Vector2 Dead(Vector2 v)
    {
        if (DeadZoneMode == FlxGamepadDeadZoneMode.Independent)
            return new Vector2(MathF.Abs(v.X) < DeadZone ? 0 : v.X, MathF.Abs(v.Y) < DeadZone ? 0 : v.Y);

        float len = v.Length();
        if (len < DeadZone || len == 0) return Vector2.Zero;
        float scaled = Math.Min(1f, (len - DeadZone) / (1f - DeadZone));
        return v / len * scaled;
    }

    /// <summary>The left stick, -1 to 1 on each axis, with positive Y pointing down (like the screen).</summary>
    public Vector2 LeftStick => Dead(Flip(_cur.ThumbSticks.Left));
    public Vector2 RightStick => Dead(Flip(_cur.ThumbSticks.Right));
    internal Vector2 PrevLeftStick => Dead(Flip(_prev.ThumbSticks.Left));
    internal Vector2 PrevRightStick => Dead(Flip(_prev.ThumbSticks.Right));

    static Vector2 Flip(Vector2 v) => new(v.X, -v.Y);

    public float LeftTrigger => _cur.Triggers.Left;
    public float RightTrigger => _cur.Triggers.Right;

    /// <summary>The stick or trigger value for an input ID: sticks give their axis (X for Left / Right, Y for Up / Down, with Up and Left negative), triggers 0 to 1, buttons 0 or 1.</summary>
    public float GetAxis(FlxGamepadInputID id)
    {
        switch (id)
        {
            case FlxGamepadInputID.LeftTrigger: return LeftTrigger;
            case FlxGamepadInputID.RightTrigger: return RightTrigger;
            case FlxGamepadInputID.LeftStickLeft: case FlxGamepadInputID.LeftStickRight: return LeftStick.X;
            case FlxGamepadInputID.LeftStickUp: case FlxGamepadInputID.LeftStickDown: return LeftStick.Y;
            case FlxGamepadInputID.RightStickLeft: case FlxGamepadInputID.RightStickRight: return RightStick.X;
            case FlxGamepadInputID.RightStickUp: case FlxGamepadInputID.RightStickDown: return RightStick.Y;
            default: return Pressed(id) ? 1f : 0f;
        }
    }

    /// <summary>The stick (Left or Right analog stick ID) as a vector, or the trigger as (value, 0).</summary>
    public Vector2 GetAnalog(FlxGamepadInputID id) => id switch
    {
        FlxGamepadInputID.LeftAnalogStick or FlxGamepadInputID.LeftStickUp or FlxGamepadInputID.LeftStickDown or FlxGamepadInputID.LeftStickLeft or FlxGamepadInputID.LeftStickRight => LeftStick,
        FlxGamepadInputID.RightAnalogStick or FlxGamepadInputID.RightStickUp or FlxGamepadInputID.RightStickDown or FlxGamepadInputID.RightStickLeft or FlxGamepadInputID.RightStickRight => RightStick,
        FlxGamepadInputID.LeftTrigger => new Vector2(LeftTrigger, 0),
        FlxGamepadInputID.RightTrigger => new Vector2(RightTrigger, 0),
        _ => Vector2.Zero
    };

    // ------------------------------------------------------------------ digital state

    bool Down(FlxGamepadInputID id, bool previous)
    {
        var s = previous ? _prev : _cur;
        var left = previous ? PrevLeftStick : LeftStick;
        var right = previous ? PrevRightStick : RightStick;
        float t = AnalogThreshold;

        return id switch
        {
            FlxGamepadInputID.A => s.Buttons.A == ButtonState.Pressed,
            FlxGamepadInputID.B => s.Buttons.B == ButtonState.Pressed,
            FlxGamepadInputID.X => s.Buttons.X == ButtonState.Pressed,
            FlxGamepadInputID.Y => s.Buttons.Y == ButtonState.Pressed,
            FlxGamepadInputID.Back => s.Buttons.Back == ButtonState.Pressed,
            FlxGamepadInputID.Guide => s.Buttons.BigButton == ButtonState.Pressed,
            FlxGamepadInputID.Start => s.Buttons.Start == ButtonState.Pressed,
            FlxGamepadInputID.LeftStickClick => s.Buttons.LeftStick == ButtonState.Pressed,
            FlxGamepadInputID.RightStickClick => s.Buttons.RightStick == ButtonState.Pressed,
            FlxGamepadInputID.LeftShoulder => s.Buttons.LeftShoulder == ButtonState.Pressed,
            FlxGamepadInputID.RightShoulder => s.Buttons.RightShoulder == ButtonState.Pressed,
            FlxGamepadInputID.DPadUp => s.DPad.Up == ButtonState.Pressed,
            FlxGamepadInputID.DPadDown => s.DPad.Down == ButtonState.Pressed,
            FlxGamepadInputID.DPadLeft => s.DPad.Left == ButtonState.Pressed,
            FlxGamepadInputID.DPadRight => s.DPad.Right == ButtonState.Pressed,
            FlxGamepadInputID.LeftTrigger => s.Triggers.Left >= t,
            FlxGamepadInputID.RightTrigger => s.Triggers.Right >= t,
            FlxGamepadInputID.LeftStickUp => left.Y <= -t,
            FlxGamepadInputID.LeftStickDown => left.Y >= t,
            FlxGamepadInputID.LeftStickLeft => left.X <= -t,
            FlxGamepadInputID.LeftStickRight => left.X >= t,
            FlxGamepadInputID.RightStickUp => right.Y <= -t,
            FlxGamepadInputID.RightStickDown => right.Y >= t,
            FlxGamepadInputID.RightStickLeft => right.X <= -t,
            FlxGamepadInputID.RightStickRight => right.X >= t,
            FlxGamepadInputID.LeftAnalogStick => left.Length() >= t,
            FlxGamepadInputID.RightAnalogStick => right.Length() >= t,
            _ => false
        };
    }

    public bool Pressed(FlxGamepadInputID id) => _connected && Down(id, false);
    public bool Released(FlxGamepadInputID id) => !Pressed(id);
    public bool JustPressed(FlxGamepadInputID id) => _connected && Down(id, false) && !Down(id, true);
    public bool JustReleased(FlxGamepadInputID id) => _connected && !Down(id, false) && Down(id, true);

    public bool AnyPressed(params FlxGamepadInputID[] ids) { foreach (var i in ids) if (Pressed(i)) return true; return false; }
    public bool AnyJustPressed(params FlxGamepadInputID[] ids) { foreach (var i in ids) if (JustPressed(i)) return true; return false; }
    public bool AnyJustReleased(params FlxGamepadInputID[] ids) { foreach (var i in ids) if (JustReleased(i)) return true; return false; }

    static readonly FlxGamepadInputID[] Buttons_ =
    {
        FlxGamepadInputID.A, FlxGamepadInputID.B, FlxGamepadInputID.X, FlxGamepadInputID.Y, FlxGamepadInputID.Back, FlxGamepadInputID.Guide, FlxGamepadInputID.Start,
        FlxGamepadInputID.LeftStickClick, FlxGamepadInputID.RightStickClick, FlxGamepadInputID.LeftShoulder, FlxGamepadInputID.RightShoulder,
        FlxGamepadInputID.DPadUp, FlxGamepadInputID.DPadDown, FlxGamepadInputID.DPadLeft, FlxGamepadInputID.DPadRight,
        FlxGamepadInputID.LeftTrigger, FlxGamepadInputID.RightTrigger,
        FlxGamepadInputID.LeftStickUp, FlxGamepadInputID.LeftStickDown, FlxGamepadInputID.LeftStickLeft, FlxGamepadInputID.LeftStickRight,
        FlxGamepadInputID.RightStickUp, FlxGamepadInputID.RightStickDown, FlxGamepadInputID.RightStickLeft, FlxGamepadInputID.RightStickRight
    };

    /// <summary>The first input that is held (buttons, d-pad, triggers, stick directions), or None.</summary>
    public FlxGamepadInputID FirstPressedId { get { foreach (var i in Buttons_) if (Pressed(i)) return i; return FlxGamepadInputID.None; } }

    /// <summary>The first input that went down this frame, or None: "press a button to bind".</summary>
    public FlxGamepadInputID FirstJustPressedId { get { foreach (var i in Buttons_) if (JustPressed(i)) return i; return FlxGamepadInputID.None; } }

    public FlxGamepadInputID FirstJustReleasedId { get { foreach (var i in Buttons_) if (JustReleased(i)) return i; return FlxGamepadInputID.None; } }

    /// <summary>True if anything is pressed or a stick/trigger is moved.</summary>
    public bool AnyInput => _connected && (FirstPressedId != FlxGamepadInputID.None);

    /// <summary>True if anything went down this frame.</summary>
    public bool AnyButtonJustPressed => FirstJustPressedId != FlxGamepadInputID.None;

    /// <summary>What a button is called on this pad (see <see cref="FlxGamepadModels.Label"/>).</summary>
    public string GetInputLabel(FlxGamepadInputID id) => FlxGamepadModels.Label(Model, id);

    // ------------------------------------------------------------------ MonoGame Buttons

    public bool Pressed(Buttons b) => _connected && _cur.IsButtonDown(b);
    public bool Released(Buttons b) => !Pressed(b);
    public bool JustPressed(Buttons b) => _connected && _cur.IsButtonDown(b) && _prev.IsButtonUp(b);
    public bool JustReleased(Buttons b) => _connected && _cur.IsButtonUp(b) && _prev.IsButtonDown(b);
    public bool AnyPressed(params Buttons[] bs) { foreach (var b in bs) if (Pressed(b)) return true; return false; }
    public bool AnyJustPressed(params Buttons[] bs) { foreach (var b in bs) if (JustPressed(b)) return true; return false; }
    public bool AnyJustReleased(params Buttons[] bs) { foreach (var b in bs) if (JustReleased(b)) return true; return false; }

    // ------------------------------------------------------------------ rumble

    /// <summary>Shakes the pad for a while. Strengths are 0 to 1 (the low rumble on the left, the high one on the right; right defaults to the same).</summary>
    public void Rumble(float seconds, float left = 1f, float? right = null)
    {
        if (!_connected) return;
        _rumbleLeft = seconds;
        Manager?.Vibration?.Invoke(Id, Math.Clamp(left, 0, 1), Math.Clamp(right ?? left, 0, 1));
    }

    public void StopRumble()
    {
        _rumbleLeft = 0;
        Manager?.Vibration?.Invoke(Id, 0, 0);
    }

    public bool Rumbling => _rumbleLeft > 0;

    /// <summary>A connected pad that doesn't reset: used when nothing is plugged in so FlxG.Gamepad is never null.</summary>
    public override string ToString() => _connected ? $"{Id}: {Name} ({Model})" : $"{Id}: not connected";
}

/// <summary>Which pads count as "active" for FlxG.Gamepad and the Any* helpers.</summary>
public enum FlxGamepadAcceptMode
{
    /// <summary>Every connected pad.</summary>
    All,
    /// <summary>Only the one most recently used.</summary>
    LastActive
}

/// <summary>
/// The four gamepad slots (FlxG.Gamepads).
/// </summary>
public class FlxGamepadManager
{
    public const int MaxPads = 4;

    readonly FlxGamepad[] _pads = new FlxGamepad[MaxPads];
    FlxGamepad? _lastActive;

    /// <summary>Where the pad states come from (default: the real pads). Replace in tests.</summary>
    public Func<int, GamePadState> Source = i => GamePad.GetState(i);

    /// <summary>Where the pad names come from (default: the real pads' names). Replace in tests.</summary>
    public Func<int, string> NameSource = i =>
    {
        try { return GamePad.GetCapabilities(i).DisplayName ?? ""; } catch { return ""; }
    };

    /// <summary>How rumble reaches the pad (slot, left, right). Default: the real pads.</summary>
    public Action<int, float, float>? Vibration = (i, l, r) => { try { GamePad.SetVibration(i, l, r); } catch { } };

    /// <summary>Default dead zone for pads when they connect.</summary>
    public float GlobalDeadZone
    {
        get => _globalDeadZone;
        set { _globalDeadZone = value; foreach (var p in _pads) p.DeadZone = value; }
    }
    float _globalDeadZone = 0.15f;

    public FlxGamepadAcceptMode AcceptMode = FlxGamepadAcceptMode.All;

    public readonly FlxSignal<FlxGamepad> DeviceConnected = new();
    public readonly FlxSignal<FlxGamepad> DeviceDisconnected = new();

    public FlxGamepadManager()
    {
        for (int i = 0; i < MaxPads; i++) _pads[i] = new FlxGamepad(i) { Manager = this };
    }

    public FlxGamepad this[int slot] => _pads[slot];
    public IReadOnlyList<FlxGamepad> List => _pads;

    internal void Update(float dt)
    {
        for (int i = 0; i < MaxPads; i++)
        {
            var pad = _pads[i];
            bool was = pad.Connected;

            GamePadState s = default;
            bool connected = false;
            try { s = Source(i); connected = s.IsConnected; } catch { }

            pad.Update(s, connected, connected && !was ? NameSource(i) : pad.Name, dt);

            if (connected && !was) { pad.DeadZone = _globalDeadZone; DeviceConnected.Dispatch(pad); }
            else if (!connected && was) { if (_lastActive == pad) _lastActive = null; DeviceDisconnected.Dispatch(pad); }

            if (connected && pad.AnyInput) _lastActive = pad;
        }
    }

    /// <summary>The pad most recently used (a button pressed or a stick moved), or null.</summary>
    public FlxGamepad? LastActive => _lastActive is { Connected: true } ? _lastActive : null;

    /// <summary>The first connected pad, or the one in slot 0 when nothing is plugged in: never null, and everything reads as not pressed then.</summary>
    public FlxGamepad First
    {
        get
        {
            if (AcceptMode == FlxGamepadAcceptMode.LastActive && LastActive is { } last) return last;
            foreach (var p in _pads) if (p.Connected) return p;
            return _pads[0];
        }
    }

    /// <summary>The first connected pad that has something pressed right now, or null.</summary>
    public FlxGamepad? FirstActive
    {
        get { foreach (var p in _pads) if (p.Connected && p.AnyInput) return p; return null; }
    }

    public int NumActive { get { int n = 0; foreach (var p in _pads) if (p.Connected) n++; return n; } }

    public IEnumerable<FlxGamepad> Connected { get { foreach (var p in _pads) if (p.Connected) yield return p; } }

    IEnumerable<FlxGamepad> Accepted => AcceptMode == FlxGamepadAcceptMode.LastActive && LastActive is { } l ? new[] { l } : Connected;

    /// <summary>Is the input pressed on any pad?</summary>
    public bool AnyPressed(FlxGamepadInputID id) { foreach (var p in Accepted) if (p.Pressed(id)) return true; return false; }
    public bool AnyJustPressed(FlxGamepadInputID id) { foreach (var p in Accepted) if (p.JustPressed(id)) return true; return false; }
    public bool AnyJustReleased(FlxGamepadInputID id) { foreach (var p in Accepted) if (p.JustReleased(id)) return true; return false; }

    /// <summary>The pad that pressed this input this frame, or null.</summary>
    public FlxGamepad? WhoJustPressed(FlxGamepadInputID id) { foreach (var p in Accepted) if (p.JustPressed(id)) return p; return null; }

    public void StopAllRumble() { foreach (var p in _pads) p.StopRumble(); }

    /// <summary>Forgets everything (nothing pressed). Used when the pads' owner changes, e.g. in tests.</summary>
    public void Reset()
    {
        _lastActive = null;
        foreach (var p in _pads) p.Update(default, false, "", 0);
    }
}
