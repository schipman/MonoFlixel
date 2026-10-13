using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>A list of callbacks you can add to and remove from, and call all at once with Dispatch().</summary>
public sealed class FlxSignal
{
    readonly List<(Action Handler, bool Once)> _handlers = new();

    public int Count => _handlers.Count;

    public void Add(Action handler) => _handlers.Add((handler, false));

    /// <summary>Adds a callback that removes itself after it ran once.</summary>
    public void AddOnce(Action handler) => _handlers.Add((handler, true));

    public void Remove(Action handler) => _handlers.RemoveAll(h => h.Handler == handler);

    public bool Has(Action handler) => _handlers.Exists(h => h.Handler == handler);

    public void RemoveAll() => _handlers.Clear();

    public void Dispatch()
    {
        if (_handlers.Count == 0) return;

        foreach (var (handler, once) in _handlers.ToArray())
        {
            if (once) Remove(handler);
            handler();
        }
    }
}

/// <summary>Like FlxSignal, but passes one value to every callback.</summary>
public sealed class FlxSignal<T>
{
    readonly List<(Action<T> Handler, bool Once)> _handlers = new();

    public int Count => _handlers.Count;

    public void Add(Action<T> handler) => _handlers.Add((handler, false));
    public void AddOnce(Action<T> handler) => _handlers.Add((handler, true));
    public void Remove(Action<T> handler) => _handlers.RemoveAll(h => h.Handler == handler);
    public bool Has(Action<T> handler) => _handlers.Exists(h => h.Handler == handler);
    public void RemoveAll() => _handlers.Clear();

    public void Dispatch(T value)
    {
        if (_handlers.Count == 0) return;

        foreach (var (handler, once) in _handlers.ToArray())
        {
            if (once) Remove(handler);
            handler(value);
        }
    }
}

/// <summary>Game-wide events (FlxG.Signals). Callbacks added here survive state changes - remove them yourself.</summary>
public sealed class FlxSignals
{
    /// <summary>A new state is about to replace the current one (the old state is not destroyed yet).</summary>
    public readonly FlxSignal PreStateSwitch = new();

    /// <summary>A new state was created and is now the current one.</summary>
    public readonly FlxSignal PostStateSwitch = new();

    public readonly FlxSignal PreUpdate = new(), PostUpdate = new();
    public readonly FlxSignal PreDraw = new(), PostDraw = new();

    /// <summary>The window (or the game resolution) changed; the value is the new window size in pixels.</summary>
    public readonly FlxSignal<Point> GameResized = new();
}
