using System;

namespace MonoFlixel;

/// <summary>Base class for everything that can live in a state or group.</summary>
public class FlxBasic
{
    public bool Active = true;   // Update() is called
    public bool Visible = true;  // Draw() is called
    public bool Exists = true;   // Both of the above, plus collisions
    public bool Alive = true;

    /// <summary>If set, only these cameras draw this object (null = every camera).</summary>
    public FlxCamera[]? Cameras;
    public bool OnCamera(FlxCamera cam) => Cameras == null || Array.IndexOf(Cameras, cam) >= 0;

    bool _drawAfterPostProcess;

    /// <summary>True once any object asked to be drawn after post-processing (so the extra pass can be skipped otherwise).</summary>
    internal static bool AnyDrawAfterPostProcess;

    /// <summary>
    /// Draw this object (and, for a group, its members) after the post-process shaders instead of before them, so effects like
    /// Wiggle or Bloom don't change it. Only matters while FlxG.PostProcess has an active effect; without one everything
    /// is drawn in the usual order. Text objects have this on by default (FlxBitmapText.DefaultDrawAfterPostProcess).
    /// </summary>
    public bool DrawAfterPostProcess
    {
        get => _drawAfterPostProcess;
        set { _drawAfterPostProcess = value; if (value) AnyDrawAfterPostProcess = true; }
    }

    public virtual void Update(float elapsed) { }
    public virtual void Draw() { }
    public virtual void Kill() { Alive = false; Exists = false; }
    public virtual void Revive() { Alive = true; Exists = true; }
    public virtual void Destroy() { Alive = false; Exists = false; }
}
