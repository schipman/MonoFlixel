using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// A weapon: a pool of bullets that are fired from an object (or a place) in a direction, at the mouse, at a position or at a target.
/// Port of Richard Davey's (Photon Storm) Flixel Power Tools <c>FlxWeapon</c> and its <c>Bullet</c> (here <see cref="FlxBullet"/>).
/// <code>
/// var gun = new FlxWeapon("gun", player);
/// gun.MakePixelBullet(40, 2, 2, Color.Yellow);
/// gun.BulletSpeed = 300;
/// gun.FireRate = 100;                 // milliseconds between two shots
/// gun.BulletLifeSpan = 1500;          // milliseconds
/// gun.RandomAngle = 3;                // a little spread
/// Add(gun.Group);                     // the bullets are an ordinary group: add it, collide it, overlap it
/// ...
/// if (FlxG.Mouse.Pressed) gun.FireFromAngle(player.Angle);
/// </code>
/// Make the bullets first (<see cref="MakePixelBullet"/>, <see cref="MakeImageBullet(int, string, Vector2, bool, int, int)"/> or
/// <see cref="MakeAnimatedBullet(int, string, int, int, int[], float, bool, Vector2)"/>), then fire. Every Fire method returns false
/// when it did not shoot: the weapon is still reloading (<see cref="FireRate"/>) or all bullets are in the air.
/// Times (<see cref="FireRate"/>, <see cref="BulletLifeSpan"/>) are in milliseconds, as in the original, and run on the real clock
/// (<see cref="FlxG.RealTime"/>), so they ignore <see cref="FlxG.TimeScale"/>.
/// Differences from the original: the parent is an object instead of two variable names, <see cref="CenterOnParent"/> is new, bullets are
/// revived with <c>Revive()</c>, and the magazine fields that the original declared but never used are left out.
/// </summary>
public class FlxWeapon
{
    public const int BulletUp = -90, BulletDown = 90, BulletLeft = 180, BulletRight = 0;
    public const int BulletNorthEast = -45, BulletNorthWest = -135, BulletSouthEast = 45, BulletSouthWest = 135;

    public string Name;

    /// <summary>The bullets. Add it to a state; collide or overlap it with what the bullets should hit.</summary>
    public FlxTypedGroup<FlxBullet> Group { get; private set; } = new();

    /// <summary>Bullets that leave this rectangle (by their top-left corner) are killed. Defaults to the screen: (0, 0, FlxG.Width, FlxG.Height).</summary>
    public Rectangle Bounds;

    /// <summary>Pixels per second. For accelerating bullets (<see cref="SetBulletAcceleration"/>) it is the acceleration of a bullet fired at a target.</summary>
    public float BulletSpeed;

    /// <summary>Milliseconds that must pass between two shots. 0 = no limit.</summary>
    public float FireRate;

    /// <summary>Milliseconds a bullet lives. 0 = until it leaves <see cref="Bounds"/> or is killed.</summary>
    public float BulletLifeSpan;

    /// <summary>0 = bullets do not bounce. Anything above sets the bullets' Elasticity.</summary>
    public float BulletElasticity;

    /// <summary>Each bullet's angle is off by a random amount up to this many degrees either way.</summary>
    public int RandomAngle;

    /// <summary>Each bullet's speed is off by up to this much either way.</summary>
    public int RandomSpeed;

    /// <summary>Each bullet's start position is off by up to this much either way, per axis.</summary>
    public Vector2 RandomPosition;

    /// <summary>Each bullet's life span is off by up to this many milliseconds either way.</summary>
    public int RandomLifeSpan;

    /// <summary>
    /// New in this port. When firing from a parent, put the middle of the bullet on the middle of the parent. Without it the bullet's
    /// top-left corner starts at the parent's top-left corner (plus the offset), as in the original.
    /// </summary>
    public bool CenterOnParent;

    /// <summary>Called just before a shot, a shot's bullet takes off, and just after. Sounds are played at the same moments.</summary>
    public Action? OnPreFire, OnFire, OnPostFire;
    public FlxSound? OnPreFireSound, OnFireSound, OnPostFireSound;

    /// <summary>How many shots were fired so far.</summary>
    public int BulletsFired { get; private set; }

    /// <summary>The bullet fired last.</summary>
    public FlxBullet? CurrentBullet { get; private set; }

