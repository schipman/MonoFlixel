using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>
/// Looks after the mouse for <see cref="FlxExtendedSprite"/>s: which sprite was clicked when several overlap, dragging, and how fast
/// the mouse moves (for throwing). Add one to the state before using the mouse features of an extended sprite.
/// Port of Richard Davey's (Photon Storm) Flixel Power Tools <c>FlxMouseControl</c> (see NOTICE-flixel-power-tools.txt), as an object instead of a plugin.
/// <code>
/// Add(new FlxMouseControl());                         // once per state
/// var box = new FlxExtendedSprite(100, 100, "box.png");
/// box.EnableMouseDrag();
/// box.EnableMouseThrow(10, 10);
/// Add(box);
/// </code>
/// </summary>
public class FlxMouseControl : FlxBasic
{
    /// <summary>The control that is in the state now (extended sprites find it here).</summary>
    public static FlxMouseControl? Current { get; private set; }

    /// <summary>When several sprites are under the mouse, the one with the highest value of this wins (default: Y, so the lowest one on screen, which is drawn on top in a top-down view).</summary>
    public Func<FlxExtendedSprite, float> SortKey = s => s.Y;

    /// <summary>Clicks only count when the mouse is inside this world rectangle (null = anywhere).</summary>
    public Rectangle? MouseZone;

    /// <summary>Let go of what is held when the mouse leaves the camera's deadzone (instead of <see cref="MouseZone"/>).</summary>
    public bool LinkToDeadZone;

    public bool IsDragging { get; private set; }
    public FlxExtendedSprite? DragTarget { get; private set; }
    public FlxExtendedSprite? ClickTarget { get; private set; }

    /// <summary>How far the mouse moved in the last frame, in game pixels. Throwing multiplies it by a factor, as in the original.</summary>
    public int SpeedX { get; private set; }
    public int SpeedY { get; private set; }

    readonly List<FlxExtendedSprite> _clickStack = new();
    Vector2 _clickCoords;
    Point _lastMouse;

    public FlxMouseControl() => Current = this;

    internal void AddToStack(FlxExtendedSprite item)
    {
        if (MouseZone is { } zone && !zone.Contains(FlxG.Mouse.GetWorldPosition().ToPoint())) return;
        _clickStack.Add(item);
    }

    public override void Update(float elapsed)
    {
        var mouse = FlxG.Mouse;
        var now = new Point((int)mouse.X, (int)mouse.Y);
        SpeedX = now.X - _lastMouse.X;
        SpeedY = now.Y - _lastMouse.Y;
        _lastMouse = now;

        if (ClickTarget != null)
        {
            if (mouse.Pressed)
            {
                var world = mouse.GetWorldPosition();
                if (!IsDragging && ClickTarget.Draggable && (_clickCoords.X != world.X || _clickCoords.Y != world.Y))
                {
                    IsDragging = true;
                    DragTarget = ClickTarget;
                    DragTarget.StartDrag();
                }
            }
            else ReleaseMouse();

            if (ClickTarget != null)
            {
                if (LinkToDeadZone)
                {
                    if (FlxG.Camera.Deadzone is { } dead && !dead.Contains(mouse.GetPositionInCameraView().ToPoint())) ReleaseMouse();
                }
                else if (MouseZone is { } zone && !zone.Contains(mouse.GetWorldPosition().ToPoint())) ReleaseMouse();
            }
        }
        else
        {
            if (mouse.Pressed && _clickStack.Count > 0) AssignClickedSprite();
            if (!mouse.Pressed) _clickStack.Clear();
        }
    }

    void ReleaseMouse()
    {
        var target = ClickTarget;
        ClickTarget = null;
        IsDragging = false;
        DragTarget = null;
        target?.MouseReleasedHandler();
    }

    void AssignClickedSprite()
    {
        // the last of the sorted list is the one on top
        if (_clickStack.Count > 1) _clickStack.Sort((a, b) => SortKey(a).CompareTo(SortKey(b)));
        ClickTarget = _clickStack[^1];
        _clickCoords = FlxG.Mouse.GetWorldPosition();
        _clickStack.Clear();
        ClickTarget.MousePressedHandler();
    }

