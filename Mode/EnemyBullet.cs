using Microsoft.Xna.Framework;

namespace MonoFlixel.Mode;

public class EnemyBullet : FlxSprite
{
    public float Speed;

    public EnemyBullet()
    {
        LoadGraphic(ModeAssets.Image("bot_bullet"), true);
        Animation.Add("idle", new[] { 0, 1 }, 50);
        Animation.Add("poof", new[] { 2, 3, 4 }, 50, false);
        Speed = 120;
    }

    public override void Update(float elapsed)
    {
        if (!Alive)
        {
            if (Animation.Finished) Exists = false;
        }
        else if (Touching != FlxDirection.None)
        {
            Kill();
        }

        base.Update(elapsed);
    }

    public override void Kill()
    {
        if (!Alive) return;

        Velocity = Vector2.Zero;
        if (IsOnScreen()) FlxG.Sound.Play(ModeAssets.Sound("jump"));

        Alive = false;
        Solid = false;
        Animation.Play("poof");
    }

    public void Shoot(Vector2 location, float angle)
    {
        FlxG.Sound.Play(ModeAssets.Sound("enemy"), 0.5f);

        Reset(location.X - Width / 2, location.Y - Height / 2);
        Velocity = FlxAngle.Polar(Speed, angle);
        Solid = true;
        Animation.Play("idle");
    }
}
