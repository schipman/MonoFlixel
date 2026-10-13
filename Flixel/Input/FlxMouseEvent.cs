using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// Mouse events for objects: say what should happen when the mouse goes down on, comes up on, moves over or leaves an object, and the
/// engine works out which object is under the mouse. Port of flixel-addons' FlxMouseEvent.
/// <code>
/// FlxMouseEvent.Add(button, onDown: b => b.Tint = Color.Red, onUp: b => b.Tint = Color.White, onOver: Highlight, onOut: Unhighlight);
/// FlxMouseEvent.SetClickCallback(button, b => Buy(b));          // a press and release on the same object
/// </code>
/// Only the object on top (the one with the highest <see cref="Priority"/>; later added objects are on top; <see cref="Reorder"/> uses the
/// draw order of the state) gets the events. Sprites are tested by their pixels (transparent parts don't count) unless you turn
/// <c>pixelPerfect</c> off. All registrations are removed when the state changes.
/// </summary>
public static class FlxMouseEvent
{
    sealed class Registration
    {
        public FlxBasic Object = null!;
        public Action<FlxBasic>? Down, Up, Click, DoubleClick, Over, Out, Move, Wheel;
        public bool MouseChildren, MouseEnabled = true, PixelPerfect = true;
        public FlxMouseButtonID[] Buttons = { FlxMouseButtonID.Left };
        public int Priority;
    }

    static readonly List<Registration> Regs = new();
    static int _nextPriority;
    static Registration? _over;
    static readonly Registration?[] DownOn = new Registration?[3];       // which object each button went down on

    /// <summary>The mouse button of the event being handled (also valid inside the callbacks).</summary>
    public static FlxMouseButtonID? CurrentButton { get; private set; }

    /// <summary>The object the mouse is over right now (the top one), or null.</summary>
    public static FlxBasic? ObjectUnderMouse => _over?.Object;

    static Action<FlxBasic>? Wrap<T>(Action<T>? callback) where T : FlxBasic =>
        callback == null ? null : o => callback((T)o);

    /// <summary>Registers an object. Callbacks get the object back.</summary>
    /// <param name="mouseChildren">For a group: its members count as part of it (the mouse is "over" the group when it is over a member).</param>
    /// <param name="pixelPerfect">For sprites: only the pixels that are not transparent count.</param>
    /// <param name="mouseButtons">Which buttons make down / up / click events (default: left).</param>
    public static T Add<T>(T obj, Action<T>? onMouseDown = null, Action<T>? onMouseUp = null, Action<T>? onMouseOver = null, Action<T>? onMouseOut = null,
        bool mouseChildren = false, bool mouseEnabled = true, bool pixelPerfect = true, params FlxMouseButtonID[] mouseButtons) where T : FlxBasic
    {
        Remove(obj);
        Regs.Add(new Registration
        {
            Object = obj,
            Down = Wrap(onMouseDown), Up = Wrap(onMouseUp), Over = Wrap(onMouseOver), Out = Wrap(onMouseOut),
            MouseChildren = mouseChildren, MouseEnabled = mouseEnabled, PixelPerfect = pixelPerfect,
            Buttons = mouseButtons is { Length: > 0 } ? mouseButtons : new[] { FlxMouseButtonID.Left },
            Priority = _nextPriority++
        });
        return obj;
    }

    public static T Remove<T>(T obj) where T : FlxBasic
    {
        for (int i = Regs.Count - 1; i >= 0; i--)
            if (ReferenceEquals(Regs[i].Object, obj)) Forget(Regs[i], i);
        return obj;
    }

    static void Forget(Registration r, int index)
    {
        Regs.RemoveAt(index);
        if (_over == r) _over = null;
        for (int i = 0; i < DownOn.Length; i++) if (DownOn[i] == r) DownOn[i] = null;
    }

    public static void RemoveAll()
    {
        Regs.Clear();
        _over = null;
        Array.Clear(DownOn);
    }

    public static int Count => Regs.Count;
    public static bool IsRegistered(FlxBasic obj) => Find(obj) != null;

    static Registration? Find(FlxBasic obj) => Regs.Find(r => ReferenceEquals(r.Object, obj));

