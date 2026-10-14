using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// FlxWeapon (Flixel Power Tools): five weapons built from the same class with different settings. Move with WASD, aim with the mouse,
/// hold the left button (or SPACE) to fire, 1-5 (or Q / E) change weapon. Shoot the bots; the grenades bounce off the floor.
/// </summary>
public class WeaponDemoState : FlxState
{
    const int W = 480, H = 270, FloorHeight = 16;

    sealed record Gun(string Name, string Info, FlxWeapon Weapon, Action<FlxWeapon> Shoot);

    FlxSprite _player = null!, _floor = null!;
    FlxTypedGroup<FlxSprite> _bots = null!;
    FlxText _hud = null!;
    Gun[] _guns = null!;
    int _current;
    int _score;
    float _cooldown;

    public override void Create()
    {
        FlxG.Game.Window.Title = "FlxWeapon";
        FlxG.Cameras.BgColor = new Color(20, 24, 38);
        FlxG.Mouse.Visible = true;

        _floor = new FlxSprite(0, H - FloorHeight).MakeGraphic(W, FloorHeight, new Color(60, 70, 96));
        _floor.Immovable = true;
        _floor.Moves = false;
        Add(_floor);

        _player = new FlxSprite(W / 2f - 5, 120).MakeGraphic(10, 10, new Color(120, 230, 140));
        _player.Drag = new Vector2(500, 500);
        _player.MaxVelocity = new Vector2(110, 110);
        Add(_player);

        _bots = new FlxTypedGroup<FlxSprite>();
        Add(_bots);
        for (int i = 0; i < 6; i++) SpawnBot();

        _guns = BuildGuns();
        foreach (var g in _guns) Add(g.Weapon.Group);

        _hud = new FlxText(6, 4, W - 12, "");
        Add(_hud);
        Refresh();
    }

    FlxSprite SpawnBot()
    {
        var bot = _bots.Recycle(() =>
        {
            var b = new FlxSprite().MakeGraphic(12, 12, new Color(230, 90, 90));
            b.Elasticity = 1;
            return b;
        });
        bot.Reset(FlxG.Random.Float(30, W - 42), FlxG.Random.Float(40, 150));
        bot.Velocity = new Vector2(FlxG.Random.Float(-40, 40), FlxG.Random.Float(-25, 25));
        return bot;
    }

    Gun[] BuildGuns()
    {
        // 1. machine gun: fast, a little spread, lots of small bullets
        var machine = new FlxWeapon("machine gun", _player) { BulletSpeed = 320, FireRate = 70, BulletLifeSpan = 1200, RandomAngle = 4, CenterOnParent = true };
        machine.MakePixelBullet(60, 2, 2, new Color(255, 230, 90));

        // 2. spinners: a picture baked at 16 angles, turning as it flies
        var spinner = new FlxWeapon("spinner", _player) { BulletSpeed = 170, FireRate = 160, BulletLifeSpan = 2000, CenterOnParent = true };
        spinner.MakeImageBullet(24, "mode/images/bot_bullet.png", rotate: true);
        foreach (var b in spinner.Group.Members) b.AngularVelocity = 720;

        // 3. shotgun: several bullets in one go, wide spread, short range
        var shotgun = new FlxWeapon("shotgun", _player) { BulletSpeed = 260, BulletLifeSpan = 500, RandomAngle = 11, RandomSpeed = 50, CenterOnParent = true };
        shotgun.MakePixelBullet(80, 3, 3, new Color(255, 160, 80));

        // 4. rockets: start slow and speed up towards the mouse
        var rocket = new FlxWeapon("rocket", _player) { BulletSpeed = 420, FireRate = 450, BulletLifeSpan = 2500, CenterOnParent = true };
        rocket.MakePixelBullet(10, 5, 5, new Color(255, 120, 60));
        rocket.SetBulletAcceleration(1, 1, 300, 300);

        // 5. grenades: gravity and bounce
        var grenade = new FlxWeapon("grenade", _player) { BulletSpeed = 230, FireRate = 250, BulletLifeSpan = 3500, BulletElasticity = 0.65f, RandomAngle = 3, CenterOnParent = true };
        grenade.MakePixelBullet(20, 4, 4, new Color(140, 255, 140));
        grenade.SetBulletGravity(0, 380);

        return new[]
        {
            new Gun("MACHINE GUN", "FireRate 70, RandomAngle 4", machine, w => w.FireFromAngle(Aim)),
            new Gun("SPINNER", "baked rotations + AngularVelocity", spinner, w => w.FireFromAngle(Aim)),
            new Gun("SHOTGUN", "one trigger pull, eight Fire calls", shotgun, w => { for (int i = 0; i < 8; i++) w.FireFromAngle(Aim); }),
            new Gun("ROCKET", "SetBulletAcceleration + FireAtMouse", rocket, w => w.FireAtMouse()),
            new Gun("GRENADE", "SetBulletGravity + BulletElasticity", grenade, w => w.FireFromAngle(Aim - 12)),
        };
    }

