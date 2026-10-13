using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// Both modes expect Flixel's 16-frame auto-tile sheet (e.g. autotiles.png / autotiles_alt.png, 8x8 tiles).
/// Any non-zero map value counts as "filled"; map edges count as filled. A tile's frame is
/// mask = 1 (up neighbour) + 2 (right) + 4 (down) + 8 (left), so frame 0 is the isolated tile and frame 15
/// is fully surrounded. Internally the map value is mask + 1 and FrameOffset is set to 1, exactly like Flixel.
/// </summary>
public enum FlxAutoTiling
{
    Off,
    /// <summary>Good for thin walls that don't need interior corner art.</summary>
    Auto,
    /// <summary>Better for thick walls: fully surrounded tiles whose diagonal is open use frames 1, 2, 4 or 8 (interior corners).</summary>
    Alt,
    /// <summary>
    /// The 47-tile "blob" set: every combination of the 8 neighbours that can occur has its own frame (edges, outer and inner
    /// corners, single tiles, thin lines). Needs a tileset of 47 frames in Flixel's FULL order.
    /// </summary>
    Full
}

/// <summary>One tile TYPE (not one tile on the map). Reused as the collision object while a tilemap collides.</summary>
public sealed class FlxTile : FlxObject
{
    public readonly FlxTilemap Tilemap;
    public readonly int Index;
    public int MapIndex;                       // which map cell it is currently positioned on
    public Action<FlxTile, FlxObject>? Callback;
    public Type? Filter;                       // callback only fires for objects of this type (or subclasses)

    public FlxTile(FlxTilemap map, int index, float width, float height, bool visible, FlxDirection allow)
        : base(0, 0, width, height)
    {
        Tilemap = map; Index = index;
        Visible = visible;                     // false = never drawn
        AllowCollisions = allow;
        Immovable = true;
        Moves = false;
    }
}

/// <summary>
/// Grid of tiles drawn from a tileset image, with Flixel-style collision:
/// FlxG.Collide(player, tilemap) / FlxG.Overlap(player, tilemap).
///
/// Tile value v: not drawn if v &lt; DrawIndex; collides if v &gt;= CollideIndex; drawn using tileset
/// frame (v - FrameOffset), frames counted left-to-right, top-to-bottom. Use FrameOffset = 1 for
/// Tiled maps (their ids start at 1). To move the map, call Reset(x, y).
/// </summary>
public class FlxTilemap : FlxObject
{
    public Texture2D? Tileset { get; private set; }
    public int TileWidth { get; private set; }
    public int TileHeight { get; private set; }
    public int WidthInTiles { get; private set; }
    public int HeightInTiles { get; private set; }
    public int DrawIndex { get; private set; }
    public int CollideIndex { get; private set; }
    public int TotalTiles => _data.Length;

    public int FrameOffset;
    public Color Tint = Color.White;

    public Rectangle Bounds => new((int)X, (int)Y, (int)Width, (int)Height);

    int[] _data = Array.Empty<int>();
    FlxTile[] _tiles = Array.Empty<FlxTile>();
    int _columns = 1, _frameCount;
    FlxAutoTiling _auto;

    /// <summary>Pixels around the tileset image before the first tile (Tiled's "margin"). Set before loading.</summary>
    public int TilesetMargin;

    /// <summary>Pixels between tiles in the tileset image (Tiled's "spacing"). Set before loading.</summary>
    public int TilesetSpacing;

    sealed class TileAnimation
    {
        public int[] Frames = Array.Empty<int>();
        public float[] Ends = Array.Empty<float>();   // cumulative end time of each frame, in seconds
        public float Total;
    }

    readonly Dictionary<int, TileAnimation> _animations = new();
    float[]? _phases;       // per-cell start offset (seconds) so neighbouring animated tiles are not in lock-step
    float _animTime;
    byte[]? _flags;         // per-cell flip bits (see FlxTileFlip)

    public FlxTilemap() : base(0, 0, 0, 0)
    {
        Immovable = true;
        Moves = false;
    }

