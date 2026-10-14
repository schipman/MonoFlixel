using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>How a tween behaves when it reaches its end.</summary>
public enum FlxTweenType
{
    /// <summary>Plays once, then is removed (default).</summary>
    OneShot,
    /// <summary>Plays once and stays in the manager, finished, until cancelled or restarted.</summary>
    Persist,
    /// <summary>Starts over from the beginning, forever.</summary>
    Looping,
    /// <summary>Plays forward, then backward, then forward again, forever.</summary>
    PingPong,
    /// <summary>Plays once, from the end value to the start value.</summary>
    Backward
}

[Flags]
public enum FlxAxes { None = 0, X = 1, Y = 2, XY = 3 }

/// <summary>Optional settings for a tween: <c>new TweenOptions { Ease = FlxEase.QuadOut, Type = FlxTweenType.PingPong }</c>.</summary>
public class TweenOptions
{
    public FlxTweenType Type = FlxTweenType.OneShot;
    public Func<float, float>? Ease;
    public float StartDelay;
    /// <summary>Delay before each repeat of a Looping / PingPong tween.</summary>
    public float LoopDelay;
    public Action<FlxTween>? OnStart, OnUpdate, OnComplete;
}

/// <summary>
/// Changes values over time. Create one with the static helpers - they start it right away:
/// <code>
/// FlxTween.Tween(sprite, new { X = 200, Alpha = 0.5f }, 1f, new TweenOptions { Ease = FlxEase.QuadOut });
/// FlxTween.Tween(sprite, new { Scale = new { X = 2f, Y = 2f } }, 0.5f);       // nested members (Vector2 etc.)
/// FlxTween.Num(0, 100, 1f, v => bar.Value = v);                                 // any number, applied by you
/// FlxTween.LinearMotion(sprite, 0, 0, 100, 50, 1.5f);
/// </code>
/// All tweens run on FlxG.Tweens and follow FlxG.TimeScale.
/// </summary>
public class FlxTween
{
    public float Duration { get; protected set; }
    public FlxTweenType Type;
    public Func<float, float> Ease;
    public float StartDelay, LoopDelay;
    public Action<FlxTween>? OnStart, OnUpdate, OnComplete;

    /// <summary>Eased progress of the current cycle, 0 to 1 (flipped while playing backward).</summary>
    public float Scale { get; protected set; }

    /// <summary>Plain (un-eased) progress of the current cycle, 0 to 1.</summary>
    public float Percent { get; private set; }

    /// <summary>How many times the tween has reached its end.</summary>
    public int Executions { get; private set; }

    /// <summary>True while the tween runs (false once it finished, or after Cancel).</summary>
    public bool Active { get; private set; }
    public bool Finished { get; private set; }
    /// <summary>While true the tween holds still (its time stops too).</summary>
    public bool Paused;

    /// <summary>The object this tween changes, if any (used by CancelTweensOf).</summary>
    public virtual object? Target => null;

    /// <summary>Names of the members this tween changes, if it tweens members (used by CancelTweensOf).</summary>
    public virtual IEnumerable<string> Fields => Array.Empty<string>();

    internal FlxTweenManager? Manager;
    float _time, _delay;
    bool _started, _backward;

    protected FlxTween(float duration, TweenOptions? options)
    {
        Duration = duration;
        options ??= new TweenOptions();
        Type = options.Type;
        Ease = options.Ease ?? FlxEase.Linear;
        StartDelay = options.StartDelay;
        LoopDelay = options.LoopDelay;
        OnStart = options.OnStart;
        OnUpdate = options.OnUpdate;
        OnComplete = options.OnComplete;
    }

    /// <summary>Starts (or restarts) the tween from the beginning, and adds it to FlxG.Tweens if needed.</summary>
    public FlxTween Start()
    {
        _time = 0;
        _delay = StartDelay;
        _started = false;
        _backward = Type == FlxTweenType.Backward;
        Executions = 0;
        Active = true;
        Finished = false;
        if (Manager == null) FlxG.Tweens.Add(this, start: false);
        return this;
    }

    /// <summary>Stops the tween where it is and removes it. OnComplete is not called.</summary>
    public void Cancel()
    {
        if (Finished && Manager == null) return;
        Active = false;
        Finished = true;
        Canceled();
        Manager?.Remove(this);
    }