    /// <summary>Lets go of everything and forgets the settings.</summary>
    public void Clear()
    {
        var target = ClickTarget;
        ClickTarget = null;
        target?.MouseReleasedHandler();
        IsDragging = false;
        DragTarget?.StopDrag();
        DragTarget = null;
        SpeedX = SpeedY = 0;
        MouseZone = null;
        LinkToDeadZone = false;
        _clickStack.Clear();
    }

    public override void Destroy()
    {
        Clear();
        if (ReferenceEquals(Current, this)) Current = null;
        base.Destroy();
    }
}

/// <summary>The mouse pulls a sprite with a spring: it overshoots, wobbles and settles on the pointer. Made by <see cref="FlxExtendedSprite.EnableMouseSpring"/>.</summary>
public class FlxMouseSpring
{
    public FlxExtendedSprite Sprite;

    /// <summary>How hard the spring pulls (0.1), how much speed is kept each frame (0.95), and a constant pull downwards (0).</summary>
    public float Tension = 0.1f, Friction = 0.95f, Gravity;

    float _vx, _vy;

    public FlxMouseSpring(FlxExtendedSprite sprite, float tension = 0.1f, float friction = 0.95f, float gravity = 0)
    {
        Sprite = sprite;
        Tension = tension;
        Friction = friction;
        Gravity = gravity;
    }

    public void Update()
    {
        var mouse = FlxG.Mouse.GetWorldPosition();
        _vx += (mouse.X - Sprite.SpringX) * Tension;
        _vy += (mouse.Y - Sprite.SpringY) * Tension;
        _vy += Gravity;
        _vx *= Friction;
        _vy *= Friction;
        Sprite.X += _vx;
        Sprite.Y += _vy;
    }

    public void Reset() => _vx = _vy = 0;
}

/// <summary>
/// A sprite that can be clicked, dragged and thrown with the mouse, held by a spring, snapped to a grid, kept inside bounds, and given
/// gravity with friction. Port of Richard Davey's (Photon Storm) Flixel Power Tools <c>FlxExtendedSprite</c> (see
/// NOTICE-flixel-power-tools.txt). The mouse features need an <see cref="FlxMouseControl"/> in the state.
/// <code>
/// Add(new FlxMouseControl());
/// var card = new FlxExtendedSprite(40, 40).MakeGraphic(32, 48, Color.Orange);
/// card.EnableMouseDrag(boundsRect: new Rectangle(0, 0, 320, 240));
/// card.EnableMouseSnap(16, 16, onDrag: false, onRelease: true);      // snaps to a 16 pixel grid when let go
/// card.MouseReleasedCallback = (s, x, y) => Console.WriteLine("dropped");
/// Add(card);
/// </code>
/// Differences from the original: the start and stop drag callbacks are really called (the original never did), a dragged sprite does not
/// move on by its own speed while the mouse holds it, mouse positions are
/// world coordinates, and <see cref="PriorityId"/> and the pixel test use <see cref="FlxCollision.PixelPerfectPointCheck"/>.
/// </summary>
public class FlxExtendedSprite : FlxSprite
{
    /// <summary>Called with (sprite, x, y inside the sprite) when the mouse is pressed on it or let go of it.</summary>
    public Action<FlxExtendedSprite, int, int>? MousePressedCallback, MouseReleasedCallback;

    /// <summary>Called when a drag starts and stops.</summary>
    public Action<FlxExtendedSprite>? MouseStartDragCallback, MouseStopDragCallback;

    /// <summary>Not used by this port, kept for code that sets it.</summary>
    public uint PriorityId;

    public bool IsPressed { get; private set; }
    public bool Clickable { get; private set; }
    public bool Draggable { get; private set; }
    public bool IsDragged { get; private set; }
    public bool Throwable { get; private set; }

