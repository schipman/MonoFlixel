using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo.PostApoc;

/// <summary>Everything that stands on the road is drawn in the order of how far down the screen its feet are, so the ones in front cover the ones behind.</summary>
public sealed class YSortedGroup : FlxTypedGroup<FlxObject>
{
    public override void Update(float elapsed)
    {
        base.Update(elapsed);
        Sort((order, a, b) => FlxSort.ByValues(FlxSort.Ascending, a.Y + a.Height, b.Y + b.Height));
    }
}

/// <summary>The controls, as actions: keyboard and gamepad (and the mouse for the windows, which have their own events).</summary>
public sealed class PostApocInput
{
    public readonly FlxActionSet Set = new("postapoc");
    public readonly FlxActionAnalog Move;
    public readonly FlxActionDigital Fire, Inventory, Equipment, Save, Back;
    public readonly FlxActionDigital[] Quick = new FlxActionDigital[PostApocData.QuickSlots];

    public PostApocInput()
    {
        Move = Set.Add(new FlxActionAnalog("move")
            .AddKeys(Keys.W, Keys.S, Keys.A, Keys.D)
            .AddKeys(Keys.Up, Keys.Down, Keys.Left, Keys.Right)
            .AddGamepad(FlxGamepadInputID.LeftAnalogStick));
        Fire = Set.Add(new FlxActionDigital("fire").AddKey(Keys.Z, FlxInputState.Pressed).AddKey(Keys.Space, FlxInputState.Pressed).AddGamepad(FlxGamepadInputID.X, FlxInputState.Pressed));
        Inventory = Set.Add(new FlxActionDigital("inventory").AddKey(Keys.I).AddGamepad(FlxGamepadInputID.Y));
        Equipment = Set.Add(new FlxActionDigital("equipment").AddKey(Keys.U).AddGamepad(FlxGamepadInputID.Back));
        Save = Set.Add(new FlxActionDigital("save").AddKey(Keys.P));
        Back = Set.Add(new FlxActionDigital("back").AddKey(Keys.Escape).AddGamepad(FlxGamepadInputID.Start));
        for (int i = 0; i < Quick.Length; i++)
            Quick[i] = Set.Add(new FlxActionDigital("quick" + (i + 1)).AddKey(Keys.D1 + i));

        FlxG.Actions.Activate(Set);
    }
}

/// <summary>Where the characters can walk: the road, as the range of the y of their feet.</summary>
public static class Road
{
    public const float MinY = 214, MaxY = 312;
}

/// <summary>The survivor. Walks along the road (all four ways), shoots a pistol, gets hungry and thirsty.</summary>
public class PostApocPlayer : FlxSprite
{
    public const int MaxHealth = 100, MaxHunger = 100, MaxThirst = 100, MaxFatigue = 100;

    // how fast the needs grow: a little with time, mostly with every step (a room is 2560 pixels long)
    public const float HungerPerPixel = 0.008f, ThirstPerPixel = 0.012f, FatiguePerPixel = 0.012f;
    public const float HungerPerSecond = 1f / 12f, ThirstPerSecond = 1f / 8f;

    readonly PostApocInput _input;

    public readonly FlxSprite Gun;
    public new int Health = MaxHealth;
    public float Hunger, Thirst, Fatigue;
    public int Ammo = PostApocData.StartingAmmo;
    public readonly RunStats Stats = new();
    public bool Resting;
    /// <summary>Inside a doorway for a moment: nothing can hurt the survivor.</summary>
    public bool Hidden;
    /// <summary>Why the survivor died (shown on the game over screen).</summary>
    public string Cause { get; private set; } = "";
    public bool Female;
    public bool Control = true;
    public bool IsDead { get; private set; }
    public bool Facing_Left { get; private set; }

    /// <summary>Raised when the pistol fires: where the bullet starts and which way it goes (-1 or 1).</summary>
    public event Action<Vector2, int>? Fired;

