using System;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>
/// A state that is shown on top of another one: a pause menu, a dialog, a game-over overlay.
/// Open it with <c>OpenSubState(new MySubState())</c> from the state underneath and close it with <see cref="Close"/>.
/// While it is open the state below is not updated (unless its PersistentUpdate is true) but is still drawn.
/// </summary>
public class FlxSubState : FlxState
{
    /// <summary>Color drawn over the whole screen behind the substate's contents (use an alpha for a dimmed backdrop).</summary>
    public Color BgColor;

    /// <summary>The state this substate is open on, or null if it is not open.</summary>
    public FlxState? ParentState { get; internal set; }

    internal bool Created;

    /// <summary>Called right after the substate was opened / closed. Closed runs before the substate is destroyed.</summary>
    public Action? OnOpen, OnClose;

    public FlxSubState(Color? bgColor = null)
    {
        BgColor = bgColor ?? Color.Transparent;
    }

    /// <summary>Closes this substate (the state below resumes).</summary>
    public void Close() => ParentState?.CloseSubState();

    internal void Opened() => OnOpen?.Invoke();
    internal void Closed() => OnClose?.Invoke();

    public override void Draw()
    {
        // With post-processing the backdrop is painted in the overlay pass, on top of everything the state below drew in it
        // (text, buttons), and not in the scene pass: it would dim the scene twice.
        if (BgColor.A > 0 && FlxG.DrawLayer != FlxG.DrawLayerKind.Scene)
        {
            var cam = FlxG.Camera;
            FlxG.SpriteBatch.Draw(FlxG.Pixel,
                new Rectangle(0, 0, (int)MathF.Ceiling(cam.ViewWidth), (int)MathF.Ceiling(cam.ViewHeight)), BgColor);
        }
        base.Draw();
    }
}
