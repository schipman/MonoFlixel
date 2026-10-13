using System;
using System.Collections.Generic;

namespace MonoFlixel;

public interface IFlxGroup { IReadOnlyList<FlxBasic> Basics { get; } }

public class FlxTypedGroup<T> : FlxBasic, IFlxGroup where T : FlxBasic
{
    public readonly List<T> Members = new();
    public int MaxSize;   // 0 = unlimited (used by Recycle)
    int _marker;

    IReadOnlyList<FlxBasic> IFlxGroup.Basics => Members;

    public FlxTypedGroup(int maxSize = 0) { MaxSize = maxSize; }

    public int Length => Members.Count;

    public T Add(T m) { if (!Members.Contains(m)) Members.Add(m); return m; }
    public bool Remove(T m) => Members.Remove(m);
    public void Clear() { Members.Clear(); _marker = 0; }

    public override void Update(float elapsed)
    {
        for (int i = 0; i < Members.Count; i++)
        {
            var m = Members[i];
            if (m != null && m.Exists && m.Active) m.Update(elapsed);
        }
    }

    public override void Draw()
    {
        var cam = FlxG.Camera;
        for (int i = 0; i < Members.Count; i++)
        {
            var m = Members[i];
            if (m != null && m.Exists && m.Visible && m.OnCamera(cam)) FlxG.DrawChild(m);
        }
    }

    public T? GetFirstAvailable() => Members.Find(m => !m.Exists);

    /// <summary>First member that exists and is alive.</summary>
    public T? GetFirstAlive() => Members.Find(m => m != null && m.Exists && m.Alive);

    /// <summary>First member that is dead (not alive).</summary>
    public T? GetFirstDead() => Members.Find(m => m != null && !m.Alive);

    /// <summary>A random member from the given range of the member list (the whole list by default).</summary>
    public T? GetRandom(int startIndex = 0, int length = 0)
    {
        if (Members.Count == 0) return null;
        startIndex = System.Math.Clamp(startIndex, 0, Members.Count - 1);
        if (length <= 0 || startIndex + length > Members.Count) length = Members.Count - startIndex;
        return Members[FlxG.Random.Int(startIndex, startIndex + length - 1)];
    }

    /// <summary>
    /// Sorts the members, e.g. <c>group.Sort(FlxSort.ByY)</c> to draw lower things on top.
    /// Stable: members that compare equal keep their order.
    /// </summary>
    public void Sort(Func<int, T, T, int> compare, int order = FlxSort.Ascending)
    {
        var indexed = new (T Item, int Index)[Members.Count];
        for (int i = 0; i < indexed.Length; i++) indexed[i] = (Members[i], i);

        Array.Sort(indexed, (a, b) =>
        {
            int c = compare(order, a.Item, b.Item);
            return c != 0 ? c : a.Index.CompareTo(b.Index);
        });

        for (int i = 0; i < indexed.Length; i++) Members[i] = indexed[i].Item;
    }

    /// <summary>
    /// Same rules as Flixel's recycle(): with MaxSize set, new members are created until the group is full,
    /// after which members are reused round-robin (even live ones). Without MaxSize, a dead member is reused,
    /// or a new one is created. The returned member is revived.
    /// </summary>
    public T Recycle(Func<T> factory)
    {
        if (MaxSize > 0)
        {
            if (Members.Count < MaxSize) return Add(factory());
            var m = Members[_marker++];
            if (_marker >= MaxSize) _marker = 0;
            m.Revive();
            return m;
        }

        var free = GetFirstAvailable();
        if (free != null) { free.Revive(); return free; }
        return Add(factory());
    }

    public int CountDead() { int n = 0; foreach (var m in Members) if (!m.Alive) n++; return n; }

    public int CountLiving() { int n = 0; foreach (var m in Members) if (m.Exists && m.Alive) n++; return n; }

    public void ForEach(Action<T> action)
    {
        for (int i = 0; i < Members.Count; i++) action(Members[i]);
    }

    public void ForEachAlive(Action<T> action)
    {
        for (int i = 0; i < Members.Count; i++)
            if (Members[i].Exists && Members[i].Alive) action(Members[i]);
    }

    public override void Destroy()
    {
        foreach (var m in Members) m.Destroy();
        Members.Clear();
        base.Destroy();
    }
}

public class FlxGroup : FlxTypedGroup<FlxBasic>
{
    public FlxGroup(int maxSize = 0) : base(maxSize) { }
}

public static class FlxGroupExtensions
{
    /// <summary>
    /// Sorts a group by object properties: <c>group.Sort(FlxSort.ByY)</c>. This is what makes it work on a plain
    /// FlxGroup (whose members are FlxBasic); members that aren't FlxObjects count as equal and keep their order.
    /// </summary>
    public static void Sort<T>(this FlxTypedGroup<T> group, Func<int, FlxObject, FlxObject, int> compare, int order = FlxSort.Ascending)
        where T : FlxBasic =>
        group.Sort((o, a, b) => a is FlxObject fa && b is FlxObject fb ? compare(o, fa, fb) : 0, order);
}
