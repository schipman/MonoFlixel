using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel.Demo.Drown;

/// <summary>
/// The things that sit on the sea floor: kelp, coral and the three kinds of resource crystal. One class with a kind, instead of a class
/// for each. When the tile it stands on is mined away it breaks (resources drop shards).
/// </summary>
public class Deco : FlxSprite
{
    public readonly int Id;
    public readonly EntityKind Kind;
    public readonly int SupportCol, SupportRow;
    readonly DrownDiveState _world;
    readonly float _swayPhase;
    float _time;

    public Deco(DrownDiveState world, Placement p) : base(p.Col * DiveLevel.Tile, p.Row * DiveLevel.Tile)
    {
        _world = world;
        Id = p.Id;
        Kind = p.Kind;
        SupportCol = p.Col;
        SupportRow = p.SupportRow;
        _swayPhase = p.Id * 0.7f;
        Moves = false;

        string image = Kind switch
        {
            EntityKind.Kelp => "drown/kelp.png",
            EntityKind.Coral1 => "drown/Decor/coral1.png",
            EntityKind.Coral2 => "drown/Decor/coral2.png",
            EntityKind.Oceanite => $"drown/Resources/oceanite_{1 + p.Id % 2}.png",
            EntityKind.Territe => $"drown/Resources/territe_{1 + p.Id % 2}.png",
            _ => $"drown/Resources/magmite_{1 + p.Id % 2}.png",
        };
        LoadGraphic(image);

        if (Kind == EntityKind.Kelp)
        {
            // sways from its base
            Origin = new Vector2(FrameWidth * 0.5f, FrameHeight);
        }
    }

    public bool IsResource => Kind is EntityKind.Oceanite or EntityKind.Territe or EntityKind.Magmite;

    Color GibColor => Kind switch
    {
        EntityKind.Kelp => Color.Green,
        EntityKind.Coral1 or EntityKind.Coral2 => Color.LightYellow,
        EntityKind.Oceanite => Color.CornflowerBlue,
        EntityKind.Territe => Color.SandyBrown,
        _ => Color.OrangeRed,
    };

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        // kelp sways (only worth doing where it can be seen)
        if (Kind == EntityKind.Kelp && IsOnScreen())
        {
            _time += elapsed;
            Angle = MathF.Sin(_time * 1.3f + _swayPhase) * 4f;
        }
    }

    public override void Draw()
    {
        if (IsOnScreen()) base.Draw();
    }

    /// <summary>Removes the object. <paramref name="drop"/>: resources give their shards.</summary>
    public void Break(bool drop)
    {
        if (!Exists) return;
        Kill();
        _world.TakeEntity(Id);

        var mid = Midpoint;
        _world.Gibs.Plant.Burst(mid, GibColor, 5);

        if (drop && IsResource)
        {
            var shard = Kind switch { EntityKind.Oceanite => ItemKind.Oceanite, EntityKind.Territe => ItemKind.Territe, _ => ItemKind.Magmite };
            for (int i = 0; i < 3; i++)
                _world.SpawnPickup(shard, mid.X, mid.Y,
                    FlxG.Random.Float(-100, 100), FlxG.Random.Float(-120, -80), FlxG.Random.Float(-1000, 1000));
        }
    }
}

/// <summary>A shard lying about. It falls (slowly: it is underwater), bounces off the rock and is picked up when the player touches it.</summary>
public class Pickup : FlxSprite
{
    public readonly ItemKind Kind;

    public Pickup(ItemKind kind, float x, float y, float vx, float vy, float spin) : base(x - 8, y - 8)
    {
        Kind = kind;
        string file = kind switch
        {
            ItemKind.Oceanite => "oceanite_shard",
            ItemKind.Territe => "territe_shard",
            _ => "magmite_shard"
        };
        LoadGraphic($"drown/Pickups/{file}.png");

        Velocity = new Vector2(vx, vy);
        AngularVelocity = spin;
        AngularDrag = MathF.Abs(spin / 5f);
        Acceleration.Y = 70;
        MaxVelocity.Y = 80;
        Drag = new Vector2(10, 200);
    }
}

