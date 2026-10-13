using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

public class MegaManPlayer : FlxSprite
{
    const float Speed = 110f;
    const float JumpPower = 350f;

    // megaman_run.png is 8 columns x 2 rows of 49x49 frames:
    //   frames 0-10  = run cycle (row 0 + first 3 of row 1)
    //   frames 13-15 = run with arm cannon extended
    // The sheet has no idle/jump art, so we borrow run frames for those poses.
    const int IdleFrame = 0;
    const int AirFrame = 2;       // widest stride

    public MegaManPlayer(float x, float y) : base(x, y)
    {
        LoadGraphic(FlxAssets.Get("megaman_run.png"), animated: true, frameWidth: 49, frameHeight: 49);

        // Hitbox is much smaller than the 49x49 frame. Offset places it inside the frame so the
        // hitbox bottom lines up with the feet (frame row ~41) and it is centered horizontally.
        Width = 19;
        Height = 34;
        Offset = new Vector2(15, 7);

        Animation.Add("idle", new[] { IdleFrame }, 1);
        Animation.Add("run", FlxAnimationController.Range(0, 10), 15);
        Animation.Add("run_shoot", new[] { 13, 14, 15 }, 15);     // registered for later, not bound to a key yet
        Animation.Add("air", new[] { AirFrame }, 1);
        Animation.Play("idle");

        Acceleration.Y = 800f;                    // gravity
        MaxVelocity = new Vector2(Speed, 420f);
        Drag.X = Speed * 8f;                      // stops quickly when no key is held
    }

    public override void Update(float elapsed)
    {
        // Touching is read BEFORE base.Update() resets it (same as Flixel).
        bool onFloor = IsTouching(FlxDirection.Floor);

        bool left = FlxG.Keys.AnyPressed(Keys.Left, Keys.A);
        bool right = FlxG.Keys.AnyPressed(Keys.Right, Keys.D);

        Acceleration.X = 0;
        if (left) { Acceleration.X -= Drag.X; FlipX = true; }     // sheet faces right
        if (right) { Acceleration.X += Drag.X; FlipX = false; }

        bool jumped = false;
        if (onFloor && FlxG.Keys.AnyJustPressed(Keys.Space, Keys.Up, Keys.W))
        {
            Velocity.Y = -JumpPower;
            jumped = true;
        }

        if (!onFloor || jumped) Animation.Play("air");
        else if (left != right) Animation.Play("run");            // exactly one direction held
        else Animation.Play("idle");

        base.Update(elapsed);
    }
}
