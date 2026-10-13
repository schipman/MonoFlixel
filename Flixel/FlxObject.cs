using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

[Flags]
public enum FlxDirection
{
    None = 0, Left = 1, Right = 2, Up = 4, Down = 8,
    Wall = Left | Right, Ceiling = Up, Floor = Down, Any = Left | Right | Up | Down
}

/// <summary>A rectangle with motion + collision. Port of the core of FlxObject.</summary>
public class FlxObject : FlxBasic
{
    public const float OverlapBias = 4f;

    public float X, Y, Width, Height;
    public Vector2 Last;                       // position at the start of the frame
    public Vector2 Velocity, Acceleration, Drag, MaxVelocity;
    public float Angle, AngularVelocity, AngularAcceleration, AngularDrag, MaxAngular;
    public bool Moves = true, Immovable;
    public float Mass = 1f, Elasticity;
    public FlxDirection Touching, WasTouching, AllowCollisions = FlxDirection.Any;
    public Vector2 ScrollFactor = Vector2.One;

    /// <summary>The path this object is following, if any (see FollowPath).</summary>
    public FlxPath? Path;

    public FlxObject(float x = 0, float y = 0, float width = 0, float height = 0)
    {
        X = x; Y = y; Width = width; Height = height;
        Last = new Vector2(x, y);
    }

    /// <summary>Like Flixel: solid == (AllowCollisions != None). Non-solid objects are ignored by Collide/Overlap.</summary>
    public bool Solid
    {
        get => (AllowCollisions & FlxDirection.Any) != 0;
        set => AllowCollisions = value ? FlxDirection.Any : FlxDirection.None;
    }

    /// <summary>
    /// Makes the object's middle travel through the points at the given speed (it sets Velocity each frame, so the
    /// object needs Moves = true). Replaces any path it was following.
    /// </summary>
    public FlxPath FollowPath(IEnumerable<Vector2> nodes, float speed = 100f, FlxPathMode mode = FlxPathMode.Forward, bool autoRotate = false)
    {
        Path ??= new FlxPath();
        Path.Object = this;
        return Path.Start(nodes, speed, mode, autoRotate);
    }

    /// <summary>Stops following the current path.</summary>
    public void StopFollowingPath() => Path?.Cancel();

    public Vector2 Midpoint => new(X + Width * 0.5f, Y + Height * 0.5f);

    /// <summary>True if the object overlaps the camera's view (main camera by default).</summary>
    public bool IsOnScreen(FlxCamera? cam = null)
    {
        cam ??= FlxG.Cameras.Main;
        float sx = X - cam.Scroll.X * ScrollFactor.X;
        float sy = Y - cam.Scroll.Y * ScrollFactor.Y;
        return sx + Width > 0 && sx < cam.ViewWidth && sy + Height > 0 && sy < cam.ViewHeight;
    }

    public override void Destroy()
    {
        Path = null;
        base.Destroy();
    }

    public bool IsTouching(FlxDirection d) => (Touching & d) != 0;
    public bool JustTouched(FlxDirection d) => (Touching & d) != 0 && (WasTouching & d) == 0;

    public void Reset(float x, float y)
    {
        X = x; Y = y; Last = new Vector2(x, y);
        Velocity = Vector2.Zero;
        Touching = WasTouching = FlxDirection.None;
        Revive();
    }

    /// <summary>
    /// Same order as Flixel: remember last position, move, then reset touching.
    /// So read Touching/IsTouching() BEFORE calling base.Update() in your own Update().
    /// </summary>
    public override void Update(float elapsed)
    {
        Last = new Vector2(X, Y);
        if (Path is { Active: true }) Path.UpdatePath(elapsed);     // aims Velocity at the next node
        if (Moves) UpdateMotion(elapsed);
        WasTouching = Touching;
        Touching = FlxDirection.None;
    }

