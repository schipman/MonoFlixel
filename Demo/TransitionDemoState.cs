using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// Port of the HaxeFlixel Transitions demo. Pick how the screen leaves (OUT) and enters (IN): fade or tiles, color,
/// duration, direction. TRANSITION! (or SPACE) switches between STATE A and STATE B using those settings.
/// </summary>
public class TransitionDemoState : FlxTransitionableState
{
    sealed class Side
    {
        public float Duration = 1f;
        public TransitionType Type = TransitionType.Fade;
        public string Tile = "diamond", Color = "white", Dir = "center";
    }

    // the settings live for the whole run, so they carry over from state A to state B and back
    static readonly Side In = new(), Out = new();
    static bool _initialized;

    readonly bool _isB;

    public TransitionDemoState(bool isB = false)
    {
        _isB = isB;

        if (!_initialized)
        {
            _initialized = true;

            // Every FlxTransitionableState takes these as its TransIn / TransOut unless it gets its own.
            DefaultTransIn = new TransitionData(TransitionType.Fade, Color.White, 1f, null, new TransitionTileData(FlxTransitionAssets.Diamond, 32, 32));
            DefaultTransOut = new TransitionData(TransitionType.Fade, Color.White, 1f, null, new TransitionTileData(FlxTransitionAssets.Diamond, 32, 32));
        }
        TransIn = DefaultTransIn;
        TransOut = DefaultTransOut;
    }

    public override void Create()
    {
        FlxG.Game.Window.Title = "MonoFlixel - transitions";
        FlxG.Cameras.BgColor = Color.Black;
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = true;

        Add(new FlxSprite().MakeGraphic(FlxG.Width, FlxG.Height, _isB ? new Color(0, 0, 0xAA) : new Color(0xAA, 0, 0)));

        var welcome = new FlxText(0, 6, FlxG.Width, _isB ? "STATE B" : "STATE A", 16) { Alignment = FlxTextAlign.Center };
        welcome.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black, 1);
        Add(welcome);

        BuildSide("IN:", 30, In);
        BuildSide("OUT:", 350, Out);

        Add(new FlxButton(FlxG.Width / 2f - 40, FlxG.Height - 60, "TRANSITION!", Transition));

