using System;
using System.Collections.Generic;

namespace MonoFlixel;

/// <summary>
/// A quad tree for fast overlap checks, ported from Flixel's FlxQuadTree. Like the original it works on two
/// lists: add objects to list A (and optionally list B), then compare A against itself, or A against B.
/// Groups are flattened automatically; dead and non-solid objects are skipped.
///
/// Differences from the original: nodes keep plain lists instead of linked lists, objects stored in an ancestor
/// node are tested against everything below it, and every pair is reported once even if both objects span
/// several nodes.
///
/// <code>
/// var tree = new FlxQuadTree(0, 0, FlxG.Width, FlxG.Height);
/// tree.Add(balls);
/// tree.Overlap(false, (a, b) => { a.Kill(); return true; });
/// </code>
/// </summary>
public sealed class FlxQuadTree
{
    public const int AList = 0;
    public const int BList = 1;

    sealed class Node
    {
        public readonly float X, Y, W, H;
        public readonly int Depth;
        public readonly bool CanSubdivide;
        public List<FlxObject>? A, B;
        public Node?[]? Children;           // NW, NE, SE, SW

        public Node(float x, float y, float w, float h, int depth, float minSize)
        {
            X = x; Y = y; W = w; H = h; Depth = depth;
            CanSubdivide = w > minSize || h > minSize;
        }
    }

    /// <summary>Read-only view of one node, for drawing or debugging the tree (see ForEachNode).</summary>
    public readonly record struct NodeInfo(float X, float Y, float Width, float Height, int Depth, int CountA, int CountB, bool IsLeaf);

    readonly Node _root;
    readonly float _minSize;
    readonly HashSet<(FlxObject, FlxObject)> _seen = new();
    readonly List<FlxObject> _stackA = new(), _stackB = new();   // objects stored in ancestors of the node being visited
    Func<FlxObject, FlxObject, bool>? _callback;
    bool _both;

    public float X => _root.X;
    public float Y => _root.Y;
    public float Width => _root.W;
    public float Height => _root.H;

    /// <summary>Number of objects added (an object that spans several nodes counts once).</summary>
    public int ObjectCount { get; private set; }

    /// <summary>Number of pair tests (AABB checks) made by the last Overlap call - handy to compare with brute force.</summary>
    public int PairTests { get; private set; }

    /// <summary>
    /// Creates the tree for the given world area. <paramref name="divisions"/> controls the granularity: nodes stop
    /// splitting once they are smaller than (width + height) / (2 * divisions). Flixel's default is 3.
    /// </summary>
    public FlxQuadTree(float x, float y, float width, float height, int divisions = 3)
    {
        _minSize = (width + height) / (2f * Math.Max(1, divisions));
        _root = new Node(x, y, width, height, 0, _minSize);
    }

    /// <summary>Adds an object, or all members of a group (recursively), to a list (AList or BList).</summary>
    public void Add(FlxBasic basic, int list = AList)
    {
        if (!basic.Exists) return;

        if (basic is IFlxGroup g)
        {
            var members = g.Basics;
            for (int i = 0; i < members.Count; i++) if (members[i] != null) Add(members[i], list);
        }
        else if (basic is FlxObject o && o.Solid)
        {
            ObjectCount++;
            if (!Overlaps(_root, o)) StoreIn(_root, o, list);   // outside the tree's area: keep it at the root, never lost
            else Insert(_root, o, list);
        }
    }

    // ----------------------------------------------------------------- building

    static bool Overlaps(Node n, FlxObject o) =>
        o.X + o.Width >= n.X && o.X <= n.X + n.W && o.Y + o.Height >= n.Y && o.Y <= n.Y + n.H;

    static void StoreIn(Node n, FlxObject o, int list)
    {
        if (list == AList) (n.A ??= new()).Add(o);
        else (n.B ??= new()).Add(o);
    }

