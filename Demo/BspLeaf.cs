using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel.Demo;

/// <summary>
/// One node of the binary space partition from the HaxeFlixel BSPMapGen demo. A leaf is split in two until
/// the pieces are small, every unsplit leaf gets a room, and sibling leaves are joined by an L-shaped hallway.
/// All sizes are in tiles.
/// </summary>
public class BspLeaf
{
    public const int MinSize = 6;
    public const int MaxSize = 24;

    public int X, Y, Width, Height;
    public BspLeaf? LeftChild, RightChild;
    public Rectangle? Room;
    public List<Rectangle>? Hallways;

    public BspLeaf(int x, int y, int width, int height)
    {
        X = x; Y = y; Width = width; Height = height;
    }

    public bool IsSplit => LeftChild != null || RightChild != null;

    /// <summary>Splits this leaf into two children. Returns false if it is already split or too small.</summary>
    public bool Split()
    {
        if (IsSplit) return false;

        // Split direction: across the longer side, or random for a roughly square leaf
        bool splitH = FlxG.Random.Float() > 0.5f;
        if (Width > Height && Height / (float)Width >= 0.05f) splitH = false;
        else if (Height > Width && Width / (float)Height >= 0.05f) splitH = true;

        int max = (splitH ? Height : Width) - MinSize;
        if (max <= MinSize) return false;   // too small to split any more

        int split = (int)FlxG.Random.Float(MinSize, max);

        if (splitH)
        {
            LeftChild = new BspLeaf(X, Y, Width, split);
            RightChild = new BspLeaf(X, Y + split, Width, Height - split);
        }
        else
        {
            LeftChild = new BspLeaf(X, Y, split, Height);
            RightChild = new BspLeaf(X + split, Y, Width - split, Height);
        }
        return true;
    }

    /// <summary>This leaf's room, or a random room from one of its children (used to pick the hallway ends).</summary>
    public Rectangle? GetRoom()
    {
        if (Room != null) return Room;

        var l = LeftChild?.GetRoom();
        var r = RightChild?.GetRoom();
        if (l == null && r == null) return null;
        if (r == null) return l;
        if (l == null) return r;
        return FlxG.Random.Float() > 0.5f ? l : r;
    }

    /// <summary>Makes rooms in every leaf below this one and hallways between sibling leaves.</summary>
    public void CreateRooms()
    {
        if (IsSplit)
        {
            LeftChild?.CreateRooms();
            RightChild?.CreateRooms();

            if (LeftChild != null && RightChild != null)
            {
                var a = LeftChild.GetRoom();
                var b = RightChild.GetRoom();
                if (a != null && b != null) CreateHall(a.Value, b.Value);
            }
            return;
        }

        // A room is between 3x3 tiles and the leaf size - 2, and never touches the leaf's edge (rooms would merge)
        float sizeX = FlxG.Random.Float(3, Width - 2), sizeY = FlxG.Random.Float(3, Height - 2);
        float posX = FlxG.Random.Float(1, Width - sizeX - 1), posY = FlxG.Random.Float(1, Height - sizeY - 1);

        Room = new Rectangle((int)MathF.Floor(X + posX), (int)MathF.Floor(Y + posY), (int)MathF.Floor(sizeX), (int)MathF.Floor(sizeY));
    }

    /// <summary>Joins two rooms with one or two one-tile-wide hallway rectangles.</summary>
    public void CreateHall(Rectangle left, Rectangle right)
    {
        Hallways = new List<Rectangle>();

        float p1x = FlxG.Random.Float(left.Left + 1, left.Right - 2), p1y = FlxG.Random.Float(left.Top + 1, left.Bottom - 2);
        float p2x = FlxG.Random.Float(right.Left + 1, right.Right - 2), p2y = FlxG.Random.Float(right.Top + 1, right.Bottom - 2);

        float w = p2x - p1x;
        float h = p2y - p1y;

        static int C(float v) => (int)MathF.Ceiling(v);
        Rectangle Hall(float x, float y, float width, float height) => new(C(x), C(y), C(width), C(height));
        float aw = MathF.Abs(w), ah = MathF.Abs(h);
        bool coin = FlxG.Random.Float() > 0.5f;

        if (w < 0)
        {
            if (h < 0)
            {
                if (coin) { Hallways.Add(Hall(p2x, p1y, aw, 1)); Hallways.Add(Hall(p2x, p2y, 1, ah)); }
                else { Hallways.Add(Hall(p2x, p2y, aw, 1)); Hallways.Add(Hall(p1x, p2y, 1, ah)); }
            }
            else if (h > 0)
            {
                if (coin) { Hallways.Add(Hall(p2x, p1y, aw, 1)); Hallways.Add(Hall(p2x, p1y, 1, ah)); }
                else { Hallways.Add(Hall(p2x, p2y, aw, 1)); Hallways.Add(Hall(p1x, p1y, 1, ah)); }
            }
            else Hallways.Add(Hall(p2x, p2y, aw, 1));
        }
        else if (w > 0)
        {
            if (h < 0)
            {
                if (coin) { Hallways.Add(Hall(p1x, p2y, aw, 1)); Hallways.Add(Hall(p1x, p2y, 1, ah)); }
                else { Hallways.Add(Hall(p1x, p1y, aw, 1)); Hallways.Add(Hall(p2x, p2y, 1, ah)); }
            }
            else if (h > 0)
            {
                if (coin) { Hallways.Add(Hall(p1x, p1y, aw, 1)); Hallways.Add(Hall(p2x, p1y, 1, ah)); }
                else { Hallways.Add(Hall(p1x, p2y, aw, 1)); Hallways.Add(Hall(p1x, p1y, 1, ah)); }
            }
            else Hallways.Add(Hall(p1x, p1y, aw, 1));
        }
        else
        {
            if (h < 0) Hallways.Add(Hall(p2x, p2y, 1, ah));
            else if (h > 0) Hallways.Add(Hall(p1x, p1y, 1, ah));
        }
    }
}