    // ================================================================ loading

    public FlxTilemap LoadMapFromArray(int[] mapData, int widthInTiles, int heightInTiles, Texture2D tileset,
        int tileWidth = 0, int tileHeight = 0, FlxAutoTiling autoTile = FlxAutoTiling.Off,
        int drawIndex = 1, int collideIndex = 1, int startingIndex = 0) =>
        LoadCore(mapData, widthInTiles, heightInTiles, tileset, tileWidth, tileHeight, autoTile, drawIndex, collideIndex, startingIndex);

    public FlxTilemap LoadMapFromArray(int[] mapData, int widthInTiles, int heightInTiles, string tilesetPath,
        int tileWidth = 0, int tileHeight = 0, FlxAutoTiling autoTile = FlxAutoTiling.Off,
        int drawIndex = 1, int collideIndex = 1, int startingIndex = 0) =>
        LoadCore(mapData, widthInTiles, heightInTiles, LoadTexture(tilesetPath), tileWidth, tileHeight, autoTile, drawIndex, collideIndex, startingIndex);

    /// <summary>Comma separated values, one map row per line (the format Tiled / Ogmo export as CSV).</summary>
    public FlxTilemap LoadMapFromCsv(string csv, Texture2D tileset,
        int tileWidth = 0, int tileHeight = 0, FlxAutoTiling autoTile = FlxAutoTiling.Off,
        int drawIndex = 1, int collideIndex = 1, int startingIndex = 0)
    {
        var data = ParseCsv(csv, out int w, out int h);
        return LoadCore(data, w, h, tileset, tileWidth, tileHeight, autoTile, drawIndex, collideIndex, startingIndex);
    }

    public FlxTilemap LoadMapFromCsv(string csv, string tilesetPath,
        int tileWidth = 0, int tileHeight = 0, FlxAutoTiling autoTile = FlxAutoTiling.Off,
        int drawIndex = 1, int collideIndex = 1, int startingIndex = 0) =>
        LoadMapFromCsv(csv, LoadTexture(tilesetPath), tileWidth, tileHeight, autoTile, drawIndex, collideIndex, startingIndex);

    /// <summary>Same as LoadMapFromCsv but reads the CSV from a file.</summary>
    public FlxTilemap LoadMapFromCsvFile(string csvPath, string tilesetPath,
        int tileWidth = 0, int tileHeight = 0, FlxAutoTiling autoTile = FlxAutoTiling.Off,
        int drawIndex = 1, int collideIndex = 1, int startingIndex = 0) =>
        LoadMapFromCsv(File.ReadAllText(csvPath), LoadTexture(tilesetPath), tileWidth, tileHeight, autoTile, drawIndex, collideIndex, startingIndex);

    static Texture2D LoadTexture(string path) => FlxAssets.GetTexture(path);

