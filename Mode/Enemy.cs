using Microsoft.Xna.Framework;

namespace MonoFlixel.Mode;

public class Enemy : FlxSprite, IHurt
{
    FlxObject _player = null!;                       // the player (only its position is needed)
    FlxTypedGroup<EnemyBullet> _bullets = null!;     // enemy bullets that enemies shoot
    FlxEmitter _gibs = null!;                        // bits that explode when the enemy dies
    float _thrust;                                   // how fast the ship is flying
    FlxEmitter _jets;                                // little poofs shooting out the back
    float _timer;                                    // decides when to fly and when to stop
    float _shotClock;                                // decides when to shoot
    int _health = 2;

    /// <summary>Recycled by the Spawner, so no required constructor parameters. Call Init() afterwards.</summary>
    public Enemy()
    {
        LoadGraphic(ModeAssets.Image("bot"));

        // the hit box is a bit smaller than the graphic
        Width = 12;
        Height = 12;
        CenterOffsets();

        _jets = new FlxEmitter();
        _jets.LoadParticles(ModeAssets.Image("jet"), 15, 0, false);

        MaxAngular = 120;
        AngularDrag = 400;
        Drag.X = 35;
        _thrust = 0;
    }

    public void Init(int xPos, int yPos, FlxTypedGroup<EnemyBullet> bullets, FlxEmitter gibs, FlxObject player)
    {
        _player = player;
        _bullets = bullets;
        _gibs = gibs;

        Reset(xPos - Width / 2, yPos - Height / 2);
        Angle = AngleTowardPlayer();
        _health = 2;          // enemies take 2 shots to kill
        _timer = 0;
        _shotClock = 0;
    }

    public override void Update(float elapsed)
    {
        // Rotate toward the player, but wobbly rather than instantly
        float da = AngleTowardPlayer();

        if (da < Angle) AngularAcceleration = -AngularDrag;
        else if (da > Angle) AngularAcceleration = AngularDrag;
        else AngularAcceleration = 0;

        // Jets on for 6 seconds, off for 2
        _timer += elapsed;
        if (_timer > 8) _timer = 0;
        bool jetsOn = _timer < 6;

        _thrust = ComputeVelocity(_thrust, jetsOn ? 90 : 0, Drag.X, 60, elapsed);
        Velocity = FlxAngle.Polar(_thrust, Angle);

        // Three shots every few seconds, only while on screen
        if (IsOnScreen())
        {
            bool shoot = false;
            float os = _shotClock;
            _shotClock += elapsed;

            if (os < 4.0f && _shotClock >= 4.0f)
            {
                _shotClock = 0;
                shoot = true;
            }
            else if (os < 3.5f && _shotClock >= 3.5f) shoot = true;
            else if (os < 3.0f && _shotClock >= 3.0f) shoot = true;

            if (shoot)
            {
                var b = _bullets.Recycle(() => new EnemyBullet());
                b.Shoot(Midpoint, Angle);
            }
        }

        base.Update(elapsed);

        // Jet particles out the back of the ship
        if (jetsOn)
        {
            if (!_jets.Emitting)
            {
                _jets.Start(false, 0.01f);
                if (IsOnScreen()) FlxG.Sound.Play(ModeAssets.Sound("jet"));
            }
            _jets.FocusOn(this);
            _jets.Velocity.Set(-Velocity.X - 30, -Velocity.Y - 30, -Velocity.X + 30, -Velocity.Y + 30);
        }
        else
        {
            _jets.Kill();
        }

        _jets.Update(elapsed);
    }

    /// <summary>The jets are drawn first so they appear underneath the ship.</summary>
    public override void Draw()
    {
        _jets.Draw();
        base.Draw();
    }

    public void Hurt(int damage = 1)
    {
        FlxG.Sound.Play(ModeAssets.Sound("hit"));
        FlxFlicker.Flicker(this, 0.2f, 0.02f, true);
        Reg.Score += 10;

        _health -= damage;
        if (_health <= 0) Kill();
    }

    public override void Kill()
    {
        if (!Alive) return;

        FlxG.Sound.Play(ModeAssets.Sound("asplode"));

        base.Kill();

        _jets.Kill();
        _gibs.FocusOn(this);
        _gibs.Start(true, 0, 20);
        Reg.Score += 200;
    }

    float AngleTowardPlayer() => FlxAngle.DegreesTo(Midpoint, _player.Midpoint);
}
