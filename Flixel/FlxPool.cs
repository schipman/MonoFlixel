using System;
using System.Collections.Generic;

namespace MonoFlixel;

/// <summary>
/// Reuses objects instead of creating new ones all the time (bullets, particles, vectors in hot loops...).
/// <code>
/// var pool = new FlxPool&lt;Bullet&gt;(() => new Bullet());
/// var b = pool.Get();     // a recycled bullet, or a new one
/// pool.Put(b);            // give it back when done
/// </code>
/// </summary>
public sealed class FlxPool<T> where T : class
{
    readonly Func<T> _factory;
    readonly Stack<T> _free = new();

    /// <summary>Called on an object when it is taken from the pool (reset it here).</summary>
    public Action<T>? OnGet;

    /// <summary>Called on an object when it is given back (release references here).</summary>
    public Action<T>? OnPut;

    /// <summary>The pool keeps at most this many free objects (0 = no limit); extras are left to the garbage collector.</summary>
    public int MaxSize;

    public FlxPool(Func<T> factory, int maxSize = 0)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        MaxSize = maxSize;
    }

    /// <summary>Number of free objects waiting in the pool.</summary>
    public int Count => _free.Count;

    public T Get()
    {
        var item = _free.Count > 0 ? _free.Pop() : _factory();
        OnGet?.Invoke(item);
        return item;
    }

    public void Put(T item)
    {
        if (item == null) return;
        OnPut?.Invoke(item);
        if (MaxSize <= 0 || _free.Count < MaxSize) _free.Push(item);
    }

    /// <summary>Creates objects ahead of time so the first Get() calls don't allocate.</summary>
    public void Preallocate(int amount)
    {
        for (int i = 0; i < amount; i++) _free.Push(_factory());
    }

    public void Clear() => _free.Clear();
}
