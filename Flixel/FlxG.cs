using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>Global access point, like FlxG in HaxeFlixel.</summary>
public static class FlxG
{
    static FlxCamera? _drawCamera;

    public static FlxGame Game { get; internal set; } = null!;
    public static float Elapsed { get; internal set; }

    /// <summary>Multiplies elapsed time for updates, timers, tweens and camera effects (1 = normal, 0.35 = slow motion).</summary>
    public static float TimeScale { get; set; } = 1f;

    public static int Width => Game.GameWidth;
    public static int Height => Game.GameHeight;
    public static FlxState State => Game.State;
    public static GraphicsDevice GraphicsDevice => Game.GraphicsDevice;
    public static SpriteBatch SpriteBatch => Game.SpriteBatch;
    public static Texture2D Pixel => Game.Pixel;

    /// <summary>The main camera while updating; the camera currently being drawn while drawing.</summary>
    public static FlxCamera Camera => _drawCamera ?? Cameras.Main;

    public static readonly FlxCameraFrontEnd Cameras = new();
    public static readonly FlxKeyboard Keys = new();
    public static readonly FlxMouse Mouse = new();
    public static readonly FlxTouchManager Touches = new();
    public static readonly FlxGamepadManager Gamepads = new();
    public static readonly FlxActionManager Actions = new();

    /// <summary>The gamepad to use for a single player: the first connected one (never null; reads as not pressed when none is plugged in).</summary>
    public static FlxGamepad Gamepad => Gamepads.First;

    /// <summary>Seconds of real time since the game started (not slowed down by TimeScale): for timing double clicks and the like.</summary>
    public static double RealTime { get; internal set; }
    public static readonly FlxSoundFrontEnd Sound = new();
    public static readonly FlxTimerManager Timers = new();
    public static readonly FlxTweenManager Tweens = new();
    public static readonly FlxRandom Random = new();

    /// <summary>The game-wide save file (named after the window title, written when the game exits or on Flush()).</summary>
    public static readonly FlxSave Save = new();

    /// <summary>Game-wide events: state switches, update/draw hooks, window resize.</summary>
    public static readonly FlxSignals Signals = new();

    /// <summary>Full-screen shader effects applied to the finished game screen, in order.</summary>
    public static readonly FlxPostProcessChain PostProcess = new();

    /// <summary>Draws a line in world coordinates (call it from Draw()): handy for debugging paths and ray casts.</summary>
    public static void DrawLine(Microsoft.Xna.Framework.Vector2 from, Microsoft.Xna.Framework.Vector2 to,
        Microsoft.Xna.Framework.Color color, float thickness = 1f)
    {
        var cam = Camera;
        var a = new Microsoft.Xna.Framework.Vector2(from.X - cam.ScrollX, from.Y - cam.ScrollY);
        var b = new Microsoft.Xna.Framework.Vector2(to.X - cam.ScrollX, to.Y - cam.ScrollY);
        var d = b - a;
        float len = d.Length();
        if (len < 0.001f) return;

        SpriteBatch.Draw(Pixel, a, null, color, MathF.Atan2(d.Y, d.X), new Microsoft.Xna.Framework.Vector2(0, 0.5f),
            new Microsoft.Xna.Framework.Vector2(len, thickness), SpriteEffects.None, 0f);
    }

    /// <summary>Which objects the current draw pass draws (see FlxBasic.DrawAfterPostProcess).</summary>
    internal enum DrawLayerKind { All, Scene, Overlay }

    internal static DrawLayerKind DrawLayer = DrawLayerKind.All;
    static int _overlayScope;

