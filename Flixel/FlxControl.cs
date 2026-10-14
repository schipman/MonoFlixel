using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel;

public enum FlxControlMovement { Instant, Accelerates }
public enum FlxControlStopping { Instant, Decelerates, Never }
public enum FlxControlRotation { Instant, Accelerates }
public enum FlxControlRotationStopping { Instant, Decelerates, Never }

/// <summary>When a fire or jump key counts: while held, on the frame it goes down, or on the frame it is let go.</summary>
public enum FlxKeyMode { Pressed, JustDown, Released }

/// <summary>
/// Keyboard control for sprites, ready made: walk, accelerate and slide, rotate, thrust like a spaceship, jump with gravity, fire with a
/// repeat rate. Port of Richard Davey's (Photon Storm) Flixel Power Tools <c>FlxControl</c> (see NOTICE-flixel-power-tools.txt), as an object
/// instead of a plugin. Add it to the state <b>before</b> the sprites it moves (the jump needs to read what they touched in the last frame).
/// <code>
/// var control = new FlxControl();
/// Add(control);
/// Add(player);
/// var c = control.Create(player, FlxControlMovement.Accelerates, FlxControlStopping.Decelerates);
/// c.SetWASDControl();                                    // or SetCursorControl() (the default), ESDF, IJKL, ...
/// c.SetMovementSpeed(400, 400, 100, 100, 300, 300);      // acceleration, top speed, deceleration
/// c.SetGravity(0, 600);
/// c.SetJumpButton(Keys.Space, FlxKeyMode.JustDown, 200, FlxDirection.Floor, 250);
/// </code>
/// </summary>
public class FlxControl : FlxBasic
{
    readonly List<FlxControlHandler> _handlers = new();

    /// <summary>The handlers made for players 1 to 4 (null until made).</summary>
    public FlxControlHandler? Player1, Player2, Player3, Player4;

    /// <summary>Makes a handler for a sprite. <paramref name="player"/> 1 to 4 also stores it in Player1..Player4.</summary>
    public FlxControlHandler Create(FlxSprite source, FlxControlMovement movementType, FlxControlStopping stoppingType, int player = 1, bool updateFacing = false, bool enableArrowKeys = true)
    {
        var handler = new FlxControlHandler(source, movementType, stoppingType, updateFacing, enableArrowKeys);
        _handlers.Add(handler);
        switch (player)
        {
            case 1: Player1 = handler; break;
            case 2: Player2 = handler; break;
            case 3: Player3 = handler; break;
            case 4: Player4 = handler; break;
        }
        return handler;
    }

    public bool Remove(FlxControlHandler handler)
    {
        if (Player1 == handler) Player1 = null;
        if (Player2 == handler) Player2 = null;
        if (Player3 == handler) Player3 = null;
        if (Player4 == handler) Player4 = null;
        return _handlers.Remove(handler);
    }

    /// <summary>Switch one handler, or all of them, on or off.</summary>
    public void Start(FlxControlHandler? handler = null)
    {
        if (handler != null) handler.Enabled = true;
        else foreach (var h in _handlers) h.Enabled = true;
    }

    public void Stop(FlxControlHandler? handler = null)
    {
        if (handler != null) handler.Enabled = false;
        else foreach (var h in _handlers) h.Enabled = false;
    }

    public void Clear()
    {
        _handlers.Clear();
        Player1 = Player2 = Player3 = Player4 = null;
    }

    public override void Update(float elapsed)
    {
        foreach (var h in _handlers)
            if (h.Enabled) h.Update(elapsed);
    }

    public override void Destroy()
    {
        Clear();
        base.Destroy();
    }
}

/// <summary>The control settings and logic for one sprite. Made by <see cref="FlxControl.Create"/>.</summary>
public class FlxControlHandler
{
    public bool Enabled = true;

    /// <summary>Which direction keys were down in the last update.</summary>
    public bool IsPressedUp, IsPressedDown, IsPressedLeft, IsPressedRight;

    readonly FlxSprite _entity;
    readonly FlxControlMovement _movement;
    readonly FlxControlStopping _stopping;
    readonly bool _xFacing, _yFacing;

    Rectangle? _bounds;

    // keys
    bool _up, _down, _left, _right;
    Keys _upKey, _downKey, _leftKey, _rightKey;

    // speeds
    float _upSpeed, _downSpeed, _leftSpeed, _rightSpeed;
    bool _capVelocity;
    int _gravityX, _gravityY;