    float Aim
    {
        get
        {
            var m = FlxG.Mouse.GetWorldPosition();
            var p = _player.Midpoint;
            return MathF.Atan2(m.Y - p.Y, m.X - p.X) * FlxAngle.RadToDeg;
        }
    }

    void Refresh()
    {
        var g = _guns[_current];
        _hud.Text = $"{_current + 1}: {g.Name}  ({g.Info})   fired {g.Weapon.BulletsFired}   score {_score}\n" +
                    "WASD move, mouse aim + fire, 1-5 / Q E change weapon, ESC back";
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        // moving
        var k = FlxG.Keys;
        var accel = Vector2.Zero;
        if (k.Pressed(Keys.A)) accel.X -= 600;
        if (k.Pressed(Keys.D)) accel.X += 600;
        if (k.Pressed(Keys.W)) accel.Y -= 600;
        if (k.Pressed(Keys.S)) accel.Y += 600;
        _player.Acceleration = accel;
        _player.X = MathHelper.Clamp(_player.X, 0, W - _player.Width);
        _player.Y = MathHelper.Clamp(_player.Y, 0, H - FloorHeight - _player.Height);

        // choosing a weapon
        for (int i = 0; i < _guns.Length; i++)
            if (k.JustPressed(Keys.D1 + i)) { _current = i; Refresh(); }
        if (k.JustPressed(Keys.E)) { _current = (_current + 1) % _guns.Length; Refresh(); }
        if (k.JustPressed(Keys.Q)) { _current = (_current + _guns.Length - 1) % _guns.Length; Refresh(); }

        // firing: the weapon limits itself with FireRate; the shotgun has none, so the demo waits between pulls
        _cooldown -= elapsed;
        if ((FlxG.Mouse.Pressed || k.Pressed(Keys.Space)) && _cooldown <= 0)
        {
            var gun = _guns[_current];
            gun.Shoot(gun.Weapon);
            if (gun.Name == "SHOTGUN") _cooldown = 0.55f;
            Refresh();
        }

        // bots bounce around the screen
        foreach (var bot in _bots.Members)
        {
            if (!bot.Exists) continue;
            if (bot.X < 0 || bot.X > W - bot.Width) bot.Velocity.X = -bot.Velocity.X;
            if (bot.Y < 30 || bot.Y > H - FloorHeight - bot.Height) bot.Velocity.Y = -bot.Velocity.Y;
            bot.X = MathHelper.Clamp(bot.X, 0, W - bot.Width);
            bot.Y = MathHelper.Clamp(bot.Y, 30, H - FloorHeight - bot.Height);
        }

        // hits
        foreach (var gun in _guns)
        {
            var weapon = gun.Weapon;
            FlxG.Overlap(weapon.Group, _bots, (bullet, bot) =>
            {
                if (gun.Name != "GRENADE") bullet.Kill();
                bot.Kill();
                _score++;
                Refresh();
                new FlxTimer().Start(1.0f, _ => SpawnBot());
            });
        }
        FlxG.Collide(_floor, _guns[4].Weapon.Group);

        if (k.JustPressed(Keys.Escape)) LauncherState.Return();
    }
}
