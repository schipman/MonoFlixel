using System;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>
/// Flixel's stock 80x20 button (Assets/flixel/button.png: normal / hover / pressed frames) with a text label.
/// Tint the button with Tint and the label with Label.Tint. OnUp fires when the mouse is released over it.
/// </summary>
public class FlxButton : FlxSprite
{
    public readonly FlxText Label;
    public Action? OnUp;

    /// <summary>Show the hover frame even without the mouse (e.g. keyboard selection).</summary>
    public bool Highlighted;

    bool _pressStartedHere;

    public FlxButton(float x = 0, float y = 0, string text = "", Action? onClick = null) : base(x, y)
    {
        LoadGraphic(FlxAssets.GetTexture(FlxAssets.Get("flixel/button.png")), true, 80, 20);
        Label = new FlxText(x, y + 3, 80, text) { Alignment = FlxTextAlign.Center, Tint = new Color(0x33, 0x33, 0x33) };
        OnUp = onClick;
        Moves = false;
        DrawAfterPostProcess = FlxBitmapText.DefaultDrawAfterPostProcess;   // buttons are UI, like their label
    }

    bool MouseOver()
    {
        var cam = FlxG.Camera;
        float mx = FlxG.Mouse.X + cam.ScrollX * ScrollFactor.X;
        float my = FlxG.Mouse.Y + cam.ScrollY * ScrollFactor.Y;
        return mx >= X && mx < X + Width && my >= Y && my < Y + Height;
    }

    public override void Update(float elapsed)
    {
        bool over = MouseOver();
        var mouse = FlxG.Mouse;

        if (over && mouse.JustPressed) _pressStartedHere = true;

        bool pressed = over && mouse.Pressed && _pressStartedHere;
        if (!mouse.Pressed && mouse.JustReleased)
        {
            if (over && _pressStartedHere) OnUp?.Invoke();
            _pressStartedHere = false;
        }
        if (!mouse.Pressed) _pressStartedHere = false;

        Frame = pressed ? 2 : (over || Highlighted) ? 1 : 0;

        Label.X = X;
        Label.Y = Y + 3 + (pressed ? 1 : 0);
        Label.ScrollFactor = ScrollFactor;
        Label.Alpha = Alpha;
        Label.Update(elapsed);

        base.Update(elapsed);
    }

    public override void Draw()
    {
        base.Draw();
        Label.Draw();
    }
}