    // rotation
    bool _isRotating, _capAngularVelocity;
    FlxControlRotation _rotation = FlxControlRotation.Instant;
    FlxControlRotationStopping _rotationStopping = FlxControlRotationStopping.Instant;
    float _antiClockwiseSpeed, _clockwiseSpeed;
    Keys _antiClockwiseKey = Keys.Left, _clockwiseKey = Keys.Right;

    // thrust
    bool _thrustEnabled, _reverseEnabled;
    float _thrustSpeed, _reverseSpeed;
    Keys _thrustKey, _reverseKey;

    // fire
    bool _fire;
    Keys _fireKey;
    FlxKeyMode _fireMode;
    int _fireRate;
    Action? _fireCallback;
    double _nextFireTime;

    // jump
    bool _jump;
    Keys _jumpKey;
    FlxKeyMode _jumpMode;
    float _jumpHeight;
    int _jumpRate, _jumpFromFallTime;
    FlxDirection _jumpSurface;
    Action? _jumpCallback;
    double _nextJumpTime, _lastJumpTime, _extraSurfaceTime;

    FlxSound? _jumpSound, _fireSound, _walkSound, _thrustSound;

    static double Now => FlxG.RealTime * 1000;

    public FlxControlHandler(FlxSprite source, FlxControlMovement movementType, FlxControlStopping stoppingType, bool updateFacing = false, bool enableArrowKeys = true)
    {
        _entity = source;
        _movement = movementType;
        _stopping = stoppingType;
        _xFacing = _yFacing = updateFacing;
        if (enableArrowKeys) SetCursorControl();
    }

    // ------------------------------------------------------------ speeds

    /// <summary>
    /// Speeds in pixels per second: how fast it accelerates (or moves, with instant movement), the top speed, and how fast it slows down when no key is held.
    /// </summary>
    public void SetMovementSpeed(float xSpeed, float ySpeed, float xSpeedMax, float ySpeedMax, float xDeceleration = 0, float yDeceleration = 0)
    {
        _leftSpeed = -xSpeed;
        _rightSpeed = xSpeed;
        _upSpeed = -ySpeed;
        _downSpeed = ySpeed;
        SetMaximumSpeed(xSpeedMax, ySpeedMax);
        SetDeceleration(xDeceleration, yDeceleration);
    }

    /// <summary>One number for all of it: speed, top speed and (with <paramref name="acceleration"/>) deceleration.</summary>
    public void SetStandardSpeed(float speed, bool acceleration = true)
    {
        if (acceleration) SetMovementSpeed(speed, speed, speed, speed, speed, speed);
        else SetMovementSpeed(speed, speed, speed, speed);
    }

    public void SetAdvancedMovementSpeed(float leftSpeed, float rightSpeed, float upSpeed, float downSpeed, float xSpeedMax, float ySpeedMax, float xDeceleration = 0, float yDeceleration = 0)
    {
        _leftSpeed = -leftSpeed;
        _rightSpeed = rightSpeed;
        _upSpeed = -upSpeed;
        _downSpeed = downSpeed;
        SetMaximumSpeed(xSpeedMax, ySpeedMax);
        SetDeceleration(xDeceleration, yDeceleration);
    }

    public void SetMaximumSpeed(float xSpeed, float ySpeed, bool limitVelocity = true)
    {
        _entity.MaxVelocity = new Vector2(xSpeed, ySpeed);
        _capVelocity = limitVelocity;
    }

    public void SetDeceleration(float xSpeed, float ySpeed) => _entity.Drag = new Vector2(xSpeed, ySpeed);

    public void SetGravity(int xForce, int yForce)
    {
        _gravityX = xForce;
        _gravityY = yForce;
        _entity.Acceleration = new Vector2(xForce, yForce);
    }

    public void FlipGravity()
    {
        if (_gravityX != 0) { _gravityX = -_gravityX; _entity.Acceleration.X = _gravityX; }
        if (_gravityY != 0) { _gravityY = -_gravityY; _entity.Acceleration.Y = _gravityY; }
    }

    /// <summary>The sprite cannot leave this rectangle (checked when a direction key moves it towards the edge).</summary>
    public void SetBounds(int x, int y, int width, int height) => _bounds = new Rectangle(x, y, width, height);
    public void RemoveBounds() => _bounds = null;

    // ------------------------------------------------------------ rotation and thrust

