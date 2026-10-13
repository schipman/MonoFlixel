using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo.Drown;

public enum Tool { Harpoon, Laser }

/// <summary>
/// The controls of the dive, as actions: each can come from the keyboard, the mouse or a gamepad (left stick moves, right stick aims,
/// A jumps, right trigger fires). FlxG.Actions clears its sets when the state changes, so the dive makes the set again every time.
/// </summary>
public sealed class DrownInput
{
    public readonly FlxActionSet Set = new("drown");
    public readonly FlxActionAnalog Move, Aim;
    public readonly FlxActionDigital Jump, Float, Fire, FireHeld, HarpoonTool, LaserTool, Drill, Inventory, Light, Surface, Back;

    public DrownInput()
    {
        Move = Set.Add(new FlxActionAnalog("move")
            .AddKeys(Keys.W, Keys.S, Keys.A, Keys.D)
            .AddKeys(Keys.Up, Keys.Down, Keys.Left, Keys.Right)
            .AddGamepad(FlxGamepadInputID.LeftAnalogStick));
        Aim = Set.Add(new FlxActionAnalog("aim").AddGamepad(FlxGamepadInputID.RightAnalogStick));

        Jump = Set.Add(new FlxActionDigital("jump").AddKey(Keys.Space, FlxInputState.Pressed).AddGamepad(FlxGamepadInputID.A, FlxInputState.Pressed));
        Float = Set.Add(new FlxActionDigital("float").AddKey(Keys.Tab).AddGamepad(FlxGamepadInputID.B));
        Fire = Set.Add(new FlxActionDigital("fire").AddMouse(FlxMouseButtonID.Left).AddGamepad(FlxGamepadInputID.RightTrigger));
        FireHeld = Set.Add(new FlxActionDigital("fireHeld").AddMouse(FlxMouseButtonID.Left, FlxInputState.Pressed).AddGamepad(FlxGamepadInputID.RightTrigger, FlxInputState.Pressed));
        HarpoonTool = Set.Add(new FlxActionDigital("harpoon").AddKey(Keys.D1).AddGamepad(FlxGamepadInputID.LeftShoulder));
        LaserTool = Set.Add(new FlxActionDigital("laser").AddKey(Keys.D2).AddGamepad(FlxGamepadInputID.RightShoulder));
        Drill = Set.Add(new FlxActionDigital("drill").AddKey(Keys.Z));
        Inventory = Set.Add(new FlxActionDigital("inventory").AddKey(Keys.I).AddGamepad(FlxGamepadInputID.Y));
        Light = Set.Add(new FlxActionDigital("light").AddKey(Keys.L).AddGamepad(FlxGamepadInputID.X));
        Surface = Set.Add(new FlxActionDigital("surface").AddKey(Keys.R).AddGamepad(FlxGamepadInputID.Back));
        Back = Set.Add(new FlxActionDigital("back").AddKey(Keys.Escape).AddGamepad(FlxGamepadInputID.Start));

        FlxG.Actions.Activate(Set);
    }
}

/// <summary>
/// The diver. Sinks, floats up with TAB (it costs air), jumps by holding SPACE for a while. Aims with the mouse, shoots harpoons (1)
/// or fires the mining laser (2). Air runs down slowly, creatures and the pressure hurt the suit; if either runs out the diver dies
/// and starts again at the surface without the things carried.
/// </summary>
public class DrownPlayer : FlxSprite
{
    public const float MaxHealth = 100, MaxOxygen = 100;

    readonly DrownDiveState _world;
    readonly DrownInput _input;

    public readonly FlxSprite Arm, Laser;
    public readonly FlxEmitter Sparks;

    public float Oxygen = MaxOxygen;
    public Tool Tool = Tool.Harpoon;
    public bool Floating;
    public bool Drill;                  // the suit drill: mines the rock next to you while walking into it
    public bool MenuOpen;
    public bool IsDead { get; private set; }
    public bool CanRevive { get; private set; }