    /// <summary>Drag stays inside this rectangle, or inside the area of this sprite.</summary>
    public Rectangle? BoundsRect;
    public FlxSprite? BoundsSprite;

    // gravity
    public bool HasGravity { get; private set; }
    public int GravityX { get; private set; }
    public int GravityY { get; private set; }
    public float FrictionX, FrictionY, ToleranceX, ToleranceY;

    // spring
    public bool HasMouseSpring { get; private set; }
    public bool SpringOnPressed = true;
    public FlxMouseSpring? MouseSpring { get; private set; }
    public int SpringOffsetX, SpringOffsetY;

    bool _clickOnRelease, _clickPixelPerfect, _dragPixelPerfect, _dragFromPoint;
    int _clickPixelPerfectAlpha, _dragPixelPerfectAlpha, _clicks, _throwXFactor, _throwYFactor, _snapX, _snapY;
    bool _allowHorizontalDrag = true, _allowVerticalDrag = true, _snapOnDrag, _snapOnRelease;
    Vector2 _dragOffset;

    public FlxExtendedSprite(float x = 0, float y = 0, string? graphic = null) : base(x, y, graphic) { }

    static FlxMouseControl Control => FlxMouseControl.Current
        ?? throw new InvalidOperationException("This needs an FlxMouseControl in the state: Add(new FlxMouseControl()) first.");

    // ------------------------------------------------------------ clicks

    /// <summary>Count clicks on this sprite (read and reset them with <see cref="Clicks"/>). A click is the press, or the release with <paramref name="onRelease"/>.</summary>
    public void EnableMouseClicks(bool onRelease, bool pixelPerfect = false, int alphaThreshold = 255)
    {
        _ = Control;
        Clickable = true;
        _clickOnRelease = onRelease;
        _clickPixelPerfect = pixelPerfect;
        _clickPixelPerfectAlpha = alphaThreshold;
        _clicks = 0;
    }

    public void DisableMouseClicks()
    {
        Clickable = false;
        MousePressedCallback = null;
        MouseReleasedCallback = null;
    }

    public int Clicks { get => _clicks; set => _clicks = value; }

    // ------------------------------------------------------------ dragging

    /// <summary>
    /// Makes the sprite draggable. <paramref name="lockCenter"/> holds it by its middle instead of the spot you grabbed; the bounds keep
    /// it inside a rectangle or another sprite; pixel perfect means only solid pixels can grab it.
    /// </summary>
    public void EnableMouseDrag(bool lockCenter = false, bool pixelPerfect = false, int alphaThreshold = 255, Rectangle? boundsRect = null, FlxSprite? boundsSprite = null)
    {
        _ = Control;
        Draggable = true;
        _dragFromPoint = lockCenter;
        _dragPixelPerfect = pixelPerfect;
        _dragPixelPerfectAlpha = alphaThreshold;
        if (boundsRect != null) BoundsRect = boundsRect;
        if (boundsSprite != null) BoundsSprite = boundsSprite;
    }

    public void DisableMouseDrag()
    {
        if (IsDragged) Control.Clear();
        IsDragged = false;
        Draggable = false;
        MouseStartDragCallback = null;
        MouseStopDragCallback = null;
    }

    /// <summary>Allow dragging only along one axis.</summary>
    public void SetDragLock(bool allowHorizontal = true, bool allowVertical = true)
    {
        _allowHorizontalDrag = allowHorizontal;
        _allowVerticalDrag = allowVertical;
    }

    // ------------------------------------------------------------ throwing, snapping, spring

    /// <summary>When let go, the sprite flies off with the speed of the mouse times these factors (they are per frame, as in the original: try 10 to 50).</summary>
    public void EnableMouseThrow(int xFactor, int yFactor)
    {
        _ = Control;
        Throwable = true;
        _throwXFactor = xFactor;
        _throwYFactor = yFactor;
        if (!Clickable && !Draggable) Clickable = true;
    }

    public void DisableMouseThrow() => Throwable = false;