    /// <summary>The object the bullets leave from (see <see cref="SetParent"/>), if any.</summary>
    public FlxObject? Parent { get; private set; }

    Vector2 _offset, _direction, _firePosition;
    bool _fireFromParent, _fireFromPosition, _directionFromParent;
    double _nextFire;

    enum Kind { Fire, AtMouse, AtPosition, AtTarget, FromAngle, FromParentAngle }

    public FlxWeapon(string name, FlxObject? parent = null)
    {
        Name = name;
        Bounds = new Rectangle(0, 0, FlxG.Width, FlxG.Height);
        if (parent != null) SetParent(parent);
    }

    // ------------------------------------------------------------ making the bullets

    /// <summary>Makes <paramref name="quantity"/> square bullets of one color.</summary>
    public void MakePixelBullet(int quantity, int width = 2, int height = 2, Color? color = null, Vector2 offset = default)
    {
        NewGroup(quantity, _ => new FlxBullet(this).MakeBulletGraphic(width, height, color ?? Color.White), offset);
    }

    /// <summary>
    /// Makes bullets from a picture. With <paramref name="rotate"/> the picture is baked at <paramref name="rotations"/> angles, so a
    /// bullet that turns (AngularVelocity) or flies in a direction (Angle) is drawn pointing the right way; <paramref name="frame"/>
    /// picks one frame of a sheet of square frames (-1 = the whole picture).
    /// </summary>
    public void MakeImageBullet(int quantity, string imagePath, Vector2 offset = default, bool rotate = false, int rotations = 16, int frame = -1) =>
        MakeImageBullet(quantity, FlxAssets.GetTexture(imagePath), offset, rotate, rotations, frame);

    public void MakeImageBullet(int quantity, Texture2D image, Vector2 offset = default, bool rotate = false, int rotations = 16, int frame = -1)
    {
        NewGroup(quantity, _ =>
        {
            var b = new FlxBullet(this);
            if (rotate) b.LoadRotatedGraphic(image, rotations, frame);
            else b.LoadGraphic(image);
            return b;
        }, offset);
    }

    /// <summary>Makes bullets that play an animation ("fire", with these frames) each time they are fired.</summary>
    public void MakeAnimatedBullet(int quantity, string imagePath, int frameWidth, int frameHeight, int[] frames, float fps, bool looped, Vector2 offset = default) =>
        MakeAnimatedBullet(quantity, FlxAssets.GetTexture(imagePath), frameWidth, frameHeight, frames, fps, looped, offset);

    public void MakeAnimatedBullet(int quantity, Texture2D image, int frameWidth, int frameHeight, int[] frames, float fps, bool looped, Vector2 offset = default)
    {
        NewGroup(quantity, _ =>
        {
            var b = new FlxBullet(this);
            b.LoadGraphic(image, true, frameWidth, frameHeight);
            b.Animation.Add("fire", frames, fps, looped);
            b.MarkAnimated();
            return b;
        }, offset);
    }

    void NewGroup(int quantity, Func<int, FlxBullet> make, Vector2 offset)
    {
        Group = new FlxTypedGroup<FlxBullet>(quantity);
        for (int i = 0; i < quantity; i++)
        {
            var b = make(i);
            b.Id = i;
            Group.Add(b);
        }
        _offset = offset;
    }

    // ------------------------------------------------------------ where from, which way

    /// <summary>Fire from this object's position (plus an offset). With <paramref name="directionFromParent"/>, <see cref="Fire"/> shoots the way a sprite parent faces.</summary>
    public void SetParent(FlxObject parent, Vector2 offset = default, bool directionFromParent = false)
    {
        _fireFromParent = true;
        _fireFromPosition = false;
        Parent = parent;
        _offset = offset;
        _directionFromParent = directionFromParent;
    }

    /// <summary>Fire from a fixed place instead of an object.</summary>
    public void SetFiringPosition(float x, float y, Vector2 offset = default)
    {
        _fireFromPosition = true;
        _fireFromParent = false;
        _firePosition = new Vector2(x, y);
        _offset = offset;
    }

    /// <summary>Where <see cref="Fire"/> sends the bullets: an angle in degrees (0 = right, -90 = up) and a speed.</summary>
    public void SetBulletDirection(float angle, float speed) => _direction = FlxVelocity.VelocityFromAngle(angle, speed);

    /// <summary>The bullets are pulled by this much (x, y) all the time, like gravity.</summary>
    public void SetBulletGravity(float x, float y)
    {
        foreach (var b in Group.Members) b.Acceleration = new Vector2(x, y);
    }