    /// <summary>
    /// Raised halfway through a swing (what Z does with no bullets left): where the blow lands (in front of the survivor) and which
    /// way it goes (-1 or 1).
    /// </summary>
    public event Action<Vector2, int>? Swung;

    /// <summary>Raised when a swing starts (so the game can say what is going on).</summary>
    public event Action? SwingStarted;

    public bool Swinging { get; private set; }
    float _swingStart = -1, _swingHit;
    bool _swingLanded;

    /// <summary>Raised when something hurts the survivor: where the blood comes from and which way it sprays (-1, 0 for any, 1).</summary>
    public event Action<Vector2, int>? Bled;

    float _fireReady, _gunUntil, _starve, _time;
    Vector2 _lastPosition;
    bool _first = true;
    readonly string _sheet;

    public PostApocPlayer(float x, float y, bool female, PostApocInput input) : base(x, y)
    {
        _input = input;
        Female = female;
        _sheet = "postapoc/player_sprites/" + (female ? "female_comp.png" : "male_comp.png");
        LoadGraphic(_sheet, animated: true, frameWidth: 64, frameHeight: 64);

        Animation.Add("idle", new[] { 0 }, 1, false);
        Animation.Add("walk", FlxAnimationController.Range(0, 8), 10);
        Animation.Add("gunidle", new[] { 9 }, 1, false);
        Animation.Add("gunwalk", FlxAnimationController.Range(9, 17), 10);
        Animation.Add("die", FlxAnimationController.Range(36, 44), 8, false);
        Animation.Add("swing", FlxAnimationController.Range(27, 34), 16, false);       // the fourth row of the sheet
        Animation.Play("idle");

        // the hitbox is the footprint: the feet decide who is in front
        Width = 28; Height = 14;
        Offset = new Vector2(18, 48);

        const float runSpeed = 80;
        Drag = new Vector2(runSpeed * 8, runSpeed * 8);
        MaxVelocity = new Vector2(runSpeed, runSpeed * 0.8f);

        Gun = new FlxSprite(x, y).LoadGraphic("postapoc/player_sprites/pistol_anim.png", animated: true, frameWidth: 64, frameHeight: 64);
        Gun.Animation.Add("fire", new[] { 0, 1, 0 }, 10, false);
        Gun.Width = Width; Gun.Height = Height; Gun.Offset = Offset;
        Gun.Moves = false;
        Gun.Visible = false;
    }

    /// <summary>Changes the character (the loaded save may be the other one).</summary>
    public void SetFemale(bool female)
    {
        Female = female;
        LoadGraphic("postapoc/player_sprites/" + (female ? "female_comp.png" : "male_comp.png"), animated: true, frameWidth: 64, frameHeight: 64);
        Width = 28; Height = 14;
        Offset = new Vector2(18, 48);
    }

    public override void Hurt(float damage) => Hurt(damage, "You did not make it.");

    /// <param name="fromDirection">Which way the blow came (the blood flies that way): -1 left, 1 right, 0 either. Hunger and thirst do not bleed.</param>
    public void Hurt(float damage, string cause, int fromDirection = 0, bool bleeds = true)
    {
        if (IsDead || Hidden) return;
        if (bleeds) Bled?.Invoke(new Vector2(X + Width * 0.5f, Y - 30), fromDirection);
        Health -= (int)damage;
        FlxFlicker.Flicker(this, 0.3f);
        FlxG.Camera.Shake(0.005f, 0.1f);
        FlxG.Camera.Flash(new Color(140, 0, 0), 0.2f);

        if (Health <= 0)
        {
            Health = 0;
            Die(cause);
        }
    }

    void Die(string cause)
    {
        if (IsDead) return;
        IsDead = true;
        Cause = cause;
        Control = false;
        Gun.Visible = false;
        Swinging = false;
        Velocity = Vector2.Zero;
        Acceleration = Vector2.Zero;
    }

