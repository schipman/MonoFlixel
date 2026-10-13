using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>How the field of view is worked out.</summary>
public enum FlxFovAlgorithm
{
    /// <summary>
    /// Rays: a Bresenham line from the viewer to every cell on the edge of the view; everything a ray passes is seen until it hits a
    /// wall. Simple and fast, but gaps appear behind pillars and some corners look odd (libtcod's FOV_BASIC).
    /// </summary>
    Basic,

    /// <summary>
    /// Recursive shadowcasting: the view is cut into eight slices and walls cast shadows through them (Björn Bergström's version, like
    /// libtcod's FOV_SHADOW). Fast, no gaps, but not always symmetric (A can see B while B cannot see A).
    /// </summary>
    Shadowcast,

    /// <summary>
    /// Symmetric shadowcasting (Albert Ford): a floor cell is seen only if a line from the viewer's center to the cell's center is clear,
    /// so seeing is always mutual. A bit stricter than <see cref="Shadowcast"/>. (libtcod: FOV_SYMMETRIC_SHADOWCAST.)
    /// </summary>
    SymmetricShadowcast,

    /// <summary>
    /// Permissive: a cell is seen if any line from a point in the viewer's cell to any point in the cell is clear. The most generous:
    /// you see around corners and between pillars (libtcod's FOV_PERMISSIVE_n). The <c>permissiveLevel</c> (0 to 4) says how many points are tried.
    /// </summary>
    Permissive
}

/// <summary>The shape of the area that can be seen.</summary>
public enum FlxFovShape
{
    /// <summary>A circle: cells within the radius (dx*dx + dy*dy &lt;= r*r).</summary>
    Circle,
    /// <summary>A square: as far as the radius in both directions.</summary>
    Square,
    /// <summary>A diamond: the distance counted in steps up, down, left and right (|dx| + |dy| &lt;= r).</summary>
    Diamond
}

/// <summary>
/// Field of view: which cells of a grid can be seen from a cell. The result is an array with one bool per cell (row by row), true for
/// the cells in view.
/// <code>
/// bool[] seen = FlxFov.Compute(map, player.X, player.Y, 9, FlxFovAlgorithm.SymmetricShadowcast);
/// </code>
/// Cells outside the map are never seen. The viewer's own cell always is.
/// </summary>
public static class FlxFov
{
    public static bool[] Compute(IFlxGrid map, int x, int y, int radius, FlxFovAlgorithm algorithm = FlxFovAlgorithm.Shadowcast,
        bool lightWalls = true, FlxFovShape shape = FlxFovShape.Circle, int permissiveLevel = 2)
    {
        var seen = new bool[map.Width * map.Height];
        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height) return seen;
        radius = Math.Max(0, radius);

        seen[y * map.Width + x] = true;
        if (radius == 0) return seen;

