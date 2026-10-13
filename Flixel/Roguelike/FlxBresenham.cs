using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>
/// Lines and circles on a grid (Bresenham's algorithms), for lines of sight, bolts and arrows, throwing things, drawing and the like.
/// <code>
/// foreach (Point cell in FlxBresenham.Line(a.X, a.Y, b.X, b.Y)) ...        // both ends included
/// Point? hit = FlxBresenham.Cast(map, shooter, target);                    // where a bolt stops
/// </code>
/// </summary>
public static class FlxBresenham
{
    /// <summary>The cells of a line from one cell to another, in order, both ends included.</summary>
    public static IEnumerable<Point> Line(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            yield return new Point(x0, y0);
            if (x0 == x1 && y0 == y1) yield break;

            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    public static IEnumerable<Point> Line(Point from, Point to) => Line(from.X, from.Y, to.X, to.Y);

    /// <summary>The cells of a line as a list (with the start cell left out if <paramref name="includeStart"/> is false).</summary>
    public static List<Point> LineList(int x0, int y0, int x1, int y1, bool includeStart = true)
    {
        var list = new List<Point>();
        foreach (var p in Line(x0, y0, x1, y1))
        {
            if (!includeStart && p.X == x0 && p.Y == y0) continue;
            list.Add(p);
        }
        return list;
    }

    /// <summary>
    /// Follows a line until something blocks it: returns the first cell on the way (after the start) that is not transparent (a wall, a door)
    /// or outside the map, or null if the whole line is clear up to and including the end cell.
    /// </summary>
    public static Point? Cast(IFlxGrid map, int x0, int y0, int x1, int y1)
    {
        foreach (var p in Line(x0, y0, x1, y1))
        {
            if (p.X == x0 && p.Y == y0) continue;
            if (!map.IsTransparent(p.X, p.Y)) return p;
        }
        return null;
    }

    public static Point? Cast(IFlxGrid map, Point from, Point to) => Cast(map, from.X, from.Y, to.X, to.Y);

    /// <summary>
    /// True if there is a clear straight line between two cells: nothing in between blocks it (the end cell may itself be a wall,
    /// so you can see the wall you look at).
    /// </summary>
    public static bool HasLineOfSight(IFlxGrid map, int x0, int y0, int x1, int y1)
    {
        foreach (var p in Line(x0, y0, x1, y1))
        {
            if ((p.X == x0 && p.Y == y0) || (p.X == x1 && p.Y == y1)) continue;
            if (!map.IsTransparent(p.X, p.Y)) return false;
        }
        return true;
    }

    /// <summary>
    /// Like <see cref="Line"/> but stops where the walkable cells end: for a thing that flies along a line and has to stop at a wall.
    /// Returns the cells it passes (not the start); the last one may be the cell it hit if <paramref name="includeBlocker"/>.
    /// </summary>
    public static List<Point> Trace(IFlxGrid map, int x0, int y0, int x1, int y1, bool includeBlocker = false)
    {
        var path = new List<Point>();
        foreach (var p in Line(x0, y0, x1, y1))
        {
            if (p.X == x0 && p.Y == y0) continue;
            if (!map.IsWalkable(p.X, p.Y))
            {
                if (includeBlocker) path.Add(p);
                break;
            }
            path.Add(p);
        }
        return path;
    }

    /// <summary>The cells of a circle's outline (the midpoint circle algorithm). Each cell once, in no particular order.</summary>
    public static List<Point> Circle(int cx, int cy, int radius)
    {
        var set = new HashSet<Point>();
        if (radius <= 0) { set.Add(new Point(cx, cy)); return new List<Point>(set); }

        int x = radius, y = 0, err = 1 - radius;
        while (x >= y)
        {
            foreach (var (px, py) in new[] { (x, y), (y, x), (-y, x), (-x, y), (-x, -y), (-y, -x), (y, -x), (x, -y) })
                set.Add(new Point(cx + px, cy + py));
            y++;
            if (err < 0) err += 2 * y + 1;
            else { x--; err += 2 * (y - x) + 1; }
        }
        return new List<Point>(set);
    }

    /// <summary>All the cells inside a circle, the outline included.</summary>
    public static List<Point> FilledCircle(int cx, int cy, int radius) => FlxGridUtil.CellsInRadius(cx, cy, radius, FlxFovShape.Circle);
}

/// <summary>Small helpers for working with grid cells: neighbours, areas and distances.</summary>
public static class FlxGridUtil
{
    static readonly Point[] Four = { new(0, -1), new(1, 0), new(0, 1), new(-1, 0) };
    static readonly Point[] Eight = { new(0, -1), new(1, -1), new(1, 0), new(1, 1), new(0, 1), new(-1, 1), new(-1, 0), new(-1, -1) };

    /// <summary>The cells next to a cell: four (up, right, down, left) or eight (with the diagonals), clockwise from the top.</summary>
    public static IEnumerable<Point> Neighbors(int x, int y, bool diagonals = true)
    {
        foreach (var d in diagonals ? Eight : Four) yield return new Point(x + d.X, y + d.Y);
    }

    /// <summary>All the cells within a radius of a cell, in a circle, a square or a diamond (the cell itself included).</summary>
    public static List<Point> CellsInRadius(int cx, int cy, int radius, FlxFovShape shape = FlxFovShape.Circle)
    {
        var list = new List<Point>();
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                bool inside = shape switch
                {
                    FlxFovShape.Square => true,
                    FlxFovShape.Diamond => Math.Abs(dx) + Math.Abs(dy) <= radius,
                    _ => dx * dx + dy * dy <= radius * radius,
                };
                if (inside) list.Add(new Point(cx + dx, cy + dy));
            }
        return list;
    }

    /// <summary>The cells of the border of a rectangle (its outline).</summary>
    public static List<Point> BorderCells(Rectangle rect)
    {
        var list = new List<Point>();
        for (int x = rect.Left; x < rect.Right; x++) { list.Add(new Point(x, rect.Top)); if (rect.Height > 1) list.Add(new Point(x, rect.Bottom - 1)); }
        for (int y = rect.Top + 1; y < rect.Bottom - 1; y++) { list.Add(new Point(rect.Left, y)); if (rect.Width > 1) list.Add(new Point(rect.Right - 1, y)); }
        return list;
    }

    /// <summary>Steps: the number of moves up, down, left and right.</summary>
    public static int ManhattanDistance(int x0, int y0, int x1, int y1) => Math.Abs(x1 - x0) + Math.Abs(y1 - y0);

    /// <summary>Steps with diagonals allowed (a king's moves): the larger of the two differences.</summary>
    public static int ChebyshevDistance(int x0, int y0, int x1, int y1) => Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));

    /// <summary>The straight line distance.</summary>
    public static float EuclideanDistance(int x0, int y0, int x1, int y1)
    {
        float dx = x1 - x0, dy = y1 - y0;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
