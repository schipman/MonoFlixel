using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>
/// Dungeons from random rooms (ported from RogueSharp's RandomRoomsMapCreationStrategy): rooms of random size are dropped at random places and thrown out when they
/// touch a room that is already there; each room is then joined to the one before it by an L shaped corridor between their centers.
/// </summary>
public static class FlxRandomRoomsGenerator
{
    /// <param name="maxRooms">How many rooms are tried (some are thrown out for overlapping).</param>
    /// <param name="roomMin">The smallest room side, walls included.</param>
    /// <param name="roomMax">The largest room side, walls included.</param>
    /// <param name="wantRooms">Stop placing when this many rooms have been made (0: try all of <paramref name="maxRooms"/>).</param>
    /// <param name="seed">The same seed gives the same dungeon (null: a new one each time).</param>
    /// <remarks><see cref="FlxBspMap.Rooms"/> holds the floor of every room, without its wall.</remarks>
    public static FlxBspMap Generate(int width, int height, int maxRooms, int roomMax, int roomMin, int wantRooms = 0, int? seed = null)
    {
        var rnd = seed.HasValue ? new Random(seed.Value) : new Random();
        var map = new FlxBspMap { Width = width, Height = height, Data = new int[width * height] };
        Array.Fill(map.Data, 1);

        var frames = new List<Rectangle>();      // the rooms with their walls
        for (int r = 0; r < maxRooms && (wantRooms <= 0 || frames.Count < wantRooms); r++)
        {
            int w = rnd.Next(roomMin, roomMax + 1), h = rnd.Next(roomMin, roomMax + 1);
            if (w > width || h > height) continue;
            var room = new Rectangle(rnd.Next(0, width - w + 1), rnd.Next(0, height - h + 1), w, h);
            bool overlaps = false;
            foreach (var other in frames)
                if (room.Intersects(other)) { overlaps = true; break; }
            if (!overlaps) frames.Add(room);
        }

        foreach (var f in frames)
        {
            var floor = new Rectangle(f.X + 1, f.Y + 1, f.Width - 2, f.Height - 2);
            map.Rooms.Add(floor);
            for (int y = floor.Top; y < floor.Bottom; y++)
                for (int x = floor.Left; x < floor.Right; x++) map.Data[y * width + x] = 0;
        }

        for (int r = 1; r < map.Rooms.Count; r++)
        {
            var a = FlxBspMap.Center(map.Rooms[r - 1]);
            var b = FlxBspMap.Center(map.Rooms[r]);
            if (rnd.Next(2) == 0) { Horizontal(map, a.X, b.X, a.Y); Vertical(map, a.Y, b.Y, b.X); }
            else { Vertical(map, a.Y, b.Y, a.X); Horizontal(map, a.X, b.X, b.Y); }
        }
        return map;
    }

    static void Horizontal(FlxBspMap map, int x0, int x1, int y)
    {
        for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++) Carve(map, x, y);
    }

    static void Vertical(FlxBspMap map, int y0, int y1, int x)
    {
        for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++) Carve(map, x, y);
    }

    static void Carve(FlxBspMap map, int x, int y)
    {
        if (x > 0 && y > 0 && x < map.Width - 1 && y < map.Height - 1) map.Data[y * map.Width + x] = 0;
    }
}
