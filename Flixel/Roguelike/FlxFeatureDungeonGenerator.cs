using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>
/// Dungeons grown from a first room (ported from NewDungeonMapCreationStrategy, the RogueBasin "dungeon-building algorithm"): a wall with floor on exactly one side
/// is picked, and a room or a corridor is built on the far side of it if there is space (with a wall all around it); the picked wall cell becomes the doorway.
/// This goes on until enough rooms stand or the tries run out. Every room is joined to the rest by a single doorway.
/// </summary>
public static class FlxFeatureDungeonGenerator
{
    /// <param name="attempts">How many rooms and corridors are tried.</param>
    /// <param name="roomMin">The smallest room side (the sides are this and the next two sizes).</param>
    /// <param name="wantRooms">Stop when this many rooms stand, the first included (0: use all the tries).</param>
    /// <param name="seed">The same seed gives the same dungeon (null: a new one each time).</param>
    /// <param name="corridorMin">The shortest corridor. A corridor always ends in a room, so there are no dead ends.</param>
    /// <param name="corridorMax">The longest corridor.</param>
    /// <remarks><see cref="FlxBspMap.Rooms"/> holds the rooms, in the order they were built; the corridors are not in it.</remarks>
    public static FlxBspMap Generate(int width, int height, int attempts = 400, int roomMin = 5, int wantRooms = 0, int? seed = null, int corridorMin = 2, int corridorMax = 4)
    {
        var rnd = seed.HasValue ? new Random(seed.Value) : new Random();
        var map = new FlxBspMap { Width = width, Height = height, Data = new int[width * height] };
        Array.Fill(map.Data, 1);

        // the first room, in the middle of the map
        int cx = width / 2, cy = height / 2;
        var first = new Rectangle(cx - 3, cy - 3, 6, 6);
        Fill(map, first);
        map.Rooms.Add(first);

        for (int i = 0; i < attempts && (wantRooms <= 0 || map.Rooms.Count < wantRooms); i++)
        {
            // a wall with floor on one side only
            int x = 0, y = 0, dx = 0, dy = 0;
            bool found = false;
            for (int t = 0; t < 300 && !found; t++)
            {
                x = rnd.Next(0, width); y = rnd.Next(0, height);
                found = IsWall(map, x, y, out dx, out dy);
            }
            if (!found) break;

            // (ux, uy): the way the new feature grows, away from the floor
            int ux = dx, uy = dx != 0 ? 0 : dy;
            int along = rnd.Next(0, 3) + roomMin, across = rnd.Next(0, 3) + roomMin;

            var parts = new List<Rectangle>();
            if (rnd.Next(1, 10) < 5)
            {
                // a short corridor, and always a room at its end, so that no corridor is a dead end
                int length = rnd.Next(corridorMin, corridorMax + 1);
                var corridor = Feature(x + ux, y + uy, ux, uy, length, 1);
                var end = Feature(x + ux * (length + 1), y + uy * (length + 1), ux, uy, along, across);
                parts.Add(corridor);
                parts.Add(end);
            }
            else parts.Add(Feature(x + ux, y + uy, ux, uy, along, across));

            bool fits = true;
            foreach (var p in parts) fits &= CanAdd(map, new Rectangle(p.X - 1, p.Y - 1, p.Width + 2, p.Height + 2));
            if (!fits) continue;

            map.Data[y * width + x] = 0;      // the doorway
            map.Doorways.Add(new Point(x, y));
            foreach (var p in parts)
            {
                Fill(map, p);
                if (p.Width > 1 && p.Height > 1) map.Rooms.Add(p);
            }
        }
        return map;
    }

    /// <summary>The block that grows from the cell (sx, sy) in the direction (ux, uy): <paramref name="along"/> long, <paramref name="across"/> wide, centered on the line it grows along.</summary>
    static Rectangle Feature(int sx, int sy, int ux, int uy, int along, int across)
    {
        if (ux != 0) return new Rectangle(ux > 0 ? sx : sx - along + 1, sy - across / 2, along, across);
        return new Rectangle(sx - across / 2, uy > 0 ? sy : sy - along + 1, across, along);
    }

    static void Fill(FlxBspMap map, Rectangle r)
    {
        for (int y = r.Top; y < r.Bottom; y++)
            for (int x = r.Left; x < r.Right; x++) map.Data[y * map.Width + x] = 0;
    }

    /// <summary>A wall (not on the border) with floor on one side only; (dx, dy) points from the wall away from that floor.</summary>
    static bool IsWall(FlxBspMap map, int x, int y, out int dx, out int dy)
    {
        dx = 0; dy = 0;
        if (x <= 0 || x >= map.Width - 1 || y <= 0 || y >= map.Height - 1) return false;
        if (map.Data[y * map.Width + x] == 0) return false;

        int spaces = 0;
        if (map.IsFloor(x + 1, y)) { dx = -1; dy = 0; spaces++; }
        if (map.IsFloor(x - 1, y)) { dx = 1; dy = 0; spaces++; }
        if (map.IsFloor(x, y + 1)) { dx = 0; dy = -1; spaces++; }
        if (map.IsFloor(x, y - 1)) { dx = 0; dy = 1; spaces++; }
        return spaces == 1;
    }

    /// <summary>The area is inside the map (a border of wall left) and all wall.</summary>
    static bool CanAdd(FlxBspMap map, Rectangle r)
    {
        if (r.Left < 1 || r.Top < 1 || r.Right > map.Width - 1 || r.Bottom > map.Height - 1) return false;
        for (int y = r.Top; y < r.Bottom; y++)
            for (int x = r.Left; x < r.Right; x++)
                if (map.Data[y * map.Width + x] == 0) return false;
        return true;
    }
}