    /// <summary>
    /// Bullets accelerate instead of flying at a fixed speed: they start still and speed up towards where they were fired, up to the maximum.
    /// (0, 0) switches it off again.
    /// </summary>
    public void SetBulletAcceleration(float x, float y, float maxVelocityX, float maxVelocityY)
    {
        bool on = x != 0 || y != 0;
        foreach (var b in Group.Members)
        {
            b.Accelerates = on;
            if (!on) continue;
            b.AccelerationX = x;
            b.AccelerationY = y;
            b.MaxVelocity = new Vector2(maxVelocityX, maxVelocityY);
        }
    }

    // ------------------------------------------------------------ firing

    /// <summary>Fires in the direction set by <see cref="SetBulletDirection"/> (or the way the parent faces).</summary>
    public bool Fire() => Shoot(Kind.Fire, default, null, 0);

    public bool FireAtMouse() => Shoot(Kind.AtMouse, default, null, 0);

    public bool FireAtPosition(float x, float y) => Shoot(Kind.AtPosition, new Vector2(x, y), null, 0);

    public bool FireAtTarget(FlxObject target) => Shoot(Kind.AtTarget, default, target, 0);

    /// <summary>Fires in a direction in degrees: 0 = right, 90 = down, -90 = up (see the Bullet constants).</summary>
    public bool FireFromAngle(float angle) => Shoot(Kind.FromAngle, default, null, angle);

    /// <summary>Fires in the direction the parent object is turned (its Angle).</summary>
    public bool FireFromParentAngle() => Shoot(Kind.FromParentAngle, default, null, 0);

    static double Now => FlxG.RealTime * 1000;

    bool Shoot(Kind kind, Vector2 point, FlxObject? target, float angle)
    {
        if (FireRate > 0 && Now < _nextFire) return false;
        var bullet = Group.Members.Find(b => !b.Exists);
        if (bullet == null) return false;
        CurrentBullet = bullet;

        OnPreFire?.Invoke();
        OnPreFireSound?.Play();

        bullet.Velocity = Vector2.Zero;
        _nextFire = Now + FireRate;

        // where the bullet starts
        Vector2 start = _offset;
        if (_fireFromParent && Parent != null)
        {
            start += CenterOnParent
                ? Parent.Midpoint - new Vector2(bullet.Width, bullet.Height) * 0.5f
                : new Vector2(Parent.X, Parent.Y);
        }
        else if (_fireFromPosition) start += _firePosition;

        if (_directionFromParent && Parent is FlxSprite sprite) _direction = VelocityFromFacing(sprite, BulletSpeed);

        switch (kind)
        {
            case Kind.Fire: bullet.Fire(start.X, start.Y, _direction.X, _direction.Y); break;
            case Kind.AtMouse: bullet.FireAtMouse(start.X, start.Y, BulletSpeed); break;
            case Kind.AtPosition: bullet.FireAtPosition(start.X, start.Y, point.X, point.Y, BulletSpeed); break;
            case Kind.AtTarget: bullet.FireAtTarget(start.X, start.Y, target!, BulletSpeed); break;
            case Kind.FromAngle: bullet.FireFromAngle(start.X, start.Y, angle, BulletSpeed); break;
            case Kind.FromParentAngle: bullet.FireFromAngle(start.X, start.Y, Parent?.Angle ?? 0, BulletSpeed); break;
        }

        OnPostFire?.Invoke();
        OnPostFireSound?.Play();
        BulletsFired++;
        return true;
    }

    static Vector2 VelocityFromFacing(FlxSprite s, float speed)
    {
        float degrees = s.Facing switch
        {
            FlxDirection.Left => 180,
            FlxDirection.Up => -90,
            FlxDirection.Down => 90,
            _ => 0,
        };
        return FlxVelocity.VelocityFromAngle(degrees, speed);
    }

    public void Destroy()
    {
        Group.Destroy();
        Parent = null;
        OnPreFire = OnFire = OnPostFire = null;
    }
}

/// <summary>
/// One bullet of an <see cref="FlxWeapon"/> (Flixel Power Tools' <c>Bullet</c>). It starts dead (<c>Exists = false</c>); the weapon
/// launches it. It kills itself when its life span is over or it leaves the weapon's <see cref="FlxWeapon.Bounds"/>. It is an ordinary
/// sprite, so you can change anything about each bullet after making them (tint, AngularVelocity, Drag, ...).
/// </summary>
public class FlxBullet : FlxSprite
{
    public readonly FlxWeapon Weapon;

