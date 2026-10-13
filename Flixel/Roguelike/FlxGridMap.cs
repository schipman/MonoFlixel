using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>
/// What field of view and the grid tools need to know about a map: how big it is, which cells let light through and which can be walked on.
/// <see cref="FlxGridMap"/> is a ready made one; a tilemap, an array or your own class can implement it too.
/// </summary>
public interface IFlxGrid
{
    int Width { get; }
    int Height { get; }

    /// <summary>True if light (and a line of sight) passes through the cell. Cells outside the map do not.</summary>
    bool IsTransparent(int x, int y);

    /// <summary>True if something can walk on the cell. Cells outside the map cannot be walked on.</summary>
    bool IsWalkable(int x, int y);
}

/// <summary>
/// A grid of cells for turn based games, in the style of libtcod's TCOD_Map and RogueSharp's Map: every cell is transparent or not
/// (can you see through it?) and walkable or not, and the map remembers what is in view right now and what was ever seen.
/// <code>
/// var map = FlxGridMap.FromWalls(data, 60, 40);                 // 1 = wall, 0 = floor
/// map.ComputeFov(player.X, player.Y, radius: 9);                // what the player sees now
/// if (map.IsInFov(x, y)) Draw(x, y);
/// else if (map.IsExplored(x, y)) DrawRemembered(x, y);
/// </code>
/// </summary>
public class FlxGridMap : IFlxGrid
{
    readonly bool[] _transparent, _walkable, _inFov, _explored;

    public int Width { get; }
    public int Height { get; }

    public FlxGridMap(int width, int height, bool transparent = true, bool walkable = true)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("A map needs a size.");
        Width = width;
        Height = height;
        int n = width * height;
        _transparent = new bool[n]; _walkable = new bool[n]; _inFov = new bool[n]; _explored = new bool[n];
        Array.Fill(_transparent, transparent);
        Array.Fill(_walkable, walkable);
    }

    /// <summary>Makes a map from an array (row by row) where one value is a wall: walls are opaque and not walkable, everything else is open.</summary>
    public static FlxGridMap FromWalls(int[] data, int width, int height, int wallValue = 1)
    {
        var map = new FlxGridMap(width, height);
        for (int i = 0; i < data.Length && i < width * height; i++)
            map.SetCell(i % width, i / width, transparent: data[i] != wallValue, walkable: data[i] != wallValue);
        return map;
    }

    /// <summary>Makes a map from a tilemap: tiles that are solid for collisions are walls.</summary>
    public static FlxGridMap FromTilemap(FlxTilemap tilemap)
    {
        var map = new FlxGridMap(tilemap.WidthInTiles, tilemap.HeightInTiles);
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                bool solid = tilemap.IsSolidAt(tilemap.X + (x + 0.5f) * tilemap.TileWidth, tilemap.Y + (y + 0.5f) * tilemap.TileHeight);
                map.SetCell(x, y, !solid, !solid);
            }
        return map;
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    int Index(int x, int y) => y * Width + x;

    public bool IsTransparent(int x, int y) => InBounds(x, y) && _transparent[Index(x, y)];
    public bool IsWalkable(int x, int y) => InBounds(x, y) && _walkable[Index(x, y)];

    /// <summary>True if the cell is in view of the last <see cref="ComputeFov"/>.</summary>
    public bool IsInFov(int x, int y) => InBounds(x, y) && _inFov[Index(x, y)];

    /// <summary>True if the cell was ever in view (and has not been forgotten).</summary>
    public bool IsExplored(int x, int y) => InBounds(x, y) && _explored[Index(x, y)];

    public void SetCell(int x, int y, bool transparent, bool walkable)
    {
        if (!InBounds(x, y)) return;
        _transparent[Index(x, y)] = transparent;
        _walkable[Index(x, y)] = walkable;
    }

    public void SetTransparent(int x, int y, bool value) { if (InBounds(x, y)) _transparent[Index(x, y)] = value; }
    public void SetWalkable(int x, int y, bool value) { if (InBounds(x, y)) _walkable[Index(x, y)] = value; }
    public void SetExplored(int x, int y, bool value = true) { if (InBounds(x, y)) _explored[Index(x, y)] = value; }

    /// <summary>Forgets what was seen (nothing is explored, nothing is in view).</summary>
    public void ForgetAll()
    {
        Array.Clear(_explored);
        Array.Clear(_inFov);
    }

    /// <summary>Marks every cell as explored (a map spell, or a debug key).</summary>
    public void ExploreAll() => Array.Fill(_explored, true);

    /// <summary>
    /// Works out what can be seen from a cell, remembers it as explored, and returns the cells now in view.
    /// </summary>
    /// <param name="radius">How far the light reaches, in cells (0 = the cell itself only).</param>
    /// <param name="algorithm">See <see cref="FlxFovAlgorithm"/>.</param>
    /// <param name="lightWalls">Also show the walls that are hit (usually yes: you see the wall of a room).</param>
    public List<Point> ComputeFov(int x, int y, int radius, FlxFovAlgorithm algorithm = FlxFovAlgorithm.Shadowcast, bool lightWalls = true,
        FlxFovShape shape = FlxFovShape.Circle, int permissiveLevel = 2)
    {
        Array.Clear(_inFov);
        var cells = new List<Point>();
        var seen = FlxFov.Compute(this, x, y, radius, algorithm, lightWalls, shape, permissiveLevel);

        for (int i = 0; i < seen.Length; i++)
        {
            if (!seen[i]) continue;
            _inFov[i] = true;
            _explored[i] = true;
            cells.Add(new Point(i % Width, i / Width));
        }
        return cells;
    }

    /// <summary>The cells in view (from the last ComputeFov).</summary>
    public IEnumerable<Point> GetCellsInFov()
    {
        for (int i = 0; i < _inFov.Length; i++)
            if (_inFov[i]) yield return new Point(i % Width, i / Width);
    }

    /// <summary>The walkable cells next to a cell.</summary>
    public List<Point> GetWalkableNeighbors(int x, int y, bool diagonals = true)
    {
        var list = new List<Point>();
        foreach (var p in FlxGridUtil.Neighbors(x, y, diagonals))
            if (IsWalkable(p.X, p.Y)) list.Add(p);
        return list;
    }

    /// <summary>Finds a path over the walkable cells (see <see cref="FlxPathfinder"/>), or null.</summary>
    public List<(int X, int Y)>? FindPath(int fromX, int fromY, int toX, int toY, FlxPathAlgorithm algorithm = FlxPathAlgorithm.AStar,
        FlxDiagonalPolicy diagonal = FlxDiagonalPolicy.Normal, Func<int, int, float>? cost = null) =>
        FlxPathfinder.FindCells(Width, Height, (cx, cy) => !IsWalkable(cx, cy), (fromX, fromY), (toX, toY), diagonal, cost, algorithm);

    /// <summary>True if a straight line between two cells is not blocked (see <see cref="FlxBresenham.HasLineOfSight"/>).</summary>
    public bool HasLineOfSight(int x0, int y0, int x1, int y1) => FlxBresenham.HasLineOfSight(this, x0, y0, x1, y1);
}
