using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoFlixel.Demo;

namespace MonoFlixel.Mode;

/// <summary>Mode's title screen: "mo" and "de" smash together. ESC returns to the launcher.</summary>
public class MenuState : FlxState
{
    FlxEmitter _gibs = null!;
    FlxButton? _playButton;
    FlxText _title1 = null!;
    FlxText _title2 = null!;
    bool _fading;
    bool _titlesJoined;

    public override void Create()
    {
        FlxG.Cameras.BgColor = FlxColor.Rgb(0x131c1b);

        FlxG.Sound.Music?.Stop();

        // All the bits that blow up when the text smooshes together
        _gibs = new FlxEmitter(FlxG.Width / 2f - 50, FlxG.Height / 2f - 10);
        _gibs.Width = 100;
        _gibs.Height = 30;
        _gibs.Velocity.Set(0, -200, 0, -20);
        _gibs.AngularVelocity.Set(-720, 720);
        _gibs.Acceleration.Set(0, 100);
        _gibs.LoadParticles(ModeAssets.Image("spawner_gibs"), 650, 32, true);
        _gibs.Lifespan.Set(5, 5);
        Add(_gibs);

        // The letters "mo"
        _title1 = new FlxText(FlxG.Width + 16, FlxG.Height / 3f, 64, "mo");
        _title1.Size = 32;
        _title1.Tint = FlxColor.Rgb(0x3a5c39);
        _title1.Moves = true;
        _title1.Velocity.X = -FlxG.Width;
        Add(_title1);

        // The letters "de"
        _title2 = new FlxText(-60, _title1.Y, MathF.Floor(_title1.Width), "de");
        _title2.Size = _title1.Size;
        _title2.Moves = true;
        _title2.Tint = _title1.Tint;
        _title2.Velocity.X = FlxG.Width;
        Add(_title2);

        _fading = false;
        _titlesJoined = false;

        FlxG.Mouse.LoadCursor(ModeAssets.Image("cursor"), 1);
        FlxG.Mouse.Visible = true;
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        if (!_titlesJoined && _title2.X > _title1.X + _title1.Width - 4)
        {
            _titlesJoined = true;

            // Once "mo" and "de" cross each other, fix their positions
            _title2.X = _title1.X + _title1.Width - 4;
            _title1.Velocity.X = 0;
            _title2.Velocity.X = 0;

            // Then play a cool sound, change their color, and blow up pieces everywhere
            FlxG.Sound.Play(ModeAssets.Sound("menu_hit"));

            FlxG.Cameras.Flash(FlxColor.Rgb(0xd8eba2), 0.5f);
            FlxG.Cameras.Shake(0.035f, 0.5f);
            _title1.Tint = _title2.Tint = FlxColor.Rgb(0xd8eba2);
            _gibs.Start(true, 5);
            _title1.Angle = FlxG.Random.Float(-15, 15);
            _title2.Angle = FlxG.Random.Float(-15, 15);

            // Then the text and buttons that appear
            var text = new FlxText(FlxG.Width / 2f - 50, FlxG.Height / 3f + 39, 100, "by Adam Atomic");
            text.Alignment = FlxTextAlign.Center;
            text.Tint = FlxColor.Rgb(0x3a5c39);
            Add(text);

            text = new FlxText(FlxG.Width / 2f - 40, FlxG.Height / 3f + 119, 80, "X + C TO PLAY");
            text.Tint = FlxColor.Rgb(0x729954);
            text.Alignment = FlxTextAlign.Center;
            Add(text);

            var buttonColor = FlxColor.Rgb(0x729954);
            var labelColor = FlxColor.Rgb(0xd8eba2);

            var flixelButton = new FlxButton(FlxG.Width / 2f - 40, FlxG.Height / 3f + 54, "haxeflixel.com",
                () => FlxG.OpenUrl("http://haxeflixel.com"));
            flixelButton.Tint = buttonColor;
            flixelButton.Label.Tint = labelColor;
            Add(flixelButton);

            var dannyButton = new FlxButton(flixelButton.X, flixelButton.Y + 22, "music: dannyB",
                () => FlxG.OpenUrl("http://dbsoundworks.com"));
            dannyButton.Tint = buttonColor;
            dannyButton.Label.Tint = labelColor;
            Add(dannyButton);

            _playButton = new FlxButton(flixelButton.X, flixelButton.Y + 62, "CLICK HERE", OnPlay);
            _playButton.Tint = buttonColor;
            _playButton.Label.Tint = labelColor;
            Add(_playButton);

            // not in the original: a hint for getting back to the launcher
            var back = new FlxText(2, FlxG.Height - 10, 0, "ESC: back to launcher");
            back.Tint = FlxColor.Rgb(0x3a5c39);
            Add(back);
        }

        if (!_fading)
        {
            // X + C were pressed: fade out and start the game
            if (FlxG.Keys.Pressed(Keys.X) && FlxG.Keys.Pressed(Keys.C))
            {
                _fading = true;
                FlxG.Sound.Play(ModeAssets.Sound("menu_hit_2"));

                FlxG.Cameras.Flash(FlxColor.Rgb(0xd8eba2), 0.5f);
                FlxG.Cameras.Fade(FlxColor.Rgb(0x131c1b), 1, false, OnFade);
            }
        }

        if (FlxG.Gamepad.JustPressed(Buttons.A)) OnPlay();

        if (FlxG.Keys.JustPressed(Keys.Escape)) ReturnToLauncher();
    }

    void OnPlay()
    {
        OnFade();
        FlxG.Sound.Play(ModeAssets.Sound("menu_hit_2"));
    }

    /// <summary>Called when the fade finishes: start playing.</summary>
    void OnFade() => FlxG.SwitchState(new PlayState());

    static void ReturnToLauncher()
    {
        FlxG.Sound.Destroy(true);
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = true;
        Demo.LauncherState.Return();
    }
}