    /// <summary>Turn speeds in degrees per second, top speed and deceleration. Also switches the rotation keys on (left / right by default).</summary>
    public void SetRotationSpeed(float antiClockwiseSpeed, float clockwiseSpeed, float speedMax, float deceleration)
    {
        _antiClockwiseSpeed = -antiClockwiseSpeed;
        _clockwiseSpeed = clockwiseSpeed;
        SetRotationKeys();
        _entity.MaxAngular = speedMax;
        _capAngularVelocity = true;
        _entity.AngularDrag = deceleration;
    }

    public void SetRotationType(FlxControlRotation rotationType, FlxControlRotationStopping stoppingType)
    {
        _rotation = rotationType;
        _rotationStopping = stoppingType;
    }

    /// <summary>Turn with left / right (or up / down), or with keys of your own.</summary>
    public void SetRotationKeys(bool upDown = false, Keys? customAntiClockwise = null, Keys? customClockwise = null)
    {
        _isRotating = true;
        (_antiClockwiseKey, _clockwiseKey) = upDown ? (Keys.Up, Keys.Down) : (Keys.Left, Keys.Right);
        if (customAntiClockwise != null && customClockwise != null)
        {
            _antiClockwiseKey = customAntiClockwise.Value;
            _clockwiseKey = customClockwise.Value;
        }
    }

    /// <summary>Move in the direction the sprite points (its Angle) with a key, and optionally back with another: a spaceship.</summary>
    public void SetThrust(Keys thrustKey, float thrustSpeed, Keys? reverseKey = null, float reverseSpeed = 0)
    {
        _thrustKey = thrustKey;
        _thrustSpeed = thrustSpeed;
        _thrustEnabled = true;
        _reverseEnabled = reverseKey != null;
        if (reverseKey != null)
        {
            _reverseKey = reverseKey.Value;
            _reverseSpeed = reverseSpeed;
        }
    }

    // ------------------------------------------------------------ sounds, fire, jump

    /// <summary>Sounds for jumping, firing, walking (plays while moving) and thrusting.</summary>
    public void SetSounds(FlxSound? jump = null, FlxSound? fire = null, FlxSound? walk = null, FlxSound? thrust = null)
    {
        if (jump != null) _jumpSound = jump;
        if (fire != null) _fireSound = fire;
        if (walk != null) _walkSound = walk;
        if (thrust != null) _thrustSound = thrust;
    }

    /// <summary>Calls the callback when the key is used. With a repeat delay (ms) a held key fires again and again.</summary>
    public void SetFireButton(Keys key, FlxKeyMode mode, int repeatDelay, Action callback)
    {
        _fireKey = key;
        _fireMode = mode;
        _fireRate = repeatDelay;
        _fireCallback = callback;
        _fire = true;
    }

    /// <summary>
    /// A jump key. <paramref name="height"/> is the upward speed given (gravity and drag decide how high that is), the sprite may jump
    /// when it touches <paramref name="surface"/> (FlxDirection.Floor), <paramref name="repeatDelay"/> is the time between jumps (ms) and
    /// <paramref name="jumpFromFall"/> a short time after walking off an edge in which jumping still works.
    /// </summary>
    public void SetJumpButton(Keys key, FlxKeyMode mode, float height, FlxDirection surface, int repeatDelay = 250, int jumpFromFall = 0, Action? callback = null)
    {
        _jumpKey = key;
        _jumpMode = mode;
        _jumpHeight = height;
        _jumpSurface = surface;
        _jumpRate = repeatDelay;
        _jumpFromFallTime = jumpFromFall;
        _jumpCallback = callback;
        _jump = true;
    }

    // ------------------------------------------------------------ key layouts

    void SetKeys(bool up, bool down, bool left, bool right, Keys upKey, Keys downKey, Keys leftKey, Keys rightKey)
    {
        (_up, _down, _left, _right) = (up, down, left, right);
        (_upKey, _downKey, _leftKey, _rightKey) = (upKey, downKey, leftKey, rightKey);
    }

    public void SetCustomKeys(Keys? upKey, Keys? downKey, Keys? leftKey, Keys? rightKey)
    {
        if (upKey != null) { _up = true; _upKey = upKey.Value; }
        if (downKey != null) { _down = true; _downKey = downKey.Value; }
        if (leftKey != null) { _left = true; _leftKey = leftKey.Value; }
        if (rightKey != null) { _right = true; _rightKey = rightKey.Value; }
    }

    public void SetCursorControl(bool allowUp = true, bool allowDown = true, bool allowLeft = true, bool allowRight = true) =>
        SetKeys(allowUp, allowDown, allowLeft, allowRight, Keys.Up, Keys.Down, Keys.Left, Keys.Right);

