using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoFlixel;

/// <summary>Blink an object's Visible flag for a while (damage / invincibility effect).</summary>
public static class FlxFlicker
{
    static readonly Dictionary<FlxBasic, FlxTimer> Active = new();

    public static bool IsFlickering(FlxBasic obj) => Active.TryGetValue(obj, out var t) && t.Active;

    public static void Flicker(FlxBasic obj, float duration = 1f, float interval = 0.04f,
        bool endVisible = true, Action<FlxBasic>? onComplete = null)
    {
        Stop(obj);
        foreach (var k in Active.Where(kv => !kv.Value.Active).Select(kv => kv.Key).ToList()) Active.Remove(k);

        interval = MathF.Max(interval, 0.001f);
        int loops = Math.Max(1, (int)(duration / interval));
        var timer = new FlxTimer();
        Active[obj] = timer;
        timer.Start(interval, t =>
        {
            if (t.ElapsedLoops >= loops)
            {
                obj.Visible = endVisible;
                Active.Remove(obj);
                onComplete?.Invoke(obj);
            }
            else obj.Visible = !obj.Visible;
        }, loops);
    }

    public static void Stop(FlxBasic obj, bool visible = true)
    {
        if (Active.Remove(obj, out var t)) { t.Cancel(); obj.Visible = visible; }
    }
}