    /// <summary>Eating, drinking, medicine.</summary>
    public void Use(ItemDef item)
    {
        Hunger = Math.Clamp(Hunger + item.Hunger, 0, MaxHunger);
        Thirst = Math.Clamp(Thirst + item.Thirst, 0, MaxThirst);
        Health = Math.Clamp(Health + item.Health, 0, MaxHealth);
        Fatigue = Math.Clamp(Fatigue + item.Fatigue, 0, MaxFatigue);
    }

    public override void Update(float elapsed)
    {
        _time += elapsed;

        if (IsDead)
        {
            if (Animation.CurrentName != "die") Animation.Play("die", true);       // once: it stays on the last frame
            base.Update(elapsed);
            return;
        }

        // ---- the needs: they grow with time and with every step; at the limit hunger and thirst hurt, and fatigue kills
        var here = new Vector2(X, Y);
        float walked = _first ? 0 : Vector2.Distance(here, _lastPosition);
        _lastPosition = here;
        _first = false;
        Stats.Distance += walked;
        if (!Resting)
        {
            Hunger = Math.Min(MaxHunger, Hunger + elapsed * HungerPerSecond + walked * HungerPerPixel);
            Thirst = Math.Min(MaxThirst, Thirst + elapsed * ThirstPerSecond + walked * ThirstPerPixel);
            Fatigue = Math.Min(MaxFatigue, Fatigue + walked * FatiguePerPixel);
        }

        if (Fatigue >= MaxFatigue) { Die("You collapsed from exhaustion."); return; }

        if (Hunger >= MaxHunger || Thirst >= MaxThirst)
        {
            _starve += elapsed;
            if (_starve > 2f)
            {
                _starve = 0;
                Hurt(2, Hunger >= MaxHunger ? "You starved." : "You died of thirst.", bleeds: false);
                if (IsDead) return;
            }
        }

        // ---- walking
        var move = Control && !Resting ? _input.Move.Value : Vector2.Zero;
        Acceleration = new Vector2(move.X * Drag.X, move.Y * Drag.Y);
        if (move.X < -0.1f) { Facing_Left = true; }
        else if (move.X > 0.1f) { Facing_Left = false; }
        FlipX = Facing_Left;                        // the sheet looks to the right

        // ---- the pistol
        // no bullets: Z is a swing instead (it lasts half a second and lands in the middle of it)
        if (Control && !Resting && !Swinging && _input.Fire.Triggered && _time > _fireReady && Ammo <= 0)
        {
            Swinging = true;
            _swingLanded = false;
            _swingStart = _time;
            _fireReady = _time + 0.6f;
            Animation.Play("swing", true);
            SwingStarted?.Invoke();
        }
        else if (!Swinging && Control && !Resting && _input.Fire.Triggered && _time > _fireReady)
        {
            Ammo--;
            _fireReady = _time + 0.6f;
            _gunUntil = _time + 3f;
            PostApocData.Sfx("pistol_gunshot", 0.5f);
            Gun.Visible = true;
            Gun.Animation.Play("fire", true);

            // the muzzle in the picture is at (50, 17) looking right
            float muzzleX = Facing_Left ? X - Offset.X + 64 - 52 : X - Offset.X + 52;
            Fired?.Invoke(new Vector2(muzzleX, Y - Offset.Y + 17), Facing_Left ? -1 : 1);
        }
        bool armed = _time < _gunUntil;

        if (Swinging)
        {
            Acceleration = Vector2.Zero;                          // a swing stops the walk
            Velocity *= 0.5f;
            if (!_swingLanded && _time - _swingStart > 0.22f)
            {
                _swingLanded = true;
                Swung?.Invoke(new Vector2(X + Width * 0.5f + (Facing_Left ? -26 : 26), Y), Facing_Left ? -1 : 1);
            }
            if (_time - _swingStart > 0.5f) Swinging = false;
        }
        else
        {
            string anim = Velocity.LengthSquared() < 1f ? (armed ? "gunidle" : "idle") : (armed ? "gunwalk" : "walk");
            Animation.Play(anim);
        }

        base.Update(elapsed);

        // ---- stay on the road
        if (X < 0) X = 0;
        Y = Math.Clamp(Y, Road.MinY, Road.MaxY);

        // the pistol's flash is drawn over the survivor, a hair in front
        Gun.X = X; Gun.Y = Y + 0.25f;
        Gun.FlipX = FlipX;
        if (Gun.Visible && Gun.Animation.Finished) Gun.Visible = false;
    }