/// <summary>A harpoon from the player's gun. There are eight of them, used over and over. It leaves a trail of small bubbles.</summary>
public class Harpoon : FlxSprite
{
    public const float Damage = 10, Force = 20;
    readonly DrownDiveState _world;
    float _age, _trail;

    public Harpoon(DrownDiveState world)
    {
        _world = world;
        LoadGraphic("drown/harpoon.png");
        Origin = Vector2.Zero;
        Exists = false;
    }

    public void Shoot(Vector2 from, Vector2 velocity, float angle)
    {
        Reset(from.X, from.Y);
        Velocity = velocity;
        Angle = angle;
        _age = 0;
        _trail = 0;
        Visible = Active = true;
    }

    public override void Update(float elapsed)
    {
        _age += elapsed;
        if (_age > 3f) { Kill(); return; }
        base.Update(elapsed);

        // hitting rock ends it
        if (Touching != FlxDirection.None) { Kill(); return; }

        // the bubbles it stirs up behind it
        _trail -= elapsed;
        if (_trail <= 0 && Velocity != Vector2.Zero)
        {
            _trail = FlxG.Random.Float(0.025f, 0.06f);
            var back = -Vector2.Normalize(Velocity);
            var tail = new Vector2(X, Y) + back * FlxG.Random.Float(0f, 8f);
            _world.Bubbles.Spawn(tail, back * FlxG.Random.Float(5f, 25f) + new Vector2(FlxG.Random.Float(-12f, 12f), FlxG.Random.Float(-12f, 12f)),
                FlxG.Random.Float(0.3f, 0.8f), FlxG.Random.Float(0.5f, 1.2f));
        }
    }
}

/// <summary>A bubble: it rises (the bigger, the faster), wobbles from side to side and fades out as it ends.</summary>
public sealed class BubbleParticle : FlxParticle
{
    float _phase;

    public BubbleParticle()
    {
        LoadGraphic("drown/Particles/bubbletest.png");
        Moves = true;
    }

    public void Launch(Vector2 centre, Vector2 velocity, float scale, float life)
    {
        Reset(centre.X - FrameWidth * 0.5f, centre.Y - FrameHeight * 0.5f);
        Velocity = velocity;
        Scale = new Vector2(scale, scale);
        Lifespan = life;
        Age = 0;
        Alpha = 0.9f;
        _phase = FlxG.Random.Float(0f, 6.28f);
        Visible = Active = true;
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);
        if (!Exists) return;

        // buoyancy: a big bubble rises faster than a small one
        Velocity.Y -= (30f + 55f * Scale.X) * elapsed;
        Velocity.X += MathF.Sin(Age * 5f + _phase) * 22f * elapsed;
        Velocity.X *= 1f - Math.Min(1f, 1.2f * elapsed);
        Velocity.Y = Math.Max(Velocity.Y, -140f);

        float left = Lifespan - Age;
        Alpha = Math.Clamp(left * 2f, 0f, 1f) * 0.9f;
    }
}

/// <summary>All the bubbles of a dive in one pool, reused: from the diver, the harpoons and the geysers.</summary>
public sealed class BubblePool : FlxTypedGroup<BubbleParticle>
{
    public BubblePool(int count = 400) : base(count)
    {
        for (int i = 0; i < count; i++) Add(new BubbleParticle());
    }

    /// <summary>Releases a bubble (<paramref name="scale"/> 1 is the size of the picture, 10 pixels).</summary>
    public void Spawn(Vector2 centre, Vector2 velocity, float scale, float life)
    {
        var p = GetFirstAvailable();
        p?.Launch(centre, velocity, scale, life);      // if all are in use, the new one is simply left out
    }

    /// <summary>A burst of bubbles of many sizes in a direction.</summary>
    public void Burst(Vector2 at, Vector2 direction, int count, float speed = 70f, float spread = 40f)
    {
        for (int i = 0; i < count; i++)
        {
            var v = direction * FlxG.Random.Float(speed * 0.3f, speed) + new Vector2(FlxG.Random.Float(-spread, spread), FlxG.Random.Float(-spread, spread));
            Spawn(at + new Vector2(FlxG.Random.Float(-3f, 3f), FlxG.Random.Float(-3f, 3f)), v, RandomSize(), FlxG.Random.Float(0.8f, 2.0f));
        }
    }