    /// <summary>Jumps to the end value and finishes the tween (OnComplete is called).</summary>
    public void Finish()
    {
        if (!Active) return;
        if (!_started) { _started = true; Init(); OnStart?.Invoke(this); }
        Percent = 1;
        Scale = _backward ? 1 - Ease(1) : Ease(1);
        Apply();
        OnUpdate?.Invoke(this);
        Complete(forceEnd: true);
    }

    /// <summary>Called when the tween actually begins (after the start delay) - capture start values here.</summary>
    protected virtual void Init() { }

    /// <summary>Called every update with Scale set - write the values here.</summary>
    protected virtual void Apply() { }

    /// <summary>Called when a cycle ends, before OnComplete.</summary>
    protected virtual void Completed() { }

    /// <summary>Called when the tween is cancelled.</summary>
    protected virtual void Canceled() { }

    internal void Update(float dt)
    {
        if (!Active || Paused) return;

        _time += dt;
        if (_time < _delay) return;

        if (!_started)
        {
            _started = true;
            Init();
            OnStart?.Invoke(this);
        }

        float local = _time - _delay;
        Percent = Duration <= 0 ? 1f : MathHelper.Clamp(local / Duration, 0f, 1f);
        float eased = Ease(Percent);
        Scale = _backward ? 1 - eased : eased;

        Apply();
        OnUpdate?.Invoke(this);

        if (Percent >= 1f) Complete(forceEnd: false);
    }

    void Complete(bool forceEnd)
    {
        Executions++;
        Completed();

        switch (Type)
        {
            case FlxTweenType.Looping:
                if (forceEnd) { End(); break; }
                _time = 0; _delay = LoopDelay;
                OnComplete?.Invoke(this);
                return;

            case FlxTweenType.PingPong:
                if (forceEnd) { End(); break; }
                _backward = !_backward;
                _time = 0; _delay = LoopDelay;
                OnComplete?.Invoke(this);
                return;

            case FlxTweenType.Persist:
                Active = false;
                Finished = true;
                break;

            default: // OneShot, Backward
                Active = false;
                Finished = true;
                Manager?.Remove(this);
                break;
        }
        OnComplete?.Invoke(this);

        void End()
        {
            Active = false;
            Finished = true;
            Manager?.Remove(this);
        }
    }

    // ------------------------------------------------------------------ creating tweens

    /// <summary>
    /// Tweens members of <paramref name="target"/> to new values. <paramref name="values"/> is an anonymous object whose
    /// property names are the member names (any case): <c>new { X = 100, Alpha = 0 }</c>. A nested anonymous object
    /// reaches into a member: <c>new { Velocity = new { X = 0 } }</c>. Start values are read when the tween begins.
    /// </summary>
    public static FlxTween Tween(object target, object values, float duration, TweenOptions? options = null)
    {
        var vt = new VarTween(target, duration, options);
        vt.AddValues(values);
        return FlxG.Tweens.Add(vt);
    }

    /// <summary>Like Tween, with explicit member paths: <c>{ ["Scale.X"] = 2f }</c>.</summary>
    public static FlxTween Tween(object target, IDictionary<string, float> values, float duration, TweenOptions? options = null)
    {
        var vt = new VarTween(target, duration, options);
        foreach (var kv in values) vt.AddPath(kv.Key, kv.Value);
        return FlxG.Tweens.Add(vt);
    }

    /// <summary>Tweens a number and hands each value to <paramref name="setter"/>: <c>FlxTween.Num(0, 100, 1f, v => s.X = v)</c>.</summary>
    public static NumTween Num(float from, float to, float duration, Action<float>? setter = null, TweenOptions? options = null) =>
        FlxG.Tweens.Add(new NumTween(from, to, duration, setter, options));

    /// <summary>Tweens a color and hands each value to <paramref name="setter"/>: <c>FlxTween.Color(1f, Color.Red, Color.Blue, c => s.Tint = c)</c>.</summary>
    public static ColorTween Color(float duration, Color from, Color to, Action<Color>? setter = null, TweenOptions? options = null) =>
        FlxG.Tweens.Add(new ColorTween(duration, from, to, setter, options));