    /// <summary>
    /// Draws a member of a group, or skips it, depending on the pass: the normal pass draws everything; with
    /// post-processing, the scene pass skips objects flagged DrawAfterPostProcess and the overlay pass draws only those
    /// (and goes through groups to find them).
    /// </summary>
    internal static void DrawChild(FlxBasic m)
    {
        switch (DrawLayer)
        {
            case DrawLayerKind.Scene:
                if (!m.DrawAfterPostProcess) DrawWithBlend(m);
                break;

            case DrawLayerKind.Overlay:
                if (m.DrawAfterPostProcess)
                {
                    _overlayScope++;
                    try { DrawWithBlend(m); } finally { _overlayScope--; }
                }
                else if (_overlayScope > 0 || m is IFlxGroup) DrawWithBlend(m);
                break;

            default:
                DrawWithBlend(m);
                break;
        }
    }

    // ------------------------------------------------------------ blend modes

    // The settings the current sprite batch was started with, so it can be restarted with another blend state.
    static BlendState _baseBlend = BlendState.NonPremultiplied;
    static RasterizerState? _batchRasterizer;
    static Matrix _batchTransform = Matrix.Identity;
    static FlxBlend _currentBlend = FlxBlend.Alpha;

    internal static Matrix BatchTransform => _batchTransform;
    internal static RasterizerState? BatchRasterizer => _batchRasterizer;
    internal static BlendState BatchBlendState => FlxBlendStates.For(_currentBlend) ?? _baseBlend;