    public void ResetPosition(float x, float y)
    {
        X = x; Y = y;
        Velocity = Vector2.Zero;
        Facing_Left = false;
        _first = true;               // a jump to the start of a room is not a walk (no fatigue for it)
    }
}

/// <summary>A pistol bullet: flies along its lane, ends at the edge of the room or when it hits something.</summary>
public sealed class Bullet : FlxSprite
{
    public float Lane;       // the y of the feet of whoever it can hit
    /// <summary>Fired by an enemy: it hurts the survivor instead.</summary>
    public readonly bool Hostile;
    float _life;

    public Bullet(bool hostile = false)
    {
        Hostile = hostile;
        MakeGraphic(7, 2, hostile ? new Color(255, 90, 60) : new Color(255, 230, 120));
        Exists = false;
    }

    public void Shoot(Vector2 from, int direction, float lane, float speed = 460f)
    {
        Reset(from.X, from.Y);
        Lane = lane;
        Velocity = new Vector2(direction * speed, 0);
        _life = 0;
        Visible = Active = true;
    }

    public override void Update(float elapsed)
    {
        _life += elapsed;
        if (_life > 1.1f) { Kill(); return; }
        base.Update(elapsed);
    }
}

public enum EnemyKind { Human, Dog, Gunman }

/// <summary>
/// A bandit or a dog. Both stand about until the survivor comes close, then run at them and bite or hit (after a moment's warning).
/// Two bullets bring one down. The original had one class for each; they were the same except for the numbers and the pictures.
/// </summary>
public class PostApocEnemy : FlxSprite
{
    enum State { Idle, Seeking, Attacking, Dead }

    public readonly EnemyKind Kind;
    readonly PostApocPlayer _player;
    readonly Action<PostApocEnemy> _died;
    State _state = State.Idle;
    float _attackTimer, _cooldown;
    readonly float _speed;
    float _aggro = 220f;
    readonly int _damage;
    readonly Action<Vector2, int, float>? _shoot;
    float _aim, _reload = 1f;

    public bool IsDead => _state == State.Dead;

    /// <summary>How far away it notices the survivor.</summary>
    public float Aggro => _aggro;

    /// <summary>How many bullets it leaves behind (the chance, 0 to 1, and how many).</summary>
    public (float Chance, int Count) AmmoDrop => Kind switch
    {
        EnemyKind.Human => (0.4f, 3),
        EnemyKind.Gunman => (0.7f, 3),
        _ => (0f, 0),
    };

    public PostApocEnemy(float x, float y, EnemyKind kind, PostApocPlayer player, Action<PostApocEnemy> died, Action<Vector2, int, float>? shoot = null) : base(x, y)
    {
        Kind = kind;
        _player = player;
        _died = died;
        _shoot = shoot;
        if (kind == EnemyKind.Gunman) _aggro = 400f;

        if (kind == EnemyKind.Human)
        {
            LoadGraphic("postapoc/mobs/bandit_male_comp.png", animated: true, frameWidth: 64, frameHeight: 64);
            Animation.Add("run", FlxAnimationController.Range(18, 26), 15);
            Animation.Add("idle", new[] { 18 }, 1, false);
            Animation.Add("attack", FlxAnimationController.Range(27, 34), 15);
            Animation.Add("die", FlxAnimationController.Range(36, 44), 8, false);
            Width = 28; Height = 14; Offset = new Vector2(18, 48);
            Health = 40; _speed = 125; _damage = 8;        // fast: it runs you down
            _aggro = 380f;
            FlipX = true;                         // the sheet looks to the right, they start by looking left
        }
        else if (kind == EnemyKind.Gunman)
        {
            // a raider with a pistol: the other survivor's sheet, in darker clothes
            LoadGraphic("postapoc/player_sprites/" + (player.Female ? "male_comp.png" : "female_comp.png"), animated: true, frameWidth: 64, frameHeight: 64);
            Animation.Add("walk", FlxAnimationController.Range(9, 17), 10);
            Animation.Add("idle", new[] { 9 }, 1, false);
            Animation.Add("die", FlxAnimationController.Range(36, 44), 8, false);
            Width = 28; Height = 14; Offset = new Vector2(18, 48);
            Health = 30; _speed = 55; _damage = 10;
            Tint = new Color(150, 160, 205);
        }
        else
        {
            LoadGraphic("postapoc/mobs/dog_shepard_atlas.png", animated: true, frameWidth: 64, frameHeight: 41);
            Animation.Add("run", FlxAnimationController.Range(0, 7), 15);
            Animation.Add("idle", new[] { 2 }, 1, false);
            Width = 46; Height = 12; Offset = new Vector2(9, 27);
            Health = 25; _speed = 130; _damage = 6;
        }
        Animation.Play("idle");
        MaxVelocity = new Vector2(_speed, _speed);
    }

