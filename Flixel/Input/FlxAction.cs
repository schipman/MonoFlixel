using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel;

/// <summary>The kind of device an input comes from.</summary>
public enum FlxInputDevice { All, Keyboard, Mouse, Gamepad, Touch, Other }

/// <summary>Special device numbers (a gamepad slot is 0 to 3).</summary>
public static class FlxInputDeviceID
{
    /// <summary>Any device of the kind.</summary>
    public const int All = -1;
    /// <summary>The device that has been used first (for gamepads: the first one with something pressed).</summary>
    public const int FirstActive = -2;
    /// <summary>No device: the input never fires.</summary>
    public const int None = -3;
}

/// <summary>How an analog input must be behaving for the action to fire.</summary>
public enum FlxAnalogState
{
    /// <summary>Away from zero (or, for the mouse, moving).</summary>
    Moving,
    /// <summary>Went away from zero this frame.</summary>
    JustMoved,
    /// <summary>At zero.</summary>
    Stopped,
    /// <summary>Came back to zero this frame.</summary>
    JustStopped
}

public enum FlxAnalogAxis { Both, X, Y }

// ====================================================================== inputs

/// <summary>Something an action can listen to. Inputs are made by the action's Add... methods.</summary>
public abstract class FlxActionInput
{
    public abstract FlxInputDevice Device { get; }

    /// <summary>The device number (All, FirstActive, or a gamepad slot); only gamepads have more than one.</summary>
    public int DeviceID = FlxInputDeviceID.All;

    internal abstract void Write(Utf8JsonWriter w);

    internal static FlxActionInput? Read(JsonElement e)
    {
        string kind = e.GetProperty("kind").GetString() ?? "";
        int dev = e.TryGetProperty("device", out var d) ? d.GetInt32() : FlxInputDeviceID.All;
        FlxInputState Trig(string def) => Enum.Parse<FlxInputState>(e.TryGetProperty("trigger", out var t) ? t.GetString()! : def);
        FlxMouseButtonID Btn() => Enum.Parse<FlxMouseButtonID>(e.GetProperty("button").GetString()!);
        float Scale() => e.TryGetProperty("scale", out var sc) ? sc.GetSingle() : 1f;
        Keys K(string n) => FlxKey.Parse(e.GetProperty(n).GetString()!);

        FlxActionInput? input = kind switch
        {
            "key" => new FlxKeyInput(K("key"), Trig("JustPressed")),
            "mouse" => new FlxMouseButtonInput(Btn(), Trig("JustPressed")),
            "wheel" => new FlxMouseWheelInput(e.GetProperty("positive").GetBoolean()),
            "pad" => new FlxGamepadButtonInput(Enum.Parse<FlxGamepadInputID>(e.GetProperty("id").GetString()!), Trig("JustPressed")),
            "stick" => new FlxGamepadStickInput(Enum.Parse<FlxGamepadInputID>(e.GetProperty("id").GetString()!), Enum.Parse<FlxAnalogState>(e.GetProperty("state").GetString()!)),
            "mouseMotion" => new FlxMouseMotionInput(Enum.Parse<FlxAnalogState>(e.GetProperty("state").GetString()!), Scale()),
            "mousePos" => new FlxMousePositionInput(Enum.Parse<FlxAnalogState>(e.GetProperty("state").GetString()!)),
            "drag" => new FlxMouseDragInput(Btn(), Enum.Parse<FlxAnalogState>(e.GetProperty("state").GetString()!), Scale()),
            "keys" => new FlxKeyAxisInput(K("up"), K("down"), K("left"), K("right"), Enum.Parse<FlxAnalogState>(e.GetProperty("state").GetString()!)),
            _ => null
        };
        if (input != null) input.DeviceID = dev;
        return input;
    }

    protected void Header(Utf8JsonWriter w, string kind)
    {
        w.WriteStartObject();
        w.WriteString("kind", kind);
        if (DeviceID != FlxInputDeviceID.All) w.WriteNumber("device", DeviceID);
    }

