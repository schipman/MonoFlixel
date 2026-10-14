using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// The post-processes that come with the engine, by name, with settings that look good. The launcher's effects menu is made from
/// this list; the global stack is saved by these names.
/// <code>FlxG.PostProcess.AddGlobal(FlxPostProcessLibrary.Create("VHS"));</code>
/// </summary>
public static class FlxPostProcessLibrary
{
    public sealed record Entry(string Name, string Description, Func<FlxPostProcess> Create);

    /// <summary>All of them, in the order a stack usually wants: image changes first, the "screen" effects (VHS, CRT) last.</summary>
    public static readonly IReadOnlyList<Entry> All = new Entry[]
    {
        new("Bloom", "Bright parts glow (at window resolution).", () => FlxBloom.Create(threshold: 0.65f, intensity: 0.6f)),
        new("Desaturate", "Colors fade towards grey.", () => Make("Desaturate", ("Amount", 0.85f))),
        new("GaussianBlur", "A smooth gaussian blur.", () => FlxGaussianBlur.Create(radius: 1.2f)),
        new("RadialBlur", "Zoom blur: smeared towards the middle of the screen.", () => Make("RadialBlur", "RadialBlur", SamplerState.LinearClamp, false, ("Strength", 0.14f), ("Inner", 0.12f), ("Center", new Vector2(0.5f, 0.5f)))),
        new("Wiggle", "Water wobble.", () => Make("Wiggle", "Wiggle", SamplerState.LinearClamp, false, ("Amplitude", 0.004f), ("Frequency", 20f), ("Speed", 3f))),
        new("Refraction", "A scrolling displacement picture ripples the screen, like looking through moving water (from Drown).", () => Make("Refraction", "Refraction", SamplerState.LinearClamp, false,
            ("Strength", 0.05f), ("Scale", 3f), ("Speed", 0.02f), ("DisplacementTex", FlxAssets.GetTexture("flixel/waterfall.jpg")))),
        new("ChromaticAberration", "Red and blue split apart towards the edges, like a cheap lens.", () => Make("ChromaticAberration", "ChromaticAberration", SamplerState.LinearClamp, false, ("Amount", 0.008f), ("Falloff", 1.5f))),
        new("Vignette", "Darkened corners.", () => Make("Vignette", ("Intensity", 0.7f), ("Radius", 0.45f), ("Softness", 0.55f), ("Roundness", 0.6f))),
        new("HeatVision", "A thermal camera: black, blue, red, yellow, white.", () => Make("HeatVision", "HeatVision", SamplerState.LinearClamp, false, ("Amount", 1f), ("Shimmer", 1f))),
        new("NightVision", "Green night vision with grain and a round lens.", () => Make("NightVision", ("Gain", 2.1f), ("Noise", 0.18f), ("Contrast", 1.25f), ("VignetteSize", 0.75f))),
        new("VHS", "Video tape: color bleed, tracking noise, tearing.", () => Make("Vhs", "VHS", SamplerState.LinearClamp, false, ("Amount", 1f), ("Noise", 0.1f), ("ChromaOffset", 2f), ("Jitter", 1f))),
        new("CRT", "A curved CRT with scanlines and a shadow mask.", () => Make("Crt", "CRT", SamplerState.LinearClamp, true, ("Curvature", 0.12f), ("ScanlineStrength", 0.7f), ("MaskStrength", 0.45f), ("Vignette", 0.6f),
            ("Brightness", 1.25f), ("Sharpness", 3f))),
        new("CRT Flat", "The same CRT without the curved glass: the picture is not warped.", () => Make("Crt", "CRT Flat", SamplerState.LinearClamp, true, ("Curvature", 0f), ("ScanlineStrength", 0.7f), ("MaskStrength", 0.45f), ("Vignette", 0.35f),
            ("Brightness", 1.25f), ("Sharpness", 3f))),
    };

    public static IEnumerable<string> Names => All.Select(e => e.Name);

    /// <summary>A new instance of the effect with this name (any case). Throws if there is no such effect.</summary>
    public static FlxPostProcess Create(string name)
    {
        var entry = All.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentException($"There is no post-process called '{name}'.", nameof(name));
        return entry.Create();
    }

    public static Entry Find(string name) => All.First(e => e.Name == name);

    static FlxPostProcess Make(string shader, params (string Name, object Value)[] settings) =>
        Make(shader, shader, null, false, settings);

    static FlxPostProcess Make(string shader, string name, SamplerState? sampler, bool window, params (string Name, object Value)[] settings)
    {
        var p = new FlxPostProcess(FlxG.Game.Content.Load<Effect>($"Shaders/{shader}"), name);
        foreach (var (key, value) in settings)
        {
            switch (value)
            {
                case float f: p.Set(key, f); break;
                case Vector2 v: p.Set(key, v); break;
                case Vector4 v4: p.Set(key, v4); break;
                case Texture2D t: p.Set(key, t); break;
            }
        }
        if (sampler != null) p.Sampler = sampler;
        p.WindowResolution = window;
        return p;
    }
}
