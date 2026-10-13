using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel.Demo.Drown;

public enum EntityKind { Kelp, Coral1, Coral2, Oceanite, Territe, Magmite, MobSpawner, SharkSpawner, JellySpawner, Geyser }

/// <summary>Something placed in a dive site: where it is and which tile it stands on.</summary>
public readonly record struct Placement(int Id, EntityKind Kind, int Col, int Row, int BaseRow)
{
    /// <summary>The map cell of the rock it stands on.</summary>
    public int SupportRow => BaseRow + 1;
}

/// <summary>
/// One dive site made from a seed: the rock, a smaller background cave, and where everything goes. The same seed always gives the same
/// site, so only what the player changed has to be saved (see <see cref="SiteChanges"/>).
/// The original game used one cellular automaton for everything; here the shallows still do, and deeper sites use FlxNoise
/// (fractal Brownian motion, with domain warping in the abyss) for rounder, more flowing caverns.
/// </summary>
public sealed class DiveLevel
{
    public const int Tile = 30;
    public const int SurfaceRows = 6;          // open water at the top of every site

    public static readonly string[] DepthNames = { "", "Shallows", "Deep", "Abyss" };

    public readonly int Depth, Seed, Cols, Rows;

    /// <summary>1 = rock, 0 = open water.</summary>
    public int[] Rock = Array.Empty<int>();

    public int BgCols, BgRows;
    public int[] Background = Array.Empty<int>();

    public readonly List<Placement> Placements = new();
    public Vector2 StartPosition => new(Cols / 2 * Tile, 0);

    DiveLevel(int depth, int seed)
    {
        Depth = Math.Clamp(depth, 1, 3);
        Seed = seed;
        Cols = Rows = Depth switch { 1 => 100, 2 => 130, _ => 160 };
    }

    public static DiveLevel Generate(int seed, int depth)
    {
        var level = new DiveLevel(depth, seed);
        level.BuildRock();
        level.BuildBackground();
        level.Populate();
        return level;
    }

    // ------------------------------------------------------------------ rock

    void BuildRock()
    {
        // try again with another variant of the seed until a big enough cavern is reachable from the surface
        for (int attempt = 0; attempt < 12; attempt++)
        {
            var data = RawCave(Cols, Rows, Depth, Seed + attempt * 977);
            SealAndConnect(data, Cols, Rows);

            int open = 0;
            for (int i = SurfaceRows * Cols; i < data.Length; i++) if (data[i] == 0) open++;
            Rock = data;
            if (open >= Cols * Rows * 0.18) break;
        }
    }

    /// <summary>The raw cave of a depth: 1 = rock, 0 = open.</summary>
    static int[] RawCave(int cols, int rows, int depth, int seed)
    {
        switch (depth)
        {
            case 1:
                return Cellular(cols, rows, seed, 5, 0.5f);
            case 2:
                return new FlxNoise(seed).GenerateCave(cols, rows, FlxNoiseType.Fbm, 0.05f, 5, 0.42f);
            default:
                var noise = new FlxNoise(seed) { FbmWarp = 1.1f };
                return noise.GenerateCave(cols, rows, FlxNoiseType.Fbm, 0.045f, 6, 0.44f);
        }
    }