    /// <summary>Tweens a sprite's Angle (degrees). Pass float.NaN as <paramref name="from"/> to start at its current angle.</summary>
    public static NumTween Angle(FlxSprite sprite, float from, float to, float duration, TweenOptions? options = null)
    {
        var t = new NumTween(float.IsNaN(from) ? sprite.Angle : from, to, duration, v => sprite.Angle = v, options);
        return FlxG.Tweens.Add(t);
    }

    /// <summary>Shakes a sprite by changing its Offset. Intensity is a fraction of the sprite's size and fades out over the duration.</summary>
    public static ShakeTween Shake(FlxSprite sprite, float intensity = 0.05f, float duration = 1f, FlxAxes axes = FlxAxes.XY, TweenOptions? options = null) =>
        FlxG.Tweens.Add(new ShakeTween(sprite, intensity, duration, axes, options));

    /// <summary>Moves an object in a straight line. With useDuration false, <paramref name="durationOrSpeed"/> is a speed in pixels per second.</summary>
    public static LinearMotion LinearMotion(FlxObject? obj, float fromX, float fromY, float toX, float toY,
        float durationOrSpeed = 2f, bool useDuration = true, TweenOptions? options = null) =>
        FlxG.Tweens.Add(new LinearMotion(obj, fromX, fromY, toX, toY, durationOrSpeed, useDuration, options));

    /// <summary>Moves an object along a quadratic Bezier curve.</summary>
    public static QuadMotion QuadMotion(FlxObject? obj, float fromX, float fromY, float controlX, float controlY, float toX, float toY,
        float durationOrSpeed = 2f, bool useDuration = true, TweenOptions? options = null) =>
        FlxG.Tweens.Add(new QuadMotion(obj, fromX, fromY, controlX, controlY, toX, toY, durationOrSpeed, useDuration, options));

    /// <summary>Moves an object along a cubic Bezier curve.</summary>
    public static CubicMotion CubicMotion(FlxObject? obj, float fromX, float fromY, float aX, float aY, float bX, float bY, float toX, float toY,
        float durationOrSpeed = 2f, bool useDuration = true, TweenOptions? options = null) =>
        FlxG.Tweens.Add(new CubicMotion(obj, fromX, fromY, aX, aY, bX, bY, toX, toY, durationOrSpeed, useDuration, options));

    /// <summary>Moves an object once around a circle, starting at <paramref name="angle"/> degrees.</summary>
    public static CircularMotion CircularMotion(FlxObject? obj, float centerX, float centerY, float radius, float angle = 0f,
        bool clockwise = true, float durationOrSpeed = 2f, bool useDuration = true, TweenOptions? options = null) =>
        FlxG.Tweens.Add(new CircularMotion(obj, centerX, centerY, radius, angle, clockwise, durationOrSpeed, useDuration, options));

    /// <summary>Moves an object along straight segments through the points.</summary>
    public static LinearPath LinearPath(FlxObject? obj, IEnumerable<Vector2> points, float durationOrSpeed = 2f, bool useDuration = true, TweenOptions? options = null) =>
        FlxG.Tweens.Add(new LinearPath(obj, points, durationOrSpeed, useDuration, options));

    /// <summary>Moves an object along chained quadratic curves. Points are start, control, end, control, end... (an odd number, at least 3).</summary>
    public static QuadPath QuadPath(FlxObject? obj, IEnumerable<Vector2> points, float durationOrSpeed = 2f, bool useDuration = true, TweenOptions? options = null) =>
        FlxG.Tweens.Add(new QuadPath(obj, points, durationOrSpeed, useDuration, options));

    /// <summary>Cancels every tween that changes <paramref name="target"/> (only the named members, if you list some).</summary>
    public static void CancelTweensOf(object target, params string[] fields) => FlxG.Tweens.CancelTweensOf(target, fields);

    /// <summary>Finishes every tween that changes <paramref name="target"/> right now.</summary>
    public static void CompleteTweensOf(object target, params string[] fields) => FlxG.Tweens.CompleteTweensOf(target, fields);

    /// <summary>True if a running tween changes <paramref name="target"/>.</summary>
    public static bool IsTweening(object target, params string[] fields) => FlxG.Tweens.IsTweening(target, fields);
}

// ====================================================================== value tweens

