using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// A full-screen post-process: a pixel-shader Effect that is run over the finished game screen.
/// Shaders are normal .fx files (e.g. Content/Shaders/Wiggle.fx) listed in Content/Content.mgcb, so the
/// MonoGame content pipeline compiles them at build time; load them by name:
/// <code>FlxG.PostProcess.Add(FlxPostProcess.Load("Wiggle"));</code>
/// Parameters are set by name with Set(); these are filled in automatically when the shader declares them:
/// fTimer / Time (seconds, scaled by FlxG.TimeScale), TexelSize (1/width, 1/height) / Resolution (width, height) of the
/// picture it reads, and OutputSize (width, height) of what it draws to.
/// </summary>
public class FlxPostProcess
{
    public Effect Effect { get; }
    public string Name { get; }
    public bool Enabled = true;

    /// <summary>Texture filtering used when the shader reads the screen. Point = crisp pixels; Linear = smooth (blurs, glows).</summary>
    public SamplerState Sampler = SamplerState.PointClamp;

    /// <summary>
    /// The effect works at the size of the window, not at the size of the (small) game screen: it is given a window sized picture
    /// to draw on, and every effect after it is too. Needed for things that must be finer than a game pixel, like a CRT's
    /// shadow mask. (Effects at the end of the chain are always drawn at window size anyway.)
    /// </summary>
    public bool WindowResolution;

    readonly EffectParameter? _time, _texel, _resolution, _output;
    float _timer;

    public FlxPostProcess(Effect effect, string name = "")
    {
        Effect = effect;
        Name = name;
        _time = effect.Parameters["fTimer"] ?? effect.Parameters["Time"];
        _texel = effect.Parameters["TexelSize"];
        _resolution = effect.Parameters["Resolution"];
        _output = effect.Parameters["OutputSize"];
    }

    /// <summary>Loads a compiled shader from Content/Shaders/&lt;name&gt; (built from Shaders/&lt;name&gt;.fx).</summary>
    /// <remarks>
    /// Default values written in the .fx (<c>float Amount = 1.0;</c>) do NOT survive compilation: every parameter
    /// starts at 0. Pass the values you want here, e.g. <c>Load("Bloom", ("Intensity", 1f), ("Threshold", 0.55f))</c>.
    /// </remarks>
    public static FlxPostProcess Load(string name, params (string parameter, float value)[] values)
    {
        var p = new FlxPostProcess(FlxG.Game.Content.Load<Effect>($"Shaders/{name}"), name);
        foreach (var (parameter, value) in values) p.Set(parameter, value);

        // shaders that move or smear the picture by less than a pixel need smooth sampling (they do the crisp part themselves)
        if (name is "Wiggle" or "HeatVision" or "RadialBlur") p.Sampler = SamplerState.LinearClamp;
        return p;
    }

    /// <summary>Sets a shader parameter by name; silently ignored if the shader does not have it.</summary>
    public FlxPostProcess Set(string parameter, float value) { Effect.Parameters[parameter]?.SetValue(value); return this; }
    public FlxPostProcess Set(string parameter, Vector2 value) { Effect.Parameters[parameter]?.SetValue(value); return this; }
    public FlxPostProcess Set(string parameter, Vector4 value) { Effect.Parameters[parameter]?.SetValue(value); return this; }
    public FlxPostProcess Set(string parameter, Texture2D value) { Effect.Parameters[parameter]?.SetValue(value); return this; }

    internal void Update(float elapsed) => _timer += elapsed;

    /// <summary>Draws <paramref name="source"/> to the current render target through the shader.</summary>
    internal virtual void Draw(SpriteBatch batch, Texture2D source, Rectangle dest)
    {
        _time?.SetValue(_timer);
        _texel?.SetValue(new Vector2(1f / source.Width, 1f / source.Height));
        _resolution?.SetValue(new Vector2(source.Width, source.Height));
        _output?.SetValue(new Vector2(dest.Width, dest.Height));

        batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, Sampler, effect: Effect);
        batch.Draw(source, dest, Color.White);
        batch.End();
    }
}