    /// <summary>The classic cellular automaton cave (what FlxCaveGenerator does), but from a seed.</summary>
    static int[] Cellular(int cols, int rows, int seed, int iterations, float wallRatio)
    {
        var rnd = new Random(seed);
        var a = new int[cols * rows];
        var b = new int[cols * rows];
        for (int i = 0; i < a.Length; i++) a[i] = rnd.NextSingle() < wallRatio ? 1 : 0;

        for (int it = 0; it < iterations; it++)
        {
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int near = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= cols || ny >= rows || a[ny * cols + nx] == 1) near++;
                        }
                    b[y * cols + x] = near >= 5 ? 1 : 0;       // the 4-5 rule
                }
            (a, b) = (b, a);
        }
        return a;
    }

    /// <summary>Opens the water at the top, closes the borders, and fills every pocket that can't be reached from the surface with rock.</summary>
    static void SealAndConnect(int[] data, int cols, int rows)
    {
        for (int y = 0; y < SurfaceRows; y++)
            for (int x = 1; x < cols - 1; x++) data[y * cols + x] = 0;

        for (int y = 0; y < rows; y++) { data[y * cols] = 1; data[y * cols + cols - 1] = 1; }
        for (int x = 0; x < cols; x++) data[(rows - 1) * cols + x] = 1;

        var reached = new bool[data.Length];
        var stack = new Stack<int>();
        stack.Push(cols / 2);
        reached[cols / 2] = true;
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            int x = i % cols, y = i / cols;
            void Visit(int nx, int ny)
            {
                if (nx < 0 || ny < 0 || nx >= cols || ny >= rows) return;
                int n = ny * cols + nx;
                if (reached[n] || data[n] != 0) return;
                reached[n] = true;
                stack.Push(n);
            }
            Visit(x + 1, y); Visit(x - 1, y); Visit(x, y + 1); Visit(x, y - 1);
        }
        for (int i = 0; i < data.Length; i++) if (!reached[i]) data[i] = 1;
    }

    // ------------------------------------------------------------------ background

    void BuildBackground()
    {
        // a smaller cave that scrolls at half speed behind the rock
        BgCols = Cols / 2;
        BgRows = Rows / 2;
        Background = Cellular(BgCols, BgRows, Seed ^ 0x5bd1e995, 5, 0.5f);
    }

    // ------------------------------------------------------------------ what lives here

    public bool IsRock(int col, int row) => col < 0 || row < 0 || col >= Cols || row >= Rows || Rock[row * Cols + col] == 1;

    void Populate()
    {
        var rnd = new Random(Seed ^ 0x2545F491);

        // floors: open cells with rock below, away from the surface
        var floors = new List<int>();
        var openCells = new List<int>();
        for (int y = SurfaceRows + 2; y < Rows - 2; y++)
            for (int x = 2; x < Cols - 2; x++)
            {
                if (Rock[y * Cols + x] != 0) continue;
                openCells.Add(y * Cols + x);
                if (Rock[(y + 1) * Cols + x] == 1) floors.Add(y * Cols + x);
            }
        Shuffle(floors, rnd);
        Shuffle(openCells, rnd);

        int id = 0;
        var used = new HashSet<int>();

        // how much of the floor each thing takes (the floor is the open cells with rock below)
        void PlaceOnFloors(EntityKind kind, float share)
        {
            int wanted = (int)(floors.Count * share), placed = 0;
            foreach (int cell in floors)
            {
                if (placed >= wanted) break;
                if (!used.Add(cell)) continue;
                Placements.Add(new Placement(id++, kind, cell % Cols, cell / Cols, cell / Cols));
                placed++;
            }
        }

        PlaceOnFloors(EntityKind.Oceanite, 0.10f);
        PlaceOnFloors(EntityKind.Territe, 0.08f);
        PlaceOnFloors(EntityKind.Magmite, 0.07f);
        PlaceOnFloors(EntityKind.Coral1, 0.07f);
        PlaceOnFloors(EntityKind.Coral2, 0.07f);

        // kelp grows up from the floor, 3 to 11 tiles tall, while the water is open
        int kelpBases = (int)(floors.Count * 0.3f);
        int made = 0;
        foreach (int cell in floors)
        {
            if (made >= kelpBases) break;
            if (!used.Add(cell)) continue;
            made++;
            int height = rnd.Next(3, 12);
            int x = cell % Cols, y = cell / Cols;
            for (int h = 0; h < height && y - h > SurfaceRows; h++)
            {
                if (Rock[(y - h) * Cols + x] != 0) break;
                Placements.Add(new Placement(id++, EntityKind.Kelp, x, y - h, y));
            }
        }

        // mob spawners in the open water, a little way from where the player starts
        int[] counts = Depth switch { 1 => new[] { 6, 3, 3 }, 2 => new[] { 14, 6, 8 }, _ => new[] { 24, 10, 12 } };
        EntityKind[] kinds = { EntityKind.MobSpawner, EntityKind.SharkSpawner, EntityKind.JellySpawner };
        int next = 0;
        for (int k = 0; k < kinds.Length; k++)
        {
            int placed = 0;
            while (placed < counts[k] && next < openCells.Count)
            {
                int cell = openCells[next++];
                int x = cell % Cols, y = cell / Cols;
                if (y < SurfaceRows + 12) continue;
                Placements.Add(new Placement(id++, kinds[k], x, y, y));
                placed++;
            }
        }

        PlaceGeysers(floors, used, ref id, rnd);
    }

    /// <summary>
    /// A few bubble geysers (made last, so the ids of everything else stay as they were): on a floor with room above it, at least
    /// <see cref="GeyserSpacing"/> tiles from each other, and not many: 3 in the shallows, 4 deep, 6 in the abyss.
    /// </summary>
    void PlaceGeysers(List<int> floors, HashSet<int> used, ref int id, Random rnd)
    {
        int wanted = Depth switch { 1 => 3, 2 => 4, _ => 6 };
        var chosen = new List<(int X, int Y)>();

        foreach (int cell in floors)
        {
            if (chosen.Count >= wanted) break;
            int x = cell % Cols, y = cell / Cols;
            if (y < SurfaceRows + 10 || used.Contains(cell)) continue;
            if (GeyserHeadroom(x, y) < 5) continue;
            if (chosen.Exists(c => Math.Abs(c.X - x) + Math.Abs(c.Y - y) < GeyserSpacing)) continue;

            used.Add(cell);
            chosen.Add((x, y));
            Placements.Add(new Placement(id++, EntityKind.Geyser, x, y, y));
        }
    }

    public const int GeyserSpacing = 24;

    /// <summary>How many open cells there are above a floor cell (up to 7): the height of a geyser's column.</summary>
    public int GeyserHeadroom(int col, int row)
    {
        int n = 0;
        while (n < 7 && row - 1 - n > SurfaceRows && !IsRock(col, row - 1 - n)) n++;
        return n;
    }

    static void Shuffle<T>(List<T> list, Random rnd)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