    public float AimAngle { get; private set; }
    public Vector2 AimVector { get; private set; } = Vector2.UnitX;
    public Vector2 GunPoint { get; private set; }

    /// <summary>The laser is on this frame; <see cref="LaserHit"/> is where it ends (on rock, or at full reach in open water).</summary>
    public bool LaserOn { get; private set; }
    public Vector2 LaserHit { get; private set; }
    public bool LaserHitsRock { get; private set; }

    const float DefaultGravity = 60, Buoyancy = -70, LaserReach = 110;

    float _jump, _breath, _deadTime;
    int _lastFootFrame = -1;
    string _armImage = "";
    bool _hasAimedWithStick;
    FlxSound? _laserLoop;

    public DrownPlayer(DrownDiveState world, DrownInput input, float x, float y) : base(x, y)
    {
        _world = world;
        _input = input;
        Health = MaxHealth;

        LoadGraphic("drown/player_atlas.png", animated: true, frameWidth: 32, frameHeight: 64);
        Animation.Add("idle", new[] { 0 });
        Animation.Add("run", new[] { 1, 2, 3, 5 }, 6);
        Animation.Add("jump", new[] { 2 });
        Animation.Add("dead", new[] { 0, 6 }, 4, looped: false);

        // the hitbox is smaller than the picture
        Width = 30;
        Height = 46;
        Offset = new Vector2(2, 17);

        Acceleration.Y = DefaultGravity;
        MaxVelocity = new Vector2(70, 80);
        Drag = new Vector2(MaxVelocity.X * 10, 200);

        Arm = new FlxSprite(x, y).LoadGraphic("drown/arm_cannon.png");
        Arm.Origin = new Vector2(6, 6);
        Arm.Moves = false;
        _armImage = "right";

        Laser = new FlxSprite(x, y).LoadGraphic("drown/laser_anim.png", animated: true, frameWidth: 16, frameHeight: 3);
        Laser.Animation.Add("beam", FlxAnimationController.Range(0, 15), 60);
        Laser.Animation.Play("beam");
        Laser.Origin = new Vector2(0, 1.5f);
        Laser.Moves = false;
        Laser.Visible = false;
        Laser.Blend = FlxBlend.Add;

        Sparks = new FlxEmitter();
        Sparks.LoadParticles("drown/sparktest.png", 40);
        Sparks.Velocity.Set(-30, -30, 30, 45);
        Sparks.Acceleration.Set(0, 70, 0, 70);
        Sparks.AngularVelocity.Set(-20, 20);
        Sparks.Lifespan.Set(0.3f, 0.7f);
    }

    /// <summary>Back to the start with full air and health.</summary>
    public void Respawn(Vector2 at)
    {
        IsDead = CanRevive = false;
        _deadTime = 0;
        Health = MaxHealth;
        Oxygen = MaxOxygen;
        Floating = false;
        Velocity = Vector2.Zero;
        Reset(at.X, at.Y);
        Arm.Visible = true;
        Acceleration.Y = DefaultGravity;
    }

    public override void Hurt(float damage)
    {
        if (IsDead) return;
        Health -= damage;
        FlxFlicker.Flicker(this, 0.5f);
        FlxFlicker.Flicker(Arm, 0.5f);
    }

    public override void Update(float elapsed)
    {
        if (!IsDead && (Health <= 0 || Oxygen <= 0)) Die();

        if (IsDead)
        {
            _deadTime += elapsed;
            if (_deadTime > 3) CanRevive = true;
            if (Animation.CurrentName != "dead") Animation.Play("dead", true);     // once: it stays on the last frame
            Acceleration.X = 0;
            LaserOn = false;
            Laser.Visible = false;
            StopLaserSound();
            base.Update(elapsed);
            return;
        }

        HandleTools();
        Aim();
        Move(elapsed);
        Breathe(elapsed);

        base.Update(elapsed);

        // stay inside the site
        X = Math.Clamp(X, 0, _world.Map.Width - Width);
        Y = Math.Clamp(Y, 0, _world.Map.Height - Height);

        AttachArm();
        UpdateLaser();
        Footsteps();

        BreatheOut(elapsed);
    }

