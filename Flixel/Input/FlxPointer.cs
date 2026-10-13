using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace MonoFlixel;

/// <summary>
/// A pointing input: the mouse or a finger. Its position is in game pixels (not window pixels) and can be turned into world
/// coordinates for any camera, which matters when the camera scrolls, zooms or covers only part of the screen. Port of Flixel's FlxPointer.
/// </summary>
public class FlxPointer
{
    /// <summary>Position on the game screen, in game pixels.</summary>
    public float X { get; protected set; }
    public float Y { get; protected set; }

    /// <summary>Position in the window, in window pixels.</summary>
    public float ScreenX { get; protected set; }
    public float ScreenY { get; protected set; }

    public Vector2 Position => new(X, Y);

    /// <summary>Where the pointer is in the world, seen through a camera (the main camera by default): accounts for scroll, zoom and the camera's place on the screen.</summary>
    public Vector2 GetWorldPosition(FlxCamera? camera = null)
    {
        camera ??= FlxG.Cameras.Main;
        return GetPositionInCameraView(camera) + new Vector2(camera.ScrollX, camera.ScrollY);
    }

    /// <summary>Where the pointer is inside a camera's view, in the view's own (zoomed) pixels. Fixed objects (ScrollFactor 0) are positioned this way.</summary>
    public Vector2 GetPositionInCameraView(FlxCamera? camera = null)
    {
        camera ??= FlxG.Cameras.Main;
        return (Position - new Vector2(camera.X, camera.Y)) / Math.Max(0.0001f, camera.Zoom);
    }

    /// <summary>Where the pointer is on a camera, in game pixels from the camera's top left corner (no zoom applied).</summary>
    public Vector2 GetScreenPosition(FlxCamera? camera = null)
    {
        camera ??= FlxG.Cameras.Main;
        return Position - new Vector2(camera.X, camera.Y);
    }

    /// <summary>True if the pointer is over the object (or any member of a group), seen through a camera.</summary>
    public bool Overlaps(FlxBasic objectOrGroup, FlxCamera? camera = null)
    {
        camera ??= FlxG.Cameras.Main;
        return Contains(objectOrGroup, camera, GetPositionInCameraView(camera));
    }

    bool Contains(FlxBasic basic, FlxCamera camera, Vector2 view)
    {
        if (!basic.Exists) return false;
        if (basic is IFlxGroup g)
        {
            foreach (var m in g.Basics) if (m != null && Contains(m, camera, view)) return true;
            if (basic is not FlxObject) return false;       // a plain group has no box of its own
        }
        return basic is FlxObject o && PointInObject(o, camera, view);
    }

    /// <summary>True if a position given in camera view coordinates is inside an object's box.</summary>
    internal static bool PointInObject(FlxObject o, FlxCamera camera, Vector2 view)
    {
        float wx = view.X + camera.ScrollX * o.ScrollFactor.X;
        float wy = view.Y + camera.ScrollY * o.ScrollFactor.Y;
        return wx >= o.X && wx < o.X + o.Width && wy >= o.Y && wy < o.Y + o.Height;
    }

    protected void SetPositions(Vector2 game, Vector2 window)
    {
        X = game.X; Y = game.Y;
        ScreenX = window.X; ScreenY = window.Y;
    }

    /// <summary>Window pixels to game pixels for the picture drawn in <paramref name="dest"/>.</summary>
    internal static Vector2 ToGame(float windowX, float windowY, Rectangle dest, int gameWidth, int gameHeight) =>
        new((windowX - dest.X) * gameWidth / Math.Max(1f, dest.Width), (windowY - dest.Y) * gameHeight / Math.Max(1f, dest.Height));

    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}

public enum FlxMouseButtonID { Left, Middle, Right }

/// <summary>One mouse button: pressed / just pressed / just released, where it went down, and double clicks.</summary>
public sealed class FlxMouseButton : IFlxInput
{
    bool _down, _was;

    public FlxMouseButtonID Id { get; }

