using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>Whether (and how) a path may cut diagonally across grid cells.</summary>
public enum FlxDiagonalPolicy
{
    /// <summary>Up, down, left and right only.</summary>
    None,
    /// <summary>Diagonal steps are allowed if at least one of the two cells beside the move is open (can brush a wall corner).</summary>
    Normal,
    /// <summary>Diagonal steps need both cells beside the move to be open: the path never touches a wall corner. Best for objects with a body.</summary>
    Wide
}

/// <summary>Which search finds the path.</summary>
public enum FlxPathAlgorithm
{
    /// <summary>A*: guided towards the goal by an estimate of what is left; the cheapest path, found quickly. The usual choice.</summary>
    AStar,
    /// <summary>Dijkstra: spreads out evenly from the start (no estimate), so it explores more but needs no idea where the goal is. The cheapest path.</summary>
    Dijkstra,
    /// <summary>Breadth first: the fewest steps, every step counting the same (costs are ignored). Simple and fast on open grids.</summary>
    BreadthFirst,
    /// <summary>Greedy best first: always goes on from the cell that looks closest to the goal. Very fast, but the path can be a long way round.</summary>
    GreedyBestFirst
}

/// <summary>How much of the raw step-by-step path to throw away.</summary>
public enum FlxPathSimplify
{
    /// <summary>Every cell on the way is a node.</summary>
    None,
    /// <summary>Only the cells where the path changes direction are nodes.</summary>
    Straight,
    /// <summary>Only the nodes needed so each hop has a clear straight line: the shortest looking path.</summary>
    LineOfSight
}

/// <summary>
/// A* path finding on a grid. Port of the idea behind Flixel's FlxPathfinder / FlxTilemap.findPath.
/// <code>
/// List&lt;Vector2&gt;? path = tilemap.FindPath(enemy.Midpoint, player.Midpoint);    // world coordinates, or null if unreachable
/// if (path != null) enemy.FollowPath(path, speed: 70);
/// </code>
/// Works on any grid too: see <see cref="FindCells"/>.
/// </summary>
public static class FlxPathfinder
{
    static readonly (int Dx, int Dy)[] Straight4 = { (0, -1), (1, 0), (0, 1), (-1, 0) };
    static readonly (int Dx, int Dy)[] Diagonals = { (1, -1), (1, 1), (-1, 1), (-1, -1) };
    const float Sqrt2 = 1.41421356f;

    /// <summary>
    /// Finds the cheapest path between two cells of a grid, as a list of cells from start to end, or null if there is none.
    /// </summary>
    /// <param name="isSolid">True for cells that block the way. The start cell may be solid (an object overlapping a wall can still get out); the end cell may not.</param>
    /// <param name="cost">Optional extra cost of entering a cell: 1 is normal, 3 is swampy, 0.5 is a road. Must not be below 0.</param>
    public static List<(int X, int Y)>? FindCells(int columns, int rows, Func<int, int, bool> isSolid, (int X, int Y) start, (int X, int Y) end,
        FlxDiagonalPolicy diagonal = FlxDiagonalPolicy.Wide, Func<int, int, float>? cost = null, FlxPathAlgorithm algorithm = FlxPathAlgorithm.AStar)
    {
        bool InGrid(int x, int y) => x >= 0 && y >= 0 && x < columns && y < rows;

        if (!InGrid(start.X, start.Y) || !InGrid(end.X, end.Y)) return null;
        if (isSolid(end.X, end.Y)) return null;
        if (start == end) return new List<(int, int)> { start };

        int n = columns * rows;
        int Id(int x, int y) => y * columns + x;
        int goal = Id(end.X, end.Y), first = Id(start.X, start.Y);

        var g = new float[n];
        Array.Fill(g, float.PositiveInfinity);
        var parent = new int[n];
        Array.Fill(parent, -1);
        var closed = new bool[n];

        float Heuristic(int x, int y)
        {
            float dx = Math.Abs(x - end.X), dy = Math.Abs(y - end.Y);
            return diagonal == FlxDiagonalPolicy.None ? dx + dy : (dx + dy) + (Sqrt2 - 2f) * Math.Min(dx, dy);
        }

        // what a cell is worth exploring first: A* adds the estimate to the cost so far, Dijkstra and breadth first use the cost alone,
        // greedy best first only the estimate
        float Priority(float cost, int x, int y) => algorithm switch
        {
            FlxPathAlgorithm.AStar => cost + Heuristic(x, y),
            FlxPathAlgorithm.GreedyBestFirst => Heuristic(x, y),
            _ => cost
        };

        var open = new PriorityQueue<int, float>();
        g[first] = 0;
        open.Enqueue(first, Priority(0, start.X, start.Y));

        while (open.TryDequeue(out int cur, out _))
        {
            if (closed[cur]) continue;
            closed[cur] = true;
            if (cur == goal) return Rebuild(parent, cur, columns);

            int cx = cur % columns, cy = cur / columns;

            foreach (var (dx, dy) in Straight4)
                Relax(cx, cy, cx + dx, cy + dy, 1f);

            if (diagonal != FlxDiagonalPolicy.None)
                foreach (var (dx, dy) in Diagonals)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (!InGrid(nx, ny)) continue;

                    bool sideA = InGrid(cx + dx, cy) && !isSolid(cx + dx, cy);
                    bool sideB = InGrid(cx, cy + dy) && !isSolid(cx, cy + dy);
                    bool allowed = diagonal == FlxDiagonalPolicy.Wide ? sideA && sideB : sideA || sideB;
                    if (allowed) Relax(cx, cy, nx, ny, Sqrt2);
                }
        }
        return null;

