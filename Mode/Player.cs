using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Mode;

public class Player : FlxSprite, IHurt
{
    const float FireRate = 1f / 10f;   // 10 shots per second

    public bool IsReadyToJump = true;
    public bool Flickering;

    readonly FlxTimer _shootTimer = new();
    readonly int _jumpPower = 200;
    FlxDirection _aim = FlxDirection.Right;
    FlxEmitter? _gibs;
    FlxTypedGroup<Bullet>? _bullets;

    public Player(int x, int y, FlxTypedGroup<Bullet> bullets, FlxEmitter gibs) : base(x, y)
    {
        LoadGraphic(ModeAssets.Image("spaceman"), true, 8);

        SetFacingFlip(FlxDirection.Left, true, false);
        SetFacingFlip(FlxDirection.Right, false, false);

        // Bounding box tweaks
        Width = 6;
        Height = 7;
        Offset = new Vector2(1, 1);

        // Basic player physics
        int runSpeed = 80;
        Drag.X = runSpeed * 8;
        Acceleration.Y = 420;
        MaxVelocity = new Vector2(runSpeed, _jumpPower);

        // Animations
        Animation.Add("idle", new[] { 0 });
        Animation.Add("idle_up", new[] { 5 });
        Animation.Add("run", new[] { 1, 2, 3, 0 }, 12);
        Animation.Add("run_up", new[] { 6, 7, 8, 5 }, 12);
        Animation.Add("jump", new[] { 4 });
        Animation.Add("jump_up", new[] { 9 });
        Animation.Add("jump_down", new[] { 10 });

        _bullets = bullets;
        _gibs = gibs;
    }

    // ------------------------------------------------------------ input (keyboard + gamepad)

    static bool Up => FlxG.Keys.AnyPressed(Keys.Up, Keys.W) || FlxG.Gamepad.AnyPressed(Buttons.DPadUp, Buttons.LeftThumbstickUp);
    static bool Down => FlxG.Keys.AnyPressed(Keys.Down, Keys.S) || FlxG.Gamepad.AnyPressed(Buttons.DPadDown, Buttons.LeftThumbstickDown);
    static bool Left => FlxG.Keys.AnyPressed(Keys.Left, Keys.A) || FlxG.Gamepad.AnyPressed(Buttons.DPadLeft, Buttons.LeftThumbstickLeft);
    static bool Right => FlxG.Keys.AnyPressed(Keys.Right, Keys.D) || FlxG.Gamepad.AnyPressed(Buttons.DPadRight, Buttons.LeftThumbstickRight);
    static bool JumpPressed => FlxG.Keys.JustPressed(Keys.X) || FlxG.Gamepad.JustPressed(Buttons.A);
    static bool ShootPressed => FlxG.Keys.Pressed(Keys.C) || FlxG.Gamepad.Pressed(Buttons.X);

    public override void Update(float elapsed)
    {
        Acceleration.X = 0;
        UpdateInput();
        UpdateAnimations();
        base.Update(elapsed);
    }

    void UpdateInput()
    {
        if (Left) MoveLeft();
        else if (Right) MoveRight();

        if (Up) _aim = FlxDirection.Up;
        else if (Down) _aim = FlxDirection.Down;

        if (JumpPressed) Jump();
        if (ShootPressed) Shoot();
    }

    void UpdateAnimations()
    {
        if (Velocity.Y != 0)
        {
            Animation.Play(_aim switch
            {
                FlxDirection.Up => "jump_up",
                FlxDirection.Down => "jump_down",
                _ => "jump"
            });
        }
        else if (Velocity.X == 0)
        {
            Animation.Play(_aim == FlxDirection.Up ? "idle_up" : "idle");
        }
        else
        {
            Animation.Play(_aim == FlxDirection.Up ? "run_up" : "run");
        }
    }

    // ------------------------------------------------------------ health

    public void Hurt(int damage = 1)
    {
        if (Flickering) return;

        FlxG.Sound.Play(ModeAssets.Sound("hurt"));

        StartFlicker(1.3f);

        if (Reg.Score > 1000) Reg.Score -= 1000;

        if (Velocity.X > 0) Velocity.X = -MaxVelocity.X;
        else Velocity.X = MaxVelocity.X;
    }

    void StartFlicker(float duration)
    {
        FlxFlicker.Flicker(this, duration, 0.02f, true, _ => Flickering = false);
        Flickering = true;
    }

    public override void Kill()
    {
        if (!Alive) return;

        Solid = false;
        FlxG.Sound.Play(ModeAssets.Sound("asplode"));
        FlxG.Sound.Play(ModeAssets.Sound("menu_hit_2"));

        FlxFlicker.Stop(this, false);   // don't let a running flicker bring the dead player back
        base.Kill();

        Exists = true;
        Active = false;
        Visible = false;
        Moves = false;
        Velocity = Vector2.Zero;
        Acceleration = Vector2.Zero;
        FlxG.Camera.Shake(0.005f, 0.35f);
        FlxG.Camera.Flash(FlxColor.Rgb(0xd8eba2), 0.35f);

        if (_gibs != null)
        {
            _gibs.FocusOn(this);
            _gibs.Start(true, 0, 50);
        }

        new FlxTimer().Start(2, _ => FlxG.ResetState());
    }

    // ------------------------------------------------------------ actions

    void MoveLeft()
    {
        Facing = _aim = FlxDirection.Left;
        Acceleration.X -= Drag.X;
    }

    void MoveRight()
    {
        Facing = _aim = FlxDirection.Right;
        Acceleration.X += Drag.X;
    }

    void Jump()
    {
        if (IsReadyToJump && Velocity.Y == 0)
        {
            Velocity.Y = -_jumpPower;
            FlxG.Sound.Play(ModeAssets.Sound("jump"));
        }
    }

    void Shoot()
    {
        if (_shootTimer.Active) return;
        _shootTimer.Start(FireRate);

        if (Flickering)
        {
            FlxG.Sound.Play(ModeAssets.Sound("jam"));
        }
        else
        {
            _bullets!.Recycle(() => new Bullet()).Shoot(Midpoint, _aim);

            if (_aim == FlxDirection.Down) Velocity.Y -= 36;
        }
    }

    public override void Destroy()
    {
        base.Destroy();
        _bullets = null;
        _gibs = null;
    }
}