    /// <summary>The gamepad slots an input of a pad should look at, given the device the set is active for.</summary>
    internal static IEnumerable<FlxGamepad> Pads(int inputDevice, int filter)
    {
        if (inputDevice == FlxInputDeviceID.None || filter == FlxInputDeviceID.None) yield break;

        foreach (var pad in FlxG.Gamepads.Connected)
        {
            int slot = inputDevice;
            if (inputDevice == FlxInputDeviceID.FirstActive) slot = FlxG.Gamepads.FirstActive?.Id ?? FlxInputDeviceID.None;
            if (slot >= 0 && pad.Id != slot) continue;

            int f = filter;
            if (f == FlxInputDeviceID.FirstActive) f = FlxG.Gamepads.FirstActive?.Id ?? FlxInputDeviceID.None;
            if (f >= 0 && pad.Id != f) continue;
            if (f == FlxInputDeviceID.None) continue;
            yield return pad;
        }
    }
}

public abstract class FlxDigitalInput : FlxActionInput
{
    public FlxInputState Trigger;

    /// <summary>Is the trigger met right now? <paramref name="filter"/> is the device the set is active for.</summary>
    internal abstract bool Check(int filter);

    protected void WriteTrigger(Utf8JsonWriter w) => w.WriteString("trigger", Trigger.ToString());
}

public abstract class FlxAnalogInput : FlxActionInput
{
    public FlxAnalogState State;

    Vector2 _value;
    bool _nonZero, _wasNonZero;

    internal Vector2 Value => _value;
    internal bool Fired { get; private set; }

    /// <summary>The input's value this frame and whether it counts as "away from zero".</summary>
    protected abstract void Sample(int filter, out Vector2 value, out bool nonZero);

    internal void Evaluate(int filter)
    {
        Sample(filter, out _value, out _nonZero);
        Fired = State switch
        {
            FlxAnalogState.Moving => _nonZero,
            FlxAnalogState.JustMoved => _nonZero && !_wasNonZero,
            FlxAnalogState.Stopped => !_nonZero,
            _ => !_nonZero && _wasNonZero
        };
        _wasNonZero = _nonZero;
    }

    protected void WriteState(Utf8JsonWriter w) => w.WriteString("state", State.ToString());
}

public sealed class FlxKeyInput : FlxDigitalInput
{
    public Keys Key;
    public FlxKeyInput(Keys key, FlxInputState trigger) { Key = key; Trigger = trigger; }
    public override FlxInputDevice Device => FlxInputDevice.Keyboard;

    internal override bool Check(int filter) => Trigger switch
    {
        FlxInputState.Pressed => FlxG.Keys.Pressed(Key),
        FlxInputState.JustPressed => FlxG.Keys.JustPressed(Key),
        FlxInputState.JustReleased => FlxG.Keys.JustReleased(Key),
        _ => FlxG.Keys.Released(Key)
    };

    internal override void Write(Utf8JsonWriter w) { Header(w, "key"); w.WriteString("key", Key.ToString()); WriteTrigger(w); w.WriteEndObject(); }
}

public sealed class FlxMouseButtonInput : FlxDigitalInput
{
    public FlxMouseButtonID Button;
    public FlxMouseButtonInput(FlxMouseButtonID button, FlxInputState trigger) { Button = button; Trigger = trigger; }
    public override FlxInputDevice Device => FlxInputDevice.Mouse;
    internal override bool Check(int filter) => FlxG.Mouse[Button].Check(Trigger);
    internal override void Write(Utf8JsonWriter w) { Header(w, "mouse"); w.WriteString("button", Button.ToString()); WriteTrigger(w); w.WriteEndObject(); }
}

/// <summary>Fires on the frame the wheel turns in a direction.</summary>
public sealed class FlxMouseWheelInput : FlxDigitalInput
{
    public bool Positive;
    public FlxMouseWheelInput(bool positive) { Positive = positive; Trigger = FlxInputState.JustPressed; }
    public override FlxInputDevice Device => FlxInputDevice.Mouse;
    internal override bool Check(int filter) => Positive ? FlxG.Mouse.Wheel > 0 : FlxG.Mouse.Wheel < 0;
    internal override void Write(Utf8JsonWriter w) { Header(w, "wheel"); w.WriteBoolean("positive", Positive); w.WriteEndObject(); }
}

public sealed class FlxGamepadButtonInput : FlxDigitalInput
{
    public FlxGamepadInputID Id;
    public FlxGamepadButtonInput(FlxGamepadInputID id, FlxInputState trigger, int deviceId = FlxInputDeviceID.All) { Id = id; Trigger = trigger; DeviceID = deviceId; }
    public override FlxInputDevice Device => FlxInputDevice.Gamepad;

