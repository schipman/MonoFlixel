using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

public enum FlxCameraFollowStyle { LockOn, Platformer, TopDown, TopDownTight, NoDeadZone }

/// <summary>
/// A camera is a viewport (X, Y, Width, Height in game pixels) showing the world at a Zoom level.
/// Scroll is the world position of the view's top-left corner. The main camera covers the whole game.
/// </summary>
public class FlxCamera
{
    public int X, Y;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float Zoom = 1f;

    /// <summary>Size of the world area this camera shows (Width / Zoom).</summary>
    public float ViewWidth => Width / Zoom;
    public float ViewHeight => Height / Zoom;

    public Vector2 Scroll;
    public Color BgColor = Color.Black;
    public FlxObject? Target;
    public float Lerp = 1f;
    public Rectangle? Deadzone;     // in view coordinates; the target may move inside it without scrolling
    public Vector2 TargetOffset;
    public Rectangle? Bounds;       // world bounds the camera may not leave

    Vector2 _shakeOffset;
    float _shakeIntensity, _shakeTime;

    Color _flashColor; float _flashDur, _flashTime; Action? _flashDone;
    Color _fadeColor; float _fadeDur, _fadeTime; bool _fadeIn, _fadeActive, _fadeHold; Action? _fadeDone;

    public float ScrollX => Scroll.X + _shakeOffset.X;
    public float ScrollY => Scroll.Y + _shakeOffset.Y;

    public FlxCamera(int x, int y, int width, int height, float zoom = 1f)
    {
        X = x; Y = y; Width = width; Height = height; Zoom = zoom;
    }

    internal void Resize(int width, int height)
    {
        X = 0; Y = 0; Width = width; Height = height; Zoom = 1f;
    }

    // ------------------------------------------------------------ following

    /// <summary>Center on the target and smooth with lerp (1 = instant). Snaps to the target immediately.</summary>
    public void Follow(FlxObject target, float lerp)
    {
        Target = target; Lerp = lerp; Deadzone = null;
        Scroll = TargetScroll();
        ClampToBounds();
    }

    /// <summary>Follow with one of Flixel's styles (Platformer = small dead zone, NoDeadZone = always centered).</summary>
    public void Follow(FlxObject target, FlxCameraFollowStyle style = FlxCameraFollowStyle.LockOn, float lerp = 1f)
    {
        Target = target; Lerp = lerp;
        float w = ViewWidth, h = ViewHeight;
        switch (style)
        {
            case FlxCameraFollowStyle.Platformer:
            {
                float dw = w / 8f, dh = h / 3f;
                Deadzone = new Rectangle((int)((w - dw) / 2f), (int)((h - dh) / 2f - dh * 0.25f), (int)dw, (int)dh);
                break;
            }
            case FlxCameraFollowStyle.TopDown:
            {
                float s = MathF.Max(w, h) / 4f;
                Deadzone = new Rectangle((int)((w - s) / 2f), (int)((h - s) / 2f), (int)s, (int)s);
                break;
            }
            case FlxCameraFollowStyle.TopDownTight:
            {
                float s = MathF.Max(w, h) / 8f;
                Deadzone = new Rectangle((int)((w - s) / 2f), (int)((h - s) / 2f), (int)s, (int)s);
                break;
            }
            default:
                Deadzone = null;
                break;
        }
    }

    Vector2 TargetScroll() =>
        new(Target!.X + Target.Width * 0.5f - ViewWidth * 0.5f, Target.Y + Target.Height * 0.5f - ViewHeight * 0.5f);

    void ClampToBounds()
    {
        if (Bounds is not Rectangle b) return;
        Scroll.X = MathHelper.Clamp(Scroll.X, b.Left, MathF.Max(b.Left, b.Right - ViewWidth));
        Scroll.Y = MathHelper.Clamp(Scroll.Y, b.Top, MathF.Max(b.Top, b.Bottom - ViewHeight));
    }

    // ------------------------------------------------------------ effects

    public void Shake(float intensity = 0.05f, float duration = 0.5f) { _shakeIntensity = intensity; _shakeTime = duration; }

    public void Flash(Color color, float duration = 1f, Action? onComplete = null)
    {
        _flashColor = color; _flashDur = MathF.Max(duration, 0.0001f); _flashTime = _flashDur; _flashDone = onComplete;
    }

    public void Fade(Color color, float duration = 1f, bool fadeIn = false, Action? onComplete = null)
    {
        _fadeColor = color; _fadeDur = MathF.Max(duration, 0.0001f); _fadeTime = _fadeDur;
        _fadeIn = fadeIn; _fadeActive = true; _fadeHold = false; _fadeDone = onComplete;
    }

    public void Reset()
    {
        Target = null; Bounds = null; Deadzone = null; TargetOffset = Vector2.Zero; Lerp = 1f;
        Scroll = Vector2.Zero; _shakeOffset = Vector2.Zero;
        _shakeTime = _flashTime = 0;
        _fadeActive = _fadeHold = false;
        _flashDone = _fadeDone = null;
    }

    // ------------------------------------------------------------ per frame

