using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>
/// A group of sprites that moves as one unit. Like Flixel, members are positioned RELATIVE to the group:
/// adding a sprite at (10,0) to a group at (100,50) puts it at (110,50). Moving the group (X/Y or Velocity)
/// moves every member. FlxG.Collide/Overlap treat it as a group of its members.
/// </summary>
public class FlxTypedSpriteGroup<T> : FlxSprite, IFlxGroup where T : FlxSprite
{
    public readonly List<T> Members = new();
    float _syncX, _syncY;

    IReadOnlyList<FlxBasic> IFlxGroup.Basics => Members;

    public FlxTypedSpriteGroup(float x = 0, float y = 0) : base(x, y)
    {
        _syncX = x; _syncY = y;
    }

    public T Add(T m)
    {
        if (Members.Contains(m)) return m;
        Sync();
        m.X += X; m.Y += Y;
        m.Last = new Vector2(m.X, m.Y);
        Members.Add(m);
        UpdateBounds();
        return m;
    }

    public bool Remove(T m)
    {
        bool removed = Members.Remove(m);
        if (removed) UpdateBounds();
        return removed;
    }

    /// <summary>Recalculate Width/Height from the members (measured from the group's position).</summary>
    public void UpdateBounds()
    {
        float r = X, b = Y;
        foreach (var m in Members) { r = MathF.Max(r, m.X + m.Width); b = MathF.Max(b, m.Y + m.Height); }
        Width = r - X; Height = b - Y;
    }

    public void ForEach(Action<T> action)
    {
        for (int i = 0; i < Members.Count; i++) action(Members[i]);
    }

    // Moves all members by however far the group moved since the last sync.
    void Sync()
    {
        float dx = X - _syncX, dy = Y - _syncY;
        if (dx == 0 && dy == 0) return;
        foreach (var m in Members) { m.X += dx; m.Y += dy; }
        _syncX = X; _syncY = Y;
    }

    public override void Update(float elapsed)
    {
        Sync();                                   // the group was moved by hand
        for (int i = 0; i < Members.Count; i++)
        {
            var m = Members[i];
            if (m.Exists && m.Active) m.Update(elapsed);
        }
        base.Update(elapsed);                     // group's own motion (Velocity etc.)
        Sync();                                   // carry members along
    }

    public override void Draw()
    {
        Sync();
        for (int i = 0; i < Members.Count; i++)
        {
            var m = Members[i];
            if (m.Exists && m.Visible && m.OnCamera(FlxG.Camera)) FlxG.DrawChild(m);
        }
    }

    public override void Kill() { base.Kill(); foreach (var m in Members) m.Kill(); }
    public override void Revive() { base.Revive(); foreach (var m in Members) m.Revive(); }

    public override void Destroy()
    {
        foreach (var m in Members) m.Destroy();
        Members.Clear();
        base.Destroy();
    }
}

public class FlxSpriteGroup : FlxTypedSpriteGroup<FlxSprite>
{
    public FlxSpriteGroup(float x = 0, float y = 0) : base(x, y) { }
}