    public override void Update(float elapsed)
    {
        if (_state == State.Dead) { base.Update(elapsed); return; }

        _cooldown -= elapsed;

        if (Kind == EnemyKind.Gunman)
        {
            UpdateGunman(elapsed);
            base.Update(elapsed);
            Y = Math.Clamp(Y, Road.MinY, Road.MaxY);
            return;
        }

        var target = _player.Midpoint;
        var me = Midpoint;
        float distance = Vector2.Distance(me, target);
        bool touching = MathF.Abs(me.X - target.X) < 36 && MathF.Abs(Y - _player.Y) < 12;

        if (_state == State.Attacking)
        {
            Velocity = Vector2.Zero;
            _attackTimer -= elapsed;
            if (_attackTimer <= 0)
            {
                // the blow lands if the survivor is still there
                if (touching && !_player.IsDead)
                {
                    _player.Hurt(_damage, "You did not make it.", MathF.Sign(target.X - me.X));
                    if (Kind == EnemyKind.Dog) PostApocData.Sfx("dog_bark", 0.5f);
                }
                _state = State.Idle;
                _cooldown = 0.5f;
            }
        }
        else if (!_player.IsDead && distance < _aggro)
        {
            if (touching && _cooldown <= 0)
            {
                _state = State.Attacking;
                _attackTimer = 0.5f;
                Animation.Play(Kind == EnemyKind.Human ? "attack" : "idle", true);
            }
            else
            {
                _state = State.Seeking;
                var dir = new Vector2(target.X - me.X, _player.Y - Y);
                if (dir != Vector2.Zero) dir.Normalize();
                Velocity = dir * _speed;
                Animation.Play("run");
                float side = dir.X;
                if (Kind == EnemyKind.Human) { if (side > 0.1f) FlipX = false; else if (side < -0.1f) FlipX = true; }
                else { if (side > 0.1f) FlipX = false; else if (side < -0.1f) FlipX = true; }   // the dog's picture looks to the right, like the survivors
            }
        }
        else
        {
            // lost sight: stand still
            Velocity = Vector2.Zero;
            _state = State.Idle;
            Animation.Play("idle");
        }

        base.Update(elapsed);
        Y = Math.Clamp(Y, Road.MinY, Road.MaxY);
    }