/// <summary>Tweens one number. Read Value, or pass a setter.</summary>
public class NumTween : FlxTween
{
    public float From, To;
    public float Value { get; private set; }
    readonly Action<float>? _setter;

    public NumTween(float from, float to, float duration, Action<float>? setter, TweenOptions? options) : base(duration, options)
    {
        From = from; To = to; Value = from; _setter = setter;
    }

    protected override void Apply()
    {
        Value = From + (To - From) * Scale;
        _setter?.Invoke(Value);
    }
}

/// <summary>Tweens a color (all four channels).</summary>
public class ColorTween : FlxTween
{
    public Color From, To;
    public Color Value { get; private set; }
    readonly Action<Color>? _setter;

    public ColorTween(float duration, Color from, Color to, Action<Color>? setter, TweenOptions? options) : base(duration, options)
    {
        From = from; To = to; Value = from; _setter = setter;
    }

    protected override void Apply()
    {
        Value = Microsoft.Xna.Framework.Color.Lerp(From, To, Scale);
        _setter?.Invoke(Value);
    }
}

/// <summary>Tweens numeric members of an object, found by name. See FlxTween.Tween.</summary>
public class VarTween : FlxTween
{
    sealed class Entry
    {
        public string[] Path = Array.Empty<string>();
        public float To, From;
    }

    readonly object _target;
    readonly List<Entry> _entries = new();

    public VarTween(object target, float duration, TweenOptions? options) : base(duration, options)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public override object? Target => _target;

    public override IEnumerable<string> Fields
    {
        get { foreach (var e in _entries) yield return string.Join('.', e.Path); }
    }

    public void AddPath(string path, float to)
    {
        var p = path.Split('.');
        FlxMembers.Resolve(_target, p);          // fail early on a bad name
        _entries.Add(new Entry { Path = p, To = to });
    }

    /// <summary>Adds the properties of an anonymous object (nested objects reach into members).</summary>
    public void AddValues(object values) => Flatten(values, "");

    void Flatten(object values, string prefix)
    {
        foreach (var prop in values.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            object? v = prop.GetValue(values);
            if (v == null) continue;
            string path = prefix + prop.Name;
            if (FlxMembers.IsNumber(v)) AddPath(path, Convert.ToSingle(v, CultureInfo.InvariantCulture));
            else Flatten(v, path + ".");
        }
    }

    protected override void Init()
    {
        foreach (var e in _entries) e.From = FlxMembers.Get(_target, e.Path);
    }

    protected override void Apply()
    {
        foreach (var e in _entries) FlxMembers.Set(_target, e.Path, e.From + (e.To - e.From) * Scale);
    }
}

/// <summary>Shakes a sprite by changing its Offset; the shake fades out and the original Offset is restored at the end.</summary>
public class ShakeTween : FlxTween
{
    readonly FlxSprite _sprite;
    readonly float _intensity;
    readonly FlxAxes _axes;
    Vector2 _initial;

    public ShakeTween(FlxSprite sprite, float intensity, float duration, FlxAxes axes, TweenOptions? options) : base(duration, options)
    {
        _sprite = sprite; _intensity = intensity; _axes = axes;
    }

    public override object? Target => _sprite;

    protected override void Init() => _initial = _sprite.Offset;

    protected override void Apply()
    {
        float fade = 1f - Percent;
        var o = _initial;
        if ((_axes & FlxAxes.X) != 0) o.X += FlxG.Random.Float(-1, 1) * _intensity * _sprite.Width * fade;
        if ((_axes & FlxAxes.Y) != 0) o.Y += FlxG.Random.Float(-1, 1) * _intensity * _sprite.Height * fade;
        _sprite.Offset = o;
    }

    protected override void Completed() => _sprite.Offset = _initial;
    protected override void Canceled() { if (Executions == 0 && _initial != default) _sprite.Offset = _initial; }
}

// ====================================================================== motion tweens

/// <summary>Base of the tweens that compute an X/Y position and move an object there.</summary>
public abstract class Motion : FlxTween
{
    public float X { get; protected set; }
    public float Y { get; protected set; }
    public FlxObject? Object { get; set; }

    protected Motion(FlxObject? obj, float duration, TweenOptions? options) : base(duration, options) { Object = obj; }

    public override object? Target => Object;

    protected abstract void Position(float t, out float x, out float y);