    static int[] ParseCsv(string csv, out int w, out int h)
    {
        var rows = new List<int[]>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            var cells = line.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (cells.Length == 0) continue;
            rows.Add(Array.ConvertAll(cells, s => int.Parse(s, CultureInfo.InvariantCulture)));
        }
        h = rows.Count;
        w = h > 0 ? rows[0].Length : 0;
        var data = new int[w * h];
        for (int r = 0; r < h; r++)
        {
            if (rows[r].Length != w)
                throw new InvalidDataException($"CSV row {r} has {rows[r].Length} cells, expected {w}.");
            Array.Copy(rows[r], 0, data, r * w, w);
        }
        return data;
    }

    FlxTilemap LoadCore(int[] data, int w, int h, Texture2D tileset, int tw, int th,
        FlxAutoTiling auto, int drawIndex, int collideIndex, int startingIndex)
    {
        if (w <= 0 || h <= 0) throw new ArgumentException("Map must be at least 1x1 tiles.");
        if (data.Length != w * h)
            throw new ArgumentException($"Map data has {data.Length} entries but {w}x{h} needs {w * h}.");
        if (tw <= 0) tw = tileset.Height;
        if (th <= 0) th = tw;

        if (auto != FlxAutoTiling.Off)
        {
            // Flixel: auto-tiling overrides these (tile value 0 = empty, value n draws frame n - 1)
            FrameOffset = 1;
            drawIndex = 1;
            collideIndex = 1;
        }
        else if (startingIndex != 0)
        {
            FrameOffset = startingIndex;     // e.g. Tiled's firstgid: tile value v draws frame v - startingIndex
        }

        Tileset = tileset;
        TileWidth = tw; TileHeight = th;
        WidthInTiles = w; HeightInTiles = h;
        DrawIndex = drawIndex; CollideIndex = collideIndex;
        _auto = auto;
        _data = (int[])data.Clone();
        for (int i = 0; i < _data.Length; i++) if (_data[i] < 0) _data[i] = 0;   // some formats use -1 for empty

        _columns = Math.Max(1, (tileset.Width - 2 * TilesetMargin + TilesetSpacing) / (tw + TilesetSpacing));
        int rows = Math.Max(1, (tileset.Height - 2 * TilesetMargin + TilesetSpacing) / (th + TilesetSpacing));
        _frameCount = _columns * rows;
        Width = w * tw; Height = h * th;

        if (auto != FlxAutoTiling.Off) AutoTile();

        int max = 0;
        foreach (var v in _data) if (v > max) max = v;
        _tiles = Array.Empty<FlxTile>();
        EnsureTileObjects(Math.Max(_frameCount + FrameOffset, max + 1));
        return this;
    }

    void EnsureTileObjects(int count)
    {
        if (count <= _tiles.Length) return;
        int old = _tiles.Length;
        Array.Resize(ref _tiles, count);
        for (int i = old; i < count; i++)
            _tiles[i] = new FlxTile(this, i, TileWidth, TileHeight, i >= DrawIndex,
                i >= CollideIndex ? FlxDirection.Any : FlxDirection.None);
    }

    // Flixel's table that turns the 8-neighbour bit mask (0..255) into a frame of the 47-tile FULL set: frame = mask - FullOffsets[mask].
    static readonly int[] FullOffsets =
    {
         0,   0, 0, 0,  2,   2, 0,   3, 0, 0, 0, 0,  0,   0, 0,   0,
        11,  11, 0, 0, 13,  13, 0,  14, 0, 0, 0, 0, 18,  18, 0,  19,
         0,   0, 0, 0,  0,   0, 0,   0, 0, 0, 0, 0,  0,   0, 0,   0,
         0,   0, 0, 0,  0,   0, 0,   0, 0, 0, 0, 0,  0,   0, 0,   0,
        51,  51, 0, 0, 53,  53, 0,  54, 0, 0, 0, 0,  0,   0, 0,   0,
        62,  62, 0, 0, 64,  64, 0,  65, 0, 0, 0, 0, 69,  69, 0,  70,
         0,   0, 0, 0,  0,   0, 0,   0, 0, 0, 0, 0,  0,   0, 0,   0,
        86,  86, 0, 0, 88,  88, 0,  89, 0, 0, 0, 0, 93,  93, 0,  94,
         0,   0, 0, 0,  0,   0, 0,   0, 0, 0, 0, 0,  0,   0, 0,   0,
         0,   0, 0, 0,  0,   0, 0,   0, 0, 0, 0, 0,  0,   0, 0,   0,
         0,   0, 0, 0,  0,   0, 0,   0, 0, 0, 0, 0,  0,   0, 0,   0,
         0,   0, 0, 0,  0,   0, 0,   0, 0, 0, 0, 0,  0,   0, 0,   0,
         0, 159, 0, 0,  0, 162, 0, 163, 0, 0, 0, 0,  0,   0, 0,   0,
         0, 172, 0, 0,  0, 175, 0, 176, 0, 0, 0, 0,  0, 181, 0, 182,
         0,   0, 0, 0,  0,   0, 0,   0, 0, 0, 0, 0,  0,   0, 0,   0,
         0, 199, 0, 0,  0, 202, 0, 203, 0, 0, 0, 0,  0, 208, 0, 209
    };

    void AutoTileFull(int[] src, int i)
    {
        int w = WidthInTiles, total = src.Length;

        // the map edge counts as filled
        bool wallUp = i - w < 0, wallRight = i % w >= w - 1, wallDown = i + w >= total, wallLeft = i % w <= 0;

        bool up = wallUp || src[i - w] > 0;
        bool upRight = wallUp || wallRight || src[i - w + 1] > 0;
        bool right = wallRight || src[i + 1] > 0;
        bool rightDown = wallRight || wallDown || src[i + w + 1] > 0;
        bool down = wallDown || src[i + w] > 0;
        bool downLeft = wallDown || wallLeft || src[i + w - 1] > 0;
        bool left = wallLeft || src[i - 1] > 0;
        bool leftUp = wallLeft || wallUp || src[i - w - 1] > 0;

        // a corner only counts when both edges beside it are filled too
        int mask = 0;
        if (up) mask += 1;
        if (upRight && up && right) mask += 2;
        if (right) mask += 4;
        if (rightDown && right && down) mask += 8;
        if (down) mask += 16;
        if (downLeft && down && left) mask += 32;
        if (left) mask += 64;
        if (leftUp && left && up) mask += 128;

        _data[i] = mask - FullOffsets[mask] + 1;       // value n draws frame n - 1
    }

    void AutoTile()
    {
        int w = WidthInTiles, total = _data.Length;
        var src = (int[])_data.Clone();           // "filled" is judged on the original data

        if (_auto == FlxAutoTiling.Full)
        {
            for (int i = 0; i < total; i++) if (src[i] != 0) AutoTileFull(src, i);
            return;
        }
        for (int i = 0; i < total; i++)
        {
            if (src[i] == 0) continue;

            bool hasUp = i - w >= 0, hasDown = i + w < total;
            bool hasLeft = i % w > 0, hasRight = i % w < w - 1;

            int v = 0;
            if (!hasUp || src[i - w] > 0) v += 1;
            if (!hasRight || src[i + 1] > 0) v += 2;
            if (!hasDown || src[i + w] > 0) v += 4;
            if (!hasLeft || src[i - 1] > 0) v += 8;

            // ALT also checks interior corners (later checks win, same order as Flixel)
            if (_auto == FlxAutoTiling.Alt && v == 15)
            {
                if (hasLeft && hasDown && src[i + w - 1] <= 0) v = 1;    // bottom-left open
                if (hasLeft && hasUp && src[i - w - 1] <= 0) v = 2;      // top-left open
                if (hasRight && hasUp && src[i - w + 1] <= 0) v = 4;     // top-right open
                if (hasRight && hasDown && src[i + w + 1] <= 0) v = 8;   // bottom-right open
            }

            _data[i] = v + 1;
        }
    }

    // ================================================================ tile data access

    public int GetTile(int column, int row) =>
        column < 0 || row < 0 || column >= WidthInTiles || row >= HeightInTiles ? -1 : _data[row * WidthInTiles + column];

    public int GetTileByIndex(int mapIndex) => mapIndex < 0 || mapIndex >= _data.Length ? -1 : _data[mapIndex];

    /// <summary>Tile value at a world position, or -1 outside the map.</summary>
    public int GetTileAt(float worldX, float worldY)
    {
        int i = GetMapIndexAt(worldX, worldY);
        return i < 0 ? -1 : _data[i];
    }

    /// <summary>Map index (row * WidthInTiles + column) at a world position, or -1 outside the map.</summary>
    public int GetMapIndexAt(float worldX, float worldY)
    {
        int c = (int)MathF.Floor((worldX - X) / TileWidth);
        int r = (int)MathF.Floor((worldY - Y) / TileHeight);
        if (c < 0 || r < 0 || c >= WidthInTiles || r >= HeightInTiles) return -1;
        return r * WidthInTiles + c;
    }

    public Vector2 GetTileCoords(int mapIndex, bool midpoint = true)
    {
        int c = mapIndex % WidthInTiles, r = mapIndex / WidthInTiles;
        var p = new Vector2(X + c * TileWidth, Y + r * TileHeight);
        return midpoint ? p + new Vector2(TileWidth * 0.5f, TileHeight * 0.5f) : p;
    }

    public bool SetTile(int column, int row, int tileIndex)
    {
        if (column < 0 || row < 0 || column >= WidthInTiles || row >= HeightInTiles) return false;
        return SetTileByIndex(row * WidthInTiles + column, tileIndex);
    }

    /// <summary>With auto-tiling (Auto/Alt) any non-zero value means "filled" and the whole map is re-tiled.</summary>
    public bool SetTileByIndex(int mapIndex, int tileIndex)
    {
        if (mapIndex < 0 || mapIndex >= _data.Length || tileIndex < 0) return false;
        _data[mapIndex] = tileIndex;
        if (_flags != null) _flags[mapIndex] = 0;
        EnsureTileObjects(tileIndex + 1);
        if (_auto != FlxAutoTiling.Off) AutoTile();
        return true;
    }

    /// <summary>All map indices holding the given tile value.</summary>
    public List<int> FindTiles(int tileIndex)
    {
        var l = new List<int>();
        for (int i = 0; i < _data.Length; i++) if (_data[i] == tileIndex) l.Add(i);
        return l;
    }

    public FlxTile? GetTileObject(int tileIndex) =>
        tileIndex >= 0 && tileIndex < _tiles.Length ? _tiles[tileIndex] : null;

    /// <summary>
    /// Configure a tile type (or a range of types): which sides collide, plus an optional callback that fires
    /// when an object touches it. A tile with AllowCollisions = None and a callback is a trigger zone.
    /// </summary>
    public void SetTileProperties(int tile, FlxDirection allowCollisions = FlxDirection.Any,
        Action<FlxTile, FlxObject>? callback = null, Type? filter = null, int range = 1)
    {
        if (tile < 0 || range < 1) return;
        EnsureTileObjects(tile + range);
        for (int i = tile; i < tile + range; i++)
        {
            var t = _tiles[i];
            t.AllowCollisions = allowCollisions;
            t.Callback = callback;
            t.Filter = filter;
        }
    }

    bool SolidAtCell(int c, int r)
    {
        if (c < 0 || r < 0 || c >= WidthInTiles || r >= HeightInTiles) return false;
        int v = _data[r * WidthInTiles + c];
        return v >= 0 && v < _tiles.Length && _tiles[v].AllowCollisions != FlxDirection.None;
    }

    public bool IsSolidAt(float worldX, float worldY)
    {
        int i = GetMapIndexAt(worldX, worldY);
        return i >= 0 && SolidAtCell(i % WidthInTiles, i / WidthInTiles);
    }

    /// <summary>
    /// A* path between two world positions through the tiles that are not solid. Returns world-coordinate nodes
    /// (tile middles, from <paramref name="start"/> to <paramref name="end"/>) or null if there is no way. See FlxPathfinder.
    /// </summary>
    public List<Vector2>? FindPath(Vector2 start, Vector2 end, FlxDiagonalPolicy diagonal = FlxDiagonalPolicy.Wide,
        FlxPathSimplify simplify = FlxPathSimplify.Straight, Func<int, int, float>? cost = null) =>
        FlxPathfinder.FindPath(this, start, end, diagonal, simplify, cost);

    // ================================================================ collision

    public override bool Overlaps(FlxObject o) => OverlapsWithCallback(o, null);

    internal bool SeparateObject(FlxObject obj) => Solid && OverlapsWithCallback(obj, FlxObject.Separate);

    /// <summary>
    /// Visits every tile the object touches (including the area it swept through this frame).
    /// Solid tiles go through <paramref name="process"/> (e.g. FlxObject.Separate), or a plain overlap test if null.
    /// Non-solid tiles with a callback act as triggers. Returns true if anything was hit.
    /// </summary>
    public bool OverlapsWithCallback(FlxObject obj, Func<FlxObject, FlxObject, bool>? process)
    {
        if (_data.Length == 0) return false;

        float left = MathF.Min(obj.X, obj.Last.X);
        float right = MathF.Max(obj.X, obj.Last.X) + obj.Width;
        float top = MathF.Min(obj.Y, obj.Last.Y);
        float bottom = MathF.Max(obj.Y, obj.Last.Y) + obj.Height;

        int c0 = Math.Max(0, (int)MathF.Floor((left - X) / TileWidth));
        int r0 = Math.Max(0, (int)MathF.Floor((top - Y) / TileHeight));
        int c1 = Math.Min(WidthInTiles - 1, (int)MathF.Floor((right - X) / TileWidth));
        int r1 = Math.Min(HeightInTiles - 1, (int)MathF.Floor((bottom - Y) / TileHeight));

        float dx = X - Last.X, dy = Y - Last.Y;   // non-zero only for a moving tilemap
        bool results = false;

        for (int r = r0; r <= r1; r++)
        {
            for (int c = c0; c <= c1; c++)
            {
                int mapIndex = r * WidthInTiles + c;
                int v = _data[mapIndex];
                if (v < 0 || v >= _tiles.Length) continue;

                var tile = _tiles[v];
                bool solid = tile.AllowCollisions != FlxDirection.None;
                if (!solid && tile.Callback == null) continue;

                tile.X = X + c * TileWidth;
                tile.Y = Y + r * TileHeight;
                tile.Last = new Vector2(tile.X - dx, tile.Y - dy);
                tile.MapIndex = mapIndex;
                tile.Touching = FlxDirection.None;

                bool found = solid && process != null ? process(tile, obj) : tile.Overlaps(obj);
                if (!found) continue;

                results = true;
                if (tile.Callback != null && (tile.Filter == null || tile.Filter.IsInstanceOfType(obj)))
                    tile.Callback(tile, obj);
            }
        }
        return results;
    }

    /// <summary>
    /// Casts a ray between two world points. Returns true if the way is clear; otherwise false and
    /// <paramref name="hit"/> is the point where it entered the first solid tile.
    /// </summary>
    public bool Ray(Vector2 start, Vector2 end, out Vector2 hit)
    {
        hit = end;
        float sx = (start.X - X) / TileWidth, sy = (start.Y - Y) / TileHeight;
        float ex = (end.X - X) / TileWidth, ey = (end.Y - Y) / TileHeight;
        int cx = (int)MathF.Floor(sx), cy = (int)MathF.Floor(sy);

        if (SolidAtCell(cx, cy)) { hit = start; return false; }

        float dx = ex - sx, dy = ey - sy;
        int stepX = dx > 0 ? 1 : -1, stepY = dy > 0 ? 1 : -1;
        float tDeltaX = dx == 0 ? float.PositiveInfinity : MathF.Abs(1f / dx);
        float tDeltaY = dy == 0 ? float.PositiveInfinity : MathF.Abs(1f / dy);
        float tMaxX = dx == 0 ? float.PositiveInfinity : (dx > 0 ? cx + 1 - sx : sx - cx) * tDeltaX;
        float tMaxY = dy == 0 ? float.PositiveInfinity : (dy > 0 ? cy + 1 - sy : sy - cy) * tDeltaY;

        while (true)
        {
            float t;
            if (tMaxX < tMaxY) { t = tMaxX; cx += stepX; tMaxX += tDeltaX; }
            else { t = tMaxY; cy += stepY; tMaxY += tDeltaY; }

            if (t > 1f) return true;
            if (SolidAtCell(cx, cy)) { hit = start + (end - start) * t; return false; }
        }
    }

    // ================================================================ drawing

    // ================================================================ animated + flipped tiles

    /// <summary>
    /// Make every cell holding <paramref name="tileValue"/> cycle through other tile values over time.
    /// frameValues are map values (for Tiled: firstgid + tile id), durations are in milliseconds.
    /// With randomizePhase each cell starts at a random point of the cycle. Call after loading the map.
    /// Collision still uses the original value.
    /// </summary>
    public void SetTileAnimation(int tileValue, IReadOnlyList<int> frameValues, IReadOnlyList<int> durationsMs, bool randomizePhase = false)
    {
        if (frameValues.Count == 0 || frameValues.Count != durationsMs.Count)
            throw new ArgumentException("An animation needs the same, non-zero number of frames and durations.");

        var anim = new TileAnimation { Frames = frameValues.ToArray(), Ends = new float[frameValues.Count] };
        float t = 0;
        for (int i = 0; i < anim.Ends.Length; i++)
        {
            t += Math.Max(1, durationsMs[i]) / 1000f;
            anim.Ends[i] = t;
        }
        anim.Total = t;
        _animations[tileValue] = anim;

        if (randomizePhase)
        {
            _phases ??= new float[_data.Length];
            for (int i = 0; i < _data.Length; i++)
                if (_data[i] == tileValue) _phases[i] = FlxG.Random.Float(0, anim.Total);
        }
    }

    int AnimatedValue(int cell, int value)
    {
        if (!_animations.TryGetValue(value, out var a)) return value;
        float t = (_animTime + (_phases != null ? _phases[cell] : 0f)) % a.Total;
        for (int i = 0; i < a.Ends.Length; i++)
            if (t < a.Ends[i]) return a.Frames[i];
        return a.Frames[^1];
    }

    /// <summary>Per-cell flip bits (FlxTileFlip.Horizontal / Vertical / Diagonal), same layout as the map data.</summary>
    public void SetTileFlags(byte[] flags)
    {
        if (flags.Length != _data.Length)
            throw new ArgumentException($"Expected {_data.Length} flag entries but got {flags.Length}.");
        _flags = flags.Any(f => f != 0) ? (byte[])flags.Clone() : null;
    }

    public override void Update(float elapsed)
    {
        if (_animations.Count > 0) _animTime += elapsed;
        base.Update(elapsed);
    }

    // ================================================================ drawing

    public override void Draw()
    {
        if (Tileset == null || _data.Length == 0) return;
        var cam = FlxG.Camera;

        float left = X - cam.ScrollX * ScrollFactor.X;
        float top = Y - cam.ScrollY * ScrollFactor.Y;
        int ox = (int)MathF.Round(left), oy = (int)MathF.Round(top);

        int c0 = Math.Max(0, (int)MathF.Floor(-left / TileWidth));
        int r0 = Math.Max(0, (int)MathF.Floor(-top / TileHeight));
        int c1 = Math.Min(WidthInTiles - 1, (int)MathF.Floor((cam.ViewWidth - left) / TileWidth));
        int r1 = Math.Min(HeightInTiles - 1, (int)MathF.Floor((cam.ViewHeight - top) / TileHeight));

        var batch = FlxG.SpriteBatch;
        int stepX = TileWidth + TilesetSpacing, stepY = TileHeight + TilesetSpacing;
        bool square = TileWidth == TileHeight;
        var half = new Vector2(TileWidth * 0.5f, TileHeight * 0.5f);

        for (int r = r0; r <= r1; r++)
        {
            for (int c = c0; c <= c1; c++)
            {
                int cell = r * WidthInTiles + c;
                int v = _data[cell];
                if (v < 0 || v >= _tiles.Length || !_tiles[v].Visible) continue;

                int shown = _animations.Count == 0 ? v : AnimatedValue(cell, v);
                int frame = shown - FrameOffset;
                if (frame < 0 || frame >= _frameCount) continue;

                var src = new Rectangle(TilesetMargin + (frame % _columns) * stepX, TilesetMargin + (frame / _columns) * stepY, TileWidth, TileHeight);
                var dest = new Rectangle(ox + c * TileWidth, oy + r * TileHeight, TileWidth, TileHeight);

                byte flags = _flags == null ? (byte)0 : _flags[cell];
                if (!square) flags &= unchecked((byte)~FlxTileFlip.Diagonal);   // a 90 degree turn only makes sense for square tiles
                if (flags == 0)
                {
                    batch.Draw(Tileset, dest, src, Tint);
                    continue;
                }

                var (effects, rotation) = FlxTileFlip.Resolve(flags);
                batch.Draw(Tileset, new Vector2(dest.X, dest.Y) + half, src, Tint, rotation, half, 1f, effects, 0f);
            }
        }
    }
}

