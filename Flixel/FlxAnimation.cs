using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoFlixel;

/// <summary>One animation of a sprite: a list of frame indices played at a speed.</summary>
public sealed class FlxAnimation
{
    public string Name = "";

    /// <summary>The sprite's frame index for each step of the animation.</summary>
    public int[] Frames = Array.Empty<int>();

    /// <summary>Frames per second (used when FrameTimes is null). 0 = the animation stays on its first frame.</summary>
    public float Fps = 30;

    /// <summary>Seconds each step is shown, when the frames have their own durations (Aseprite). Same length as Frames.</summary>
    public float[]? FrameTimes;

    /// <summary>Start again when finished. With PingPong, go forward and backward again and again.</summary>
    public bool Loop = true;

    /// <summary>Play to the end, then back to the start (the end frames are not shown twice).</summary>
    public bool PingPong;

    /// <summary>Mirror the sprite while this animation plays (on top of the sprite's own FlipX / FlipY).</summary>
    public bool FlipX, FlipY;

    public int NumFrames => Frames.Length;

    /// <summary>How long step <paramref name="step"/> is shown, in seconds.</summary>
    public float StepDuration(int step) =>
        FrameTimes != null ? FrameTimes[Math.Clamp(step, 0, FrameTimes.Length - 1)] : Fps > 0 ? 1f / Fps : 0f;

    /// <summary>Seconds one pass through the frames takes.</summary>
    public float Duration
    {
        get { float t = 0; for (int i = 0; i < Frames.Length; i++) t += StepDuration(i); return t; }
    }
}

/// <summary>
/// Plays animations on a sprite (sprite.Animation). Port of Flixel's FlxAnimationController.
/// <code>
/// sprite.Animation.Add("run", new[] { 0, 1, 2, 3 }, 12);
/// sprite.Animation.AddByPrefix("walk", "walk_", fps: 10);          // frames named walk_0001, walk_0002...
/// sprite.Animation.Play("run");
/// sprite.Animation.Play("run", reversed: true, startFrame: 2);
/// sprite.Animation.OnFrame = (name, step, frame) => { if (frame == 2) Footstep(); };
/// </code>
/// </summary>
public class FlxAnimationController
{
    readonly FlxSprite _sprite;
    readonly Dictionary<string, FlxAnimation> _anims = new();
    float _time;
    int _step, _dir = 1;

    /// <summary>The animation that is playing (or last played).</summary>
    public FlxAnimation? Current { get; private set; }

    public string? CurrentName => Current?.Name;

    /// <summary>True when a non-looping animation reached its end.</summary>
    public bool Finished { get; private set; }

    public bool Paused;

    /// <summary>Multiplies the speed of every animation (2 = twice as fast, 0.5 = half speed).</summary>
    public float SpeedScale = 1f;

    /// <summary>True while the animation plays backward.</summary>
    public bool Reversed => _dir < 0;

    /// <summary>Which step of the current animation is showing (0 = the first).</summary>
    public int FrameNumber => _step;

    /// <summary>The sprite frame currently shown.</summary>
    public int FrameIndex => _sprite.Frame;

    /// <summary>Called when a non-looping animation reaches its end (with the animation's name).</summary>
    public Action<string>? OnFinish;

    /// <summary>Called each time a looping animation starts over.</summary>
    public Action<string>? OnLoop;

    /// <summary>Called every time the shown frame changes: (animation name, step number, sprite frame index).</summary>
    public Action<string, int, int>? OnFrame;

    public FlxAnimationController(FlxSprite sprite) { _sprite = sprite; }

    public IEnumerable<string> Names => _anims.Keys;

    public bool Exists(string name) => _anims.ContainsKey(name);

    public FlxAnimation? Get(string name) => _anims.GetValueOrDefault(name);

    /// <summary>Frames start..end inclusive, e.g. Range(4, 7) = {4,5,6,7}; counts down if end is smaller.</summary>
    public static int[] Range(int start, int end)
    {
        int step = start <= end ? 1 : -1;
        var r = new int[Math.Abs(end - start) + 1];
        for (int i = 0; i < r.Length; i++) r[i] = start + i * step;
        return r;
    }

    // ------------------------------------------------------------------ defining animations

    public FlxAnimation Add(string name, int[] frames, float fps = 30, bool looped = true, bool flipX = false, bool flipY = false, bool pingPong = false)
    {
        if (frames.Length == 0) throw new ArgumentException($"Animation '{name}' needs at least one frame.", nameof(frames));
        var a = new FlxAnimation { Name = name, Frames = frames, Fps = fps, Loop = looped, FlipX = flipX, FlipY = flipY, PingPong = pingPong };
        _anims[name] = a;
        return a;
    }

    /// <summary>
    /// An animation from the frames of an atlas whose names start with <paramref name="prefix"/> (and end with <paramref name="postfix"/>),
    /// in the order of the number that follows the prefix: "run0001", "run0002"...
    /// </summary>
    public FlxAnimation AddByPrefix(string name, string prefix, float fps = 30, bool looped = true, string postfix = "",
        bool flipX = false, bool flipY = false, bool pingPong = false)
    {
        var frames = Atlas().IndicesByPrefix(prefix, postfix);
        if (frames.Count == 0) throw new KeyNotFoundException($"No frame name starts with '{prefix}'.");
        return Add(name, frames.ToArray(), fps, looped, flipX, flipY, pingPong);
    }

    /// <summary>An animation from named frames, in the given order.</summary>
    public FlxAnimation AddByNames(string name, IEnumerable<string> frameNames, float fps = 30, bool looped = true,
        bool flipX = false, bool flipY = false, bool pingPong = false) =>
        Add(name, Atlas().IndicesByNames(frameNames).ToArray(), fps, looped, flipX, flipY, pingPong);

