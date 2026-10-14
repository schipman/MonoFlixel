using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// Port of the HaxeFlixel FlxFSM demo (art and sounds from flixel-demos; music "Happy New Year" by 8-BITek, CC BY-NC-ND 3.0).
/// A slime whose abilities are states in a state machine: pick up the powerups and the transition table grows
/// (jump becomes a super jump, and a ground pound appears).
/// </summary>
public class FsmDemoState : FlxState
{
    const string BaseInfo = "LEFT & RIGHT to move, UP to jump";
    const string GroundPoundInfo = "DOWN (in the air) to ground-pound";
    const string ResetInfo = "R to Reset\nESC: back\n\nCurrent State: {STATE}";

    FlxTilemap _map = null!;
    Slime _slime = null!;
    FlxSprite _superJump = null!, _groundPound = null!;
    FlxText _txtInfo = null!;
    string _info = BaseInfo + "\n" + ResetInfo;

    public override void Create()
    {
        FlxG.Game.Window.Title = "FlxFSM";
        FlxG.Cameras.BgColor = new Color(0x66, 0x11, 0x66);

        FlxG.Sound.PlayMusic("fsm/happynewyear.ogg", 0f);
        FlxG.Sound.Music?.FadeIn(2f, 0f, 0.2f);

        const int J = 99, G = 100, columns = 20, tileSize = 16;
        int[] data =
        {
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 1,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, G, 0, 1,
            1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1,
            1, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1,
            1, 0, 0, J, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        };

        // take the powerups out of the map, remember where they go
        int superJumpIndex = Array.IndexOf(data, J); data[superJumpIndex] = 0;
        int groundPoundIndex = Array.IndexOf(data, G); data[groundPoundIndex] = 0;

        _map = new FlxTilemap().LoadMapFromArray(data, 20, 15, "fsm/tiles.png", tileSize, tileSize);
        Add(_map);

        _slime = new Slime(192, 128);
        Add(_slime);

        _superJump = new FlxSprite((superJumpIndex % columns) * tileSize, (superJumpIndex / columns) * tileSize, "fsm/powerup.png");
        Add(_superJump);

        _groundPound = new FlxSprite((groundPoundIndex % columns) * tileSize, (groundPoundIndex / columns) * tileSize + 4, "fsm/powerup.png") { FlipY = true };
        Add(_groundPound);

        _txtInfo = new FlxText(16, 16, 288, _info);
        Add(_txtInfo);
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        FlxG.Collide(_map, _slime);

        FlxG.Overlap(_slime, _superJump, (_, _) =>
        {
            FlxG.Sound.Play("fsm/powerup.ogg");
            _slime.AddSuperJump();
            _superJump.Kill();
        });

        FlxG.Overlap(_slime, _groundPound, (_, _) =>
        {
            FlxG.Sound.Play("fsm/powerup.ogg");
            _slime.AddGroundPound();
            _groundPound.Kill();
            StartGroundPoundInfoTween();
        });

        _txtInfo.Text = _info.Replace("{STATE}", _slime.Fsm.StateClass?.Name ?? "");

        if (FlxG.Keys.JustReleased(Keys.R)) FlxG.Camera.Flash(Color.Black, 0.1f, FlxG.ResetState);
        if (FlxG.Keys.JustPressed(Keys.Escape)) { FlxG.Sound.Music?.Stop(); LauncherState.Return(); }
    }

    // the new hint types itself out in flashing yellow
    void StartGroundPoundInfoTween()
    {
        const float duration = 2f, flashPeriod = 0.15f / duration;
        var oldColor = _txtInfo.Tint;
        FlxTween.Num(0, 1, duration, n =>
        {
            _txtInfo.Tint = (n / flashPeriod) % 1 > 0.5f ? Color.Yellow : oldColor;
            int chars = (int)(n * GroundPoundInfo.Length);
            _info = BaseInfo + "\n" + GroundPoundInfo[..Math.Min(chars, GroundPoundInfo.Length)] + "\n" + ResetInfo;
        }, new TweenOptions
        {
            OnComplete = _ =>
            {
                _txtInfo.Tint = oldColor;
                _info = BaseInfo + "\n" + GroundPoundInfo + "\n" + ResetInfo;
            },
        });
    }
}

/// <summary>The slime: a sprite whose behaviour is an FSM. Its abilities are transitions, added as it picks things up.</summary>
public class Slime : FlxSprite
{
    public const float Gravity = 600;

    public FlxFSM<Slime> Fsm;

    public Slime(float x = 0, float y = 0) : base(x, y)
    {
        LoadGraphic("fsm/slime.png", true, 16, 16);
        SetFacingFlip(FlxDirection.Left, true, false);
        SetFacingFlip(FlxDirection.Right, false, false);
        Facing = FlxDirection.Right;

        Animation.Add("standing", new[] { 0, 1 }, 3);
        Animation.Add("walking", new[] { 0, 1 }, 12);
        Animation.Add("jumping", new[] { 2 });
        Animation.Add("pound", new[] { 3 });
        Animation.Add("landing", new[] { 4, 0, 1, 0 }, 8, false);

        Acceleration.Y = Gravity;
        MaxVelocity = new Vector2(100, Gravity);

        Fsm = new FlxFSM<Slime>(this);
        Fsm.Transitions.Add(typeof(Idle), typeof(Jump), Conditions.Jump);
        Fsm.Transitions.Add(typeof(Jump), typeof(Idle), Conditions.Grounded);
        Fsm.Transitions.Start(typeof(Idle));
    }

