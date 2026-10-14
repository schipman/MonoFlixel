using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// Port of the HaxeFlixel FlxEffectSprite demo: one sprite sent through rainbow, outline, wave, glitch, trail and shake
/// effects. The buttons switch each effect on and off (SHAKE fires once); DIRECTION and MODE change the wave and glitch.
/// </summary>
public class EffectSpriteDemoState : FlxState
{
    FlxSprite _sprite = null!;
    FlxEffectSprite _effectSprite = null!;
    FlxRainbowEffect _rainbow = null!;
    FlxOutlineEffect _outline = null!;
    FlxWaveEffect _wave = null!;
    FlxGlitchEffect _glitch = null!;
    FlxTrailEffect _trail = null!;
    FlxShakeEffect _shake = null!;
    FlxTween _tween = null!;

    public override void Create()
    {
        FlxG.Game.Window.Title = "FlxEffectSprite";
        FlxG.Cameras.BgColor = new Color(0x22, 0x22, 0x33);

        // the target sprite is not added to the state: the effect sprite shows it
        _sprite = new FlxSprite().LoadGraphic("flixel/flags.png", true, 140, 140);
        _sprite.Animation.Add("anim", new[] { 0, 1, 2, 3 }, 3, true);
        _sprite.Animation.Play("anim");
        _sprite.ScreenCenter();

        _effectSprite = new FlxEffectSprite(_sprite);
        Add(_effectSprite);

        _rainbow = new FlxRainbowEffect(0.5f);
        _outline = new FlxOutlineEffect(FlxOutlineMode.Fast, Color.Red, 8);
        _wave = new FlxWaveEffect(FlxWaveMode.All);
        _glitch = new FlxGlitchEffect(10, 2, 0.1f);
        _trail = new FlxTrailEffect(_effectSprite, 10, 0.5f, 8);
        _shake = new FlxShakeEffect(10, 1);
        _effectSprite.Effects.AddRange(new IFlxEffect[] { _rainbow, _outline, _wave, _glitch, _trail, _shake });

        CreateButtons();

        _tween = FlxTween.CircularMotion(_effectSprite, FlxG.Width * 0.5f - _sprite.Width / 2, FlxG.Height * 0.5f - _sprite.Height / 2,
            _sprite.Width, 359, true, 3, true, new TweenOptions { Type = FlxTweenType.PingPong });

        Add(new FlxText(0, 6, FlxG.Width, "ESC: back") { Alignment = FlxTextAlign.Center, Tint = new Color(150, 150, 170) });
    }

    void CreateButtons()
    {
        ToggleButton(30, 400, "Rainbow", _rainbow);
        ToggleButton(130, 400, "Outline", _outline);
        ToggleButton(230, 400, "Wave", _wave);
        ToggleButton(330, 400, "Glitch", _glitch);
        ToggleButton(430, 400, "Trail", _trail);
        Add(new FlxButton(530, 400, "Shake", () => _shake.Start()));

        Add(new FlxButton(230, 425, "Direction", () =>
            _wave.Direction = _wave.Direction == FlxWaveDirection.Vertical ? FlxWaveDirection.Horizontal : FlxWaveDirection.Vertical));
        Add(new FlxButton(230, 450, "Mode", () =>
            _wave.Mode = _wave.Mode switch { FlxWaveMode.All => FlxWaveMode.Start, FlxWaveMode.Start => FlxWaveMode.End, _ => FlxWaveMode.All }));
        Add(new FlxButton(330, 425, "Direction", () =>
            _glitch.Direction = _glitch.Direction == FlxGlitchDirection.Vertical ? FlxGlitchDirection.Horizontal : FlxGlitchDirection.Vertical));
    }

    void ToggleButton(float x, float y, string label, IFlxEffect effect)
    {
        var button = new FlxButton(x, y, label);
        button.OnUp = () =>
        {
            effect.Active = !effect.Active;
            button.Alpha = effect.Active ? 1f : 0.6f;
        };
        Add(button);
    }

    public override void Update(float elapsed)
    {
        // without the trail the sprite stays put (and the tween stops)
        _tween.Paused = !_trail.Active;
        if (!_trail.Active) { _effectSprite.X = _sprite.X; _effectSprite.Y = _sprite.Y; }

        base.Update(elapsed);
        if (FlxG.Keys.JustPressed(Keys.Escape)) LauncherState.Return();
    }
}