        var hint = new FlxText(0, FlxG.Height - 14, FlxG.Width, "Click the options.  SPACE: transition   ESC: back to the launcher")
            { Alignment = FlxTextAlign.Center, Tint = new Color(230, 230, 230) };
        hint.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black, 1);
        Add(hint);

        ApplySettings();

        // the in-transition starts here (it is a substate of this state)
        base.Create();
    }

    // ------------------------------------------------------------------ UI

    FlxText Label(float x, float y, string text)
    {
        var t = new FlxText(x, y, 0, text);
        t.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black, 1);
        Add(t);
        return t;
    }

    void BuildSide(string title, float x0, Side side)
    {
        Label(x0, 30, title);

        // duration: [-] 1.0 [+]
        Label(x0 + 5, 50, "DURATION:");
        var value = Label(x0 + 100, 50, side.Duration.ToString("0.0"));
        Add(new ClickText(x0 + 84, 50, "-", () =>
        {
            side.Duration = MathF.Round(Math.Max(0.1f, side.Duration - 0.1f), 1);
            value.Text = side.Duration.ToString("0.0");
            ApplySettings();
        }));
        Add(new ClickText(x0 + 126, 50, "+", () =>
        {
            side.Duration = MathF.Round(Math.Min(3f, side.Duration + 0.1f), 1);
            value.Text = side.Duration.ToString("0.0");
            ApplySettings();
        }));

        // type
        Label(x0 + 5, 80, "TYPE:");
        var types = new Dictionary<string, TransitionType> { ["NONE"] = TransitionType.None, ["FADE"] = TransitionType.Fade, ["TILES"] = TransitionType.Tiles };

        // tile shape (only for the Tiles type)
        var tileLabel = Label(x0 + 65, 80, "TILE:");
        var tileList = new OptionList(x0 + 65, 96, new[] { "DIAMOND", "CIRCLE", "SQUARE" }, side.Tile.ToUpperInvariant(), v =>
        {
            side.Tile = v.ToLowerInvariant();
            ApplySettings();
        });
        Add(tileList);

        void ShowTileOptions()
        {
            bool tiles = side.Type == TransitionType.Tiles;
            tileLabel.Visible = tileList.Visible = tileList.Active = tiles;
        }

        string current = side.Type switch { TransitionType.None => "NONE", TransitionType.Tiles => "TILES", _ => "FADE" };
        Add(new OptionList(x0 + 5, 96, new[] { "NONE", "FADE", "TILES" }, current, v =>
        {
            side.Type = types[v];
            ShowTileOptions();
            ApplySettings();
        }));
        ShowTileOptions();

        // color
        Label(x0 + 135, 80, "COLOR:");
        Add(new OptionList(x0 + 135, 96, new[] { "BLACK", "WHITE", "BLUE", "GREEN", "RED" }, side.Color.ToUpperInvariant(), v =>
        {
            side.Color = v.ToLowerInvariant();
            ApplySettings();
        }));

        // direction
        Label(x0 + 195, 80, "DIRECTION:");
        Add(new OptionList(x0 + 195, 96, new[] { "CENTER", "N", "S", "W", "E", "NW", "NE", "SW", "SE" }, side.Dir.ToUpperInvariant(), v =>
        {
            side.Dir = v.ToLowerInvariant();
            ApplySettings();
        }));
    }

    // ------------------------------------------------------------------ settings -> transition data

    static void ApplySettings()
    {
        Apply(In, DefaultTransIn!);
        Apply(Out, DefaultTransOut!);
    }

    static void Apply(Side s, TransitionData d)
    {
        d.Type = s.Type;
        d.Duration = s.Duration;
        d.Color = s.Color switch
        {
            "white" => Color.White,
            "blue" => Color.Blue,
            "green" => Color.Green,
            "red" => Color.Red,
            _ => Color.Black
        };
        d.Direction = s.Dir switch
        {
            "n" => new Vector2(0, -1),
            "s" => new Vector2(0, 1),
            "w" => new Vector2(-1, 0),
            "e" => new Vector2(1, 0),
            "nw" => new Vector2(-1, -1),
            "ne" => new Vector2(1, -1),
            "sw" => new Vector2(-1, 1),
            "se" => new Vector2(1, 1),
            _ => Vector2.Zero
        };

        d.TileData ??= new TransitionTileData();
        d.TileData.Asset = s.Tile switch
        {
            "circle" => FlxTransitionAssets.Circle,
            "square" => FlxTransitionAssets.Square,
            _ => FlxTransitionAssets.Diamond
        };
    }

    // ------------------------------------------------------------------ switching

    void Transition()
    {
        // plays this state's TransOut, then the next state plays its TransIn
        FlxG.SwitchState(new TransitionDemoState(!_isB));
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        if (FlxG.Keys.JustPressed(Keys.Space)) Transition();
        if (FlxG.Keys.JustPressed(Keys.Escape)) LauncherState.Return();
    }
}

/// <summary>A piece of text you can click.</summary>
sealed class ClickText : FlxText
{
    public Action? OnClick;
    public bool Selected;

    static readonly Color Normal = Color.White, Hover = new(255, 200, 120), Chosen = new(255, 235, 80);

    readonly float _width;

    public ClickText(float x, float y, string text, Action? onClick = null) : base(x, y, 0, text)
    {
        OnClick = onClick;
        _width = FlxBitmapFont.Default.Measure(text);
        SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black, 1);
        Tint = Normal;
    }

    bool MouseOver()
    {
        var m = FlxG.Mouse;
        return m.X >= X - 2 && m.X <= X + _width + 2 && m.Y >= Y - 1 && m.Y <= Y + 10;
    }

    public override void Update(float elapsed)
    {
        bool over = MouseOver();
        Tint = Selected ? Chosen : over ? Hover : Normal;
        if (over && FlxG.Mouse.JustPressed) OnClick?.Invoke();
        base.Update(elapsed);
    }
}

/// <summary>A column of ClickTexts where exactly one is selected (a radio group).</summary>
sealed class OptionList : FlxGroup
{
    readonly List<ClickText> _items = new();

    public OptionList(float x, float y, string[] labels, string selected, Action<string> onChanged)
    {
        for (int i = 0; i < labels.Length; i++)
        {
            string label = labels[i];
            var item = new ClickText(x, y + i * 14, label) { Selected = label == selected };
            item.OnClick = () =>
            {
                foreach (var other in _items) other.Selected = ReferenceEquals(other, item);
                onChanged(label);
            };
            _items.Add(item);
            Add(item);
        }
    }
}