    /// <summary>Snaps to a grid of this size while dragging and/or when let go.</summary>
    public void EnableMouseSnap(int snapX, int snapY, bool onDrag = true, bool onRelease = false)
    {
        _snapOnDrag = onDrag;
        _snapOnRelease = onRelease;
        _snapX = snapX;
        _snapY = snapY;
    }

    public void DisableMouseSnap() => _snapOnDrag = _snapOnRelease = false;

    /// <summary>A spring pulls the sprite to the mouse (while pressed, or always with <paramref name="onPressed"/> false).</summary>
    public FlxMouseSpring EnableMouseSpring(bool onPressed = true, float tension = 0.1f, float friction = 0.95f, float gravity = 0)
    {
        _ = Control;
        HasMouseSpring = true;
        SpringOnPressed = onPressed;
        if (MouseSpring == null) MouseSpring = new FlxMouseSpring(this, tension, friction, gravity);
        else
        {
            MouseSpring.Tension = tension;
            MouseSpring.Friction = friction;
            MouseSpring.Gravity = gravity;
        }
        if (!Clickable && !Draggable) Clickable = true;
        return MouseSpring;
    }

    public void DisableMouseSpring()
    {
        HasMouseSpring = false;
        MouseSpring = null;
    }

    public float SpringX => X + SpringOffsetX;
    public float SpringY => Y + SpringOffsetY;

    // ------------------------------------------------------------ gravity

    /// <summary>
    /// Gravity for a sprite that slides and bounces: the force, and when it touches something the friction that slows it down and the
    /// speed below which it stops dead.
    /// </summary>
    public void SetGravity(int gravityX, int gravityY, float frictionX = 500, float frictionY = 500, float toleranceX = 10, float toleranceY = 10)
    {
        HasGravity = gravityX != 0 || gravityY != 0;
        GravityX = gravityX;
        GravityY = gravityY;
        FrictionX = frictionX;
        FrictionY = frictionY;
        ToleranceX = toleranceX;
        ToleranceY = toleranceY;
        Acceleration = new Vector2(gravityX, gravityY);
    }

    public void FlipGravity()
    {
        if (GravityX != 0) { GravityX = -GravityX; Acceleration.X = GravityX; }
        if (GravityY != 0) { GravityY = -GravityY; Acceleration.Y = GravityY; }
    }

    // ------------------------------------------------------------ mouse state

    public bool MouseOver => FlxMath.PointInCoordinates(FlxG.Mouse.GetWorldPosition().X, FlxG.Mouse.GetWorldPosition().Y, X, Y, Width, Height);

    /// <summary>The mouse position inside the sprite, or -1 when it is not over it.</summary>
    public int MouseX => MouseOver ? (int)(FlxG.Mouse.GetWorldPosition().X - X) : -1;
    public int MouseY => MouseOver ? (int)(FlxG.Mouse.GetWorldPosition().Y - Y) : -1;

    // ------------------------------------------------------------ update

    public override void Update(float elapsed)
    {
        if (Draggable && IsDragged)
        {
            Velocity = Vector2.Zero;     // held by the mouse: gravity must not pull it away from the pointer
            UpdateDrag();
        }

        if (!IsPressed && FlxG.Mouse.JustPressed) CheckForClick();

        if (HasGravity) UpdateGravity();

        if (HasMouseSpring && MouseSpring != null)
        {
            if (!SpringOnPressed || IsPressed) MouseSpring.Update();
            else MouseSpring.Reset();
        }

        base.Update(elapsed);
    }

