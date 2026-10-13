using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Mode;

/// <summary>Shown when the player has destroyed every spawner.</summary>
public class VictoryState : FlxState
{
    float _timer;
    bool _fading;

    public override void Create()
    {
        FlxG.Cameras.Flash(FlxColor.Rgb(0xd8eba2));

        // Gibs raining down from above
        var gibs = new FlxEmitter(0, -50);
        gibs.Width = FlxG.Width;
        gibs.Velocity.Set(0, 0, 0, 100);
        gibs.AngularVelocity.Set(-360, 360);
        gibs.Acceleration.Set(0, 80);
        gibs.LoadParticles(ModeAssets.Image("spawner_gibs"), 800, 32, true);
        Add(gibs);
        gibs.Start(false, 0.005f);

        var text = new FlxText(0, 0, FlxG.Width, "VICTORY\n\nSCORE: " + Reg.Score, 16);
        text.Alignment = FlxTextAlign.Center;
        text.Tint = FlxColor.Rgb(0xd8eba2);
        text.ScreenCenter(false, true);
        Add(text);
    }

    public override void Update(float elapsed)
    {
        if (!_fading)
        {
            _timer += elapsed;

            if (_timer > 0.35f && (_timer > 10 || FlxG.Keys.AnyJustPressed(Keys.X, Keys.C)))
            {
                _fading = true;
                FlxG.Sound.Play(ModeAssets.Sound("menu_hit_2"));
                FlxG.Cameras.Fade(FlxColor.Rgb(0x131c1b), 2, false, () => FlxG.SwitchState(new PlayState()));
            }
        }

        base.Update(elapsed);
    }
}
