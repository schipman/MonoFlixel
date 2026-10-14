using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>The Flixel Power Tools SpecialFX effects, one in each cell. SPACE restarts the center slide, ESC goes back.</summary>
public class SpecialFxDemoState : FlxState
{
    CenterSlideFX _slide = null!;
    FlxSprite _ball = null!;

    public override void Create()
    {
        FlxG.Game.Window.Title = "SpecialFX";
        FlxG.Cameras.BgColor = new Color(12, 12, 24);
        FlxSpecialFX.Clear();

        // a picture to play with: part of a Drown map
        var src = FxBitmap.FromTexture(FlxAssets.GetTexture("drown/overworld.png"), new Rectangle(16, 40, 128, 64));
        var pic = new Texture2D(FlxG.GraphicsDevice, src.Width, src.Height);
        pic.SetData(src.Data);

        const int w = 200, h = 100;
        Cell(0, 0, "Plasma", FlxSpecialFX.Plasma().Create(0, 0, w / 2, h / 2, 2, 2));
        Cell(1, 0, "Starfield 2D", FlxSpecialFX.Starfield().Create(0, 0, w, h, 150));
        Cell(2, 0, "Starfield 3D", FlxSpecialFX.Starfield().Create(0, 0, w, h, 120, StarfieldType.Warp3D, 30));

        Cell(0, 1, "Sine wave", FlxSpecialFX.SineWave().CreateFromTexture(pic, 0, 0, SineWaveType.VerticalSine, 16, 128, 4));
        Cell(1, 1, "Glitch", FlxSpecialFX.Glitch().CreateFromTexture(pic, 0, 0, 12, 4));
        _slide = FlxSpecialFX.CenterSlide();
        Cell(2, 1, "Center slide (SPACE)", _slide.CreateFromTexture(pic, 0, 0, CenterSlideMode.RevealVertical, 2));

        Cell(0, 2, "Flood fill", FlxSpecialFX.FloodFill().Create(pic, 0, 0, 128, 64, 1));
        Cell(1, 2, "Copper", FlxSpecialFX.WowCopper().Create(0, 0, w, h));

        // blur: a ball that leaves a soft trail (the ball itself is never drawn, only stamped on the blur canvas)
        var blur = FlxSpecialFX.Blur();
        var blurSprite = blur.Create(w, h, 3, 3);
        _ball = new FlxSprite(60, 40).MakeGraphic(10, 10, Color.Orange);
        _ball.Velocity = new Vector2(90, 55);
        blur.AddSprite(_ball, false);
        Cell(2, 2, "Blur trail", blurSprite);

        var rainbow = FlxSpecialFX.RainbowLine().Create(10, FlxG.Height - 28, FlxG.Width - 20, 8, colorSpeed: 3, stepSize: 2);
        Add(rainbow);

        Add(new FlxText(0, FlxG.Height - 12, FlxG.Width, "SPACE: restart center slide    ESC: back") { Alignment = FlxTextAlign.Center, Tint = new Color(110, 160, 190) });
    }

    // places an effect's sprite in a 3x3 grid, with a label above it
    void Cell(int col, int row, string label, FlxSprite s)
    {
        float x = 10 + col * 210, y = 22 + row * 104;
        s.X = x; s.Y = y;
        Add(s);
        Add(new FlxText(x, y - 11, 200, label) { Tint = new Color(190, 235, 255) });
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);
        _ball.Update(elapsed);
        // keep the blur ball inside its cell
        if (_ball.X < 0 || _ball.X > 188) _ball.Velocity = new Vector2(-_ball.Velocity.X, _ball.Velocity.Y);
        if (_ball.Y < 0 || _ball.Y > 88) _ball.Velocity = new Vector2(_ball.Velocity.X, -_ball.Velocity.Y);

        if (FlxG.Keys.JustPressed(Keys.Space))
        {
            if (_slide.Complete) _slide.Reverse();
        }
        if (FlxG.Keys.JustPressed(Keys.Escape)) { FlxSpecialFX.Clear(); LauncherState.Return(); }
    }
}