    /// <summary>
    /// Keeps its distance (about 200 to 300 pixels), gets into the survivor's lane, then aims for a moment and shoots. Moving to another
    /// lane (up or down the road) makes it miss.
    /// </summary>
    void UpdateGunman(float elapsed)
    {
        var me = Midpoint;
        var target = _player.Midpoint;
        float dx = target.X - me.X, dy = _player.Y - Y;
        float distance = MathF.Abs(dx);
        FlipX = dx < 0;

        if (_player.IsDead || MathF.Abs(dx) > _aggro)
        {
            Velocity = Vector2.Zero;
            Animation.Play("idle");
            _aim = 0;
            return;
        }

        var move = Vector2.Zero;
        if (distance > 300) move.X = MathF.Sign(dx);
        else if (distance < 190) move.X = -MathF.Sign(dx);
        if (MathF.Abs(dy) > 8) move.Y = MathF.Sign(dy) * 0.8f;
        Velocity = move * _speed;
        Animation.Play(move != Vector2.Zero ? "walk" : "idle");

        _reload -= elapsed;
        bool lined = MathF.Abs(dy) <= 12 && distance is > 120 and < 340;
        if (lined && move == Vector2.Zero)
        {
            _aim += elapsed;
            if (_aim > 0.6f && _reload <= 0)
            {
                _aim = 0;
                _reload = 1.8f;
                PostApocData.Sfx("pistol_gunshot", 0.25f);
                int dir = dx < 0 ? -1 : 1;
                float muzzle = dir < 0 ? X - Offset.X + 64 - 52 : X - Offset.X + 52;
                _shoot?.Invoke(new Vector2(muzzle, Y - Offset.Y + 17), dir, Y);
            }
        }
        else _aim = 0;
    }

    /// <summary>A bullet hit.</summary>
    public void Shot(float damage, int direction)
    {
        if (_state == State.Dead) return;
        Health -= damage;
        FlxFlicker.Flicker(this, 0.2f);
        X += direction * 4;

        if (Health <= 0)
        {
            _state = State.Dead;
            Velocity = Vector2.Zero;
            Acceleration = Vector2.Zero;
            Immovable = true;
            if (Kind != EnemyKind.Dog) Animation.Play("die", true);
            else { Angle = 180; Animation.Play("idle"); }
            _died(this);
            FlxTween.Tween(this, new { Alpha = 0f }, 0.8f, new TweenOptions { StartDelay = 2.5f, OnComplete = _ => Kill() });
        }
    }
}

/// <summary>A few bullets, dropped by the ones who carried them. Walking over them fills the pistol.</summary>
public class AmmoPickup : FlxSprite
{
    public readonly int Count;
    static Microsoft.Xna.Framework.Graphics.Texture2D? _texture;

    public AmmoPickup(float x, float y, int count) : base(x, y)
    {
        Count = count;
        LoadGraphic(Texture());
        Width = 16; Height = 12;
        Offset = new Vector2(0, 4);
        Moves = false;
    }

    /// <summary>A small box of brass, made here.</summary>
    static Microsoft.Xna.Framework.Graphics.Texture2D Texture()
    {
        if (_texture != null) return _texture;
        const int w = 16, h = 16;
        var tex = new Microsoft.Xna.Framework.Graphics.Texture2D(FlxG.GraphicsDevice, w, h);
        var px = new Color[w * h];
        for (int y = 4; y < 15; y++)
            for (int x = 0; x < 16; x++)
            {
                bool edge = x == 0 || x == 15 || y == 4 || y == 14;
                px[y * w + x] = edge ? new Color(60, 48, 30) : new Color(96, 78, 44);
            }
        for (int i = 0; i < 4; i++)                       // four bullets in a row
        {
            int x = 2 + i * 3;
            for (int y = 5; y < 13; y++) px[y * w + x] = y < 8 ? new Color(190, 120, 60) : new Color(230, 190, 80);
        }
        tex.SetData(px);
        return _texture = tex;
    }
}

/// <summary>Something on the ground to pick up.</summary>
public class ItemPickup : FlxSprite
{
    public readonly ItemDef Def;

    /// <param name="x">The left of the footprint.</param>
    /// <param name="y">Where it lies on the road (the y of the feet of whoever could stand on it).</param>
    public ItemPickup(float x, float y, ItemDef def) : base(x, y)
    {
        Def = def;
        LoadGraphic(def.Texture);
        // a small box in the middle of the picture
        Width = 16; Height = 12;
        Offset = new Vector2(8, 20);
        Moves = false;
    }
}