    public void AddSuperJump()
    {
        // remove the regular jump now
        Fsm.Transitions.Remove(typeof(Idle), typeof(Jump), Conditions.Jump, removeNow: true);
        Fsm.Transitions.Add(typeof(Idle), typeof(SuperJump), Conditions.Jump);
        // replace the rest when it is safe (not while the slime is in the old jump)
        Fsm.Transitions.Replace(typeof(Jump), typeof(SuperJump), removeNow: false);
    }

    public void AddGroundPound()
    {
        Fsm.Transitions.Add(typeof(GroundPound), typeof(GroundPoundFinish), Conditions.Grounded);
        Fsm.Transitions.Add(typeof(GroundPoundFinish), typeof(Idle), Conditions.AnimationFinished);

        if (Fsm.Transitions.Has(typeof(Idle), typeof(Jump))) Fsm.Transitions.Add(typeof(Jump), typeof(GroundPound), Conditions.GroundSlam);
        if (Fsm.Transitions.Has(typeof(Idle), typeof(SuperJump))) Fsm.Transitions.Add(typeof(SuperJump), typeof(GroundPound), Conditions.GroundSlam);
    }

    public override void Update(float elapsed)
    {
        Fsm.Update(elapsed);
        base.Update(elapsed);
    }

    public override void Destroy()
    {
        Fsm.Dispose();
        base.Destroy();
    }
}

public static class Conditions
{
    public static bool Jump(Slime owner) => FlxG.Keys.JustPressed(Keys.Up) && owner.IsTouching(FlxDirection.Down);
    public static bool Grounded(Slime owner) => owner.IsTouching(FlxDirection.Down);
    public static bool GroundSlam(Slime owner) => FlxG.Keys.JustPressed(Keys.Down) && !owner.IsTouching(FlxDirection.Down);
    public static bool AnimationFinished(Slime owner) => owner.Animation.Finished;
}

public class Idle : FlxFSMState<Slime>
{
    FlxSound? _walkSnd;

    public override void Enter(Slime owner, FlxFSM<Slime> fsm)
    {
        _walkSnd = FlxG.Sound.Load("fsm/walk.ogg", 0.4f);
        owner.Animation.Play("standing");
    }

    public override void Update(float elapsed, Slime owner, FlxFSM<Slime> fsm)
    {
        owner.Acceleration.X = 0;
        bool left = FlxG.Keys.Pressed(Keys.Left), right = FlxG.Keys.Pressed(Keys.Right);
        if (left || right)
        {
            _walkSnd?.Play();
            owner.Facing = left ? FlxDirection.Left : FlxDirection.Right;
            owner.Animation.Play("walking");
            owner.Acceleration.X = left ? -300 : 300;
        }
        else
        {
            owner.Animation.Play("standing");
            owner.Velocity.X *= 0.9f;
        }
    }

    public override void Exit(Slime owner) => _walkSnd?.Stop();
}

public class Jump : FlxFSMState<Slime>
{
    public override void Enter(Slime owner, FlxFSM<Slime> fsm)
    {
        FlxG.Sound.Play("fsm/jump.ogg", FlxG.Random.Float(0.9f, 1f));
        owner.Animation.Play("jumping");
        owner.Velocity.Y = -200;
    }

    public override void Update(float elapsed, Slime owner, FlxFSM<Slime> fsm)
    {
        owner.Acceleration.X = 0;
        if (FlxG.Keys.Pressed(Keys.Left) || FlxG.Keys.Pressed(Keys.Right))
            owner.Acceleration.X = FlxG.Keys.Pressed(Keys.Left) ? -300 : 300;
    }
}

public class SuperJump : Jump
{
    public override void Enter(Slime owner, FlxFSM<Slime> fsm)
    {
        FlxG.Sound.Play("fsm/superjump.ogg", FlxG.Random.Float(0.9f, 1f));
        owner.Animation.Play("jumping");
        owner.Velocity.Y = -300;
    }
}

public class GroundPound : FlxFSMState<Slime>
{
    float _time;

    public override void Enter(Slime owner, FlxFSM<Slime> fsm)
    {
        FlxG.Sound.Play("fsm/groundpound.ogg");
        owner.Animation.Play("pound");
        owner.Velocity.X = 0;
        owner.Acceleration.X = 0;
        _time = 0;
    }

    public override void Update(float elapsed, Slime owner, FlxFSM<Slime> fsm)
    {
        _time += elapsed;
        // hang for a moment, then slam down
        owner.Velocity.Y = _time < 0.25f ? 0 : Slime.Gravity;
    }
}

public class GroundPoundFinish : FlxFSMState<Slime>
{
    public override void Enter(Slime owner, FlxFSM<Slime> fsm)
    {
        FlxG.Sound.Play("fsm/groundpoundfinish.ogg");
        owner.Animation.Play("landing");
        FlxG.Camera.Shake(0.025f, 0.25f);
        owner.Velocity.X = 0;
        owner.Acceleration.X = 0;
    }
}