    protected override void Apply()
    {
        Position(Scale, out float x, out float y);
        X = x; Y = y;
        if (Object != null) { Object.X = x; Object.Y = y; }
    }

    /// <summary>Length of a curve, measured by walking along it (used when a speed is given instead of a duration).</summary>
    protected float MeasureCurve(int steps = 48)
    {
        Position(0, out float px, out float py);
        float len = 0;
        for (int i = 1; i <= steps; i++)
        {
            Position(i / (float)steps, out float x, out float y);
            len += MathF.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
            px = x; py = y;
        }
        return len;
    }

    protected void SetDuration(float durationOrSpeed, bool useDuration, float distance) =>
        Duration = useDuration ? durationOrSpeed : (durationOrSpeed <= 0 ? 0 : distance / durationOrSpeed);
}

public sealed class LinearMotion : Motion
{
    readonly float _fx, _fy, _tx, _ty;

    public LinearMotion(FlxObject? obj, float fromX, float fromY, float toX, float toY, float durationOrSpeed, bool useDuration, TweenOptions? options)
        : base(obj, 0, options)
    {
        _fx = fromX; _fy = fromY; _tx = toX; _ty = toY; X = fromX; Y = fromY;
        SetDuration(durationOrSpeed, useDuration, MathF.Sqrt((toX - fromX) * (toX - fromX) + (toY - fromY) * (toY - fromY)));
    }

    protected override void Position(float t, out float x, out float y)
    {
        x = _fx + (_tx - _fx) * t;
        y = _fy + (_ty - _fy) * t;
    }
}

public sealed class QuadMotion : Motion
{
    readonly float _fx, _fy, _cx, _cy, _tx, _ty;

    public QuadMotion(FlxObject? obj, float fromX, float fromY, float controlX, float controlY, float toX, float toY,
        float durationOrSpeed, bool useDuration, TweenOptions? options) : base(obj, 0, options)
    {
        _fx = fromX; _fy = fromY; _cx = controlX; _cy = controlY; _tx = toX; _ty = toY; X = fromX; Y = fromY;
        SetDuration(durationOrSpeed, useDuration, MeasureCurve());
    }

    protected override void Position(float t, out float x, out float y)
    {
        float u = 1 - t;
        x = u * u * _fx + 2 * u * t * _cx + t * t * _tx;
        y = u * u * _fy + 2 * u * t * _cy + t * t * _ty;
    }
}

public sealed class CubicMotion : Motion
{
    readonly float _fx, _fy, _ax, _ay, _bx, _by, _tx, _ty;

    public CubicMotion(FlxObject? obj, float fromX, float fromY, float aX, float aY, float bX, float bY, float toX, float toY,
        float durationOrSpeed, bool useDuration, TweenOptions? options) : base(obj, 0, options)
    {
        _fx = fromX; _fy = fromY; _ax = aX; _ay = aY; _bx = bX; _by = bY; _tx = toX; _ty = toY; X = fromX; Y = fromY;
        SetDuration(durationOrSpeed, useDuration, MeasureCurve());
    }

    protected override void Position(float t, out float x, out float y)
    {
        float u = 1 - t;
        x = u * u * u * _fx + 3 * u * u * t * _ax + 3 * u * t * t * _bx + t * t * t * _tx;
        y = u * u * u * _fy + 3 * u * u * t * _ay + 3 * u * t * t * _by + t * t * t * _ty;
    }
}

public sealed class CircularMotion : Motion
{
    readonly float _cx, _cy, _radius, _startAngle;
    readonly bool _clockwise;

    public CircularMotion(FlxObject? obj, float centerX, float centerY, float radius, float angle, bool clockwise,
        float durationOrSpeed, bool useDuration, TweenOptions? options) : base(obj, 0, options)
    {
        _cx = centerX; _cy = centerY; _radius = radius; _startAngle = MathHelper.ToRadians(angle); _clockwise = clockwise;
        Position(0, out float x, out float y); X = x; Y = y;
        SetDuration(durationOrSpeed, useDuration, 2 * MathF.PI * radius);
    }

    protected override void Position(float t, out float x, out float y)
    {
        float a = _startAngle + (_clockwise ? 1 : -1) * t * 2 * MathF.PI;
        x = _cx + MathF.Cos(a) * _radius;
        y = _cy + MathF.Sin(a) * _radius;
    }
}