    static Registration Need(FlxBasic obj) => Find(obj) ?? throw new InvalidOperationException("The object is not registered: call FlxMouseEvent.Add first.");

    public static void SetClickCallback<T>(T obj, Action<T>? callback) where T : FlxBasic => Need(obj).Click = Wrap(callback);
    public static void SetDoubleClickCallback<T>(T obj, Action<T>? callback) where T : FlxBasic => Need(obj).DoubleClick = Wrap(callback);
    public static void SetDownCallback<T>(T obj, Action<T>? callback) where T : FlxBasic => Need(obj).Down = Wrap(callback);
    public static void SetUpCallback<T>(T obj, Action<T>? callback) where T : FlxBasic => Need(obj).Up = Wrap(callback);
    public static void SetOverCallback<T>(T obj, Action<T>? callback) where T : FlxBasic => Need(obj).Over = Wrap(callback);
    public static void SetOutCallback<T>(T obj, Action<T>? callback) where T : FlxBasic => Need(obj).Out = Wrap(callback);

    /// <summary>Called every frame the mouse moves while it is over the object.</summary>
    public static void SetMoveCallback<T>(T obj, Action<T>? callback) where T : FlxBasic => Need(obj).Move = Wrap(callback);

    /// <summary>Called when the wheel turns while the mouse is over the object (read FlxG.Mouse.Wheel).</summary>
    public static void SetWheelCallback<T>(T obj, Action<T>? callback) where T : FlxBasic => Need(obj).Wheel = Wrap(callback);

    public static void SetMouseEnabled(FlxBasic obj, bool enabled) => Need(obj).MouseEnabled = enabled;
    public static bool IsMouseEnabled(FlxBasic obj) => Need(obj).MouseEnabled;
    public static void SetMouseChildren(FlxBasic obj, bool value) => Need(obj).MouseChildren = value;
    public static void SetPixelPerfect(FlxBasic obj, bool value) => Need(obj).PixelPerfect = value;
    public static void SetButtons(FlxBasic obj, params FlxMouseButtonID[] buttons) => Need(obj).Buttons = buttons.Length > 0 ? buttons : new[] { FlxMouseButtonID.Left };

    /// <summary>Which object is on top when they overlap: higher wins. Later registered objects start higher.</summary>
    public static void SetPriority(FlxBasic obj, int priority) => Need(obj).Priority = priority;

    /// <summary>Sets the priorities from the draw order of the current state: what is drawn later (on top) wins.</summary>
    public static void Reorder()
    {
        var order = new Dictionary<FlxBasic, int>();
        int n = 0;
        void Walk(FlxBasic b)
        {
            order[b] = n++;
            if (b is IFlxGroup g) foreach (var m in g.Basics) if (m != null) Walk(m);
        }
        if (FlxG.Game?.State != null) Walk(FlxG.Game.State);

        foreach (var r in Regs) r.Priority = order.TryGetValue(r.Object, out int p) ? p : -1;
    }

    // ------------------------------------------------------------------ hit testing

    static FlxCamera CameraFor(FlxBasic o) =>
        o.Cameras is { Length: > 0 } cams ? cams[0] : FlxG.Cameras.Main;

    static bool Hit(Registration r, FlxBasic o, FlxCamera cam, Vector2 view, bool testChildren)
    {
        if (!o.Exists || !o.Visible) return false;

        if (testChildren && o is IFlxGroup g)
        {
            foreach (var m in g.Basics)
                if (m != null && Hit(r, m, cam, view, true)) return true;
        }

        if (o is not FlxObject obj || !FlxPointer.PointInObject(obj, cam, view)) return false;
        if (r.PixelPerfect && o is FlxSprite s && s.Pixels != null) return PixelHit(s, cam, view);
        return true;
    }

    // ---- pixel perfect: is the pixel under the mouse not transparent?

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Texture2D, bool[]> Masks = new();

    static bool[] MaskOf(Texture2D tex)
    {
        if (Masks.TryGetValue(tex, out var mask)) return mask;

        var data = new Color[tex.Width * tex.Height];
        tex.GetData(data);
        mask = new bool[data.Length];
        for (int i = 0; i < data.Length; i++) mask[i] = data[i].A > 0;
        Masks.Add(tex, mask);
        return mask;
    }

