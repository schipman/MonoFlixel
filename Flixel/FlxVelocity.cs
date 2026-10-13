using System;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>Helpers that set an object's velocity or acceleration towards something. Port of Flixel's FlxVelocity.</summary>
public static class FlxVelocity
{
    /// <summary>
    /// Sets the velocity of <paramref name="source"/> so it moves towards the middle of <paramref name="target"/> at the given speed.
    /// With maxTimeMs &gt; 0 the speed is raised if needed so it arrives in that time. Call once, or every frame to home in.
    /// </summary>
    public static void MoveTowardsObject(FlxObject source, FlxObject target, float speed = 60f, int maxTimeMs = 0) =>
        MoveTowardsPoint(source, target.Midpoint, speed, maxTimeMs);

    public static void MoveTowardsPoint(FlxObject source, Vector2 target, float speed = 60f, int maxTimeMs = 0)
    {
        var from = source.Midpoint;
        float dx = target.X - from.X, dy = target.Y - from.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);

        if (distance <= 0.0001f)
        {
            source.Velocity = Vector2.Zero;
            return;
        }

        if (maxTimeMs > 0)
        {
            // fastest of: the wanted speed, or the speed needed to arrive in time
            float needed = distance / (maxTimeMs / 1000f);
            if (needed > speed) speed = needed;
        }

        source.Velocity = new Vector2(dx / distance * speed, dy / distance * speed);
    }

    /// <summary>Moves towards the mouse (world coordinates).</summary>
    public static void MoveTowardsMouse(FlxObject source, float speed = 60f, int maxTimeMs = 0) =>
        MoveTowardsPoint(source, FlxG.Mouse.WorldPosition, speed, maxTimeMs);

    /// <summary>
    /// Sets the acceleration of <paramref name="source"/> towards the target and caps its velocity.
    /// A maxSpeedY of 0 uses the same cap as X. Call every frame.
    /// </summary>
    public static void AccelerateTowardsObject(FlxObject source, FlxObject target, float acceleration, float maxSpeedX = 0, float maxSpeedY = 0) =>
        AccelerateTowardsPoint(source, target.Midpoint, acceleration, maxSpeedX, maxSpeedY);

    public static void AccelerateTowardsPoint(FlxObject source, Vector2 target, float acceleration, float maxSpeedX = 0, float maxSpeedY = 0)
    {
        var from = source.Midpoint;
        float angle = MathF.Atan2(target.Y - from.Y, target.X - from.X);

        source.Acceleration = new Vector2(MathF.Cos(angle) * acceleration, MathF.Sin(angle) * acceleration);
        source.MaxVelocity = new Vector2(maxSpeedX, maxSpeedY == 0 ? maxSpeedX : maxSpeedY);
    }

    public static void AccelerateTowardsMouse(FlxObject source, float acceleration, float maxSpeedX = 0, float maxSpeedY = 0) =>
        AccelerateTowardsPoint(source, FlxG.Mouse.WorldPosition, acceleration, maxSpeedX, maxSpeedY);

    /// <summary>A velocity of the given speed in the given direction (degrees, 0 = right, 90 = down).</summary>
    public static Vector2 VelocityFromAngle(float degrees, float speed) => FlxAngle.Polar(speed, degrees);

    /// <summary>Distance between the middles of two objects.</summary>
    public static float DistanceBetween(FlxObject a, FlxObject b) => Vector2.Distance(a.Midpoint, b.Midpoint);

    /// <summary>Distance from the middle of an object to a point.</summary>
    public static float DistanceToPoint(FlxObject a, Vector2 point) => Vector2.Distance(a.Midpoint, point);

    /// <summary>Distance from the middle of an object to the mouse (world coordinates).</summary>
    public static float DistanceToMouse(FlxObject a) => Vector2.Distance(a.Midpoint, FlxG.Mouse.WorldPosition);
}