    internal override bool Check(int filter)
    {
        foreach (var pad in Pads(DeviceID, filter))
        {
            bool hit = Trigger switch
            {
                FlxInputState.Pressed => pad.Pressed(Id),
                FlxInputState.JustPressed => pad.JustPressed(Id),
                FlxInputState.JustReleased => pad.JustReleased(Id),
                _ => pad.Released(Id)
            };
            if (hit) return true;
        }
        return false;
    }

    internal override void Write(Utf8JsonWriter w) { Header(w, "pad"); w.WriteString("id", Id.ToString()); WriteTrigger(w); w.WriteEndObject(); }
}

/// <summary>Any <see cref="IFlxInput"/> of your own. Not saved with the bindings.</summary>
public sealed class FlxGenericInput : FlxDigitalInput
{
    readonly IFlxInput _input;
    public FlxGenericInput(IFlxInput input, FlxInputState trigger) { _input = input; Trigger = trigger; }
    public override FlxInputDevice Device => FlxInputDevice.Other;
    internal override bool Check(int filter) => _input.Check(Trigger);
    internal override void Write(Utf8JsonWriter w) { w.WriteStartObject(); w.WriteString("kind", "generic"); w.WriteEndObject(); }
}

public sealed class FlxGamepadStickInput : FlxAnalogInput
{
    public FlxGamepadInputID Id;
    public FlxGamepadStickInput(FlxGamepadInputID stickOrTrigger, FlxAnalogState state, int deviceId = FlxInputDeviceID.All) { Id = stickOrTrigger; State = state; DeviceID = deviceId; }
    public override FlxInputDevice Device => FlxInputDevice.Gamepad;

    protected override void Sample(int filter, out Vector2 value, out bool nonZero)
    {
        value = Vector2.Zero;
        foreach (var pad in Pads(DeviceID, filter))
        {
            var v = pad.GetAnalog(Id);
            if (v.LengthSquared() > value.LengthSquared()) value = v;
        }
        nonZero = value != Vector2.Zero;
    }

    internal override void Write(Utf8JsonWriter w) { Header(w, "stick"); w.WriteString("id", Id.ToString()); WriteState(w); w.WriteEndObject(); }
}

/// <summary>How far the mouse moved this frame (times a scale).</summary>
public sealed class FlxMouseMotionInput : FlxAnalogInput
{
    public float Scale;
    public FlxMouseMotionInput(FlxAnalogState state, float scale = 1f) { State = state; Scale = scale; }
    public override FlxInputDevice Device => FlxInputDevice.Mouse;

    protected override void Sample(int filter, out Vector2 value, out bool nonZero)
    {
        value = FlxG.Mouse.Delta * Scale;
        nonZero = value != Vector2.Zero;
    }

    internal override void Write(Utf8JsonWriter w) { Header(w, "mouseMotion"); WriteState(w); w.WriteNumber("scale", Scale); w.WriteEndObject(); }
}

/// <summary>Where the mouse is (in game pixels). It counts as moving on the frames it moved.</summary>
public sealed class FlxMousePositionInput : FlxAnalogInput
{
    public FlxMousePositionInput(FlxAnalogState state) { State = state; }
    public override FlxInputDevice Device => FlxInputDevice.Mouse;

    protected override void Sample(int filter, out Vector2 value, out bool nonZero)
    {
        value = FlxG.Mouse.Position;
        nonZero = FlxG.Mouse.JustMoved;
    }

    internal override void Write(Utf8JsonWriter w) { Header(w, "mousePos"); WriteState(w); w.WriteEndObject(); }
}

/// <summary>How far the mouse moved while a button is held (to drag things).</summary>
public sealed class FlxMouseDragInput : FlxAnalogInput
{
    public FlxMouseButtonID Button;
    public float Scale;
    public FlxMouseDragInput(FlxMouseButtonID button, FlxAnalogState state, float scale = 1f) { Button = button; State = state; Scale = scale; }
    public override FlxInputDevice Device => FlxInputDevice.Mouse;

    protected override void Sample(int filter, out Vector2 value, out bool nonZero)
    {
        value = FlxG.Mouse[Button].Pressed ? FlxG.Mouse.Delta * Scale : Vector2.Zero;
        nonZero = value != Vector2.Zero;
    }

    internal override void Write(Utf8JsonWriter w) { Header(w, "drag"); w.WriteString("button", Button.ToString()); WriteState(w); w.WriteNumber("scale", Scale); w.WriteEndObject(); }
}

/// <summary>Four keys as a stick: (-1, 0) for left, (0, -1) for up, and so on.</summary>
public sealed class FlxKeyAxisInput : FlxAnalogInput
{
    public Keys Up, Down, Left, Right;