    public void SetWASDControl(bool allowUp = true, bool allowDown = true, bool allowLeft = true, bool allowRight = true) =>
        SetKeys(allowUp, allowDown, allowLeft, allowRight, Keys.W, Keys.S, Keys.A, Keys.D);

    public void SetESDFControl(bool allowUp = true, bool allowDown = true, bool allowLeft = true, bool allowRight = true) =>
        SetKeys(allowUp, allowDown, allowLeft, allowRight, Keys.E, Keys.D, Keys.S, Keys.F);

    public void SetIJKLControl(bool allowUp = true, bool allowDown = true, bool allowLeft = true, bool allowRight = true) =>
        SetKeys(allowUp, allowDown, allowLeft, allowRight, Keys.I, Keys.K, Keys.J, Keys.L);

    public void SetHJKLControl(bool allowUp = true, bool allowDown = true, bool allowLeft = true, bool allowRight = true) =>
        SetKeys(allowUp, allowDown, allowLeft, allowRight, Keys.K, Keys.J, Keys.H, Keys.L);

    public void SetZQSDControl(bool allowUp = true, bool allowDown = true, bool allowLeft = true, bool allowRight = true) =>
        SetKeys(allowUp, allowDown, allowLeft, allowRight, Keys.Z, Keys.S, Keys.Q, Keys.D);

    public void SetDvorakSimplifiedControl(bool allowUp = true, bool allowDown = true, bool allowLeft = true, bool allowRight = true) =>
        SetKeys(allowUp, allowDown, allowLeft, allowRight, Keys.OemComma, Keys.O, Keys.A, Keys.E);

    public void SetNumpadControl(bool allowUp = true, bool allowDown = true, bool allowLeft = true, bool allowRight = true) =>
        SetKeys(allowUp, allowDown, allowLeft, allowRight, Keys.NumPad8, Keys.NumPad2, Keys.NumPad4, Keys.NumPad6);

    // ------------------------------------------------------------ moving

    static bool Down(Keys k) => FlxG.Keys.Pressed(k);

    bool MoveUp()
    {
        if (!Down(_upKey)) return false;
        IsPressedUp = true;
        if (_yFacing) _entity.Facing = FlxDirection.Up;
        if (_movement == FlxControlMovement.Instant) _entity.Velocity.Y = _upSpeed;
        else _entity.Acceleration.Y = _upSpeed;
        if (_bounds is { } b && _entity.Y < b.Top) _entity.Y = b.Top;
        return true;
    }

    bool MoveDown()
    {
        if (!Down(_downKey)) return false;
        IsPressedDown = true;
        if (_yFacing) _entity.Facing = FlxDirection.Down;
        if (_movement == FlxControlMovement.Instant) _entity.Velocity.Y = _downSpeed;
        else _entity.Acceleration.Y = _downSpeed;
        if (_bounds is { } b && _entity.Y > b.Bottom) _entity.Y = b.Bottom;
        return true;
    }

    bool MoveLeft()
    {
        if (!Down(_leftKey)) return false;
        IsPressedLeft = true;
        if (_xFacing) _entity.Facing = FlxDirection.Left;
        if (_movement == FlxControlMovement.Instant) _entity.Velocity.X = _leftSpeed;
        else _entity.Acceleration.X = _leftSpeed;
        if (_bounds is { } b && _entity.X < b.X) _entity.X = b.X;
        return true;
    }

    bool MoveRight()
    {
        if (!Down(_rightKey)) return false;
        IsPressedRight = true;
        if (_xFacing) _entity.Facing = FlxDirection.Right;
        if (_movement == FlxControlMovement.Instant) _entity.Velocity.X = _rightSpeed;
        else _entity.Acceleration.X = _rightSpeed;
        if (_bounds is { } b && _entity.X > b.Right) _entity.X = b.Right;
        return true;
    }

    bool Turn(Keys key, float speed)
    {
        if (!Down(key)) return false;
        if (_rotation == FlxControlRotation.Instant) _entity.AngularVelocity = speed;
        else _entity.AngularAcceleration = speed;
        return true;
    }

    bool Thrust(Keys key, float speed, int sign, FlxSound? sound)
    {
        if (!Down(key)) return false;
        var motion = FlxVelocity.VelocityFromAngle(_entity.Angle, speed) * sign;
        if (_movement == FlxControlMovement.Instant) _entity.Velocity = motion;
        else _entity.Acceleration = motion;
        if (_bounds is { } b && _entity.X < b.X) _entity.X = b.X;
        sound?.Play(false);
        return true;
    }