    static bool PixelHit(FlxSprite s, FlxCamera cam, Vector2 view)
    {
        if (s.Frames == null) return true;

        var world = new Vector2(view.X + cam.ScrollX * s.ScrollFactor.X, view.Y + cam.ScrollY * s.ScrollFactor.Y);

        // where the sprite's frame is drawn, then undo scale, rotation and flipping to find the pixel of the frame
        var frameTopLeft = new Vector2(s.X - s.Offset.X, s.Y - s.Offset.Y);
        var d = world - (frameTopLeft + s.Origin);
        if (s.Angle != 0)
        {
            float r = -MathHelper.ToRadians(s.Angle), c = MathF.Cos(r), sn = MathF.Sin(r);
            d = new Vector2(d.X * c - d.Y * sn, d.X * sn + d.Y * c);
        }
        if (s.Scale.X == 0 || s.Scale.Y == 0) return false;

        float fx = s.Origin.X + d.X / s.Scale.X, fy = s.Origin.Y + d.Y / s.Scale.Y;
        if (s.FlipX) fx = s.FrameWidth - fx;
        if (s.FlipY) fy = s.FrameHeight - fy;

        var frame = s.Frames[Math.Clamp(s.Frame, 0, s.Frames.Count - 1)];
        int px = (int)MathF.Floor(fx) - frame.Offset.X, py = (int)MathF.Floor(fy) - frame.Offset.Y;
        if (px < 0 || py < 0 || px >= frame.Source.Width || py >= frame.Source.Height) return false;

        var mask = MaskOf(s.Pixels!);
        return mask[(frame.Source.Y + py) * s.Pixels!.Width + frame.Source.X + px];
    }

    static Registration? TopUnder(Vector2 gamePos)
    {
        Registration? top = null;
        foreach (var r in Regs)
        {
            if (!r.MouseEnabled) continue;
            var cam = CameraFor(r.Object);
            var view = (gamePos - new Vector2(cam.X, cam.Y)) / Math.Max(0.0001f, cam.Zoom);
            if (!Hit(r, r.Object, cam, view, r.MouseChildren)) continue;
            if (top == null || r.Priority >= top.Priority) top = r;
        }
        return top;
    }

    // ------------------------------------------------------------------ every frame

    /// <summary>Called by the game every frame after the mouse was read.</summary>
    internal static void Update()
    {
        if (Regs.Count == 0) { _over = null; return; }

        // drop registrations whose object was destroyed
        for (int i = Regs.Count - 1; i >= 0; i--) if (!Regs[i].Object.Exists && !Regs[i].Object.Alive) Forget(Regs[i], i);

        var mouse = FlxG.Mouse;
        var top = TopUnder(mouse.Position);

        if (!ReferenceEquals(top, _over))
        {
            var previous = _over;
            _over = top;
            Call(previous, previous?.Out);
            Call(top, top?.Over);
        }

        if (top != null)
        {
            if (mouse.JustMoved) Call(top, top.Move);
            if (mouse.JustScrolled) Call(top, top.Wheel);
        }

        foreach (FlxMouseButtonID id in Enum.GetValues<FlxMouseButtonID>())
        {
            var button = mouse[id];

            if (button.JustPressed && top != null && Array.IndexOf(top.Buttons, id) >= 0)
            {
                DownOn[(int)id] = top;
                CurrentButton = id;
                Call(top, top.Down);
                if (button.JustDoubleClicked) Call(top, top.DoubleClick);
                CurrentButton = null;
            }

            if (button.JustReleased)
            {
                var down = DownOn[(int)id];
                DownOn[(int)id] = null;

                CurrentButton = id;
                if (top != null && Array.IndexOf(top.Buttons, id) >= 0)
                {
                    Call(top, top.Up);
                    if (ReferenceEquals(down, top)) Call(top, top.Click);     // pressed and released on the same object
                }
                CurrentButton = null;
            }
        }
    }

    static void Call(Registration? r, Action<FlxBasic>? callback)
    {
        if (r != null && callback != null && Regs.Contains(r)) callback(r.Object);
    }
}