    /// <summary>Mostly small bubbles, now and then a big one.</summary>
    public static float RandomSize()
    {
        float r = FlxG.Random.Float();
        return 0.35f + r * r * 1.4f;
    }
}

/// <summary>
/// A bubble geyser: a vent in the floor that blows a column of bubbles up. Standing in the column fills the suit with air again
/// (a faint glow shows where it reaches, and it is a light in the dark). There are only a few in a site, and the rock the vent
/// is in cannot be mined away.
/// </summary>
public sealed class Geyser : FlxSprite
{
    public const float RefillPerSecond = 30f, Range = 700f;

    public readonly int Id;
    public readonly Vector2 Mouth;
    readonly DrownDiveState _world;
    float _emit, _soundCooldown;
    bool _inside;

    public Geyser(DrownDiveState world, Placement p, int headroom) : base(0, 0)
    {
        _world = world;
        Id = p.Id;
        Moves = false;

        const int tile = DiveLevel.Tile, width = 54;
        int height = headroom * tile;
        Mouth = new Vector2(p.Col * tile + tile * 0.5f, p.Row * tile + tile - 2);

        // the column: pale water, brightest at the vent and fading to nothing at the top
        var tex = new Microsoft.Xna.Framework.Graphics.Texture2D(FlxG.GraphicsDevice, width, height);
        var px = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float across = 1f - MathF.Abs((x + 0.5f) / width * 2f - 1f);        // 0 at the sides, 1 in the middle
                float up = 1f - y / (float)height;                                  // 1 at the bottom
                float a = Math.Clamp(across * 1.4f, 0f, 1f) * up * 0.32f;
                px[y * width + x] = new Color(140, 235, 255, (int)(255 * a));
            }
        tex.SetData(px);
        LoadGraphic(tex);
        Blend = FlxBlend.Add;
        X = Mouth.X - width * 0.5f;
        Y = Mouth.Y - height + 2;
    }

    public override void Update(float elapsed)
    {
        float near = Vector2.Distance(Mouth, _world.Player.Midpoint);
        if (near > Range) { _inside = false; return; }

        // the stream of bubbles, of every size
        _emit -= elapsed;
        while (_emit <= 0)
        {
            _emit += FlxG.Random.Float(0.02f, 0.05f);
            _world.Bubbles.Spawn(Mouth + new Vector2(FlxG.Random.Float(-12f, 12f), 0f),
                new Vector2(FlxG.Random.Float(-10f, 10f), -FlxG.Random.Float(50f, 110f)), BubblePool.RandomSize(), FlxG.Random.Float(1.2f, 2.4f));
        }

        // standing in it: the suit fills up
        var player = _world.Player;
        bool inside = !player.IsDead && player.X < X + Width && player.X + player.Width > X && player.Y < Y + Height && player.Y + player.Height > Y;
        _soundCooldown -= elapsed;
        if (inside)
        {
            player.Oxygen = Math.Min(DrownPlayer.MaxOxygen, player.Oxygen + RefillPerSecond * elapsed);
            if (!_inside && _soundCooldown <= 0)
            {
                DrownData.Sfx("air_intake", 0.5f);
                _soundCooldown = 3f;
                if (player.Oxygen < DrownPlayer.MaxOxygen - 5) _world.Say("Fresh air from the geyser.", 1.5f);
            }
            // a few bubbles from the diver's own mouth too
            if (FlxG.Random.Bool(8)) _world.Bubbles.Spawn(player.Midpoint + new Vector2(0, -14), new Vector2(0, -40), FlxG.Random.Float(0.4f, 1.1f), 1f);
        }
        _inside = inside;

        // the column breathes a little
        Alpha = 0.75f + 0.25f * MathF.Sin((float)FlxG.RealTime * 4f + Id);
    }

    public override void Draw()
    {
        if (IsOnScreen()) base.Draw();
    }
}

