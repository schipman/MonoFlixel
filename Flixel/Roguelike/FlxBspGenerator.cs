using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>A dungeon made by <see cref="FlxBspGenerator"/>.</summary>
public sealed class FlxBspMap
{
    public int Width, Height;

    /// <summary>Row by row: 1 = wall, 0 = floor.</summary>
    public int[] Data = Array.Empty<int>();

    /// <summary>The rooms, in the order they were made.</summary>
    public readonly List<Rectangle> Rooms = new();

    /// <summary>A grid map of the dungeon (walls block sight and walking).</summary>
    public FlxGridMap ToGridMap() => FlxGridMap.FromWalls(Data, Width, Height);

    public bool IsFloor(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && Data[y * Width + x] == 0;

    public static Point Center(Rectangle r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);
}

/// <summary>
/// Dungeons from binary space partitioning: the map is cut in two, and the halves cut again, until the pieces are small; every small
/// piece gets a room, and the two halves of every cut are joined by an L shaped corridor. Every room can be reached from every other.
/// (The same idea as the BSP demo, with a seed, and as an engine class.)
/// <code>
/// FlxBspMap dungeon = FlxBspGenerator.Generate(80, 50, seed: 7);
/// var map = dungeon.ToGridMap();  Point start = FlxBspMap.Center(dungeon.Rooms[0]);
/// </code>
/// </summary>
public static class FlxBspGenerator
{
    sealed class Leaf
    {
        public Rectangle Bounds;
        public Leaf? A, B;
        public Rectangle? Room;
        public Leaf(Rectangle bounds) { Bounds = bounds; }
    }

    /// <param name="minLeaf">The smallest piece (a side shorter than twice this is not cut any more).</param>
    /// <param name="maxLeaf">A piece longer than this is always cut.</param>
    /// <param name="minRoom">The smallest room side.</param>
    /// <param name="seed">The same seed gives the same dungeon (null: a new one each time).</param>
    public static FlxBspMap Generate(int width, int height, int minLeaf = 10, int maxLeaf = 24, int minRoom = 4, int? seed = null)
    {
        var rnd = seed.HasValue ? new Random(seed.Value) : new Random();
        var map = new FlxBspMap { Width = width, Height = height, Data = new int[width * height] };
        Array.Fill(map.Data, 1);

        // the whole map, less a border of wall, is cut up
        var root = new Leaf(new Rectangle(1, 1, width - 2, height - 2));
        var leaves = new List<Leaf> { root };
        bool again = true;
        while (again)
        {
            again = false;
            foreach (var leaf in leaves.ToArray())
            {
                if (leaf.A != null) continue;
                bool tooBig = leaf.Bounds.Width > maxLeaf || leaf.Bounds.Height > maxLeaf;
                if ((tooBig || rnd.NextDouble() < 0.75) && Split(leaf, minLeaf, rnd))
                {
                    leaves.Add(leaf.A!);
                    leaves.Add(leaf.B!);
                    again = true;
                }
            }
        }

        MakeRooms(root, map, minRoom, rnd);
        return map;
    }

    static bool Split(Leaf leaf, int minLeaf, Random rnd)
    {
        var b = leaf.Bounds;
        bool horizontal = rnd.NextDouble() > 0.5;
        if (b.Width > b.Height * 1.25) horizontal = false;           // cut across the longer side
        else if (b.Height > b.Width * 1.25) horizontal = true;

        int length = horizontal ? b.Height : b.Width;
        if (length < minLeaf * 2) return false;

        int at = rnd.Next(minLeaf, length - minLeaf + 1);
        if (horizontal)
        {
            leaf.A = new Leaf(new Rectangle(b.X, b.Y, b.Width, at));
            leaf.B = new Leaf(new Rectangle(b.X, b.Y + at, b.Width, b.Height - at));
        }
        else
        {
            leaf.A = new Leaf(new Rectangle(b.X, b.Y, at, b.Height));
            leaf.B = new Leaf(new Rectangle(b.X + at, b.Y, b.Width - at, b.Height));
        }
        return true;
    }

    static void MakeRooms(Leaf leaf, FlxBspMap map, int minRoom, Random rnd)
    {
        if (leaf.A != null && leaf.B != null)
        {
            MakeRooms(leaf.A, map, minRoom, rnd);
            MakeRooms(leaf.B, map, minRoom, rnd);

            // join the two halves: a room of each, by an L shaped corridor
            var ra = AnyRoom(leaf.A, rnd);
            var rb = AnyRoom(leaf.B, rnd);
            if (ra != null && rb != null) Corridor(map, FlxBspMap.Center(ra.Value), FlxBspMap.Center(rb.Value), rnd);
            return;
        }

        // a room in the leaf, a cell away from its edges
        var b = leaf.Bounds;
        int maxW = Math.Max(minRoom, b.Width - 2), maxH = Math.Max(minRoom, b.Height - 2);
        int w = rnd.Next(Math.Min(minRoom, maxW), maxW + 1), h = rnd.Next(Math.Min(minRoom, maxH), maxH + 1);
        w = Math.Min(w, b.Width - 1); h = Math.Min(h, b.Height - 1);
        int x = b.X + rnd.Next(0, Math.Max(1, b.Width - w)), y = b.Y + rnd.Next(0, Math.Max(1, b.Height - h));

        var room = new Rectangle(x, y, w, h);
        leaf.Room = room;
        map.Rooms.Add(room);
        for (int yy = room.Top; yy < room.Bottom; yy++)
            for (int xx = room.Left; xx < room.Right; xx++)
                map.Data[yy * map.Width + xx] = 0;
    }

    static Rectangle? AnyRoom(Leaf leaf, Random rnd)
    {
        if (leaf.Room != null) return leaf.Room;
        var a = leaf.A != null ? AnyRoom(leaf.A, rnd) : null;
        var b = leaf.B != null ? AnyRoom(leaf.B, rnd) : null;
        if (a != null && b != null) return rnd.Next(2) == 0 ? a : b;
        return a ?? b;
    }

    static void Corridor(FlxBspMap map, Point from, Point to, Random rnd)
    {
        bool horizontalFirst = rnd.Next(2) == 0;
        var corner = horizontalFirst ? new Point(to.X, from.Y) : new Point(from.X, to.Y);
        Dig(map, from, corner);
        Dig(map, corner, to);
    }

    static void Dig(FlxBspMap map, Point a, Point b)
    {
        foreach (var p in FlxBresenham.Line(a, b))
            if (p.X > 0 && p.Y > 0 && p.X < map.Width - 1 && p.Y < map.Height - 1)
                map.Data[p.Y * map.Width + p.X] = 0;
    }
}