/// <summary>Moves along straight segments at a constant pace (the Ease is applied to the whole path).</summary>
public sealed class LinearPath : Motion
{
    readonly List<Vector2> _points;
    readonly float[] _cumulative;
    readonly float _length;

    public LinearPath(FlxObject? obj, IEnumerable<Vector2> points, float durationOrSpeed, bool useDuration, TweenOptions? options) : base(obj, 0, options)
    {
        _points = new List<Vector2>(points);
        if (_points.Count < 2) throw new ArgumentException("A path needs at least two points.", nameof(points));

        _cumulative = new float[_points.Count];
        for (int i = 1; i < _points.Count; i++) _cumulative[i] = _cumulative[i - 1] + Vector2.Distance(_points[i - 1], _points[i]);
        _length = _cumulative[^1];

        X = _points[0].X; Y = _points[0].Y;
        SetDuration(durationOrSpeed, useDuration, _length);
    }

    protected override void Position(float t, out float x, out float y)
    {
        if (_length <= 0) { x = _points[0].X; y = _points[0].Y; return; }

        float d = t * _length;
        int i = 1;
        while (i < _points.Count - 1 && _cumulative[i] < d) i++;

        float seg = _cumulative[i] - _cumulative[i - 1];
        float k = seg <= 0 ? 1 : MathHelper.Clamp((d - _cumulative[i - 1]) / seg, 0, 1);
        var p = Vector2.Lerp(_points[i - 1], _points[i], k);
        x = p.X; y = p.Y;
    }
}

/// <summary>Moves along chained quadratic curves: start, control, end, control, end, ...</summary>
public sealed class QuadPath : Motion
{
    readonly List<Vector2> _points;
    readonly float[] _cumulative;
    readonly int _curves;

    public QuadPath(FlxObject? obj, IEnumerable<Vector2> points, float durationOrSpeed, bool useDuration, TweenOptions? options) : base(obj, 0, options)
    {
        _points = new List<Vector2>(points);
        if (_points.Count < 3 || _points.Count % 2 == 0)
            throw new ArgumentException("A quad path needs an odd number of points (start, then control + end pairs).", nameof(points));

        _curves = (_points.Count - 1) / 2;
        _cumulative = new float[_curves + 1];
        for (int c = 0; c < _curves; c++) _cumulative[c + 1] = _cumulative[c] + CurveLength(c);

        X = _points[0].X; Y = _points[0].Y;
        SetDuration(durationOrSpeed, useDuration, _cumulative[^1]);
    }

    Vector2 CurvePoint(int c, float t)
    {
        var a = _points[c * 2]; var b = _points[c * 2 + 1]; var e = _points[c * 2 + 2];
        float u = 1 - t;
        return a * (u * u) + b * (2 * u * t) + e * (t * t);
    }

    float CurveLength(int c)
    {
        float len = 0;
        var prev = CurvePoint(c, 0);
        for (int i = 1; i <= 24; i++)
        {
            var p = CurvePoint(c, i / 24f);
            len += Vector2.Distance(prev, p);
            prev = p;
        }
        return len;
    }

    protected override void Position(float t, out float x, out float y)
    {
        float total = _cumulative[^1];
        float d = t * total;
        int c = 0;
        while (c < _curves - 1 && _cumulative[c + 1] < d) c++;

        float len = _cumulative[c + 1] - _cumulative[c];
        float k = len <= 0 ? 1 : MathHelper.Clamp((d - _cumulative[c]) / len, 0, 1);
        var p = CurvePoint(c, k);
        x = p.X; y = p.Y;
    }
}

// ====================================================================== manager

/// <summary>Owns the running tweens. FlxG.Tweens is the global one; it is cleared when the state changes.</summary>
public class FlxTweenManager
{
    readonly List<FlxTween> _tweens = new();

    public int Count => _tweens.Count;

    /// <summary>Adds a tween to this manager (and starts it unless <paramref name="start"/> is false).</summary>
    public T Add<T>(T tween, bool start = true) where T : FlxTween
    {
        if (tween.Manager == null)
        {
            tween.Manager = this;
            _tweens.Add(tween);
        }
        if (start) tween.Start();
        return tween;
    }

