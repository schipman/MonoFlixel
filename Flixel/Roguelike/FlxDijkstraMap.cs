using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>
/// A Dijkstra map (also called a distance map or flow field): for every cell of a grid, how far it is to the nearest goal. One map
/// serves any number of monsters: each just steps to the neighbouring cell with a smaller number. Great for "everything chases the
/// player", for "everything runs from the player" (<see cref="CreateFleeMap"/>) and for "where is the nearest exit".
/// <code>
/// var toPlayer = new FlxDijkstraMap(map.Width, map.Height);
/// toPlayer.Compute(new[] { (player.X, player.Y) }, (x, y) =&gt; !map.IsWalkable(x, y));      // once a turn
/// foreach (var m in monsters) if (toPlayer.NextStep(m.X, m.Y, occupied) is { } step) m.MoveTo(step.X, step.Y);
/// </code>
/// See libtcod's dijkstra and the roguelike development "Dijkstra maps" articles.
/// </summary>
public sealed class FlxDijkstraMap
{
    /// <summary>The distance of a cell that cannot reach a goal (or is farther than the limit).</summary>
    public const float Unreachable = float.PositiveInfinity;

    public int Width { get; }
    public int Height { get; }

    /// <summary>The distances, row by row.</summary>
    public readonly float[] Distance;

    Func<int, int, bool> _isSolid = (_, _) => false;
    Func<int, int, float>? _cost;
    FlxDiagonalPolicy _diagonal = FlxDiagonalPolicy.Normal;

    public FlxDijkstraMap(int width, int height)
    {
        Width = width;
        Height = height;
        Distance = new float[width * height];
        Array.Fill(Distance, Unreachable);
    }

    /// <summary>The distance from a cell to the nearest goal (<see cref="Unreachable"/> if none can be reached, or outside the map).</summary>
    public float this[int x, int y] => x < 0 || y < 0 || x >= Width || y >= Height ? Unreachable : Distance[y * Width + x];

    public bool IsReachable(int x, int y) => !float.IsPositiveInfinity(this[x, y]);

    /// <summary>
    /// Works out the distances to the goals. Goals are at distance 0 (or at their own head start, if you pass one).
    /// </summary>
    /// <param name="isSolid">True for cells that cannot be walked through.</param>
    /// <param name="cost">The cost of stepping into a cell (1 is normal, 3 is swampy). Optional.</param>
    /// <param name="maxDistance">Cells farther than this are left Unreachable (a hearing range: monsters beyond it do not notice you).</param>
    public void Compute(IEnumerable<(int X, int Y)> goals, Func<int, int, bool> isSolid, FlxDiagonalPolicy diagonal = FlxDiagonalPolicy.Normal,
        Func<int, int, float>? cost = null, float maxDistance = float.PositiveInfinity)
    {
        _isSolid = isSolid; _cost = cost; _diagonal = diagonal;
        Array.Fill(Distance, Unreachable);

        var open = new PriorityQueue<int, float>();
        foreach (var (x, y) in goals)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
            Distance[y * Width + x] = 0;
            open.Enqueue(y * Width + x, 0);
        }
        Spread(open, maxDistance);
    }

    /// <summary>Same, with a grid map: the walkable cells can be walked through.</summary>
    public void Compute(IEnumerable<(int X, int Y)> goals, IFlxGrid grid, FlxDiagonalPolicy diagonal = FlxDiagonalPolicy.Normal,
        Func<int, int, float>? cost = null, float maxDistance = float.PositiveInfinity) =>
        Compute(goals, (x, y) => !grid.IsWalkable(x, y), diagonal, cost, maxDistance);

    /// <summary>Dijkstra's algorithm from the cells already in the queue (with their distances in Distance).</summary>
    void Spread(PriorityQueue<int, float> open, float maxDistance)
    {
        while (open.TryDequeue(out int cur, out float d))
        {
            if (d > Distance[cur]) continue;           // an older, longer way
            int cx = cur % Width, cy = cur / Width;

            foreach (var (nx, ny, step) in FlxPathfinder.Steps(cx, cy, Width, Height, _isSolid, _diagonal))
            {
                float nd = d + step * (_cost?.Invoke(nx, ny) ?? 1f);
                if (nd > maxDistance) continue;
                int n = ny * Width + nx;
                if (nd < Distance[n])
                {
                    Distance[n] = nd;
                    open.Enqueue(n, nd);
                }
            }
        }
    }

    /// <summary>
    /// A map to run away with. Every distance is multiplied by a negative number (-1.2 is the usual: a bit more than running the other way)
    /// and the map is spread again, so the way out leads to far corners but not into dead ends (a monster that flees does not corner itself
    /// next to the player). Step downhill on the result like on any other map.
    /// </summary>
    public FlxDijkstraMap CreateFleeMap(float factor = -1.2f)
    {
        var flee = new FlxDijkstraMap(Width, Height) { _isSolid = _isSolid, _cost = _cost, _diagonal = _diagonal };

        var open = new PriorityQueue<int, float>();
        for (int i = 0; i < Distance.Length; i++)
        {
            if (float.IsPositiveInfinity(Distance[i])) continue;
            flee.Distance[i] = Distance[i] * factor;
            open.Enqueue(i, flee.Distance[i]);
        }
        flee.Spread(open, float.PositiveInfinity);
        return flee;
    }

    /// <summary>
    /// The neighbouring cell to step to, the one that gets closest to the goal (lowest distance, and lower than where you are), or null if
    /// there is none (you are at the goal, or cannot reach one, or every better cell is taken).
    /// </summary>
    /// <param name="isBlocked">Cells you may not step into right now, for example where another monster stands. Optional.</param>
    public (int X, int Y)? NextStep(int x, int y, Func<int, int, bool>? isBlocked = null)
    {
        float here = this[x, y];
        if (float.IsPositiveInfinity(here)) return null;

        (int X, int Y)? best = null;
        float bestValue = here - 0.0001f, bestStep = float.MaxValue;

        foreach (var (nx, ny, step) in FlxPathfinder.Steps(x, y, Width, Height, _isSolid, _diagonal))
        {
            if (isBlocked != null && isBlocked(nx, ny)) continue;
            float v = Distance[ny * Width + nx];
            if (v < bestValue - 0.0001f || (MathF.Abs(v - bestValue) <= 0.0001f && best != null && step < bestStep))
            {
                best = (nx, ny);
                bestValue = v;
                bestStep = step;
            }
        }
        return best;
    }

    /// <summary>The whole way from a cell to the nearest goal, following the map downhill (the first cell is the one you start on).</summary>
    public List<(int X, int Y)> PathFrom(int x, int y, int maxSteps = 1000)
    {
        var path = new List<(int X, int Y)>();
        if (!IsReachable(x, y)) return path;

        path.Add((x, y));
        while (path.Count <= maxSteps && NextStep(x, y) is { } next)
        {
            (x, y) = next;
            path.Add(next);
        }
        return path;
    }

    /// <summary>The biggest finite distance in the map (handy for scaling a picture of it).</summary>
    public float MaxDistance
    {
        get
        {
            float max = 0;
            foreach (var d in Distance) if (!float.IsPositiveInfinity(d) && d > max) max = d;
            return max;
        }
    }
}