    protected virtual void UpdateMotion(float dt)
    {
        float avd = 0.5f * (ComputeVelocity(AngularVelocity, AngularAcceleration, AngularDrag, MaxAngular, dt) - AngularVelocity);
        AngularVelocity += avd;
        Angle += AngularVelocity * dt;
        AngularVelocity += avd;

        float vd = 0.5f * (ComputeVelocity(Velocity.X, Acceleration.X, Drag.X, MaxVelocity.X, dt) - Velocity.X);
        Velocity.X += vd;
        X += Velocity.X * dt;
        Velocity.X += vd;

        vd = 0.5f * (ComputeVelocity(Velocity.Y, Acceleration.Y, Drag.Y, MaxVelocity.Y, dt) - Velocity.Y);
        Velocity.Y += vd;
        Y += Velocity.Y * dt;
        Velocity.Y += vd;
    }

    public static float ComputeVelocity(float v, float accel, float drag, float max, float dt)
    {
        if (accel != 0)
            v += accel * dt;
        else if (drag != 0)
        {
            float d = drag * dt;
            if (v - d > 0) v -= d;
            else if (v + d < 0) v += d;
            else v = 0;
        }
        if (v != 0 && max != 0) v = MathHelper.Clamp(v, -max, max);
        return v;
    }

    public virtual bool Overlaps(FlxObject o) =>
        X + Width > o.X && X < o.X + o.Width && Y + Height > o.Y && Y < o.Y + o.Height;

    /// <summary>Overlap test that understands tilemaps (either argument may be one).</summary>
    public static bool OverlapsPair(FlxObject a, FlxObject b)
    {
        if (a is FlxTilemap ta) return b is not FlxTilemap && ta.Overlaps(b);
        if (b is FlxTilemap tb) return tb.Overlaps(a);
        return a.Overlaps(b);
    }

    // ---------------------------------------------------------------- separation

    public static bool Separate(FlxObject a, FlxObject b)
    {
        if (a is FlxTilemap ta) return b is not FlxTilemap && ta.SeparateObject(b);
        if (b is FlxTilemap tb) return tb.SeparateObject(a);
        if (!a.Solid || !b.Solid) return false;
        bool sx = SeparateX(a, b);
        bool sy = SeparateY(a, b);
        return sx || sy;
    }

    static bool SeparateX(FlxObject a, FlxObject b)
    {
        bool ai = a.Immovable, bi = b.Immovable;
        if (ai && bi) return false;

        float overlap = ComputeOverlapX(a, b);
        if (overlap == 0) return false;

        float av = a.Velocity.X, bv = b.Velocity.X;
        if (!ai && !bi)
        {
            overlap *= 0.5f;
            a.X -= overlap; b.X += overlap;
            float an = MathF.Sqrt(bv * bv * b.Mass / a.Mass) * MathF.Sign(bv);
            float bn = MathF.Sqrt(av * av * a.Mass / b.Mass) * MathF.Sign(av);
            float avg = (an + bn) * 0.5f;
            an -= avg; bn -= avg;
            a.Velocity.X = avg + an * a.Elasticity;
            b.Velocity.X = avg + bn * b.Elasticity;
        }
        else if (!ai) { a.X -= overlap; a.Velocity.X = bv - av * a.Elasticity; }
        else { b.X += overlap; b.Velocity.X = av - bv * b.Elasticity; }
        return true;
    }