/// <summary>
/// Tiled's tile flip bits as a SpriteBatch transform. Tiled applies the diagonal flip (swap x and y) first,
/// then the horizontal, then the vertical one. SpriteBatch can mirror horizontally and then rotate, so every
/// combination is expressed as "mirror or not, then rotate by a multiple of 90 degrees clockwise".
/// </summary>
public static class FlxTileFlip
{
    public const byte Horizontal = 1, Vertical = 2, Diagonal = 4;

    static readonly int[,] Identity = { { 1, 0 }, { 0, 1 } };
    static readonly int[,] Transpose = { { 0, 1 }, { 1, 0 } };
    static readonly int[,] MirrorX = { { -1, 0 }, { 0, 1 } };
    static readonly int[,] MirrorY = { { 1, 0 }, { 0, -1 } };
    static readonly (SpriteEffects, float)[] Table = Build();

    public static (SpriteEffects Effects, float Rotation) Resolve(byte flags) => Table[flags & 7];

    /// <summary>The 2x2 matrix Tiled means by these flags (used to build and to test the table).</summary>
    public static int[,] TiledMatrix(byte flags)
    {
        var m = Identity;
        if ((flags & Diagonal) != 0) m = Mul(Transpose, m);
        if ((flags & Horizontal) != 0) m = Mul(MirrorX, m);
        if ((flags & Vertical) != 0) m = Mul(MirrorY, m);
        return m;
    }