    // Reads Touching before base.Update resets it, like the original did.
    void UpdateGravity()
    {
        if (Velocity.X != 0)
        {
            if (Acceleration.X < 0)
            {
                if ((Touching & FlxDirection.Wall) != 0)
                {
                    Drag.Y = FrictionY;
                    if ((WasTouching & FlxDirection.Wall) == 0 && Velocity.X < ToleranceX) Velocity.X = 0;
                }
                else Drag.Y = 0;
            }
            else if (Acceleration.X > 0)
            {
                if ((Touching & FlxDirection.Wall) != 0)
                {
                    Drag.Y = FrictionY;
                    if ((WasTouching & FlxDirection.Wall) == 0 && Velocity.X > -ToleranceX) Velocity.X = 0;
                }
                else Drag.Y = 0;
            }
        }

        if (Velocity.Y != 0)
        {
            if (Acceleration.Y < 0)
            {
                if ((Touching & FlxDirection.Ceiling) != 0)
                {
                    Drag.X = FrictionX;
                    if ((WasTouching & FlxDirection.Ceiling) == 0 && Velocity.Y < ToleranceY) Velocity.Y = 0;
                }
                else Drag.X = 0;
            }
            else if (Acceleration.Y > 0)
            {
                if ((Touching & FlxDirection.Floor) != 0)
                {
                    Drag.X = FrictionX;
                    if ((WasTouching & FlxDirection.Floor) == 0 && Velocity.Y > -ToleranceY) Velocity.Y = 0;
                }
                else Drag.X = 0;
            }
        }
    }

    void UpdateDrag()
    {
        var mouse = FlxG.Mouse.GetWorldPosition();
        if (_allowHorizontalDrag) X = (int)mouse.X - _dragOffset.X;
        if (_allowVerticalDrag) Y = (int)mouse.Y - _dragOffset.Y;

        if (BoundsRect is { } r)
        {
            if (X < r.Left) X = r.X;
            else if (X + Width > r.Right) X = r.Right - Width;
            if (Y < r.Top) Y = r.Y;
            else if (Y + Height > r.Bottom) Y = r.Bottom - Height;
        }

        if (BoundsSprite is { } b)
        {
            if (X < b.X) X = b.X;
            else if (X + Width > b.X + b.Width) X = b.X + b.Width - Width;
            if (Y < b.Y) Y = b.Y;
            else if (Y + Height > b.Y + b.Height) Y = b.Y + b.Height - Height;
        }

        if (_snapOnDrag) Snap();
    }

    void Snap()
    {
        X = (int)(MathF.Floor(X / _snapX) * _snapX);
        Y = (int)(MathF.Floor(Y / _snapY) * _snapY);
    }

    void CheckForClick()
    {
        if (!MouseOver) return;

        var control = Control;
        var world = FlxG.Mouse.GetWorldPosition();
        if (!_clickPixelPerfect && !_dragPixelPerfect) { control.AddToStack(this); return; }
        if (_clickPixelPerfect && FlxCollision.PixelPerfectPointCheck(world, this, _clickPixelPerfectAlpha)) { control.AddToStack(this); return; }
        if (_dragPixelPerfect && FlxCollision.PixelPerfectPointCheck(world, this, _dragPixelPerfectAlpha)) control.AddToStack(this);
    }

    // ------------------------------------------------------------ called by FlxMouseControl

    internal void MousePressedHandler()
    {
        IsPressed = true;
        if (Clickable && !_clickOnRelease) _clicks++;
        MousePressedCallback?.Invoke(this, MouseX, MouseY);
    }

    internal void MouseReleasedHandler()
    {
        IsPressed = false;
        if (IsDragged) StopDrag();
        if (Clickable && _clickOnRelease) _clicks++;
        if (Throwable)
        {
            var control = Control;
            Velocity = new Vector2(control.SpeedX * _throwXFactor, control.SpeedY * _throwYFactor);
        }
        MouseReleasedCallback?.Invoke(this, MouseX, MouseY);
    }

    internal void StartDrag()
    {
        IsDragged = true;
        if (!_dragFromPoint)
        {
            var mouse = FlxG.Mouse.GetWorldPosition();
            _dragOffset = new Vector2((int)mouse.X - X, (int)mouse.Y - Y);
        }
        else _dragOffset = new Vector2(FrameWidth / 2f, FrameHeight / 2f);
        MouseStartDragCallback?.Invoke(this);
    }

    internal void StopDrag()
    {
        IsDragged = false;
        if (_snapOnRelease) Snap();
        MouseStopDragCallback?.Invoke(this);
    }
}