    internal void Update(float dt)
    {
        if (Target != null) UpdateFollow(dt);
        ClampToBounds();

        if (_shakeTime > 0)
        {
            _shakeTime -= dt;
            _shakeOffset = _shakeTime > 0
                ? new Vector2((FlxG.Random.Float() * 2 - 1) * _shakeIntensity * ViewWidth,
                              (FlxG.Random.Float() * 2 - 1) * _shakeIntensity * ViewHeight)
                : Vector2.Zero;
        }

        if (_flashTime > 0)
        {
            _flashTime -= dt;
            if (_flashTime <= 0) { var cb = _flashDone; _flashDone = null; cb?.Invoke(); }
        }

        if (_fadeActive)
        {
            _fadeTime -= dt;
            if (_fadeTime <= 0)
            {
                _fadeActive = false; _fadeHold = !_fadeIn;
                var cb = _fadeDone; _fadeDone = null; cb?.Invoke();
            }
        }
    }

    void UpdateFollow(float dt)
    {
        var t = Target!;
        float tx = t.X + TargetOffset.X, ty = t.Y + TargetOffset.Y;
        Vector2 goal;

        if (Deadzone is not Rectangle dz)
        {
            goal = new Vector2(tx + t.Width * 0.5f - ViewWidth * 0.5f, ty + t.Height * 0.5f - ViewHeight * 0.5f);
        }
        else
        {
            goal = Scroll;
            float edge = tx - dz.X;
            if (goal.X > edge) goal.X = edge;
            edge = tx + t.Width - dz.X - dz.Width;
            if (goal.X < edge) goal.X = edge;
            edge = ty - dz.Y;
            if (goal.Y > edge) goal.Y = edge;
            edge = ty + t.Height - dz.Y - dz.Height;
            if (goal.Y < edge) goal.Y = edge;
        }

        float f = Lerp >= 1f ? 1f : 1f - MathF.Pow(1f - Lerp, dt * 60f);
        Scroll += (goal - Scroll) * f;
    }

    /// <summary>Flash / fade overlays, drawn over this camera's viewport (no zoom, game coordinates).</summary>
    internal void DrawFx(SpriteBatch batch, Texture2D pixel)
    {
        if (_flashTime > 0) Overlay(batch, pixel, _flashColor, _flashTime / _flashDur);
        if (_fadeActive) Overlay(batch, pixel, _fadeColor, _fadeIn ? _fadeTime / _fadeDur : 1f - _fadeTime / _fadeDur);
        else if (_fadeHold) Overlay(batch, pixel, _fadeColor, 1f);
    }

    void Overlay(SpriteBatch batch, Texture2D pixel, Color c, float alpha)
    {
        alpha = MathHelper.Clamp(alpha, 0f, 1f);
        if (alpha <= 0) return;
        batch.Draw(pixel, new Rectangle(X, Y, Width, Height), new Color(c.R, c.G, c.B, (byte)(255 * alpha)));
    }
}

/// <summary>FlxG.Cameras: the list of cameras plus "apply to all cameras" helpers.</summary>
public sealed class FlxCameraFrontEnd
{
    readonly List<FlxCamera> _list = new();
    FlxCamera _main = null!;
    Color _bg = Color.Black;

    public IReadOnlyList<FlxCamera> List => _list;
    public FlxCamera Main => _main;

    /// <summary>Background color of the main camera, and the default for cameras you add. Persists across states.</summary>
    public Color BgColor
    {
        get => _bg;
        set { _bg = value; if (_main != null) _main.BgColor = value; }
    }

    internal void Init(int width, int height)
    {
        _main = new FlxCamera(0, 0, width, height) { BgColor = _bg };
        _list.Clear();
        _list.Add(_main);
    }

    internal void Reset(int width, int height)
    {
        _list.Clear();
        _main.Resize(width, height);
        _main.Reset();
        _main.BgColor = _bg;
        _list.Add(_main);
    }

    public FlxCamera Add(FlxCamera camera)
    {
        camera.BgColor = _bg;
        _list.Add(camera);
        return camera;
    }

    /// <summary>Removes a camera you added (the main camera can't be removed).</summary>
    public bool Remove(FlxCamera camera) => !ReferenceEquals(camera, _main) && _list.Remove(camera);

    /// <summary>The callback fires once (from the main camera).</summary>
    public void Flash(Color color, float duration = 1f, Action? onComplete = null)
    {
        for (int i = 0; i < _list.Count; i++) _list[i].Flash(color, duration, i == 0 ? onComplete : null);
    }

    public void Fade(Color color, float duration = 1f, bool fadeIn = false, Action? onComplete = null)
    {
        for (int i = 0; i < _list.Count; i++) _list[i].Fade(color, duration, fadeIn, i == 0 ? onComplete : null);
    }

    public void Shake(float intensity = 0.05f, float duration = 0.5f)
    {
        foreach (var c in _list) c.Shake(intensity, duration);
    }

    internal void Update(float dt)
    {
        for (int i = 0; i < _list.Count; i++) _list[i].Update(dt);
    }
}