    /// <summary>An animation from frames named prefix + number + postfix, using the given numbers: AddByIndices("hit", "boom", new[] {1, 3, 5}) uses boom1, boom3, boom5.</summary>
    public FlxAnimation AddByIndices(string name, string prefix, IEnumerable<int> numbers, string postfix = "", float fps = 30, bool looped = true,
        bool flipX = false, bool flipY = false, bool pingPong = false, int digits = 0)
    {
        var names = numbers.Select(n => prefix + (digits > 0 ? n.ToString().PadLeft(digits, '0') : n.ToString()) + postfix);
        return Add(name, Atlas().IndicesByNames(names).ToArray(), fps, looped, flipX, flipY, pingPong);
    }

    /// <summary>
    /// Adds the animations stored in the atlas (Aseprite tags), with the frame durations from the file.
    /// Returns the animations that were added.
    /// </summary>
    public List<FlxAnimation> AddFromAtlas(bool looped = true)
    {
        var atlas = Atlas();
        var added = new List<FlxAnimation>();

        foreach (var tag in atlas.Animations)
        {
            int from = Math.Clamp(tag.From, 0, atlas.Count - 1), to = Math.Clamp(tag.To, 0, atlas.Count - 1);
            var indices = Range(from, to);

            bool reverse = tag.Direction.Contains("reverse", StringComparison.OrdinalIgnoreCase) && !tag.Direction.Contains("pingpong", StringComparison.OrdinalIgnoreCase);
            bool pingPong = tag.Direction.Contains("pingpong", StringComparison.OrdinalIgnoreCase);
            if (reverse) Array.Reverse(indices);

            var times = indices.Select(i => atlas[i].Duration).ToArray();
            var a = Add(tag.Name, indices, 0, looped, pingPong: pingPong);
            a.FrameTimes = times.All(t => t > 0) ? times : null;
            if (a.FrameTimes == null) a.Fps = 10;
            added.Add(a);
        }
        return added;
    }

    FlxFramesCollection Atlas() =>
        _sprite.Frames ?? throw new InvalidOperationException("This animation needs a sprite with named frames: call LoadFrames / LoadAseprite first.");

    public bool Remove(string name)
    {
        if (!_anims.Remove(name)) return false;
        if (Current?.Name == name) Stop();
        return true;
    }

    // ------------------------------------------------------------------ playing

    /// <param name="force">Restart even if this animation is already playing.</param>
    /// <param name="reversed">Play it backward (from its last frame to its first).</param>
    /// <param name="startFrame">The step to start at (0 = the first, or the last when reversed).</param>
    public void Play(string name, bool force = false, bool reversed = false, int startFrame = 0)
    {
        if (!_anims.TryGetValue(name, out var anim)) throw new KeyNotFoundException($"The sprite has no animation called '{name}'.");

        if (!force && ReferenceEquals(Current, anim) && !Finished && Reversed == reversed) return;

        Current = anim;
        Finished = false;
        _time = 0;
        _dir = reversed ? -1 : 1;
        _step = startFrame > 0 ? Math.Min(startFrame, anim.NumFrames - 1) : reversed ? anim.NumFrames - 1 : 0;
        SetFrame();
    }

    /// <summary>Turns the direction of the animation that is playing around.</summary>
    public void Reverse() => _dir = -_dir;

    public void Stop() { Current = null; Finished = false; }

    /// <summary>Jumps to the last frame (the first when reversed) and finishes a non-looping animation.</summary>
    public void Finish()
    {
        if (Current == null) return;
        _step = _dir > 0 ? Current.NumFrames - 1 : 0;
        Finished = !Current.Loop;
        SetFrame();
        if (Finished) OnFinish?.Invoke(Current.Name);
    }

    void SetFrame()
    {
        if (Current == null) return;
        _sprite.Frame = Current.Frames[_step];
        OnFrame?.Invoke(Current.Name, _step, _sprite.Frame);
    }

    internal void Update(float dt)
    {
        var a = Current;
        if (a == null || Finished || Paused) return;

        float stepTime = a.StepDuration(_step);
        if (stepTime <= 0) return;       // fps 0: stays on this frame

        _time += dt * SpeedScale;
        while (_time >= stepTime && !Finished)
        {
            _time -= stepTime;
            Advance(a);
            stepTime = a.StepDuration(_step);
            if (stepTime <= 0) break;
        }
    }

    void Advance(FlxAnimation a)
    {
        int n = a.NumFrames;
        int next = _step + _dir;

        if (next >= n)
        {
            if (a.PingPong && n > 1)
            {
                if (_dir > 0) { _dir = -1; next = n - 2; }
            }
            else if (a.Loop) { next = 0; OnLoop?.Invoke(a.Name); }
            else { End(a, n - 1); return; }
        }
        else if (next < 0)
        {
            if (a.PingPong && n > 1)
            {
                if (a.Loop) { _dir = 1; next = 1; OnLoop?.Invoke(a.Name); }
                else { End(a, 0); return; }
            }
            else if (a.Loop) { next = n - 1; OnLoop?.Invoke(a.Name); }
            else { End(a, 0); return; }
        }

        _step = Math.Clamp(next, 0, n - 1);
        SetFrame();
    }

    void End(FlxAnimation a, int lastStep)
    {
        // the last frame is usually showing already; only report a frame change if there is one
        if (_step != lastStep)
        {
            _step = lastStep;
            SetFrame();
        }
        Finished = true;
        OnFinish?.Invoke(a.Name);
    }
}