    bool KeyActive(Keys key, FlxKeyMode mode) => mode switch
    {
        FlxKeyMode.Pressed => FlxG.Keys.Pressed(key),
        FlxKeyMode.JustDown => FlxG.Keys.JustPressed(key),
        _ => FlxG.Keys.JustReleased(key),
    };

    bool RunFire()
    {
        if (!KeyActive(_fireKey, _fireMode)) return false;
        if (_fireRate > 0)
        {
            if (Now <= _nextFireTime) return false;
            _nextFireTime = Now + _fireRate;
        }
        _fireCallback?.Invoke();
        _fireSound?.Play(true);
        return true;
    }

    bool RunJump()
    {
        bool onSurface = _entity.IsTouching(_jumpSurface);
        if (onSurface) _extraSurfaceTime = Now + _jumpFromFallTime;

        if (!KeyActive(_jumpKey, _jumpMode)) return false;

        if (!onSurface)
        {
            // walked off an edge a moment ago: still allowed, but only once
            if (Now > _extraSurfaceTime) return false;
            if (_lastJumpTime > _extraSurfaceTime - _jumpFromFallTime) return false;
        }
        if (Now < _nextJumpTime) return false;

        _entity.Velocity.Y = _gravityY > 0 ? -_jumpHeight : _jumpHeight;
        _jumpCallback?.Invoke();
        _lastJumpTime = Now;
        _nextJumpTime = _lastJumpTime + _jumpRate;
        _jumpSound?.Play(true);
        return true;
    }

    /// <summary>Reads the keys and moves the sprite. <see cref="FlxControl"/> calls it every frame.</summary>
    public void Update(float elapsed)
    {
        IsPressedUp = IsPressedDown = IsPressedLeft = IsPressedRight = false;

        // no key held: stop, or let drag and gravity take over
        if (_stopping == FlxControlStopping.Instant)
        {
            if (_movement == FlxControlMovement.Instant) _entity.Velocity = Vector2.Zero;
            else _entity.Acceleration = Vector2.Zero;
        }
        else if (_stopping == FlxControlStopping.Decelerates)
        {
            if (_movement == FlxControlMovement.Instant) _entity.Velocity = Vector2.Zero;
            else _entity.Acceleration = new Vector2(_gravityX, _gravityY);
        }

        if (_isRotating)
        {
            if (_rotationStopping == FlxControlRotationStopping.Instant)
            {
                if (_rotation == FlxControlRotation.Instant) _entity.AngularVelocity = 0;
                else _entity.AngularAcceleration = 0;
            }
            else if (_rotationStopping == FlxControlRotationStopping.Decelerates && _rotation == FlxControlRotation.Instant)
                _entity.AngularVelocity = 0;

            bool anti = Turn(_antiClockwiseKey, _antiClockwiseSpeed);
            bool clock = !anti && Turn(_clockwiseKey, _clockwiseSpeed);

            if (_rotationStopping == FlxControlRotationStopping.Decelerates && _rotation == FlxControlRotation.Accelerates && !anti && !clock)
                _entity.AngularAcceleration = 0;
            if (_rotationStopping == FlxControlRotationStopping.Instant && _rotation == FlxControlRotation.Accelerates && !anti && !clock)
            {
                _entity.AngularVelocity = 0;
                _entity.AngularAcceleration = 0;
            }
        }

        if (_thrustEnabled || _reverseEnabled)
        {
            bool moved = _thrustEnabled && Thrust(_thrustKey, _thrustSpeed, 1, _thrustSound);
            if (!moved && _reverseEnabled) Thrust(_reverseKey, _reverseSpeed, -1, null);
        }
        else
        {
            bool movedX = false, movedY = false;
            if (_up) movedY = MoveUp();
            if (_down && !movedY) MoveDown();
            if (_left) movedX = MoveLeft();
            if (_right && !movedX) MoveRight();
        }

        if (_fire) RunFire();
        if (_jump) RunJump();

        if (_capVelocity)
        {
            var max = _entity.MaxVelocity;
            if (max.X > 0) _entity.Velocity.X = MathHelper.Clamp(_entity.Velocity.X, -max.X, max.X);
            if (max.Y > 0) _entity.Velocity.Y = MathHelper.Clamp(_entity.Velocity.Y, -max.Y, max.Y);
        }

        if (_walkSound != null)
        {
            bool walking = _movement == FlxControlMovement.Instant ? _entity.Velocity.X != 0 : _entity.Acceleration.X != 0;
            if (walking) _walkSound.Play(false);
            else _walkSound.Stop();
        }
    }
}