    internal void Remove(FlxTween tween)
    {
        if (_tweens.Remove(tween)) tween.Manager = null;
    }

    internal void Clear()
    {
        foreach (var t in _tweens) t.Manager = null;
        _tweens.Clear();
    }

    internal void Update(float dt)
    {
        // tweens may start or cancel other tweens from their callbacks, so walk a copy
        foreach (var t in _tweens.ToArray()) t.Update(dt);
    }

    public void ForEach(Action<FlxTween> action)
    {
        foreach (var t in _tweens.ToArray()) action(t);
    }

    bool Matches(FlxTween t, object target, string[] fields)
    {
        if (!ReferenceEquals(t.Target, target)) return false;
        if (fields.Length == 0) return true;
        foreach (var f in t.Fields)
            foreach (var wanted in fields)
                if (string.Equals(f, wanted, StringComparison.OrdinalIgnoreCase) || f.StartsWith(wanted + ".", StringComparison.OrdinalIgnoreCase))
                    return true;
        return false;
    }

    public void CancelTweensOf(object target, params string[] fields)
    {
        foreach (var t in _tweens.ToArray()) if (Matches(t, target, fields)) t.Cancel();
    }

    public void CompleteTweensOf(object target, params string[] fields)
    {
        foreach (var t in _tweens.ToArray()) if (Matches(t, target, fields)) t.Finish();
    }

    public bool IsTweening(object target, params string[] fields)
    {
        foreach (var t in _tweens) if (t.Active && Matches(t, target, fields)) return true;
        return false;
    }
}

// ====================================================================== member access by name

/// <summary>Reads and writes numeric fields / properties by name, including into structs like Vector2 ("Scale.X").</summary>
static class FlxMembers
{
    static readonly Dictionary<(Type, string), MemberInfo> Cache = new();

    public static bool IsNumber(object v) =>
        v is float or double or int or long or short or byte or sbyte or uint or ushort or ulong or decimal;

    static MemberInfo Find(Type type, string name)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue((type, name), out var cached)) return cached;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            MemberInfo? m = (MemberInfo?)type.GetField(name, flags) ?? type.GetProperty(name, flags);
            if (m == null) throw new ArgumentException($"{type.Name} has no public field or property '{name}'.");
            Cache[(type, name)] = m;
            return m;
        }
    }

    static object? GetValue(object obj, MemberInfo m) => m is FieldInfo f ? f.GetValue(obj) : ((PropertyInfo)m).GetValue(obj);

    static void SetValue(object obj, MemberInfo m, object value)
    {
        if (m is FieldInfo f) f.SetValue(obj, value);
        else ((PropertyInfo)m).SetValue(obj, value);
    }

    static Type TypeOf(MemberInfo m) => m is FieldInfo f ? f.FieldType : ((PropertyInfo)m).PropertyType;

    /// <summary>Checks that the whole path exists (throws ArgumentException otherwise).</summary>
    public static void Resolve(object target, string[] path)
    {
        Type t = target.GetType();
        for (int i = 0; i < path.Length; i++)
        {
            var m = Find(t, path[i]);
            t = TypeOf(m);
        }
    }

    public static float Get(object target, string[] path)
    {
        object? cur = target;
        foreach (var name in path)
        {
            if (cur == null) throw new InvalidOperationException($"'{name}' is null.");
            cur = GetValue(cur, Find(cur.GetType(), name));
        }
        return Convert.ToSingle(cur, CultureInfo.InvariantCulture);
    }

    public static void Set(object target, string[] path, float value) => SetAt(target, path, 0, value);

    static void SetAt(object obj, string[] path, int i, float value)
    {
        var m = Find(obj.GetType(), path[i]);
        var type = TypeOf(m);

        if (i == path.Length - 1)
        {
            object v = type == typeof(float) ? value
                : type == typeof(double) ? (double)value
                : Convert.ChangeType(MathF.Round(value), type, CultureInfo.InvariantCulture);
            SetValue(obj, m, v);
            return;
        }

        object child = GetValue(obj, m) ?? throw new InvalidOperationException($"'{path[i]}' is null.");
        SetAt(child, path, i + 1, value);
        if (type.IsValueType) SetValue(obj, m, child);   // a struct was copied out, so put the changed copy back
    }
}