    /// <summary>Starts the sprite batch for a camera's world pass and remembers its settings.</summary>
    internal static void BeginBatch(BlendState baseBlend, RasterizerState rasterizer, Matrix transform)
    {
        _baseBlend = baseBlend;
        _batchRasterizer = rasterizer;
        _batchTransform = transform;
        _currentBlend = FlxBlend.Alpha;
        SpriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: baseBlend, rasterizerState: rasterizer, transformMatrix: transform);
    }

    /// <summary>Restarts the batch with another blend mode, if it isn't the current one.</summary>
    internal static void SetBlend(FlxBlend mode)
    {
        if (mode == _currentBlend) return;
        SpriteBatch.End();
        _currentBlend = mode;
        SpriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: FlxBlendStates.For(mode) ?? _baseBlend,
            rasterizerState: _batchRasterizer, transformMatrix: _batchTransform);
    }

    /// <summary>
    /// Pauses the sprite batch while <paramref name="drawDirectly"/> draws something that isn't a sprite (triangles with their own
    /// effect...), then starts it again with the same settings.
    /// </summary>
    internal static void BreakBatch(Action drawDirectly)
    {
        SpriteBatch.End();
        try { drawDirectly(); }
        finally
        {
            SpriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BatchBlendState,
                rasterizerState: _batchRasterizer, transformMatrix: _batchTransform);
        }
    }

    /// <summary>Ends the batch started with BeginBatch.</summary>
    internal static void EndBatch()
    {
        SpriteBatch.End();
        _currentBlend = FlxBlend.Alpha;
    }

    /// <summary>Draws an object in its blend mode (sprites choose theirs, everything else uses normal blending).</summary>
    static void DrawWithBlend(FlxBasic m)
    {
        SetBlend(m is FlxSprite sprite ? sprite.Blend : FlxBlend.Alpha);
        m.Draw();
    }

    internal static void BeginDraw(FlxCamera cam) => _drawCamera = cam;
    internal static void EndDraw() => _drawCamera = null;

    /// <summary>Borderless fullscreen on/off. The game resolution is unchanged; it is scaled to the screen per ScaleMode.</summary>
    public static bool Fullscreen { get => Game.Fullscreen; set => Game.Fullscreen = value; }
    public static void ToggleFullscreen() => Game.Fullscreen = !Game.Fullscreen;

    /// <summary>How the game screen is fitted into the window: Integer, Fit or Stretch.</summary>
    public static FlxScaleMode ScaleMode { get => Game.ScaleMode; set => Game.ScaleMode = value; }

    /// <summary>
    /// Switch to a new state at the start of the next frame. Pass width/height (and zoom) to change the
    /// game's resolution at the same time; zoom also resizes the window to width*zoom x height*zoom.
    /// </summary>
    public static void SwitchState(FlxState state, int width = 0, int height = 0, int zoom = 0) =>
        Game.RequestState(state, width, height, zoom);

    /// <summary>Restart the current state (it needs a parameterless constructor).</summary>
    public static void ResetState() =>
        SwitchState((FlxState)Activator.CreateInstance(State.GetType())!);

    public static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception) { /* no browser available */ }
    }

    // ------------------------------------------------------------ collision

    /// <summary>Separates overlapping objects/groups (like FlxG.collide). Returns true if anything collided.</summary>
    public static bool Collide(FlxBasic a, FlxBasic? b = null, Action<FlxObject, FlxObject>? notify = null) =>
        Run(a, b, notify, FlxObject.Separate);

    /// <summary>Checks for overlaps without separating (like FlxG.overlap).</summary>
    public static bool Overlap(FlxBasic a, FlxBasic? b = null, Action<FlxObject, FlxObject>? notify = null) =>
        Run(a, b, notify, FlxObject.OverlapsPair);

    /// <summary>Use a FlxQuadTree inside Collide/Overlap when a lot of pairs would be compared (default on).</summary>
    public static bool UseQuadTree = true;

    /// <summary>Granularity of the quad tree used by Collide/Overlap (Flixel's default is 3).</summary>
    public static int QuadTreeDivisions = 3;

    /// <summary>Collide/Overlap only build a tree when objectsA * objectsB is above this; for small groups brute force is cheaper.</summary>
    public static int QuadTreeMinPairs = 400;

    static bool Run(FlxBasic a, FlxBasic? b, Action<FlxObject, FlxObject>? notify, Func<FlxObject, FlxObject, bool> test)
    {
        bool self = b == null || ReferenceEquals(a, b);
        var la = Flatten(a);
        var lb = self ? la : Flatten(b!);

        if (UseQuadTree && (long)la.Count * lb.Count > QuadTreeMinPairs)
            return RunQuadTree(la, lb, self, notify, test);

        bool hit = false;
        for (int i = 0; i < la.Count; i++)
            for (int j = self ? i + 1 : 0; j < lb.Count; j++)
            {
                var x = la[i]; var y = lb[j];
                if (ReferenceEquals(x, y) || !x.Exists || !y.Exists) continue;
                if (!x.Solid || !y.Solid) continue;      // non-solid objects take no part (like Flixel)
                if (test(x, y)) { hit = true; notify?.Invoke(x, y); }
            }
        return hit;
    }

    static bool RunQuadTree(List<FlxObject> la, List<FlxObject> lb, bool self, Action<FlxObject, FlxObject>? notify, Func<FlxObject, FlxObject, bool> test)
    {
        // The tree covers the area all the objects occupy.
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        void Grow(List<FlxObject> l)
        {
            foreach (var o in l)
            {
                if (!o.Exists || !o.Solid) continue;
                minX = MathF.Min(minX, o.X); minY = MathF.Min(minY, o.Y);
                maxX = MathF.Max(maxX, o.X + o.Width); maxY = MathF.Max(maxY, o.Y + o.Height);
            }
        }
        Grow(la);
        if (!self) Grow(lb);
        if (minX > maxX) return false;     // nothing solid

        var tree = new FlxQuadTree(minX, minY, MathF.Max(1, maxX - minX), MathF.Max(1, maxY - minY), QuadTreeDivisions);
        foreach (var o in la) tree.Add(o, FlxQuadTree.AList);
        if (!self) foreach (var o in lb) tree.Add(o, FlxQuadTree.BList);

        return tree.Overlap(!self, (x, y) =>
        {
            if (!test(x, y)) return false;
            notify?.Invoke(x, y);
            return true;
        });
    }

    static List<FlxObject> Flatten(FlxBasic b) { var l = new List<FlxObject>(); Collect(b, l); return l; }

    static void Collect(FlxBasic b, List<FlxObject> into)
    {
        if (!b.Exists) return;
        if (b is IFlxGroup g) { foreach (var m in g.Basics) Collect(m, into); }
        else if (b is FlxObject o) into.Add(o);
    }
}