        var view = new View(map, seen, x, y, radius, lightWalls, shape);
        switch (algorithm)
        {
            case FlxFovAlgorithm.Basic: view.Basic(); break;
            case FlxFovAlgorithm.Shadowcast: view.Shadowcast(); break;
            case FlxFovAlgorithm.SymmetricShadowcast: view.Symmetric(); break;
            default: view.Permissive(Math.Clamp(permissiveLevel, 0, 4)); break;
        }
        return seen;
    }

    /// <summary>Works out whether one cell can be seen from another with a given algorithm (a convenience for single questions: it computes the whole view).</summary>
    public static bool CanSee(IFlxGrid map, int fromX, int fromY, int toX, int toY, int radius, FlxFovAlgorithm algorithm = FlxFovAlgorithm.Shadowcast,
        FlxFovShape shape = FlxFovShape.Circle)
    {
        if (toX < 0 || toY < 0 || toX >= map.Width || toY >= map.Height) return false;
        return Compute(map, fromX, fromY, radius, algorithm, true, shape)[toY * map.Width + toX];
    }

    sealed class View
    {
        readonly IFlxGrid _map;
        readonly bool[] _seen;
        readonly int _ox, _oy, _radius;
        readonly bool _lightWalls;
        readonly FlxFovShape _shape;

        public View(IFlxGrid map, bool[] seen, int ox, int oy, int radius, bool lightWalls, FlxFovShape shape)
        {
            _map = map; _seen = seen; _ox = ox; _oy = oy; _radius = radius; _lightWalls = lightWalls; _shape = shape;
        }

        bool InShape(int dx, int dy) => _shape switch
        {
            FlxFovShape.Square => Math.Abs(dx) <= _radius && Math.Abs(dy) <= _radius,
            FlxFovShape.Diamond => Math.Abs(dx) + Math.Abs(dy) <= _radius,
            _ => dx * dx + dy * dy <= _radius * _radius + _radius          // a slightly generous circle (looks rounder on a grid)
        };

        bool Opaque(int x, int y) => !_map.IsTransparent(x, y);

        void Mark(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _map.Width || y >= _map.Height) return;
            if (!InShape(x - _ox, y - _oy)) return;
            if (Opaque(x, y) && !_lightWalls) return;
            _seen[y * _map.Width + x] = true;
        }

        // ------------------------------------------------------------ Basic: a ray to every cell on the edge

        public void Basic()
        {
            int r = _radius;
            for (int i = -r; i <= r; i++)
            {
                Ray(_ox + i, _oy - r);
                Ray(_ox + i, _oy + r);
                Ray(_ox - r, _oy + i);
                Ray(_ox + r, _oy + i);
            }
        }

        void Ray(int tx, int ty)
        {
            foreach (var p in FlxBresenham.Line(_ox, _oy, tx, ty))
            {
                if (p.X == _ox && p.Y == _oy) continue;
                if (!InShape(p.X - _ox, p.Y - _oy)) return;
                if (p.X < 0 || p.Y < 0 || p.X >= _map.Width || p.Y >= _map.Height) return;
                Mark(p.X, p.Y);
                if (Opaque(p.X, p.Y)) return;
            }
        }

        // ------------------------------------------------------------ Shadowcast: eight slices, recursive, slopes

        static readonly int[,] Octants =
        {
            { 1, 0, 0, 1 }, { 0, 1, 1, 0 }, { 0, -1, 1, 0 }, { -1, 0, 0, 1 },
            { -1, 0, 0, -1 }, { 0, -1, -1, 0 }, { 0, 1, -1, 0 }, { 1, 0, 0, -1 }
        };

        public void Shadowcast()
        {
            for (int o = 0; o < 8; o++) Cast(1, 1.0f, 0.0f, Octants[o, 0], Octants[o, 1], Octants[o, 2], Octants[o, 3]);
        }

        void Cast(int row, float start, float end, int xx, int xy, int yx, int yy)
        {
            if (start < end) return;
            float newStart = 0;

            for (int j = row; j <= _radius; j++)
            {
                int dx = -j - 1, dy = -j;
                bool blocked = false;

                while (dx <= 0)
                {
                    dx++;
                    int mx = _ox + dx * xx + dy * xy, my = _oy + dx * yx + dy * yy;
                    float leftSlope = (dx - 0.5f) / (dy + 0.5f), rightSlope = (dx + 0.5f) / (dy - 0.5f);

                    if (start < rightSlope) continue;
                    if (end > leftSlope) break;

                    Mark(mx, my);

                    bool wall = mx < 0 || my < 0 || mx >= _map.Width || my >= _map.Height || Opaque(mx, my);
                    if (blocked)
                    {
                        if (wall) { newStart = rightSlope; continue; }
                        blocked = false;
                        start = newStart;
                    }
                    else if (wall && j < _radius)
                    {
                        blocked = true;
                        Cast(j + 1, start, leftSlope, xx, xy, yx, yy);
                        newStart = rightSlope;
                    }
                }
                if (blocked) break;
            }
        }

        // ------------------------------------------------------------ Symmetric shadowcasting (Albert Ford)

        public void Symmetric()
        {
            for (int quadrant = 0; quadrant < 4; quadrant++) SymmetricScan(quadrant, 1, -1.0, 1.0);
        }

        (int X, int Y) Transform(int quadrant, int depth, int col) => quadrant switch
        {
            0 => (_ox + col, _oy - depth),       // north
            1 => (_ox + depth, _oy + col),       // east
            2 => (_ox + col, _oy + depth),       // south
            _ => (_ox - depth, _oy + col),       // west
        };

        void SymmetricScan(int quadrant, int depth, double startSlope, double endSlope)
        {
            if (depth > _radius) return;

            int minCol = (int)Math.Floor(depth * startSlope + 0.5);          // round ties up
            int maxCol = (int)Math.Ceiling(depth * endSlope - 0.5);           // round ties down

            bool? previousWall = null;            // null = no previous tile in this row
            for (int col = minCol; col <= maxCol; col++)
            {
                var (x, y) = Transform(quadrant, depth, col);
                bool inMap = x >= 0 && y >= 0 && x < _map.Width && y < _map.Height;
                bool wall = !inMap || Opaque(x, y);

                if (inMap && (wall || (col >= depth * startSlope && col <= depth * endSlope))) Mark(x, y);

                if (previousWall == true && !wall) startSlope = (2.0 * col - 1.0) / (2.0 * depth);
                if (previousWall == false && wall)
                    SymmetricScan(quadrant, depth + 1, startSlope, (2.0 * col - 1.0) / (2.0 * depth));

                previousWall = wall;
            }
            if (previousWall == false) SymmetricScan(quadrant, depth + 1, startSlope, endSlope);
        }

        // ------------------------------------------------------------ Permissive: any line from the viewer's cell to the cell

        static readonly (float X, float Y)[] Points =
        {
            (0.5f, 0.5f),                                        // 0: the middle
            (0.5f, 0.02f), (0.98f, 0.5f), (0.5f, 0.98f), (0.02f, 0.5f),       // 1-4: middles of the edges
            (0.02f, 0.02f), (0.98f, 0.02f), (0.98f, 0.98f), (0.02f, 0.98f),   // 5-8: corners
        };

        public void Permissive(int level)
        {
            // level 0: middle to middle; 1: the middle to the middle and the edges; 2: to all nine points; 3: from the middle and the
            // four corners to all nine; 4: from all nine to all nine
            int fromCount = level switch { 0 => 1, 1 => 1, 2 => 1, 3 => 5, _ => 9 };
            int toCount = level switch { 0 => 1, 1 => 5, _ => 9 };
            var from = new (float X, float Y)[fromCount];
            for (int i = 0; i < fromCount; i++) from[i] = Points[level == 3 && i > 0 ? i + 4 : i];     // level 3 uses the corners
            var to = new (float X, float Y)[toCount];
            for (int i = 0; i < toCount; i++) to[i] = Points[i];

            int r = _radius;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (!InShape(dx, dy)) continue;
                    int tx = _ox + dx, ty = _oy + dy;
                    if (tx < 0 || ty < 0 || tx >= _map.Width || ty >= _map.Height) continue;

                    if (Visible(tx, ty, from, to)) Mark(tx, ty);
                }
        }

        bool Visible(int tx, int ty, (float X, float Y)[] from, (float X, float Y)[] to)
        {
            foreach (var f in from)
                foreach (var t in to)
                    if (SegmentClear(_ox + f.X, _oy + f.Y, tx + t.X, ty + t.Y, tx, ty)) return true;
            return false;
        }

        /// <summary>Walks a segment cell by cell (all cells it touches); clear if none but the first and the target cell blocks.</summary>
        bool SegmentClear(float x0, float y0, float x1, float y1, int targetX, int targetY)
        {
            int cx = (int)MathF.Floor(x0), cy = (int)MathF.Floor(y0);
            float dx = x1 - x0, dy = y1 - y0;
            int stepX = dx > 0 ? 1 : -1, stepY = dy > 0 ? 1 : -1;
            float tDeltaX = dx == 0 ? float.PositiveInfinity : MathF.Abs(1f / dx);
            float tDeltaY = dy == 0 ? float.PositiveInfinity : MathF.Abs(1f / dy);
            float tMaxX = dx == 0 ? float.PositiveInfinity : ((dx > 0 ? cx + 1 - x0 : x0 - cx) * tDeltaX);
            float tMaxY = dy == 0 ? float.PositiveInfinity : ((dy > 0 ? cy + 1 - y0 : y0 - cy) * tDeltaY);

            int guard = 4 * (_radius + 3);
            while (guard-- > 0)
            {
                if (cx == targetX && cy == targetY) return true;
                if (!(cx == _ox && cy == _oy) && Opaque(cx, cy)) return false;

                if (MathF.Min(tMaxX, tMaxY) > 1f) return false;          // the segment is over and this is not the target cell
                if (MathF.Abs(tMaxX - tMaxY) < 1e-5f) { cx += stepX; tMaxX += tDeltaX; cy += stepY; tMaxY += tDeltaY; }      // exactly through a corner: slip between
                else if (tMaxX < tMaxY) { cx += stepX; tMaxX += tDeltaX; }
                else { cy += stepY; tMaxY += tDeltaY; }
            }
            return false;
        }
    }
}
