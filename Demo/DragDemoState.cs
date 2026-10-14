using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// Flixel Power Tools' mouse sprites: FlxExtendedSprite (drag, throw with gravity, snap to a grid, a spring), FlxMouseControl,
/// FlxCollision (pixel perfect tests, camera walls) and FlxButtonPlus. Drag the boxes and throw them at the walls, drag the card (it
/// snaps to the grid), press on the ball (a spring pulls it to the mouse), and drag the two rings together to see the difference between
/// bounding boxes touching and pixels touching. ESC goes back.
/// </summary>
public class DragDemoState : FlxState
{
    const int W = 480, H = 270;
    const string Hint = "Drag and throw the boxes. Drag the card (snaps). Press the ball (spring). ESC: back";

    // the arena: x, y, width, height
    static readonly Rectangle Arena = new(8, 24, 300, 214);

    FlxTypedGroup<FlxExtendedSprite> _boxes = null!;
    FlxGroup _walls = null!;
    FlxExtendedSprite _ringA = null!, _ringB = null!, _card = null!;
    FlxText _info = null!, _verdict = null!, _cardLabel = null!;

    public override void Create()
    {
        FlxG.Game.Window.Title = "Power Tools: mouse sprites";
        FlxG.Cameras.BgColor = new Color(18, 22, 34);
        FlxG.Mouse.Visible = true;

        Add(new FlxMouseControl());                      // needs to be in the state for the extended sprites below

        // the arena, with a see-through grid on it, walled in by four blocks around a camera-sized rectangle
        var floor = new FlxSprite(Arena.X, Arena.Y).MakeGraphic(Arena.Width, Arena.Height, new Color(28, 34, 52));
        FlxGridOverlay.Overlay(floor, 16, 16, Arena.Width, Arena.Height, true, new Color(255, 255, 255, 10), new Color(0, 0, 0, 20));
        Add(floor);
        _walls = FlxCollision.CreateCameraWall(new FlxCamera(Arena.X, Arena.Y, Arena.Width, Arena.Height), FlxCameraWall.Outside, 8);

        // boxes with gravity: drag and throw them
        _boxes = new FlxTypedGroup<FlxExtendedSprite>();
        Add(_boxes);
        var colors = new[] { new Color(230, 90, 90), new Color(240, 180, 70), new Color(90, 200, 120), new Color(90, 150, 240) };
        for (int i = 0; i < 4; i++)
        {
            var box = new FlxExtendedSprite(40 + i * 54, 60 + (i % 2) * 20);
            box.MakeGraphic(28 + i * 4, 28 + i * 4, colors[i]);
            box.Elasticity = 0.55f;
            box.SetGravity(0, 400, 220, 220, 30, 30);
            box.EnableMouseDrag(boundsRect: Arena);
            box.EnableMouseThrow(14, 14);
            _boxes.Add(box);
        }

        // a card that snaps to the grid when dropped
        _card = new FlxExtendedSprite(50, 190);
        _card.MakeGraphic(32, 32, new Color(250, 250, 250));
        _card.EnableMouseDrag(boundsRect: Arena);
        _card.EnableMouseSnap(16, 16, onDrag: false, onRelease: true);
        Add(_card);
        _cardLabel = new FlxText(0, 0, 32, "snap") { Alignment = FlxTextAlign.Center, Tint = Color.Black };
        Add(_cardLabel);

        // a ball on a spring: press on it and it follows the mouse with a wobble
        var ball = new FlxExtendedSprite(240, 190);
        ball.MakeGraphic(20, 20, new Color(255, 120, 200));
        ball.EnableMouseSpring(onPressed: true, tension: 0.12f, friction: 0.9f);
        ball.SpringOffsetX = ball.SpringOffsetY = 10;
        Add(ball);

        // two rings with a hollow middle: drag one onto the other
        _ringA = MakeRing(340, 50, new Color(120, 220, 255));
        _ringB = MakeRing(400, 100, new Color(255, 200, 100));
        Add(_ringA);
        Add(_ringB);
        _verdict = new FlxText(320, 150, 150, "");
        Add(_verdict);

        // buttons
        Add(new FlxButtonPlus(326, 190, () => { foreach (var b in _boxes.Members) b.FlipGravity(); }, "Flip gravity", 138, 20));
        var reset = new FlxButtonPlus(326, 214, FlxG.ResetState, "Reset", 138, 20)
        {
            OnMouseOver = () => _info.Text = "Reset puts everything back.",
            OnMouseOut = () => _info.Text = Hint,
            OffColors = new[] { new Color(0, 70, 130), new Color(0, 130, 210) },
            OnColors = new[] { new Color(150, 90, 0), new Color(240, 160, 0) },
        };
        Add(reset);

        _info = new FlxText(8, 4, W - 16, Hint);
        Add(_info);
    }

    /// <summary>A ring with a hollow middle: the boxes of two rings can touch while the rings themselves do not.</summary>
    static FlxExtendedSprite MakeRing(float x, float y, Color color)
    {
        const int size = 40;
        var px = new Color[size * size];
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float d = MathF.Sqrt((i - 19.5f) * (i - 19.5f) + (j - 19.5f) * (j - 19.5f));
                if (d is > 11f and < 19.5f) px[j * size + i] = color;
            }
        var tex = new Texture2D(FlxG.GraphicsDevice, size, size);
        tex.SetData(px);

        var ring = new FlxExtendedSprite(x, y);
        ring.LoadGraphic(tex);
        ring.EnableMouseDrag(pixelPerfect: true, boundsRect: new Rectangle(316, 24, 160, 120));   // only the solid pixels grab it
        return ring;
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        FlxG.Collide(_walls, _boxes);
        FlxG.Collide(_boxes, _boxes);

        _cardLabel.X = _card.X;
        _cardLabel.Y = _card.Y + 12;

        bool boxes = _ringA.Overlaps(_ringB);
        bool pixels = FlxCollision.PixelPerfectCheck(_ringA, _ringB);
        _verdict.Text = $"boxes touch: {(boxes ? "yes" : "no")}\npixels touch: {(pixels ? "YES" : "no")}";
        _verdict.Tint = pixels ? new Color(255, 120, 120) : new Color(180, 190, 210);

        if (FlxG.Keys.JustPressed(Keys.Escape)) LauncherState.Return();
    }
}
