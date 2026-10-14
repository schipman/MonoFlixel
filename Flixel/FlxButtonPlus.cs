using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// A button drawn from gradients: a colored panel with a white border that changes color when the mouse is over it, with a label,
/// a click callback and mouse over / out callbacks. Port of Richard Davey's (Photon Storm) Flixel Power Tools <c>FlxButtonPlus</c>
/// (see NOTICE-flixel-power-tools.txt); <see cref="FlxButton"/> is the engine's picture based button.
/// <code>
/// var play = new FlxButtonPlus(100, 80, () => FlxG.SwitchState(new PlayState()), "Play", 120, 24);
/// play.OffColors = new[] { new Color(0, 90, 160), new Color(0, 160, 255) };       // blue instead of green
/// play.OnMouseOver = () => FlxG.Sound.Play("click.wav");
/// Add(play);
/// </code>
/// Differences from the original: the click callback runs when the mouse button is let go over the button (the original also ran it
/// on a release elsewhere, if the button was still marked pressed), parameters are done with lambdas, and the pause proof flag is gone.
/// </summary>
public class FlxButtonPlus : FlxGroup
{
    public enum Status { Normal, Highlight, Pressed }

    public readonly FlxSprite ButtonNormal, ButtonHighlight;
    public readonly FlxText? TextNormal, TextHighlight;

    /// <summary>Called when the button is clicked (mouse pressed on it and let go over it), when the mouse comes onto it, and when it leaves.</summary>
    public Action? OnClick, OnMouseOver, OnMouseOut;

    public Status State { get; private set; } = Status.Normal;
    public int Width { get; private set; }
    public int Height { get; private set; }

    Color _borderColor = Color.White;
    Color[] _offColors = { new(0x00, 0x80, 0x00), new(0x00, 0xFF, 0x00) };
    Color[] _onColors = { new(0x80, 0x00, 0x00), new(0xFF, 0x00, 0x00) };
    float _x, _y;

    public FlxButtonPlus(float x, float y, Action onClick, string? label = null, int width = 100, int height = 20) : base(4)
    {
        _x = x;
        _y = y;
        Width = width;
        Height = height;
        OnClick = onClick;

        ButtonNormal = MakeButton(MakeTexture(_offColors));
        ButtonHighlight = MakeButton(MakeTexture(_onColors));
        ButtonHighlight.Visible = false;
        Add(ButtonNormal);
        Add(ButtonHighlight);

        if (label != null)
        {
            TextNormal = MakeLabel(label);
            TextHighlight = MakeLabel(label);
            TextHighlight.Visible = false;
            Add(TextNormal);
            Add(TextHighlight);
        }
    }

    FlxSprite MakeButton(Texture2D texture)
    {
        var s = new FlxSprite(_x, _y).LoadGraphic(texture);
        s.Solid = false;
        s.Moves = false;
        s.ScrollFactor = Vector2.Zero;
        return s;
    }

    FlxText MakeLabel(string label)
    {
        var t = new FlxText(_x, _y + 3, Width, label);
        t.SetFormat(8, Color.White, FlxTextAlign.Center);
        t.SetBorderStyle(FlxTextBorderStyle.Shadow, Color.Black);
        t.ScrollFactor = Vector2.Zero;
        return t;
    }

    Texture2D MakeTexture(Color[] colors)
    {
        var pixels = new Color[Width * Height];
        Array.Fill(pixels, _borderColor);
        if (Width > 2 && Height > 2)
        {
            var inner = FlxGradient.CreatePixels(Width - 2, Height - 2, colors);
            for (int y = 0; y < Height - 2; y++)
                Array.Copy(inner, y * (Width - 2), pixels, (y + 1) * Width + 1, Width - 2);
        }
        var tex = new Texture2D(FlxG.GraphicsDevice, Width, Height);
        tex.SetData(pixels);
        return tex;
    }

    // ------------------------------------------------------------ position and look

    public float X { get => _x; set { _x = value; Place(); } }
    public float Y { get => _y; set { _y = value; Place(); } }

    void Place()
    {
        ButtonNormal.X = ButtonHighlight.X = _x;
        ButtonNormal.Y = ButtonHighlight.Y = _y;
        if (TextNormal != null && TextHighlight != null)
        {
            TextNormal.X = TextHighlight.X = _x;
            TextNormal.Y = TextHighlight.Y = _y + 3;
        }
    }

    /// <summary>Centers the button on the screen horizontally.</summary>
    public void ScreenCenter() => X = FlxG.Width / 2f - Width / 2f;

    /// <summary>The gradient (top to bottom colors) of the button at rest and when the mouse is over it.</summary>
    public Color[] OffColors
    {
        get => _offColors;
        set { _offColors = value; ButtonNormal.LoadGraphic(MakeTexture(_offColors)); }
    }

    public Color[] OnColors
    {
        get => _onColors;
        set { _onColors = value; ButtonHighlight.LoadGraphic(MakeTexture(_onColors)); }
    }

    public string? Text
    {
        get => TextNormal?.Text;
        set
        {
            if (TextNormal == null || TextHighlight == null || value == null || TextNormal.Text == value) return;
            TextNormal.Text = value;
            TextHighlight.Text = value;
        }
    }

    /// <summary>Use your own pictures for the two states (the size of the button follows the first).</summary>
    public void LoadGraphic(FlxSprite normal, FlxSprite highlight)
    {
        if (normal.Pixels != null) ButtonNormal.LoadGraphic(normal.Pixels);
        if (highlight.Pixels != null) ButtonHighlight.LoadGraphic(highlight.Pixels);
        Width = ButtonNormal.FrameWidth;
        Height = ButtonNormal.FrameHeight;
    }

    // ------------------------------------------------------------ behaviour

    public override void Update(float elapsed)
    {
        var previous = State;
        var mouse = FlxG.Mouse;
        var inView = mouse.GetPositionInCameraView();   // the button does not scroll
        bool over = mouse.Visible && inView.X >= _x && inView.X < _x + Width && inView.Y >= _y && inView.Y < _y + Height;

        if (over)
        {
            if (mouse.JustPressed) State = Status.Pressed;
            else if (State == Status.Normal) State = Status.Highlight;

            if (mouse.JustReleased && previous == Status.Pressed)
            {
                OnClick?.Invoke();
                State = Status.Highlight;
            }
        }
        else State = Status.Normal;

        if (State == previous || (previous == Status.Pressed && State == Status.Highlight)) return;

        bool lit = State != Status.Normal;
        ButtonNormal.Visible = !lit;
        ButtonHighlight.Visible = lit;
        if (TextNormal != null && TextHighlight != null)
        {
            TextNormal.Visible = !lit;
            TextHighlight.Visible = lit;
        }
        if (State == Status.Normal) OnMouseOut?.Invoke();
        else if (previous == Status.Normal) OnMouseOver?.Invoke();
    }

    public override void Destroy()
    {
        OnClick = OnMouseOver = OnMouseOut = null;
        base.Destroy();
    }
}