/// <summary>Bursts of bits that fly off: rock, blood, plants. Each kind is an emitter with its own pool and picture.</summary>
public sealed class GibKind : FlxEmitter
{
    public GibKind(string image, int count, float gravity) : base(0, 0, count)
    {
        LoadParticles(image, count, multiple: true);
        Velocity.Set(-90, -200, 90, -20);
        Acceleration.Set(0, gravity, 0, gravity);
        AngularVelocity.Set(-720, 720);
        Lifespan.Set(0.8f, 1.6f);
    }

    public void Burst(Vector2 at, Color tint, int count, float scale = 1f)
    {
        X = at.X; Y = at.Y;
        foreach (var p in Members) { p.Tint = tint; p.Scale = new Vector2(scale, scale); }
        Start(explode: true, quantity: count);
    }
}

public sealed class Gibs
{
    public readonly GibKind Rock = new("drown/rockgibs.png", 60, 50);
    public readonly GibKind Blood = new("drown/gibs1.png", 40, 50);
    public readonly GibKind Plant = new("drown/gibs1.png", 40, 50);

    public void AddTo(FlxState state)
    {
        state.Add(Rock);
        state.Add(Blood);
        state.Add(Plant);
    }
}

public enum MobKind { Predator, Shark, Jelly }

/// <summary>
/// A sea creature: one class for the three kinds. Predators and sharks hunt the player when close, jellyfish only drift about.
/// Harpoons hurt and knock them back; they drop a shard when they die.
/// </summary>
public class Mob : FlxSprite
{
    public readonly MobKind Kind;
    readonly DrownDiveState _world;
    readonly MobSpawner _spawner;

    float _patrolTime, _waitTime, _stun, _hurtTime, _time;
    int _patrolDir;
    bool _waiting, _stunned;

    float AttackRange => Kind == MobKind.Jelly ? 0 : 200;
    float PatrolSpeed => Kind == MobKind.Predator ? 30 : 15;
    float HitDamage => Kind == MobKind.Shark ? 15 : 10;

    public Mob(DrownDiveState world, MobSpawner spawner, MobKind kind, float x, float y) : base(x, y)
    {
        _world = world;
        _spawner = spawner;
        Kind = kind;
        Health = 30;
        Drag = new Vector2(25, 25);
        _patrolTime = kind == MobKind.Predator ? 0 : 1;

        switch (kind)
        {
            case MobKind.Predator:
                LoadGraphic("drown/predator.png");
                break;
            case MobKind.Shark:
                LoadGraphic("drown/Mobs/shark.png");
                break;
            default:
                LoadGraphic("drown/Mobs/Jellyfish1.png", animated: true, frameWidth: 30, frameHeight: 30);
                Animation.Add("move", new[] { 0, 1, 2 }, 5);
                Animation.Play("move");
                break;
        }
        MaxVelocity = new Vector2(48, 48);
        _patrolDir = FlxG.Random.Int(1, 4);
    }

    public override void Update(float elapsed)
    {
        _time += elapsed;

        if (_stunned)
        {
            Acceleration = Vector2.Zero;
            _stun -= elapsed;
            if (_stun <= 0) _stunned = false;
        }
        else
        {
            var player = _world.Player;
            float distance = Vector2.Distance(Midpoint, player.Midpoint);

            if (AttackRange > 0 && distance < AttackRange && !player.IsDead) Seek(player);
            else Patrol(elapsed);
        }

        // looking where it goes
        if (Velocity.X > 2) FlipX = true;           // the pictures look to the left
        else if (Velocity.X < -2) FlipX = false;

        // touching the player: stops to bite
        if (!_world.Player.IsDead && FlxObject.OverlapsPair(this, _world.Player))
        {
            Velocity = Vector2.Zero;
            _hurtTime += elapsed;
            if (_hurtTime >= 1f)
            {
                _hurtTime = 0;
                _world.Player.Hurt(HitDamage);
            }
        }
        else _hurtTime = 0;

        base.Update(elapsed);
    }

