using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo.PostApoc;

/// <summary>The title screen: Start Game (or Continue, when there is a save), New Game, back to the launcher.</summary>
public class PostApocMenuState : FlxState
{
    /// <summary>Everything of PostApoc runs at the size of the launcher, so switching between them changes nothing.</summary>
    public const int Width = LauncherState.Width, Height = LauncherState.Height, Zoom = LauncherState.Zoom;

    public static void Open() => FlxG.SwitchState(new PostApocMenuState(), Width, Height, Zoom);

    /// <summary>A dark vertical gradient: the sky over a dead city.</summary>
    internal static FlxSprite Sky()
    {
        var tex = new Texture2D(FlxG.GraphicsDevice, 1, Height);
        var px = new Color[Height];
        for (int y = 0; y < Height; y++)
        {
            float t = y / (float)(Height - 1);
            px[y] = new Color((int)(26 + 70 * t * t), (int)(14 + 22 * t), (int)(14 + 10 * t));
        }
        tex.SetData(px);
        var sky = new FlxSprite(0, 0).LoadGraphic(tex);
        sky.Scale = new Vector2(Width, 1);
        sky.Origin = Vector2.Zero;
        sky.Moves = false;
        return sky;
    }

    float _time;
    FlxText _title = null!;

    public override void Create()
    {
        FlxG.Game.Window.Title = "POST APOC";
        FlxG.Cameras.BgColor = Color.Black;
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = true;
        FlxG.Mouse.LoadCursor("postapoc/cursor.png");

        Add(Sky());

        _title = new FlxText(0, 56, Width, "POST APOC", 32) { Alignment = FlxTextAlign.Center, Tint = new Color(210, 90, 60) };
        _title.SetBorderStyle(FlxTextBorderStyle.Outline, new Color(20, 8, 6), 2);
        Add(_title);
        // the tagline types itself
        const string tagline = "the road goes on...";
        var typed = new FlxTypeText(0, 100, 0, tagline) { Tint = new Color(150, 110, 90), ShowCursor = true, Delay = 0.09f };
        typed.X = (Width - typed.MeasureLine(tagline)) / 2f;
        typed.SetTypingVariation(0.5f);
        Add(typed);
        new FlxTimer().Start(0.8f, _ => typed.Start(callback: () => new FlxTimer().Start(1.5f, __ => typed.ShowCursor = false)));

        Add(new FlxText(0, Height - 46, Width, "A game by Scott Chipman and Joe McCormick") { Alignment = FlxTextAlign.Center, Tint = new Color(190, 140, 110) });

        bool saved = PostApocData.HasSave;
        float y = 150;

        FlxButton Button(string label, System.Action action)
        {
            var b = new FlxButton(Width / 2f - 40, y, label, action);
            Add(b);
            y += 28;
            return b;
        }

        if (saved) Button("Continue", Continue);
        Button(saved ? "New Game" : "Start Game", NewGame);
        Button("Back", Back);

        Add(new FlxText(0, Height - 24, Width, saved ? "ENTER: continue    N: new game    ESC: back" : "ENTER: start    ESC: back") { Alignment = FlxTextAlign.Center, Tint = new Color(110, 90, 80) });
        FlxG.Camera.Fade(Color.Black, 0.6f, fadeIn: true);
    }

    bool _going;

    void Go(FlxState next)
    {
        if (_going) return;
        _going = true;
        FlxG.Camera.Fade(Color.Black, 0.6f, onComplete: () => FlxG.SwitchState(next, Width, Height, Zoom));
    }

    void Continue() => Go(new PostApocPlayState(true, false));
    void NewGame() => Go(new PostApocSetupState());
    void Back() { if (!_going) LauncherState.Return(); }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);
        _time += elapsed;
        _title.Y = 56 + MathF.Sin(_time * 1.5f) * 2;

        if (FlxG.Keys.AnyJustPressed(Keys.Enter, Keys.Space) || FlxG.Gamepad.JustPressed(FlxGamepadInputID.A))
        {
            if (PostApocData.HasSave) Continue(); else NewGame();
        }
        if (FlxG.Keys.JustPressed(Keys.N)) NewGame();
        if (FlxG.Keys.JustPressed(Keys.Escape)) Back();
    }
}

/// <summary>Pick the survivor: male or female (the same two pictures as the original).</summary>
public class PostApocSetupState : FlxState
{
    int _choice;
    FlxSprite[] _portraits = null!;
    FlxText[] _labels = null!;
    bool _going;

    public override void Create()
    {
        FlxG.Game.Window.Title = "POST APOC";
        FlxG.Mouse.Visible = true;
        FlxG.Mouse.LoadCursor("postapoc/cursor.png");

        Add(PostApocMenuState.Sky());
        Add(new FlxText(0, 40, PostApocMenuState.Width, "WHO ARE YOU?", 16) { Alignment = FlxTextAlign.Center, Tint = new Color(210, 90, 60) });

        string[] names = { "Man", "Woman" };
        string[] images = { "postapoc/player_sprites/male_button.png", "postapoc/player_sprites/female_button.png" };
        _portraits = new FlxSprite[2];
        _labels = new FlxText[2];
        for (int i = 0; i < 2; i++)
        {
            float x = PostApocMenuState.Width / 2f + (i == 0 ? -160 : 32);
            _portraits[i] = new FlxSprite(x, 110).LoadGraphic(images[i]);
            _portraits[i].Scale = new Vector2(2, 2);
            _portraits[i].Origin = Vector2.Zero;
            _portraits[i].Moves = false;
            Add(_portraits[i]);
            _labels[i] = new FlxText(x, 250, 128, names[i]) { Alignment = FlxTextAlign.Center };
            Add(_labels[i]);

            int index = i;
            FlxMouseEvent.Add(_portraits[i], onMouseOver: _ => _choice = index, pixelPerfect: false);
            FlxMouseEvent.SetClickCallback(_portraits[i], _ => Start(index == 1));
        }

        Add(new FlxText(0, PostApocMenuState.Height - 24, PostApocMenuState.Width, "LEFT / RIGHT choose    ENTER start    ESC back")
        { Alignment = FlxTextAlign.Center, Tint = new Color(110, 90, 80) });
        FlxG.Camera.Fade(Color.Black, 0.5f, fadeIn: true);
    }

    void Start(bool female)
    {
        if (_going) return;
        _going = true;
        PostApocData.EraseSave();
        FlxG.Camera.Fade(Color.Black, 0.6f, onComplete: () =>
            FlxG.SwitchState(new PostApocPlayState(false, female), PostApocMenuState.Width, PostApocMenuState.Height, PostApocMenuState.Zoom));
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        if (FlxG.Keys.AnyJustPressed(Keys.Left, Keys.A, Keys.Right, Keys.D) || FlxG.Gamepad.AnyJustPressed(FlxGamepadInputID.DPadLeft, FlxGamepadInputID.DPadRight)) _choice = 1 - _choice;
        for (int i = 0; i < 2; i++)
        {
            bool on = i == _choice;
            _portraits[i].Tint = on ? Color.White : new Color(110, 100, 100);
            _labels[i].Tint = on ? new Color(255, 230, 160) : new Color(130, 110, 100);
        }

        if (FlxG.Keys.AnyJustPressed(Keys.Enter, Keys.Space) || FlxG.Gamepad.JustPressed(FlxGamepadInputID.A)) Start(_choice == 1);
        if (FlxG.Keys.JustPressed(Keys.Escape)) PostApocMenuState.Open();
    }
}