    float _bubbleTimer;

    /// <summary>Bubbles from the helmet, of all sizes: a steady stream, a few more when moving.</summary>
    void BreatheOut(float elapsed)
    {
        _bubbleTimer -= elapsed;
        if (_bubbleTimer > 0) return;

        bool busy = Math.Abs(Velocity.X) > 5 || Velocity.Y < -10;
        _bubbleTimer = FlxG.Random.Float(0.05f, busy ? 0.16f : 0.26f);

        float size = BubblePool.RandomSize();
        var mouth = new Vector2(X + Width * 0.5f + (FlipX ? 4 : -4) + FlxG.Random.Float(-3f, 3f), Y + 8);
        _world.Bubbles.Spawn(mouth, new Vector2(FlxG.Random.Float(-8f, 8f), -FlxG.Random.Float(18f, 50f)), size, FlxG.Random.Float(1.2f, 3.2f));

        // every so often a short string of them
        if (FlxG.Random.Bool(7)) _world.Bubbles.Burst(mouth, new Vector2(0, -1), 4, 40f, 10f);
    }

    void Die()
    {
        IsDead = true;
        _deadTime = 0;
        Acceleration = Vector2.Zero;
        Velocity = Vector2.Zero;
        Arm.Visible = false;
        Floating = false;
        DrownData.Sfx("air_release");
    }

    // ------------------------------------------------------------------ tools

    void HandleTools()
    {
        if (_input.HarpoonTool.Triggered) SelectTool(Tool.Harpoon);
        if (_input.LaserTool.Triggered) SelectTool(Tool.Laser);
        if (_input.Drill.Triggered) Drill = !Drill;
    }

    void SelectTool(Tool tool)
    {
        Tool = tool;
        if (tool != Tool.Laser) { LaserOn = false; Laser.Visible = false; StopLaserSound(); }
    }

    // ------------------------------------------------------------------ aiming

    void Aim()
    {
        var armCentre = new Vector2(Arm.X + 6, Arm.Y + 6);
        Vector2 aim;

        var stick = _input.Aim.Value;
        if (stick.Length() > 0.35f)
        {
            aim = stick;                             // the right stick: aim with it
            _hasAimedWithStick = true;
        }
        else if (_hasAimedWithStick && !FlxG.Mouse.JustMoved)
        {
            aim = AimVector;                          // keep the last stick direction until the mouse moves again
        }
        else
        {
            _hasAimedWithStick = false;
            aim = FlxG.Mouse.WorldPosition - armCentre;
        }

        if (aim.LengthSquared() < 0.0001f) aim = AimVector;
        aim.Normalize();
        AimVector = aim;
        AimAngle = MathHelper.ToDegrees(MathF.Atan2(aim.Y, aim.X));

        // aiming to the right turns the diver around
        FlipX = aim.X > 0;      // the diver's picture looks to the left
    }

    void AttachArm()
    {
        var armCentre = new Vector2(Arm.X + 6, Arm.Y + 6);

        string image = FlipX ? "right" : "left";
        if (image != _armImage)
        {
            Arm.LoadGraphic(FlipX ? "drown/arm_cannon.png" : "drown/arm_cannon_2.png");
            Arm.Origin = new Vector2(6, 6);
            _armImage = image;
        }

        Arm.X = X + (FlipX ? 5 : 11);
        Arm.Y = Y + (FlipX ? 3 : 4);
        Arm.Angle = AimAngle;

        armCentre = new Vector2(Arm.X + 6, Arm.Y + 6);
        GunPoint = armCentre + AimVector * 29f;
    }

