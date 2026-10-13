using Microsoft.Xna.Framework;

namespace MonoFlixel.Mode;

public class Bullet : FlxSprite
{
    readonly float _speed;

    public Bullet()
    {
        LoadGraphic(ModeAssets.Image("bullet"), true);
        Width = 6;
        Height = 6;
        Offset = new Vector2(1, 1);

        Animation.Add("up", new[] { 0 });
        Animation.Add("down", new[] { 1 });
        Animation.Add("left", new[] { 2 });
        Animation.Add("right", new[] { 3 });
        Animation.Add("poof", new[] { 4, 5, 6, 7 }, 50, false);

        _speed = 360;
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

    public void Shoot(Vector2 location, FlxDirection aim)
    {
        FlxG.Sound.Play(ModeAssets.Sound("shoot"));

        Reset(location.X - Width / 2, location.Y - Height / 2);
        Solid = true;

        switch (aim)
        {
            case FlxDirection.Up:
                Animation.Play("up");
                Velocity.Y = -_speed;
                break;
            case FlxDirection.Down:
                Animation.Play("down");
                Velocity.Y = _speed;
                break;
            case FlxDirection.Left:
                Animation.Play("left");
                Velocity.X = -_speed;
                break;
            case FlxDirection.Right:
                Animation.Play("right");
                Velocity.X = _speed;
                break;
        }
    }
}
