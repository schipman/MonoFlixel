using Microsoft.Xna.Framework;

namespace MonoFlixel.Mode;

public class Spawner : FlxSprite, IHurt
{
    float _timer;
    readonly FlxTypedGroup<Enemy> _bots;
    readonly FlxTypedGroup<EnemyBullet> _botBullets;
    readonly FlxEmitter _botGibs;
    readonly FlxEmitter _gibs;
    readonly Player _player;
    bool _open;
    int _health = 8;

    public Spawner(int x, int y, FlxEmitter gibs, FlxTypedGroup<Enemy> bots, FlxTypedGroup<EnemyBullet> botBullets,
        FlxEmitter botGibs, Player player) : base(x, y)
    {
        LoadGraphic(ModeAssets.Image("spawner"), true);
        _gibs = gibs;
        _bots = bots;
        _botBullets = botBullets;
        _botGibs = botGibs;
        _player = player;
        _timer = FlxG.Random.Float(0, 20);
        _open = false;

        Animation.Add("open", new[] { 1, 2, 3, 4, 5 }, 40, false);
        Animation.Add("close", new[] { 4, 3, 2, 1, 0 }, 40, false);
        Animation.Add("dead", new[] { 6 });
    }

    public override void Update(float elapsed)
    {
        _timer += elapsed;
        int limit = 20;

        if (IsOnScreen()) limit = 4;

        if (_timer > limit)
        {
            _timer = 0;
            MakeBot();
        }
        else if (_timer > limit - 0.35f)
        {
            if (!_open)
            {
                _open = true;
                Animation.Play("open");
            }
        }
        else if (_timer > 1)
        {
            if (_open)
            {
                Animation.Play("close");
                _open = false;
            }
        }

        base.Update(elapsed);
    }

    public void Hurt(int damage = 1)
    {
        FlxG.Sound.Play(ModeAssets.Sound("hit"));
        FlxFlicker.Flicker(this, 0.2f, 0.02f, true);
        Reg.Score += 50;

        _health -= damage;
        if (_health <= 0) Kill();
    }

    public override void Kill()
    {
        if (!Alive) return;

        FlxG.Sound.Play(ModeAssets.Sound("asplode"));
        FlxG.Sound.Play(ModeAssets.Sound("menu_hit_2"));

        base.Kill();

        Active = false;
        Exists = true;
        Solid = false;
        Animation.Play("dead");
        FlxG.Camera.Shake(0.007f, 0.25f);
        FlxG.Camera.Flash(FlxColor.Rgb(0xd8eba2), 0.65f, TurnOffSlowMo);
        FlxG.TimeScale = 0.35f;
        MakeBot();
        _gibs.FocusOn(this);
        _gibs.Start(true, 3);
        Reg.Score += 1000;
    }

    void MakeBot()
    {
        _bots.Recycle(() => new Enemy())
            .Init((int)MathF.Floor(X + Width / 2), (int)MathF.Floor(Y + Height / 2), _botBullets, _botGibs, _player);
    }

    static void TurnOffSlowMo() => FlxG.TimeScale = 1f;
}