    public bool Pressed => _down;
    public bool Released => !_down;
    public bool JustPressed => _down && !_was;
    public bool JustReleased => !_down && _was;

    /// <summary>Where (game pixels) the button last went down.</summary>
    public Vector2 JustPressedPosition { get; private set; }

    /// <summary>When (FlxG.RealTime, seconds) it last went down; -1 if never.</summary>
    public double JustPressedTime { get; private set; } = -1;

    /// <summary>How many quick presses in a row (1 or 2): 2 on the press that makes a double click.</summary>
    public int ClickCount { get; private set; }

    /// <summary>True on the frame a second press follows the first quickly (within FlxMouse.DoubleClickTime) and close by.</summary>
    public bool JustDoubleClicked => JustPressed && ClickCount >= 2;

    internal FlxMouseButton(FlxMouseButtonID id) { Id = id; }

    internal void Update(bool down, Vector2 position, double now, double doubleClickTime)
    {
        _was = _down;
        _down = down;

        if (JustPressed)
        {
            bool quick = JustPressedTime >= 0 && now - JustPressedTime <= doubleClickTime && Vector2.Distance(position, JustPressedPosition) <= 4f;
            ClickCount = quick && ClickCount < 2 ? ClickCount + 1 : 1;
            JustPressedTime = now;
            JustPressedPosition = position;
        }
    }

    internal void Reset() { _down = _was = false; ClickCount = 0; JustPressedTime = -1; }
}

/// <summary>
/// The mouse (FlxG.Mouse): three buttons, the position in game pixels, movement, the wheel and the cursor.
/// <code>
/// if (FlxG.Mouse.Left.JustPressed) Shoot(FlxG.Mouse.GetWorldPosition());
/// if (FlxG.Mouse.Right.JustReleased) CloseMenu();
/// zoom += FlxG.Mouse.Wheel * 0.1f;
/// </code>
/// To react to clicks on objects, see <see cref="FlxMouseEvent"/>.
/// </summary>
public class FlxMouse : FlxPointer
{
    MouseState _cur, _prev;
    Vector2 _previousPosition;

    /// <summary>Where the mouse state comes from (default: the real mouse). Replace it to feed in your own, e.g. in tests.</summary>
    public Func<MouseState> Source = Mouse.GetState;

    public FlxMouseButton Left { get; } = new(FlxMouseButtonID.Left);
    public FlxMouseButton Middle { get; } = new(FlxMouseButtonID.Middle);
    public FlxMouseButton Right { get; } = new(FlxMouseButtonID.Right);

    /// <summary>Two presses within this many seconds (and close together) make a double click.</summary>
    public double DoubleClickTime = 0.35;

    public FlxMouseButton this[FlxMouseButtonID id] => id switch
    {
        FlxMouseButtonID.Middle => Middle,
        FlxMouseButtonID.Right => Right,
        _ => Left
    };

    // the left and right buttons, under the names the engine has always had
    public bool Pressed => Left.Pressed;
    public bool JustPressed => Left.JustPressed;
    public bool JustReleased => Left.JustReleased;
    public bool RightPressed => Right.Pressed;
    public bool RightJustPressed => Right.JustPressed;
    public bool RightJustReleased => Right.JustReleased;
    public bool MiddlePressed => Middle.Pressed;
    public bool MiddleJustPressed => Middle.JustPressed;
    public bool MiddleJustReleased => Middle.JustReleased;

    /// <summary>True if any of the three buttons is down.</summary>
    public bool AnyPressed => Left.Pressed || Middle.Pressed || Right.Pressed;

    /// <summary>How far the mouse moved since the last frame, in game pixels.</summary>
    public Vector2 Delta { get; private set; }

    public bool JustMoved => Delta != Vector2.Zero;

    /// <summary>The wheel's movement this frame in the units the system reports (120 per notch).</summary>
    public int WheelDelta => _cur.ScrollWheelValue - _prev.ScrollWheelValue;