    public FlxKeyAxisInput(Keys up, Keys down, Keys left, Keys right, FlxAnalogState state)
    {
        Up = up; Down = down; Left = left; Right = right; State = state;
    }

    public override FlxInputDevice Device => FlxInputDevice.Keyboard;

    protected override void Sample(int filter, out Vector2 value, out bool nonZero)
    {
        var k = FlxG.Keys;
        value = new Vector2((k.Pressed(Right) ? 1 : 0) - (k.Pressed(Left) ? 1 : 0), (k.Pressed(Down) ? 1 : 0) - (k.Pressed(Up) ? 1 : 0));
        nonZero = value != Vector2.Zero;
    }

    internal override void Write(Utf8JsonWriter w)
    {
        Header(w, "keys");
        w.WriteString("up", Up.ToString()); w.WriteString("down", Down.ToString());
        w.WriteString("left", Left.ToString()); w.WriteString("right", Right.ToString());
        WriteState(w);
        w.WriteEndObject();
    }
}

// ====================================================================== actions

/// <summary>
/// Something the player can do, wired to any number of inputs on any devices: "Jump" is Space on the keyboard and A on the pad.
/// Ask <see cref="FlxActionDigital.Triggered"/> (or give a callback) and you don't care which input did it.
/// </summary>
public abstract class FlxAction
{
    public string Name { get; }
    public bool Enabled = true;

    protected FlxAction(string name) { Name = name; }

    /// <summary>When (in real seconds) the action last fired.</summary>
    public double LastTriggered { get; protected set; } = -1;

    public abstract IReadOnlyList<FlxActionInput> Inputs { get; }
    public abstract void ClearInputs();
    public abstract bool RemoveInput(FlxActionInput input);
    internal abstract void AddInputInternal(FlxActionInput input);
    internal abstract void Update(int filter, FlxInputDevice device);
    internal abstract void Reset();
}

/// <summary>An on-off action: jump, shoot, pause.</summary>
public sealed class FlxActionDigital : FlxAction
{
    readonly List<FlxDigitalInput> _inputs = new();

    /// <summary>True on the frames one of the inputs met its trigger.</summary>
    public bool Triggered { get; private set; }

    /// <summary>Called when it triggers.</summary>
    public Action<FlxActionDigital>? Callback;

    public FlxActionDigital(string name, Action<FlxActionDigital>? callback = null) : base(name) { Callback = callback; }

    public override IReadOnlyList<FlxActionInput> Inputs => _inputs;

    public FlxActionDigital AddKey(Keys key, FlxInputState trigger = FlxInputState.JustPressed) => Add(new FlxKeyInput(key, trigger));
    public FlxActionDigital AddMouse(FlxMouseButtonID button, FlxInputState trigger = FlxInputState.JustPressed) => Add(new FlxMouseButtonInput(button, trigger));
    public FlxActionDigital AddMouseWheel(bool positive) => Add(new FlxMouseWheelInput(positive));
    public FlxActionDigital AddGamepad(FlxGamepadInputID id, FlxInputState trigger = FlxInputState.JustPressed, int deviceId = FlxInputDeviceID.All) => Add(new FlxGamepadButtonInput(id, trigger, deviceId));
    public FlxActionDigital AddInput(IFlxInput input, FlxInputState trigger = FlxInputState.JustPressed) => Add(new FlxGenericInput(input, trigger));

    public FlxActionDigital Add(FlxDigitalInput input) { _inputs.Add(input); return this; }

    internal override void AddInputInternal(FlxActionInput input) { if (input is FlxDigitalInput d) _inputs.Add(d); }
    public override void ClearInputs() => _inputs.Clear();
    public override bool RemoveInput(FlxActionInput input) => input is FlxDigitalInput d && _inputs.Remove(d);

    internal override void Update(int filter, FlxInputDevice device)
    {
        Triggered = false;
        if (!Enabled) return;

        foreach (var input in _inputs)
        {
            if (device != FlxInputDevice.All && input.Device != device && input.Device != FlxInputDevice.Other) continue;
            if (input.Check(filter)) { Triggered = true; break; }
        }

        if (Triggered)
        {
            LastTriggered = FlxG.RealTime;
            Callback?.Invoke(this);
        }
    }

    internal override void Reset() => Triggered = false;
}