    /// <summary>Position in the weapon's pool.</summary>
    public int Id;

    /// <summary>Speeds up (towards where it was fired) instead of flying at a fixed speed.</summary>
    public bool Accelerates;
    public float AccelerationX, AccelerationY;

    /// <summary>How long this bullet lives, in milliseconds (0 = no limit), and when it started and ends, on the FlxG.RealTime clock.</summary>
    public float LifeSpan;
    public double LaunchTime, ExpiresTime;

    bool _animated;

    public FlxBullet(FlxWeapon weapon) : base(0, 0)
    {
        Weapon = weapon;
        Exists = false;
    }

    internal FlxBullet MakeBulletGraphic(int width, int height, Color color)
    {
        MakeGraphic(width, height, color);
        return this;
    }

    internal void MarkAnimated() => _animated = true;

    static int Rand(float range) => range == 0 ? 0 : FlxG.Random.Int(-(int)range, (int)range);

    void MoveTo(float x, float y)
    {
        X = x + Rand(Weapon.RandomPosition.X);
        Y = y + Rand(Weapon.RandomPosition.Y);
    }

    float RandomSpeed(float speed) => speed + Rand(Weapon.RandomSpeed);

    public void Fire(float x, float y, float velocityX, float velocityY)
    {
        MoveTo(x, y);
        if (Accelerates) Acceleration = new Vector2(AccelerationX + Rand(Weapon.RandomSpeed), AccelerationY + Rand(Weapon.RandomSpeed));
        else Velocity = new Vector2(velocityX + Rand(Weapon.RandomSpeed), velocityY + Rand(Weapon.RandomSpeed));
        PostFire();
    }

    public void FireAtMouse(float x, float y, float speed)
    {
        MoveTo(x, y);
        if (Accelerates) FlxVelocity.AccelerateTowardsMouse(this, RandomSpeed(speed), MaxVelocity.X, MaxVelocity.Y);
        else FlxVelocity.MoveTowardsMouse(this, RandomSpeed(speed));
        PostFire();
    }

    public void FireAtPosition(float x, float y, float targetX, float targetY, float speed)
    {
        MoveTo(x, y);
        if (Accelerates) FlxVelocity.AccelerateTowardsPoint(this, new Vector2(targetX, targetY), RandomSpeed(speed), MaxVelocity.X, MaxVelocity.Y);
        else FlxVelocity.MoveTowardsPoint(this, new Vector2(targetX, targetY), RandomSpeed(speed));
        PostFire();
    }

    public void FireAtTarget(float x, float y, FlxObject target, float speed)
    {
        MoveTo(x, y);
        if (Accelerates) FlxVelocity.AccelerateTowardsObject(this, target, RandomSpeed(speed), MaxVelocity.X, MaxVelocity.Y);
        else FlxVelocity.MoveTowardsObject(this, target, RandomSpeed(speed));
        PostFire();
    }

    public void FireFromAngle(float x, float y, float angle, float speed)
    {
        MoveTo(x, y);
        var v = FlxVelocity.VelocityFromAngle(angle + Rand(Weapon.RandomAngle), RandomSpeed(speed));
        if (Accelerates) Acceleration = v;
        else Velocity = v;
        PostFire();
    }

    void PostFire()
    {
        if (_animated) Animation.Play("fire", true);
        if (Weapon.BulletElasticity > 0) Elasticity = Weapon.BulletElasticity;
        Revive();
        LaunchTime = FlxG.RealTime * 1000;
        if (Weapon.BulletLifeSpan > 0)
        {
            LifeSpan = Weapon.BulletLifeSpan + Rand(Weapon.RandomLifeSpan);
            ExpiresTime = LaunchTime + LifeSpan;
        }
        else LifeSpan = 0;
        Weapon.OnFire?.Invoke();
        Weapon.OnFireSound?.Play();
    }

    public override void Update(float elapsed)
    {
        if (LifeSpan > 0 && FlxG.RealTime * 1000 > ExpiresTime) Kill();
        var b = Weapon.Bounds;
        if (X < b.Left || X > b.Right || Y < b.Top || Y > b.Bottom) Kill();
        base.Update(elapsed);
    }
}
