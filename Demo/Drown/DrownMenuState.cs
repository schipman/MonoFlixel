using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo.Drown;

/// <summary>
/// The title screen of DROWN, from the original game's main menu: the underwater picture with the water wobble over it, the title
/// theme, and the DROWN logo that rises from the bottom and bounces into place over ten seconds, ending with a flash and a thump.
/// Z, ENTER, SPACE or the pad's A starts the game.
/// </summary>
public class DrownMenuState : FlxState
{
    /// <summary>The menu is drawn at the size of its picture (320x240) and shown at twice that.</summary>
    public const int Width = 320, Height = 240, Zoom = 2;

    public static void Open() => FlxG.SwitchState(new DrownMenuState(), Width, Height, Zoom);

    const string Hint = "N: new game    ESC: back to the launcher";

    FlxSprite _logo = null!;
    FlxText _prompt = null!, _hint = null!;
    bool _landed, _going, _eraseAsked;
    float _time;

    public override void Create()
    {
        FlxG.Game.Window.Title = "DROWN";
        FlxG.Cameras.BgColor = new Color(0, 20, 50);
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = false;

        // the bag comes back from the save: what was carried last time
        DrownData.LoadBag();

        var backdrop = new FlxSprite(0, 0).LoadGraphic("drown/GUI/mainscreen.png");
        backdrop.Moves = false;
        Add(backdrop);

        // the logo starts below the screen and rises 170 pixels, bouncing as it lands
        _logo = new FlxSprite(Width / 2f - 100, Height).LoadGraphic("drown/GUI/DrownLogo.png");
        _logo.DrawAfterPostProcess = true;
        _logo.Moves = false;
        Add(_logo);
        FlxTween.Tween(_logo, new { Y = Height - 170f }, 10f, new TweenOptions { Ease = FlxEase.BounceOut, OnComplete = _ => Land() });

        // the water wobble over the picture (not over the text)
        FlxG.PostProcess.Set(FlxPostProcess.Load("Wiggle", ("Amplitude", 0.0035f), ("Frequency", 18f), ("Speed", 2.5f)));

        _prompt = new FlxText(0, Height - 40, Width, "") { Alignment = FlxTextAlign.Center, Tint = new Color(190, 235, 255) };
        _hint = new FlxText(0, Height - 12, Width, Hint) { Alignment = FlxTextAlign.Center, Tint = new Color(110, 160, 190) };
        var credits = new FlxText(0, Height - 25, Width, "A game by Scott Chipman and Gavin Allen") { Alignment = FlxTextAlign.Center, Tint = new Color(170, 215, 235) };
        Add(_prompt);
        Add(credits);
        Add(_hint);

        // the title theme; the first frame can take a moment while the MP3 is decoded
        FlxG.Sound.PlayMusic("drown/audio/title_theme.mp3", 0.4f);

        FlxG.Camera.Fade(Color.Black, 1.5f, fadeIn: true);
    }

    void Land()
    {
        _landed = true;
        DrownData.Sfx("drown_logobump");
        FlxG.Camera.Flash(Color.Aqua, 0.5f);
    }

    void Start()
    {
        if (_going) return;
        _going = true;
        FlxG.Sound.Music?.FadeOut(1f);
        FlxG.Camera.Fade(Color.Black, 1f, onComplete: () =>
        {
            FlxG.Sound.Music?.Stop();
            FlxG.PostProcess.Clear();
            FlxG.SwitchState(new DrownOverworldState(), 640, 480, 1);
        });
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);
        _time += elapsed;

        // "press Z" blinks once the logo is down
        _prompt.Text = _landed && !_eraseAsked && _time % 1.2f < 0.8f ? "Press Z  (or A) to start" : "";

        if (_going) return;

        if (_eraseAsked)
        {
            // starting again asks twice: N again does it, anything else cancels
            if (FlxG.Keys.JustPressed(Keys.N))
            {
                foreach (var key in FlxG.Save.Keys.Where(k => k.StartsWith("drown.")).ToList()) FlxG.Save.Remove(key);
                FlxG.Save.Flush();
                DrownData.Bag.Clear();
                DrownData.Components.Clear();
                _eraseAsked = false;
                _hint.Text = "Everything was erased.";
            }
            else if (FlxG.Keys.AnyJustPressed(Keys.Escape, Keys.Z, Keys.Enter, Keys.Space))
            {
                _eraseAsked = false;
                _hint.Text = Hint;
            }
            return;
        }

        if (FlxG.Keys.AnyJustPressed(Keys.Z, Keys.Enter, Keys.Space) || FlxG.Gamepad.JustPressed(FlxGamepadInputID.A)) Start();

        if (FlxG.Keys.JustPressed(Keys.N))
        {
            _eraseAsked = true;
            _hint.Text = "Press N again to erase the bag and every dive site (any other key cancels)";
        }

        if (FlxG.Keys.JustPressed(Keys.Escape))
        {
            FlxG.Sound.Music?.Stop();
            FlxG.PostProcess.Clear();
            LauncherState.Return();
        }
    }
}