    /// <summary>The wheel's movement this frame in notches: 1 is one notch away from you (up), -1 one notch towards you.</summary>
    public float Wheel => WheelDelta / 120f;

    public bool JustScrolled => WheelDelta != 0;

    /// <summary>The mouse position in the world, seen through the main camera (see GetWorldPosition for other cameras).</summary>
    public Vector2 WorldPosition => GetWorldPosition();

    /// <summary>Show the mouse cursor (the OS one, or your custom image if you called LoadCursor).</summary>
    public bool Visible = true;

    /// <summary>Custom cursor image; null = use the OS cursor.</summary>
    public Texture2D? Cursor { get; private set; }
    public float CursorScale { get; private set; } = 1f;
    public Vector2 CursorOffset { get; private set; }

    /// <summary>Draw a custom cursor image at the given scale (in game pixels).</summary>
    public void LoadCursor(string imagePath, float scale = 1f, int xOffset = 0, int yOffset = 0)
    {
        Cursor = FlxAssets.GetTexture(imagePath);
        CursorScale = scale;
        CursorOffset = new Vector2(xOffset, yOffset);
    }

    public void ResetCursor() { Cursor = null; CursorScale = 1f; CursorOffset = Vector2.Zero; }

    /// <summary>Forgets the buttons (nothing is down).</summary>
    public void Reset()
    {
        _cur = _prev = default;
        Left.Reset(); Middle.Reset(); Right.Reset();
        Delta = Vector2.Zero;
    }

    internal void Update(Rectangle dest, int gameWidth, int gameHeight)
    {
        _prev = _cur;
        _cur = Source();

        _previousPosition = Position;
        SetPositions(ToGame(_cur.X, _cur.Y, dest, gameWidth, gameHeight), new Vector2(_cur.X, _cur.Y));
        Delta = Position - _previousPosition;

        double now = FlxG.RealTime;
        Left.Update(_cur.LeftButton == ButtonState.Pressed, Position, now, DoubleClickTime);
        Middle.Update(_cur.MiddleButton == ButtonState.Pressed, Position, now, DoubleClickTime);
        Right.Update(_cur.RightButton == ButtonState.Pressed, Position, now, DoubleClickTime);
    }
}

/// <summary>One finger on a touch screen.</summary>
public sealed class FlxTouch : FlxPointer, IFlxInput
{
    bool _down, _was;

    public int Id { get; }

    public bool Pressed => _down;
    public bool Released => !_down;
    public bool JustPressed => _down && !_was;
    public bool JustReleased => !_down && _was;

    public Vector2 JustPressedPosition { get; private set; }
    public double JustPressedTime { get; private set; }

    internal bool Finished => !_down && !_was;

    internal void Advance() => _was = _down;

    internal FlxTouch(int id) { Id = id; }

    internal void Update(bool down, Vector2 game, Vector2 window)
    {
        _was = _down;
        _down = down;
        SetPositions(game, window);

        if (JustPressed)
        {
            JustPressedPosition = game;
            JustPressedTime = FlxG.RealTime;
        }
    }
}

public enum FlxSwipeDirection { Right, Down, Left, Up }

/// <summary>A finished drag with a finger (or the mouse, see FlxTouchManager.SimulateWithMouse): where it began and ended and how long it took.</summary>
public sealed class FlxSwipe
{
    public int Id { get; }
    public Vector2 StartPosition { get; }
    public Vector2 EndPosition { get; }
    public double Duration { get; }

    public FlxSwipe(int id, Vector2 start, Vector2 end, double duration)
    {
        Id = id; StartPosition = start; EndPosition = end; Duration = duration;
    }

    public float Distance => Vector2.Distance(StartPosition, EndPosition);

    /// <summary>Direction in degrees: 0 is right, 90 down (screen coordinates).</summary>
    public float Degrees => MathHelper.ToDegrees(Radians);

    public float Radians => MathF.Atan2(EndPosition.Y - StartPosition.Y, EndPosition.X - StartPosition.X);