    void UpdateLaser()
    {
        bool want = Tool == Tool.Laser && !MenuOpen && _input.FireHeld.Triggered;
        if (!want)
        {
            LaserOn = false;
            Laser.Visible = false;
            StopLaserSound();
            return;
        }

        LaserOn = true;

        // the beam goes on until it meets rock, or its reach ends
        var end = GunPoint + AimVector * LaserReach;
        LaserHitsRock = !_world.Map.Ray(GunPoint, end, out var hit);
        LaserHit = hit;

        float length = Vector2.Distance(GunPoint, hit);
        Laser.X = GunPoint.X;
        Laser.Y = GunPoint.Y;
        Laser.Angle = AimAngle;
        Laser.Scale = new Vector2(Math.Max(0.1f, length / 16f), 1f);
        Laser.Visible = length > 1f;

        if (_laserLoop == null) _laserLoop = DrownData.Loop("laser_loop", 0.6f);
        if (_laserLoop is { Playing: false }) { _laserLoop.Play(); }
    }

    void StopLaserSound()
    {
        if (_laserLoop is { Playing: true }) _laserLoop.Pause();
    }

    public void ReleaseSounds()
    {
        _laserLoop?.Stop();
        _laserLoop = null;
    }

    // ------------------------------------------------------------------ moving

    void Move(float elapsed)
    {
        // jumping: hold the key for up to a second of upwards push; it comes back when you stand on something
        // a push from the stick counts only when it is clearly up (a worn stick drifts a little)
        bool jumpKey = _input.Jump.Triggered || _input.Move.Y < -0.7f;
        if (jumpKey)
        {
            if (_jump >= 0)
            {
                _jump += elapsed;
                if (_jump > 1f) _jump = -1;
            }
        }
        else _jump = IsTouching(FlxDirection.Down) ? 0 : -1;       // ready again once the key is let go on the floor

        if (_jump > 0) Velocity.Y = _jump < 0.75f ? -120 : -MaxVelocity.Y;

        // sideways
        Acceleration.X = _input.Move.X * MaxVelocity.X * 4;

        // buoyancy: TAB lets air into the suit to float up, TAB again lets it out
        if (_input.Float.Triggered)
        {
            if (!Floating)
            {
                Oxygen -= 10;
                Floating = true;
                DrownData.Sfx("air_intake");
                _world.Bubbles.Burst(Midpoint, new Vector2(0, -1), 6, 50f, 25f);
            }
            else
            {
                Floating = false;
                DrownData.Sfx("air_release");
                DrownData.Sfx("air_bubbles_1", 0.6f);
                _world.Bubbles.Burst(new Vector2(X + Width * 0.5f, Y + 10), new Vector2(0, -1), 22, 90f, 45f);
            }
        }
        Acceleration.Y = Floating ? Buoyancy : DefaultGravity;

        // which way the suit drill digs
        if (Drill)
        {
            if (_input.Move.X < -0.5f) _drillDirection = new Point(-1, 0);
            else if (_input.Move.X > 0.5f) _drillDirection = new Point(1, 0);
            else if (_input.Move.Y < -0.5f) _drillDirection = new Point(0, -1);
            else if (_input.Move.Y > 0.5f) _drillDirection = new Point(0, 1);
        }

        string anim = Velocity.Y != 0 ? "jump" : Velocity.X == 0 ? "idle" : "run";
        Animation.Play(anim);
    }

    Point _drillDirection = new(0, 1);
    public Point DrillDirection => _drillDirection;

    // ------------------------------------------------------------------ air

    void Breathe(float elapsed)
    {
        // near the surface the suit fills up again
        if (Y < (DiveLevel.SurfaceRows - 2) * DiveLevel.Tile)
        {
            Oxygen = Math.Min(MaxOxygen, Oxygen + 15 * elapsed);
            return;
        }

        _breath += elapsed;
        if (_breath >= 2f)
        {
            _breath = 0;
            Oxygen -= 0.5f;
        }
    }

    void Footsteps()
    {
        if (Animation.CurrentName == "run" && IsTouching(FlxDirection.Down))
        {
            int frame = Animation.FrameNumber;
            if (frame != _lastFootFrame && (frame == 0 || frame == 2)) DrownData.Sfx(frame == 0 ? "step_1" : "step_2", 0.5f);
            _lastFootFrame = frame;
        }
        else _lastFootFrame = -1;
    }
}