    void Insert(Node n, FlxObject o, int list)
    {
        // The object covers this whole node (or the node is as small as it gets): store it here.
        if (!n.CanSubdivide || (n.X >= o.X && n.X + n.W <= o.X + o.Width && n.Y >= o.Y && n.Y + n.H <= o.Y + o.Height))
        {
            StoreIn(n, o, list);
            return;
        }

        float hw = n.W * 0.5f, hh = n.H * 0.5f, mx = n.X + hw, my = n.Y + hh;
        n.Children ??= new Node?[4];

        // NW, NE, SE, SW - the object goes into every quadrant it touches
        for (int q = 0; q < 4; q++)
        {
            float qx = (q == 1 || q == 2) ? mx : n.X;
            float qy = (q == 2 || q == 3) ? my : n.Y;
            // inclusive, so objects that merely touch a cell edge are still found (Test counts touching as overlap)
            if (!(o.X + o.Width >= qx && o.X <= qx + hw && o.Y + o.Height >= qy && o.Y <= qy + hh)) continue;
            var child = n.Children[q] ??= new Node(qx, qy, hw, hh, n.Depth + 1, _minSize);
            Insert(child, o, list);
        }
    }

    /// <summary>Empties the tree so it can be refilled (keeps the same area and granularity).</summary>
    public void Clear()
    {
        _root.A = _root.B = null;
        _root.Children = null;
        ObjectCount = 0;
    }

    // ----------------------------------------------------------------- queries

    /// <summary>
    /// Finds overlapping pairs. With <paramref name="bothLists"/> false, list A is compared against itself;
    /// otherwise every A object is compared against the B objects (callback(a, b) always gets the A object first).
    /// The callback returns true if the pair counted; with a null callback both objects are killed, like Flixel.
    /// Returns true if any pair counted.
    /// </summary>
    public bool Overlap(bool bothLists, Func<FlxObject, FlxObject, bool>? callback = null)
    {
        _both = bothLists;
        _callback = callback;
        _seen.Clear();
        _stackA.Clear();
        _stackB.Clear();
        PairTests = 0;
        return Visit(_root);
    }

    bool Visit(Node n)
    {
        bool hit = false;
        var a = n.A;
        var b = n.B;

        if (!_both)
        {
            if (a != null)
                for (int i = 0; i < a.Count; i++)
                {
                    for (int j = i + 1; j < a.Count; j++) hit |= Test(a[i], a[j]);
                    for (int j = 0; j < _stackA.Count; j++) hit |= Test(a[i], _stackA[j]);
                }
        }
        else
        {
            if (a != null)
                for (int i = 0; i < a.Count; i++)
                {
                    if (b != null) for (int j = 0; j < b.Count; j++) hit |= Test(a[i], b[j]);
                    for (int j = 0; j < _stackB.Count; j++) hit |= Test(a[i], _stackB[j]);
                }
            if (b != null)
                for (int j = 0; j < b.Count; j++)
                    for (int i = 0; i < _stackA.Count; i++) hit |= Test(_stackA[i], b[j]);
        }

        if (n.Children != null)
        {
            int na = _stackA.Count, nb = _stackB.Count;
            if (a != null) _stackA.AddRange(a);
            if (b != null) _stackB.AddRange(b);

            foreach (var child in n.Children)
                if (child != null) hit |= Visit(child);

            _stackA.RemoveRange(na, _stackA.Count - na);
            _stackB.RemoveRange(nb, _stackB.Count - nb);
        }
        return hit;
    }

    bool Test(FlxObject a, FlxObject b)
    {
        if (ReferenceEquals(a, b) || !a.Exists || !b.Exists || !a.Solid || !b.Solid) return false;
        if (a.X + a.Width < b.X || a.X > b.X + b.Width || a.Y + a.Height < b.Y || a.Y > b.Y + b.Height) return false;
        PairTests++;

        // both objects can span several nodes, so the same pair may turn up more than once
        if (!_seen.Add((a, b)) || _seen.Contains((b, a))) return false;

        if (_callback == null)
        {
            a.Kill();
            b.Kill();
            return true;
        }
        return _callback(a, b);
    }

    // ----------------------------------------------------------------- debugging

    /// <summary>Visits every node that exists (nodes are only created where objects are), root first.</summary>
    public void ForEachNode(Action<NodeInfo> visit) => Walk(_root, visit);

    static void Walk(Node n, Action<NodeInfo> visit)
    {
        bool leaf = true;
        if (n.Children != null) foreach (var c in n.Children) if (c != null) { leaf = false; break; }
        visit(new NodeInfo(n.X, n.Y, n.W, n.H, n.Depth, n.A?.Count ?? 0, n.B?.Count ?? 0, leaf));
        if (n.Children != null) foreach (var c in n.Children) if (c != null) Walk(c, visit);
    }
}
