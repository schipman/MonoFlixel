using System;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>A min/max pair, like Flixel's FlxBounds.</summary>
public sealed class FlxRange
{
    public float Min, Max;
    public void Set(float min, float? max = null) { Min = min; Max = max ?? min; }
}

/// <summary>A min/max pair of 2D vectors, like Flixel's FlxPointRangeBounds (start values only).</summary>
public sealed class FlxVecRange
{
    public Vector2 Min, Max;

    public void Set(float minX, float minY, float? maxX = null, float? maxY = null)
    {
        Min = new Vector2(minX, minY);
        Max = new Vector2(maxX ?? minX, maxY ?? minY);
    }
}

public class FlxParticle : FlxSprite
{
    public float Lifespan;     // seconds, 0 = forever
    public float Age;

    public FlxParticle() { Exists = false; }

    public override void Update(float elapsed)
    {
        if (Age < Lifespan) Age += elapsed;
        if (Lifespan != 0 && Age >= Lifespan) { Kill(); return; }
        base.Update(elapsed);
    }
}

/// <summary>
/// A pool of particle sprites you fire off: Start(explode: true, quantity: 20) for a burst,
/// Start(explode: false, frequency: 0.01f) for a stream. Particles are ordinary FlxObjects, so they
/// can collide with tilemaps (add the emitter to a group you pass to FlxG.Collide).
/// Like Flixel, an emitter does nothing (and is skipped by collisions) until Start() is called.
/// </summary>
public class FlxEmitter : FlxTypedGroup<FlxParticle>
{
    public float X, Y, Width, Height;     // the area new particles appear in

    public readonly FlxVecRange Velocity = new();
    public readonly FlxVecRange Acceleration = new();
    public readonly FlxRange AngularVelocity = new();
    public readonly FlxRange Lifespan = new();
    public readonly FlxRange Elasticity = new();

    public bool Emitting;

    bool _explode;
    float _frequency, _timer;
    int _quantity, _counter;

    public FlxEmitter(float x = 0, float y = 0, int maxSize = 0) : base(maxSize)
    {
        X = x; Y = y;
        Velocity.Set(-100, -100, 100, 100);
        Lifespan.Set(3);
        Exists = false;
    }

    /// <summary>
    /// Creates the particle pool from an image. With multiple=true the image is a strip of square frames
    /// and each particle gets a random one. (bakedRotationAngles exists for API parity and is ignored:
    /// rotation is done by the renderer.)
    /// </summary>
    public FlxEmitter LoadParticles(string imagePath, int quantity = 50, int bakedRotationAngles = 16, bool multiple = false)
    {
        MaxSize = quantity;
        var tex = FlxAssets.GetTexture(imagePath);
        int frames = multiple ? Math.Max(1, tex.Width / Math.Max(1, tex.Height)) : 1;

        for (int i = 0; i < quantity; i++)
        {
            var p = new FlxParticle();
            p.LoadGraphic(tex, multiple);
            if (multiple) p.Frame = FlxG.Random.Int(0, frames - 1);
            Add(p);
        }
        return this;
    }

    public FlxEmitter Start(bool explode = true, float frequency = 0.1f, int quantity = 0)
    {
        Exists = Visible = Active = Emitting = true;
        _explode = explode; _frequency = frequency; _quantity = quantity;
        _counter = 0; _timer = 0;
        return this;
    }

    /// <summary>Center the emission area on an object.</summary>
    public void FocusOn(FlxObject o)
    {
        var m = o.Midpoint;
        X = m.X - Width * 0.5f;
        Y = m.Y - Height * 0.5f;
    }

    public override void Update(float elapsed)
    {
        if (Emitting)
        {
            if (_explode) Explode();
            else EmitContinuously(elapsed);
        }
        base.Update(elapsed);
    }

    void Explode()
    {
        Emitting = false;
        int amount = _quantity <= 0 || _quantity > Members.Count ? Members.Count : _quantity;
        for (int i = 0; i < amount; i++) EmitParticle();
    }

    void EmitContinuously(float elapsed)
    {
        if (_frequency <= 0)
        {
            EmitParticle();
            if (_quantity > 0 && ++_counter >= _quantity) Emitting = false;
            return;
        }

        _timer += elapsed;
        while (_timer > _frequency)
        {
            _timer -= _frequency;
            EmitParticle();
            if (_quantity > 0 && ++_counter >= _quantity) { Emitting = false; break; }
        }
    }

    void EmitParticle()
    {
        if (MaxSize <= 0 && Members.Count == 0) return;     // nothing loaded

        var p = Recycle(() => new FlxParticle());
        p.Reset(0, 0);
        p.Visible = true;
        p.Active = true;

        p.X = X + (Width > 0 ? FlxG.Random.Float(0, Width) : 0f) - p.Width * 0.5f;
        p.Y = Y + (Height > 0 ? FlxG.Random.Float(0, Height) : 0f) - p.Height * 0.5f;
        p.Last = new Vector2(p.X, p.Y);

        p.Velocity = new Vector2(
            FlxG.Random.Float(Velocity.Min.X, Velocity.Max.X),
            FlxG.Random.Float(Velocity.Min.Y, Velocity.Max.Y));
        p.Acceleration = new Vector2(
            FlxG.Random.Float(Acceleration.Min.X, Acceleration.Max.X),
            FlxG.Random.Float(Acceleration.Min.Y, Acceleration.Max.Y));
        p.AngularVelocity = FlxG.Random.Float(AngularVelocity.Min, AngularVelocity.Max);
        p.Elasticity = FlxG.Random.Float(Elasticity.Min, Elasticity.Max);
        p.Lifespan = FlxG.Random.Float(Lifespan.Min, Lifespan.Max);
        p.Age = 0;
    }

    public override void Kill()
    {
        Emitting = false;
        base.Kill();
    }
}