/// <summary>
/// The post-processes run over the game screen (FlxG.PostProcess), as a stack: they run in the order they were added, each one on
/// the result of the one before, so any number can be active at the same time.
/// There are two stacks. The <b>local</b> one (Add, Push, Remove, Clear, Set) belongs to the current screen: a game puts its own look
/// there (Drown's water wobble). The <b>global</b> one (AddGlobal...) is the player's choice, made on the launcher's effects menu, and
/// stays on in every demo and across state changes; it runs after the local one. Both can be switched off by Enabled on an effect.
/// <code>
/// FlxG.PostProcess.Add(FlxPostProcess.Load("Wiggle"));         // for this screen
/// FlxG.PostProcess.AddGlobal(FlxPostProcessLibrary.Create("CRT"));   // for everything
/// </code>
/// </summary>
public sealed class FlxPostProcessChain
{
    readonly System.Collections.Generic.List<FlxPostProcess> _local = new(), _global = new(), _all = new();
    bool _dirty = true;

    /// <summary>Everything that runs, in order: the local stack, then the global one.</summary>
    public System.Collections.Generic.IReadOnlyList<FlxPostProcess> List
    {
        get
        {
            if (_dirty) { _all.Clear(); _all.AddRange(_local); _all.AddRange(_global); _dirty = false; }
            return _all;
        }
    }

    public System.Collections.Generic.IReadOnlyList<FlxPostProcess> Local => _local;
    public System.Collections.Generic.IReadOnlyList<FlxPostProcess> Global => _global;

    public bool Active
    {
        get { foreach (var p in List) if (p.Enabled) return true; return false; }
    }

    // ---- the local stack (this screen)

    public FlxPostProcess Add(FlxPostProcess p) { _local.Add(p); _dirty = true; return p; }

    /// <summary>Same as Add: on top of the stack.</summary>
    public FlxPostProcess Push(FlxPostProcess p) => Add(p);

    /// <summary>Takes the last added local effect off the stack and returns it (null if there is none).</summary>
    public FlxPostProcess? Pop()
    {
        if (_local.Count == 0) return null;
        var p = _local[^1];
        _local.RemoveAt(_local.Count - 1);
        _dirty = true;
        return p;
    }

    public bool Remove(FlxPostProcess p) { _dirty = true; return _local.Remove(p); }

    /// <summary>Removes the local effects (the player's global ones stay).</summary>
    public void Clear() { _local.Clear(); _dirty = true; }

    /// <summary>Replaces the local stack with one effect (null = none).</summary>
    public void Set(FlxPostProcess? p) { _local.Clear(); if (p != null) _local.Add(p); _dirty = true; }

    // ---- the global stack (the player's choice)

    public FlxPostProcess AddGlobal(FlxPostProcess p) { _global.Add(p); _dirty = true; SaveGlobal(); return p; }
    public bool RemoveGlobal(FlxPostProcess p) { bool r = _global.Remove(p); _dirty = true; SaveGlobal(); return r; }
    public void ClearGlobal() { _global.Clear(); _dirty = true; SaveGlobal(); }

    /// <summary>The global effect with this name, or null.</summary>
    public FlxPostProcess? FindGlobal(string name) => _global.Find(p => p.Name == name);

    /// <summary>Moves a global effect to another place in the stack (0 = runs first).</summary>
    public void MoveGlobal(FlxPostProcess p, int index)
    {
        int old = _global.IndexOf(p);
        if (old < 0) return;
        _global.RemoveAt(old);
        _global.Insert(Math.Clamp(index, 0, _global.Count), p);
        _dirty = true;
        SaveGlobal();
    }

    /// <summary>Remembers the global stack (names, order, on or off) in FlxG.Save.</summary>
    public void SaveGlobal()
    {
        if (!_restored) return;
        FlxG.Save.Set("postprocess.global", string.Join(",", _global.ConvertAll(p => (p.Enabled ? "+" : "-") + p.Name)));
    }

    bool _restored;

    /// <summary>Brings the saved global stack back (called when the game starts).</summary>
    internal void RestoreGlobal()
    {
        _restored = true;
        _global.Clear();
        foreach (var part in FlxG.Save.Get("postprocess.global", "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length < 2) continue;
            try
            {
                var p = FlxPostProcessLibrary.Create(part[1..]);
                p.Enabled = part[0] == '+';
                _global.Add(p);
            }
            catch (Exception) { /* an effect that does not exist any more is dropped */ }
        }
        _dirty = true;
    }

    internal void Update(float elapsed) { foreach (var p in List) p.Update(elapsed); }
}