/// <summary>A stick-like action: move, aim, scroll. <see cref="X"/> and <see cref="Y"/> are -1 to 1 for sticks and keys, pixels for the mouse.</summary>
public sealed class FlxActionAnalog : FlxAction
{
    readonly List<FlxAnalogInput> _inputs = new();

    public float X { get; private set; }
    public float Y { get; private set; }
    public Vector2 Value => new(X, Y);
    public bool Triggered { get; private set; }

    public FlxAnalogAxis Axis;
    public Action<FlxActionAnalog>? Callback;

    public FlxActionAnalog(string name, Action<FlxActionAnalog>? callback = null, FlxAnalogAxis axis = FlxAnalogAxis.Both) : base(name)
    {
        Callback = callback;
        Axis = axis;
    }

    public override IReadOnlyList<FlxActionInput> Inputs => _inputs;

    public FlxActionAnalog AddGamepad(FlxGamepadInputID stickOrTrigger, FlxAnalogState state = FlxAnalogState.Moving, int deviceId = FlxInputDeviceID.All) => Add(new FlxGamepadStickInput(stickOrTrigger, state, deviceId));
    public FlxActionAnalog AddMouseMotion(FlxAnalogState state = FlxAnalogState.Moving, float scale = 1f) => Add(new FlxMouseMotionInput(state, scale));
    public FlxActionAnalog AddMousePosition(FlxAnalogState state = FlxAnalogState.Moving) => Add(new FlxMousePositionInput(state));
    public FlxActionAnalog AddMouseDrag(FlxMouseButtonID button = FlxMouseButtonID.Left, FlxAnalogState state = FlxAnalogState.Moving, float scale = 1f) => Add(new FlxMouseDragInput(button, state, scale));
    public FlxActionAnalog AddKeys(Keys up, Keys down, Keys left, Keys right, FlxAnalogState state = FlxAnalogState.Moving) => Add(new FlxKeyAxisInput(up, down, left, right, state));

    public FlxActionAnalog Add(FlxAnalogInput input) { _inputs.Add(input); return this; }

    internal override void AddInputInternal(FlxActionInput input) { if (input is FlxAnalogInput a) _inputs.Add(a); }
    public override void ClearInputs() => _inputs.Clear();
    public override bool RemoveInput(FlxActionInput input) => input is FlxAnalogInput a && _inputs.Remove(a);

    internal override void Update(int filter, FlxInputDevice device)
    {
        Triggered = false;
        X = Y = 0;

        // every input has to be updated each frame, also when the action is off, so their "was moving" memory stays right
        FlxAnalogInput? best = null;
        foreach (var input in _inputs)
        {
            if (device != FlxInputDevice.All && input.Device != device) continue;
            input.Evaluate(filter);
            if (Enabled && input.Fired && (best == null || input.Value.LengthSquared() > best.Value.LengthSquared())) best = input;
        }
        if (best == null) return;

        Triggered = true;
        var v = best.Value;
        X = Axis == FlxAnalogAxis.Y ? 0 : v.X;
        Y = Axis == FlxAnalogAxis.X ? 0 : v.Y;
        LastTriggered = FlxG.RealTime;
        Callback?.Invoke(this);
    }

    internal override void Reset() { Triggered = false; X = Y = 0; }
}

// ====================================================================== sets and manager

/// <summary>A group of actions that go together and can be switched on and off together: "Gameplay", "Menu".</summary>
public sealed class FlxActionSet
{
    public string Name { get; }
    public readonly List<FlxAction> Actions = new();

    public FlxActionSet(string name, params FlxAction[] actions)
    {
        Name = name;
        Actions.AddRange(actions);
    }

    public T Add<T>(T action) where T : FlxAction { Actions.Add(action); return action; }

    public FlxAction? Find(string name) => Actions.Find(a => a.Name == name);
    public FlxActionDigital? Digital(string name) => Find(name) as FlxActionDigital;
    public FlxActionAnalog? Analog(string name) => Find(name) as FlxActionAnalog;

    /// <summary>Is this set switched on in its manager?</summary>
    public bool Active => FlxG.Actions.IsActive(this);

    // ---- bindings: what each action listens to, as JSON (so you can save the player's rebinds)