        void Relax(int fromX, int fromY, int nx, int ny, float step)
        {
            if (!InGrid(nx, ny) || isSolid(nx, ny)) return;
            int nb = Id(nx, ny);
            if (closed[nb]) return;

            float stepCost = algorithm == FlxPathAlgorithm.BreadthFirst ? 1f : step * (cost?.Invoke(nx, ny) ?? 1f);
            float ng = g[Id(fromX, fromY)] + stepCost;
            if (ng < g[nb])
            {
                g[nb] = ng;
                parent[nb] = Id(fromX, fromY);
                open.Enqueue(nb, Priority(ng, nx, ny));
            }
        }
    }

    static List<(int X, int Y)> Rebuild(int[] parent, int last, int columns)
    {
        var cells = new List<(int, int)>();
        for (int c = last; c != -1; c = parent[c]) cells.Add((c % columns, c / columns));
        cells.Reverse();
        return cells;
    }

    /// <summary>
    /// Every cell that can be reached from a cell, with what it costs to get there (Dijkstra's flood, stopping at <paramref name="maxCost"/>):
    /// "where can this monster get to in 6 moves?". Cells that cannot be reached are not in the result.
    /// </summary>
    public static Dictionary<(int X, int Y), float> Reachable(int columns, int rows, Func<int, int, bool> isSolid, (int X, int Y) start, float maxCost,
        FlxDiagonalPolicy diagonal = FlxDiagonalPolicy.Normal, Func<int, int, float>? cost = null)
    {
        var result = new Dictionary<(int X, int Y), float>();
        if (start.X < 0 || start.Y < 0 || start.X >= columns || start.Y >= rows) return result;

        var open = new PriorityQueue<(int X, int Y), float>();
        open.Enqueue(start, 0);
        var best = new Dictionary<(int X, int Y), float> { [start] = 0 };

        while (open.TryDequeue(out var cur, out float d))
        {
            if (result.ContainsKey(cur)) continue;
            result[cur] = d;

            foreach (var (nx, ny, step) in Steps(cur.X, cur.Y, columns, rows, isSolid, diagonal))
            {
                float nd = d + step * (cost?.Invoke(nx, ny) ?? 1f);
                if (nd > maxCost) continue;
                if (!best.TryGetValue((nx, ny), out float old) || nd < old)
                {
                    best[(nx, ny)] = nd;
                    open.Enqueue((nx, ny), nd);
                }
            }
        }
        return result;
    }

    /// <summary>The cells next to one that a move can go to, with the length of the step (1 or 1.41).</summary>
    internal static IEnumerable<(int X, int Y, float Step)> Steps(int cx, int cy, int columns, int rows, Func<int, int, bool> isSolid, FlxDiagonalPolicy diagonal)
    {
        bool InGrid(int x, int y) => x >= 0 && y >= 0 && x < columns && y < rows;

        foreach (var (dx, dy) in Straight4)
        {
            int nx = cx + dx, ny = cy + dy;
            if (InGrid(nx, ny) && !isSolid(nx, ny)) yield return (nx, ny, 1f);
        }

        if (diagonal == FlxDiagonalPolicy.None) yield break;
        foreach (var (dx, dy) in Diagonals)
        {
            int nx = cx + dx, ny = cy + dy;
            if (!InGrid(nx, ny) || isSolid(nx, ny)) continue;

            bool sideA = InGrid(cx + dx, cy) && !isSolid(cx + dx, cy);
            bool sideB = InGrid(cx, cy + dy) && !isSolid(cx, cy + dy);
            bool allowed = diagonal == FlxDiagonalPolicy.Wide ? sideA && sideB : sideA || sideB;
            if (allowed) yield return (nx, ny, Sqrt2);
        }
    }

    /// <summary>Removes cells from a path found by FindCells that aren't needed (see FlxPathSimplify).</summary>
    public static List<(int X, int Y)> Simplify(List<(int X, int Y)> cells, FlxPathSimplify mode, Func<int, int, bool> isSolid,
        bool allowCornerCutting = false)
    {
        if (mode == FlxPathSimplify.None || cells.Count < 3) return cells;

        var result = new List<(int X, int Y)> { cells[0] };

        if (mode == FlxPathSimplify.Straight)
        {
            for (int i = 1; i < cells.Count - 1; i++)
            {
                var d1 = (X: cells[i].X - cells[i - 1].X, Y: cells[i].Y - cells[i - 1].Y);
                var d2 = (X: cells[i + 1].X - cells[i].X, Y: cells[i + 1].Y - cells[i].Y);
                if (d1 != d2) result.Add(cells[i]);
            }
            result.Add(cells[^1]);
            return result;
        }

        // line of sight: from each node, jump to the furthest later cell that can be seen directly
        int anchor = 0;
        while (anchor < cells.Count - 1)
        {
            int next = anchor + 1;
            for (int j = cells.Count - 1; j > anchor + 1; j--)
                if (LineIsClear(cells[anchor], cells[j], isSolid, allowCornerCutting)) { next = j; break; }

            result.Add(cells[next]);
            anchor = next;
        }
        return result;
    }

    /// <summary>
    /// True if a straight line between the middles of two cells only crosses open cells. Unless
    /// <paramref name="allowCornerCutting"/> is set, the line may not squeeze diagonally between two touching walls either.
    /// </summary>
    public static bool LineIsClear((int X, int Y) a, (int X, int Y) b, Func<int, int, bool> isSolid, bool allowCornerCutting = false)
    {
        int nx = Math.Abs(b.X - a.X), ny = Math.Abs(b.Y - a.Y);
        int sx = b.X > a.X ? 1 : -1, sy = b.Y > a.Y ? 1 : -1;
        int x = a.X, y = a.Y;

        for (int ix = 0, iy = 0; ix < nx || iy < ny;)
        {
            // compare how far along the line the next horizontal and vertical cell edges are
            long decision = (1 + 2L * ix) * ny - (1 + 2L * iy) * nx;

            if (decision == 0)
            {
                // the line goes exactly through a cell corner
                if (!allowCornerCutting && (isSolid(x + sx, y) || isSolid(x, y + sy))) return false;
                x += sx; y += sy; ix++; iy++;
            }
            else if (decision < 0) { x += sx; ix++; }
            else { y += sy; iy++; }

            if (isSolid(x, y)) return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ tilemaps

    /// <summary>
    /// Finds a path through a tilemap between two world positions. Returns the nodes in world coordinates (the middles of
    /// the tiles, starting with <paramref name="start"/> and ending with <paramref name="end"/>), or null if there is no way.
    /// Tiles that block movement are the ones that are solid for collisions.
    /// </summary>
    public static List<Vector2>? FindPath(FlxTilemap map, Vector2 start, Vector2 end,
        FlxDiagonalPolicy diagonal = FlxDiagonalPolicy.Wide, FlxPathSimplify simplify = FlxPathSimplify.Straight, Func<int, int, float>? cost = null,
        FlxPathAlgorithm algorithm = FlxPathAlgorithm.AStar)
    {
        int cols = map.WidthInTiles, rows = map.HeightInTiles;
        int startIndex = map.GetMapIndexAt(start.X, start.Y), endIndex = map.GetMapIndexAt(end.X, end.Y);
        if (startIndex < 0 || endIndex < 0) return null;

        bool Solid(int c, int r) => map.IsSolidAt(map.X + (c + 0.5f) * map.TileWidth, map.Y + (r + 0.5f) * map.TileHeight);

        var cells = FindCells(cols, rows, Solid, (startIndex % cols, startIndex / cols), (endIndex % cols, endIndex / cols), diagonal, cost, algorithm);
        if (cells == null) return null;

        cells = Simplify(cells, simplify, Solid, allowCornerCutting: diagonal == FlxDiagonalPolicy.Normal);

        var path = new List<Vector2>(cells.Count);
        foreach (var (c, r) in cells)
            path.Add(new Vector2(map.X + (c + 0.5f) * map.TileWidth, map.Y + (r + 0.5f) * map.TileHeight));

        // use the exact positions asked for at both ends
        path[0] = start;
        if (path.Count == 1) path.Add(end);
        else path[^1] = end;
        return path;
    }
}
