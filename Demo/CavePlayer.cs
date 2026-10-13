using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>The little spaceman from the FlxCaveGenerator demo (8x8 frames).</summary>
public class CavePlayer : FlxSprite
{
    const int JumpPower = 200;

    public CavePlayer(float x, float y) : base(x, y)
    {
        LoadGraphic(FlxAssets.Get("cave/spaceman.png"), animated: true, frameWidth: 8);

        // bounding box tweaks
        Width = 6;
        Height = 7;
        Offset = new Vector2(1, 1);

        // basic player physics
        const int runSpeed = 80;
        Drag.X = runSpeed * 8;
        Acceleration.Y = 420;
        MaxVelocity = new Vector2(runSpeed, JumpPower);

        Animation.Add("idle", new[] { 0 });
        Animation.Add("run", new[] { 1, 2, 3, 0 }, 12);
        Animation.Add("jump", new[] { 4 });
    }

    public override void Update(float elapsed)
    {
        Acceleration.X = 0;

        if (FlxG.Keys.AnyPressed(Keys.Left, Keys.A))
        {
            FlipX = true;
            Acceleration.X -= Drag.X;
        }
        else if (FlxG.Keys.AnyPressed(Keys.Right, Keys.D))
        {
            FlipX = false;
            Acceleration.X += Drag.X;
        }

        if (FlxG.Keys.AnyJustPressed(Keys.Up, Keys.W, Keys.Space) && Velocity.Y == 0)
            Velocity.Y = -JumpPower;

        if (Velocity.Y != 0) Animation.Play("jump");
        else if (Velocity.X == 0) Animation.Play("idle");
        else Animation.Play("run");

        base.Update(elapsed);
    }
}