    static bool SeparateY(FlxObject a, FlxObject b)
    {
        bool ai = a.Immovable, bi = b.Immovable;
        if (ai && bi) return false;

        float d1 = a.Y - a.Last.Y, d2 = b.Y - b.Last.Y;
        float overlap = ComputeOverlapY(a, b);
        if (overlap == 0) return false;

        float av = a.Velocity.Y, bv = b.Velocity.Y;
        if (!ai && !bi)
        {
            overlap *= 0.5f;
            a.Y -= overlap; b.Y += overlap;
            float an = MathF.Sqrt(bv * bv * b.Mass / a.Mass) * MathF.Sign(bv);
            float bn = MathF.Sqrt(av * av * a.Mass / b.Mass) * MathF.Sign(av);
            float avg = (an + bn) * 0.5f;
            an -= avg; bn -= avg;
            a.Velocity.Y = avg + an * a.Elasticity;
            b.Velocity.Y = avg + bn * b.Elasticity;
        }
        else if (!ai)
        {
            a.Y -= overlap; a.Velocity.Y = bv - av * a.Elasticity;
            // riding a horizontally moving platform
            if (b.Active && b.Moves && d1 > d2) a.X += b.X - b.Last.X;
        }
        else
        {
            b.Y += overlap; b.Velocity.Y = av - bv * b.Elasticity;
            if (a.Active && a.Moves && d1 < d2) b.X += a.X - a.Last.X;
        }
        return true;
    }

    static bool Allows(FlxObject o, FlxDirection d) => (o.AllowCollisions & d) != 0;

    static float ComputeOverlapX(FlxObject a, FlxObject b)
    {
        float overlap = 0;
        float d1 = a.X - a.Last.X, d2 = b.X - b.Last.X;
        if (d1 == d2) return 0;

        float ad1 = MathF.Abs(d1), ad2 = MathF.Abs(d2);
        float r1x = a.X - (d1 > 0 ? d1 : 0), r1w = a.Width + ad1, r1y = a.Last.Y, r1h = a.Height;
        float r2x = b.X - (d2 > 0 ? d2 : 0), r2w = b.Width + ad2, r2y = b.Last.Y, r2h = b.Height;

        if (r1x + r1w > r2x && r1x < r2x + r2w && r1y + r1h > r2y && r1y < r2y + r2h)
        {
            float max = ad1 + ad2 + OverlapBias;
            if (d1 > d2)
            {
                overlap = a.X + a.Width - b.X;
                if (overlap > max || !Allows(a, FlxDirection.Right) || !Allows(b, FlxDirection.Left)) overlap = 0;
                else { a.Touching |= FlxDirection.Right; b.Touching |= FlxDirection.Left; }
            }
            else
            {
                overlap = a.X - b.Width - b.X;
                if (-overlap > max || !Allows(a, FlxDirection.Left) || !Allows(b, FlxDirection.Right)) overlap = 0;
                else { a.Touching |= FlxDirection.Left; b.Touching |= FlxDirection.Right; }
            }
        }
        return overlap;
    }

    static float ComputeOverlapY(FlxObject a, FlxObject b)
    {
        float overlap = 0;
        float d1 = a.Y - a.Last.Y, d2 = b.Y - b.Last.Y;
        if (d1 == d2) return 0;

        float ad1 = MathF.Abs(d1), ad2 = MathF.Abs(d2);
        float r1x = a.X, r1w = a.Width, r1y = a.Y - (d1 > 0 ? d1 : 0), r1h = a.Height + ad1;
        float r2x = b.X, r2w = b.Width, r2y = b.Y - (d2 > 0 ? d2 : 0), r2h = b.Height + ad2;

        if (r1x + r1w > r2x && r1x < r2x + r2w && r1y + r1h > r2y && r1y < r2y + r2h)
        {
            float max = ad1 + ad2 + OverlapBias;
            if (d1 > d2)
            {
                overlap = a.Y + a.Height - b.Y;
                if (overlap > max || !Allows(a, FlxDirection.Down) || !Allows(b, FlxDirection.Up)) overlap = 0;
                else { a.Touching |= FlxDirection.Down; b.Touching |= FlxDirection.Up; }
            }
            else
            {
                overlap = a.Y - b.Height - b.Y;
                if (-overlap > max || !Allows(a, FlxDirection.Up) || !Allows(b, FlxDirection.Down)) overlap = 0;
                else { a.Touching |= FlxDirection.Up; b.Touching |= FlxDirection.Down; }
            }
        }
        return overlap;
    }
}