    /// <summary>The nearest of the four directions.</summary>
    public FlxSwipeDirection Direction
    {
        get
        {
            float d = ((Degrees % 360f) + 360f) % 360f;
            if (d >= 315f || d < 45f) return FlxSwipeDirection.Right;
            if (d < 135f) return FlxSwipeDirection.Down;
            if (d < 225f) return FlxSwipeDirection.Left;
            return FlxSwipeDirection.Up;
        }
    }

    /// <summary>Pixels per second.</summary>
    public float Speed => Duration <= 0 ? 0 : (float)(Distance / Duration);
}

/// <summary>Touches and swipes (FlxG.Touches). On a desktop without a touch screen there are none, unless SimulateWithMouse is on.</summary>
public sealed class FlxTouchManager
{
    readonly List<FlxTouch> _touches = new();
    readonly List<FlxSwipe> _swipes = new();

    /// <summary>Where the touch states come from (default: the touch panel). Replace it to feed in your own.</summary>
    public Func<TouchCollection> Source = ReadPanel;

    /// <summary>Treat the left mouse button as a finger (with id -1), so touch code can be tried without a touch screen.</summary>
    public bool SimulateWithMouse;

    /// <summary>Drags shorter than this many game pixels are not reported as swipes.</summary>
    public float MinSwipeDistance = 8f;

    /// <summary>The fingers that are down, plus the ones that came up this frame.</summary>
    public IReadOnlyList<FlxTouch> List => _touches;

    /// <summary>Swipes that finished this frame (a finger came up after moving far enough).</summary>
    public IReadOnlyList<FlxSwipe> Swipes => _swipes;

    public FlxTouch? GetById(int id) => _touches.Find(t => t.Id == id);

    public bool AnyPressed { get { foreach (var t in _touches) if (t.Pressed) return true; return false; } }
    public bool AnyJustPressed { get { foreach (var t in _touches) if (t.JustPressed) return true; return false; } }

    static TouchCollection ReadPanel()
    {
        try { return TouchPanel.GetState(); }
        catch (Exception) { return default; }         // no touch support on this platform
    }

    internal void Update(Rectangle dest, int gameWidth, int gameHeight)
    {
        _swipes.Clear();

        // a finger that came up last frame has been "just released" for one frame: now it is gone
        foreach (var t in _touches) if (!t.Pressed) t.Advance();
        _touches.RemoveAll(t => t.Finished);

        var seen = new HashSet<int>();

        foreach (var loc in Source())
        {
            if (loc.State == TouchLocationState.Invalid) continue;

            var touch = GetById(loc.Id);
            if (touch == null) { touch = new FlxTouch(loc.Id); _touches.Add(touch); }
            seen.Add(loc.Id);

            bool down = loc.State != TouchLocationState.Released;
            touch.Update(down, FlxPointer.ToGame(loc.Position.X, loc.Position.Y, dest, gameWidth, gameHeight), loc.Position);
            if (touch.JustReleased) AddSwipe(touch);
        }

        if (SimulateWithMouse)
        {
            var mouse = FlxG.Mouse;
            var touch = GetById(-1);
            bool down = mouse.Left.Pressed;
            if (touch == null && down) { touch = new FlxTouch(-1); _touches.Add(touch); }
            if (touch != null)
            {
                seen.Add(-1);
                touch.Update(down, mouse.Position, new Vector2(mouse.ScreenX, mouse.ScreenY));
                if (touch.JustReleased) AddSwipe(touch);
            }
        }

        // a finger that disappeared without a release counts as released
        foreach (var t in _touches)
            if (!seen.Contains(t.Id) && t.Pressed)
            {
                t.Update(false, t.Position, new Vector2(t.ScreenX, t.ScreenY));
                AddSwipe(t);
            }
    }

    void AddSwipe(FlxTouch t)
    {
        var swipe = new FlxSwipe(t.Id, t.JustPressedPosition, t.Position, FlxG.RealTime - t.JustPressedTime);
        if (swipe.Distance >= MinSwipeDistance) _swipes.Add(swipe);
    }
}
