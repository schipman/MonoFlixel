using System;
using System.Collections.Generic;

namespace MonoFlixel;

public class FlxTimer
{
    public float Time { get; private set; }
    public int Loops { get; private set; }       // 0 = forever
    public int ElapsedLoops { get; private set; }
    public bool Active { get; private set; }

    Action<FlxTimer>? _callback;
    float _elapsed;

    public int LoopsLeft => Loops == 0 ? int.MaxValue : Loops - ElapsedLoops;

    public FlxTimer Start(float time = 1f, Action<FlxTimer>? callback = null, int loops = 1)
    {
        Time = MathF.Max(time, 0.0001f); Loops = loops; _callback = callback;
        ElapsedLoops = 0; _elapsed = 0; Active = true;
        FlxG.Timers.Add(this);
        return this;
    }

    public void Cancel() { Active = false; }

    internal void Update(float dt)
    {
        if (!Active) return;
        _elapsed += dt;
        while (Active && _elapsed >= Time)
        {
            _elapsed -= Time;
            ElapsedLoops++;
            _callback?.Invoke(this);
            if (Loops > 0 && ElapsedLoops >= Loops) Active = false;
        }
    }
}

public class FlxTimerManager
{
    readonly List<FlxTimer> _timers = new();

    internal void Add(FlxTimer t) { if (!_timers.Contains(t)) _timers.Add(t); }
    internal void Clear() { foreach (var t in _timers) t.Cancel(); _timers.Clear(); }

    internal void Update(float dt)
    {
        foreach (var t in _timers.ToArray()) t.Update(dt);
        _timers.RemoveAll(t => !t.Active);
    }
}