    /// <summary>The matrix SpriteBatch produces for an effect + rotation (mirror first, then rotate clockwise).</summary>
    public static int[,] SpriteBatchMatrix(SpriteEffects effects, float rotation)
    {
        int k = (int)MathF.Round(rotation / (MathF.PI / 2f)) & 3;
        return Mul(Rot(k), (effects & SpriteEffects.FlipHorizontally) != 0 ? MirrorX : Identity);
    }

    static (SpriteEffects, float)[] Build()
    {
        var table = new (SpriteEffects, float)[8];
        for (int f = 0; f < 8; f++)
        {
            var wanted = TiledMatrix((byte)f);
            for (int mirror = 0; mirror < 2; mirror++)
                for (int k = 0; k < 4; k++)
                {
                    var candidate = Mul(Rot(k), mirror == 1 ? MirrorX : Identity);
                    if (Same(candidate, wanted))
                        table[f] = (mirror == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, k * MathF.PI / 2f);
                }
        }
        return table;
    }

    // clockwise quarter turns on a screen where y points down
    static int[,] Rot(int k) => (k & 3) switch
    {
        0 => Identity,
        1 => new[,] { { 0, -1 }, { 1, 0 } },
        2 => new[,] { { -1, 0 }, { 0, -1 } },
        _ => new[,] { { 0, 1 }, { -1, 0 } }
    };

    static int[,] Mul(int[,] a, int[,] b) => new[,]
    {
        { a[0, 0] * b[0, 0] + a[0, 1] * b[1, 0], a[0, 0] * b[0, 1] + a[0, 1] * b[1, 1] },
        { a[1, 0] * b[0, 0] + a[1, 1] * b[1, 0], a[1, 0] * b[0, 1] + a[1, 1] * b[1, 1] }
    };

    static bool Same(int[,] a, int[,] b) =>
        a[0, 0] == b[0, 0] && a[0, 1] == b[0, 1] && a[1, 0] == b[1, 0] && a[1, 1] == b[1, 1];
}