    internal void WriteBindings(Utf8JsonWriter w)
    {
        w.WriteStartObject();
        w.WriteString("set", Name);
        w.WriteStartArray("actions");
        foreach (var a in Actions)
        {
            w.WriteStartObject();
            w.WriteString("name", a.Name);
            w.WriteStartArray("inputs");
            foreach (var i in a.Inputs) if (i is not FlxGenericInput) i.Write(w);
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    /// <summary>The bindings of every action, as JSON text.</summary>
    public string ExportBindings()
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms)) WriteBindings(w);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>Replaces the inputs of the actions found in the JSON (by name). Actions that aren't in it, and generic inputs of your own, are left alone.</summary>
    public void ApplyBindings(string json)
    {
        using var doc = JsonDocument.Parse(json);
        ApplyBindings(doc.RootElement);
    }

    internal void ApplyBindings(JsonElement root)
    {
        if (!root.TryGetProperty("actions", out var actions)) return;

        foreach (var entry in actions.EnumerateArray())
        {
            var action = Find(entry.GetProperty("name").GetString() ?? "");
            if (action == null) continue;

            // keep inputs that can't be saved, drop the rest, then add what the file says
            var keep = new List<FlxActionInput>();
            foreach (var i in action.Inputs) if (i is FlxGenericInput) keep.Add(i);
            action.ClearInputs();
            foreach (var i in keep) action.AddInputInternal(i);

            foreach (var inp in entry.GetProperty("inputs").EnumerateArray())
                if (FlxActionInput.Read(inp) is { } input) action.AddInputInternal(input);
        }
    }
}

/// <summary>What the manager does with its sets when the state changes.</summary>
public enum FlxActionResetPolicy
{
    /// <summary>Nothing: sets stay as they are.</summary>
    None,
    /// <summary>Sets are switched off but stay registered.</summary>
    Deactivate,
    /// <summary>Sets are removed (the new state makes its own). The default, like HaxeFlixel.</summary>
    RemoveAll
}

/// <summary>FlxG.Actions: holds the action sets and runs the active ones every frame.</summary>
public sealed class FlxActionManager
{
    sealed class Entry
    {
        public FlxActionSet Set = null!;
        public FlxInputDevice Device;
        public int DeviceID;
    }

    readonly List<FlxActionSet> _sets = new();
    readonly List<Entry> _active = new();

    public FlxActionResetPolicy ResetOnStateSwitch = FlxActionResetPolicy.RemoveAll;

    public IReadOnlyList<FlxActionSet> Sets => _sets;

    public FlxActionSet Add(FlxActionSet set)
    {
        if (!_sets.Contains(set)) _sets.Add(set);
        return set;
    }

    public FlxActionSet Get(string name) => _sets.Find(s => s.Name == name) ?? throw new KeyNotFoundException($"No action set called '{name}'.");

    /// <summary>
    /// Switches a set on. Only inputs of that device kind and number count: <c>Activate(set, FlxInputDevice.Gamepad, 1)</c> is "player two's pad".
    /// </summary>
    public FlxActionSet Activate(FlxActionSet set, FlxInputDevice device = FlxInputDevice.All, int deviceId = FlxInputDeviceID.All)
    {
        Add(set);
        _active.RemoveAll(e => e.Set == set);
        _active.Add(new Entry { Set = set, Device = device, DeviceID = deviceId });
        return set;
    }

    public void Deactivate(FlxActionSet set)
    {
        _active.RemoveAll(e => e.Set == set);
        foreach (var a in set.Actions) a.Reset();
    }

    public void DeactivateAll() { foreach (var e in _active.ToArray()) Deactivate(e.Set); }

    public bool IsActive(FlxActionSet set) => _active.Exists(e => e.Set == set);

    public void Remove(FlxActionSet set) { Deactivate(set); _sets.Remove(set); }

    public void Clear() { DeactivateAll(); _sets.Clear(); }

    internal void Update()
    {
        foreach (var e in _active.ToArray())
            foreach (var a in e.Set.Actions)
                a.Update(e.DeviceID, e.Device);
    }

    internal void OnStateSwitch()
    {
        switch (ResetOnStateSwitch)
        {
            case FlxActionResetPolicy.Deactivate: DeactivateAll(); break;
            case FlxActionResetPolicy.RemoveAll: Clear(); break;
        }
    }

    /// <summary>The bindings of all the sets, as JSON text (save it with FlxG.Save).</summary>
    public string ExportBindings()
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartArray();
            foreach (var s in _sets) s.WriteBindings(w);
            w.WriteEndArray();
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>Applies exported bindings to the sets with the same names.</summary>
    public void ApplyBindings(string json)
    {
        using var doc = JsonDocument.Parse(json);
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var set = _sets.Find(s => s.Name == entry.GetProperty("set").GetString());
            set?.ApplyBindings(entry);
        }
    }
}
