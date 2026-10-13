using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

public enum FlxPathMode
{
    /// <summary>Visit the nodes in order, then stop.</summary>
    Forward,
    /// <summary>Visit the nodes from the last to the first, then stop.</summary>
    Backward,
    /// <summary>Go forward, then start again at the first node, forever.</summary>
    LoopForward,
    /// <summary>Go backward, then start again at the last node, forever.</summary>
    LoopBackward,
    /// <summary>Go forward, then backward, then forward again, forever.</summary>
    Yoyo
}

/// <summary>
/// Makes an object follow a list of points at a constant speed. Port of Flixel's FlxPath.
/// The object's <i>middle</i> travels through the nodes. Attach it with <see cref="FlxObject.FollowPath"/>, or build one:
/// <code>
/// enemy.FollowPath(new[] { new Vector2(40, 40), new Vector2(200, 40), new Vector2(200, 120) }, speed: 60, FlxPathMode.Yoyo);
///
/// var path = tilemap.FindPath(enemy.Midpoint, player.Midpoint);    // A* through the level
/// if (path != null) enemy.FollowPath(path, 80);
/// </code>
/// </summary>
public class FlxPath
{
    public readonly List<Vector2> Nodes = new();

    public float Speed = 100f;
    public FlxPathMode Mode = FlxPathMode.Forward;

    /// <summary>Turn the object (its Angle) to face the way it moves.</summary>
    public bool AutoRotate;

    /// <summary>Added to Angle when AutoRotate is on (if the sprite faces up instead of right, use 90).</summary>
    public float AngleOffset;

    /// <summary>True while the object is following the path.</summary>
    public bool Active { get; private set; }

    /// <summary>True after a Forward / Backward path reached its last node.</summary>
    public bool Finished { get; private set; }

    /// <summary>Index of the node the object is heading to.</summary>
    public int NodeIndex { get; private set; }

    /// <summary>The object that follows this path (set by FlxObject.FollowPath).</summary>
    public FlxObject? Object { get; internal set; }

    /// <summary>Called each time the object reaches a node (with that node's index).</summary>
    public Action<int>? OnNodeReached;

    /// <summary>Called when a Forward / Backward path ends.</summary>
    public Action<FlxPath>? OnComplete;

    int _step = 1;

    public FlxPath(IEnumerable<Vector2>? nodes = null)
    {
        if (nodes != null) Nodes.AddRange(nodes);
    }

    // ------------------------------------------------------------------ nodes

    public FlxPath Add(Vector2 node) { Nodes.Add(node); return this; }
    public FlxPath Add(float x, float y) => Add(new Vector2(x, y));
    public FlxPath Insert(int index, Vector2 node) { Nodes.Insert(index, node); return this; }
    public bool Remove(Vector2 node) => Nodes.Remove(node);

    // ------------------------------------------------------------------ control

    /// <summary>
    /// Starts following the path. Pass nodes to replace the current ones. The object first moves to the first node
    /// (the last one for the backward modes), then through the rest.
    /// </summary>
    public FlxPath Start(IEnumerable<Vector2>? nodes = null, float? speed = null, FlxPathMode? mode = null, bool? autoRotate = null)
    {
        if (nodes != null) { Nodes.Clear(); Nodes.AddRange(nodes); }
        if (speed.HasValue) Speed = speed.Value;
        if (mode.HasValue) Mode = mode.Value;
        if (autoRotate.HasValue) AutoRotate = autoRotate.Value;

        Finished = false;
        if (Nodes.Count == 0) { Active = false; return this; }

        bool backward = Mode is FlxPathMode.Backward or FlxPathMode.LoopBackward;
        _step = backward ? -1 : 1;
        NodeIndex = backward ? Nodes.Count - 1 : 0;
        Active = true;
        return this;
    }

    /// <summary>Stops following the path and stops the object.</summary>
    public void Cancel()
    {
        Active = false;
        if (Object != null) Object.Velocity = Vector2.Zero;
    }

    // ------------------------------------------------------------------ update

    /// <summary>Called by the object every frame, before it moves. Points the object's velocity at the current node.</summary>
    internal void UpdatePath(float elapsed)
    {
        var obj = Object;
        if (!Active || obj == null || Nodes.Count == 0) return;

        // reaching a node can immediately make the next one reachable (nodes closer together than one step), so loop
        for (int guard = 0; guard < Nodes.Count + 1 && Active; guard++)
        {
            var target = Nodes[NodeIndex];
            var mid = obj.Midpoint;
            float dist = Vector2.Distance(mid, target);

            if (dist > Speed * elapsed + 0.0001f)
            {
                var dir = (target - mid) / dist;
                obj.Velocity = dir * Speed;
                if (AutoRotate) obj.Angle = MathHelper.ToDegrees(MathF.Atan2(dir.Y, dir.X)) + AngleOffset;
                return;
            }

            // close enough to land on the node this frame
            obj.X = target.X - obj.Width * 0.5f;
            obj.Y = target.Y - obj.Height * 0.5f;
            int reached = NodeIndex;
            Advance();
            OnNodeReached?.Invoke(reached);

            if (!Active) obj.Velocity = Vector2.Zero;
        }
    }

    void Advance()
    {
        int n = Nodes.Count;
        NodeIndex += _step;

        switch (Mode)
        {
            case FlxPathMode.Forward:
                if (NodeIndex >= n) Complete();
                break;
            case FlxPathMode.Backward:
                if (NodeIndex < 0) Complete();
                break;
            case FlxPathMode.LoopForward:
                if (NodeIndex >= n) NodeIndex = 0;
                break;
            case FlxPathMode.LoopBackward:
                if (NodeIndex < 0) NodeIndex = n - 1;
                break;
            case FlxPathMode.Yoyo:
                if (_step > 0 && NodeIndex >= n) { _step = -1; NodeIndex = Math.Max(0, n - 2); }
                else if (_step < 0 && NodeIndex < 0) { _step = 1; NodeIndex = Math.Min(n - 1, 1); }
                break;
        }
    }

    void Complete()
    {
        NodeIndex = Math.Clamp(NodeIndex, 0, Nodes.Count - 1);
        Active = false;
        Finished = true;
        OnComplete?.Invoke(this);
    }

    // ------------------------------------------------------------------ debug

    /// <summary>Draws the path as lines with a dot on each node (call from Draw()).</summary>
    public void DebugDraw(Color lineColor, Color? nodeColor = null, float thickness = 1f)
    {
        for (int i = 1; i < Nodes.Count; i++) FlxG.DrawLine(Nodes[i - 1], Nodes[i], lineColor, thickness);

        var cam = FlxG.Camera;
        var dot = nodeColor ?? lineColor;
        foreach (var n in Nodes)
            FlxG.SpriteBatch.Draw(FlxG.Pixel,
                new Rectangle((int)MathF.Round(n.X - cam.ScrollX) - 1, (int)MathF.Round(n.Y - cam.ScrollY) - 1, 3, 3), dot);
    }
}
