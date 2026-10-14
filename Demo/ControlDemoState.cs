using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// Flixel Power Tools' FlxControl: one sprite, three ways to steer it, each set up with a few lines. 1: top-down (accelerates, slides to a
/// stop). 2: platformer (gravity, jump with SPACE, a short grace time after walking off an edge). 3: spaceship (left / right turn, UP thrusts,
/// DOWN reverses, SPACE fires an FlxWeapon, FlxDisplay.ScreenWrap brings it back in at the other side). Arrow keys steer. ESC goes back.
/// </summary>
public class ControlDemoState : FlxState
{
    const int W = 480, H = 270;

    enum Mode { TopDown, Platformer, Spaceship }

    FlxControl _control = null!;
    FlxControlHandler _handler = null!;
    FlxSprite _player = null!;
    FlxGroup _walls = null!;
    FlxTypedGroup<FlxSprite> _platforms = null!;
    FlxWeapon _gun = null!;
    FlxText _info = null!;
    Mode _mode;

    public override void Create()
    {
        FlxG.Game.Window.Title = "Power Tools: FlxControl";
        FlxG.Cameras.BgColor = new Color(16, 20, 32);
        FlxG.Mouse.Visible = true;

        // The control goes in before the sprites it moves (the jump reads what they touched in the last frame).
        _control = new FlxControl();
        Add(_control);

        var grid = FlxGridOverlay.Create(16, 16, W, H, true, new Color(22, 28, 44), new Color(26, 32, 50));
        if (grid != null) Add(grid);

        _walls = FlxCollision.CreateCameraWall(new FlxCamera(0, 0, W, H), FlxCameraWall.Inside, 8);
        foreach (var wall in _walls.Members) ((FlxSprite)wall).MakeGraphic((int)((FlxSprite)wall).Width, (int)((FlxSprite)wall).Height, new Color(60, 72, 104));
        Add(_walls);

        _platforms = new FlxTypedGroup<FlxSprite>();
        foreach (var (x, y, w) in new[] { (60, 190, 100), (220, 150, 90), (350, 200, 90), (120, 100, 70), (330, 90, 100) })
        {
            var p = new FlxSprite(x, y).MakeGraphic(w, 8, new Color(90, 110, 150));
            p.Immovable = true;
            p.Moves = false;
            _platforms.Add(p);
        }
        Add(_platforms);

        _player = new FlxSprite(W / 2f - 8, H - 40);
        _player.LoadGraphic(MakeShip());
        _player.SetFacingFlip(FlxDirection.Left, true, false);
        _player.SetFacingFlip(FlxDirection.Right, false, false);
        Add(_player);

        // the spaceship's laser: bullets leave from the middle of the ship
        _gun = new FlxWeapon("laser", _player) { BulletSpeed = 320, BulletLifeSpan = 900, CenterOnParent = true };
        _gun.MakePixelBullet(30, 3, 3, new Color(120, 255, 255));
        Add(_gun.Group);

        for (int i = 0; i < 3; i++)
        {
            int mode = i;
            var button = new FlxButtonPlus(12 + i * 100, H - 24 - 0, () => SetMode((Mode)mode), $"{i + 1}  {(Mode)i}", 94, 16);
            Add(button);
        }

        _info = new FlxText(8, 4, W - 16, "");
        Add(_info);

        SetMode(Mode.TopDown);
    }

    /// <summary>A small ship that points right (angle 0), the way FlxControl thrusts.</summary>
    static Texture2D MakeShip()
    {
        const int size = 16;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float half = (size - 1 - x) / 2f;          // the triangle is wide at the left, a point at the right
                if (MathF.Abs(y - 7.5f) <= half) px[y * size + x] = Color.Lerp(new Color(120, 230, 140), new Color(60, 150, 90), x / (float)size);
            }
        var tex = new Texture2D(FlxG.GraphicsDevice, size, size);
        tex.SetData(px);
        return tex;
    }

    void SetMode(Mode mode)
    {
        _mode = mode;

        // a clean sprite for the new way of steering
        _control.Remove(_handler!);
        _player.Velocity = Vector2.Zero;
        _player.Acceleration = Vector2.Zero;
        _player.Drag = Vector2.Zero;
        _player.MaxVelocity = Vector2.Zero;
        _player.Angle = 0;
        _player.AngularVelocity = 0;
        _player.AngularAcceleration = 0;
        _player.AngularDrag = 0;
        _player.Facing = FlxDirection.Right;
        _player.Reset(W / 2f - 8, H - 40);

        switch (mode)
        {
            case Mode.TopDown:
                // accelerate towards a top speed, slide to a stop
                _handler = _control.Create(_player, FlxControlMovement.Accelerates, FlxControlStopping.Decelerates);
                _handler.SetMovementSpeed(500, 500, 130, 130, 400, 400);
                _info.Text = "1 TOP-DOWN: arrows move, SetMovementSpeed(accel, accel, top, top, drag, drag)";
                break;

            case Mode.Platformer:
                // gravity is the downward acceleration; left / right only; jump from the floor, a moment of grace after an edge
                _handler = _control.Create(_player, FlxControlMovement.Accelerates, FlxControlStopping.Decelerates, updateFacing: true, enableArrowKeys: false);
                _handler.SetCursorControl(allowUp: false, allowDown: false);
                _handler.SetMovementSpeed(600, 0, 95, 420, 800, 0);
                _handler.SetGravity(0, 600);
                _handler.SetJumpButton(Keys.Space, FlxKeyMode.JustDown, 250, FlxDirection.Floor, 250, jumpFromFall: 120);
                _info.Text = "2 PLATFORMER: left / right, SPACE jumps (also just after walking off an edge)";
                break;

            case Mode.Spaceship:
                // turn left / right, thrust up, reverse down, fire with SPACE (repeat every 140 ms)
                _handler = _control.Create(_player, FlxControlMovement.Accelerates, FlxControlStopping.Decelerates, enableArrowKeys: false);
                _handler.SetMovementSpeed(0, 0, 170, 170, 40, 40);
                _handler.SetRotationSpeed(300, 300, 240, 500);
                _handler.SetRotationType(FlxControlRotation.Accelerates, FlxControlRotationStopping.Decelerates);
                _handler.SetThrust(Keys.Up, 280, Keys.Down, 140);
                _handler.SetFireButton(Keys.Space, FlxKeyMode.Pressed, 140, () => _gun.FireFromParentAngle());
                _player.Reset(W / 2f - 8, H / 2f - 8);
                _info.Text = "3 SPACESHIP: left / right turn, UP thrust, DOWN reverse, SPACE fire";
                break;
        }
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        if (_mode != Mode.Spaceship)
        {
            FlxG.Collide(_walls, _player);
            FlxG.Collide(_platforms, _player);
        }
        else FlxDisplay.ScreenWrap(_player);

        var k = FlxG.Keys;
        if (k.JustPressed(Keys.D1)) SetMode(Mode.TopDown);
        if (k.JustPressed(Keys.D2)) SetMode(Mode.Platformer);
        if (k.JustPressed(Keys.D3)) SetMode(Mode.Spaceship);
        if (k.JustPressed(Keys.Escape)) LauncherState.Return();
    }
}