    void Seek(DrownPlayer player)
    {
        MaxVelocity = new Vector2(75, 75);
        var dir = player.Midpoint - Midpoint;
        if (dir == Vector2.Zero) return;
        dir.Normalize();
        Acceleration = dir * 150f;
        _waiting = false;
    }

    void Patrol(float elapsed)
    {
        MaxVelocity = new Vector2(48, 48);

        if (_patrolTime < 2.75f) _patrolTime += elapsed;
        else
        {
            _waiting = true;
            Acceleration = Vector2.Zero;
            _waitTime += elapsed;
            if (_waitTime > 1f)
            {
                _patrolDir = FlxG.Random.Int(1, Kind == MobKind.Jelly ? 5 : 4);
                _patrolTime = 0;
                _waitTime = 0;
                _waiting = false;
            }
        }

        if (_waiting) return;

        float s = PatrolSpeed;
        Acceleration = _patrolDir switch
        {
            1 => new Vector2(0, s),
            2 => new Vector2(0, -s),
            3 => new Vector2(s, 0),
            4 => new Vector2(-s, 0),
            _ => new Vector2(MathF.Cos(_time * 1.5f), MathF.Sin(_time * 1.5f)) * s,      // jellyfish also drift in circles
        };
    }

    /// <summary>A harpoon hit: damage, knockback and a moment of being stunned.</summary>
    public void HitBy(Harpoon h)
    {
        Health -= Harpoon.Damage;
        FlxFlicker.Flicker(this, 0.5f);
        _world.Gibs.Blood.Burst(Midpoint, Color.Red, 3);

        var push = h.Velocity;
        if (push != Vector2.Zero) push.Normalize();
        Velocity += push * Harpoon.Force * 5;

        _stunned = true;
        _stun = 1f;
        h.Kill();

        if (Health <= 0) Die();
    }

    void Die()
    {
        _spawner.Forget(this);
        Kill();
        var drop = Kind switch { MobKind.Predator => ItemKind.Oceanite, MobKind.Shark => ItemKind.Territe, _ => ItemKind.Magmite };
        _world.SpawnPickup(drop, Midpoint.X, Midpoint.Y, FlxG.Random.Float(-5, 5), 10, FlxG.Random.Float(-200, 200));
        _world.Gibs.Blood.Burst(Midpoint, Color.DarkRed, 8);
    }
}

/// <summary>
/// Makes mobs now and then, up to a limit, while the player is in range. Mobs that are far away are switched off, which keeps a
/// site with dozens of spawners fast.
/// </summary>
public class MobSpawner : FlxBasic
{
    const float Range = 800f;

    readonly DrownDiveState _world;
    readonly MobKind _kind;
    readonly Vector2 _position;
    readonly int _limit;
    readonly float _delay;
    float _countdown;
    readonly List<Mob> _mobs = new();

    public MobSpawner(DrownDiveState world, EntityKind kind, Vector2 position)
    {
        _world = world;
        _position = position;
        _kind = kind switch { EntityKind.SharkSpawner => MobKind.Shark, EntityKind.JellySpawner => MobKind.Jelly, _ => MobKind.Predator };
        _limit = _kind switch { MobKind.Predator => 3, MobKind.Shark => 1, _ => 2 };
        _delay = 60f;
        _countdown = 0;
    }

    public void Forget(Mob mob) => _mobs.Remove(mob);

    public override void Update(float elapsed)
    {
        bool near = Vector2.Distance(_position, _world.Player.Midpoint) < Range;

        foreach (var m in _mobs)
        {
            bool mobNear = Vector2.Distance(m.Midpoint, _world.Player.Midpoint) < Range;
            m.Exists = m.Alive && mobNear;
        }

        if (!near) return;

        _countdown -= elapsed;
        if (_countdown <= 0 && _mobs.Count < _limit)
        {
            _countdown = _delay;
            var mob = new Mob(_world, this, _kind, _position.X, _position.Y);
            _mobs.Add(mob);
            _world.Mobs.Add(mob);
        }
    }
}
