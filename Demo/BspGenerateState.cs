using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// Port of the HaxeFlixel BSPMapGen demo, generate screen: splits the map into leaves, puts a room in each and
/// joins them with hallways. G generates a new map, SPACE plays it, ESC returns to the launcher.
/// </summary>
public class BspGenerateState : FlxState
{
    public const int TileSize = 16;

    /// <summary>The last generated map, row-major: 0 = floor (room or hallway), 1 = wall. Shared with BspPlayState.</summary>
    public static int[]? MapData;
    public static int MapWidth, MapHeight;

    FlxSprite _mapSprite = null!;

    public override void Create()
    {
        FlxG.Game.Window.Title = "MonoFlixel - BSP map generator";
        FlxG.Cameras.BgColor = Color.Black;
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = true;

        MapWidth = FlxG.Width / TileSize;
        MapHeight = FlxG.Height / TileSize;

        // One pixel per map tile, scaled up to fill the screen
        _mapSprite = new FlxSprite(0, 0).MakeGraphic(MapWidth, MapHeight, Color.Black);
        _mapSprite.Scale = new Vector2(TileSize, TileSize);
        _mapSprite.ScreenCenter();
        Add(_mapSprite);

        const int gutter = 10;
        Add(new FlxButton(gutter, gutter, "Generate (G)", GenerateMap));
        Add(new FlxButton(gutter * 2 + 80, gutter, "Play (Space)", Play));

        var hint = new FlxText(0, FlxG.Height - 14, FlxG.Width, "ESC: back to launcher") { Alignment = FlxTextAlign.Center, Tint = Color.Gray };
        Add(hint);

        if (MapData == null || MapData.Length != MapWidth * MapHeight) GenerateMap();
        else UpdateSprite();
    }

    void GenerateMap()
    {
        var map = new int[MapWidth * MapHeight];
        System.Array.Fill(map, 1);

        // The root leaf is the whole map
        var root = new BspLeaf(0, 0, MapWidth, MapHeight);
        var leafs = new List<BspLeaf> { root };

        // Keep splitting until nothing more can be split
        bool didSplit = true;
        while (didSplit)
        {
            didSplit = false;

            // (iterate by index: the list grows while we go)
            for (int i = 0; i < leafs.Count; i++)
            {
                var leaf = leafs[i];
                if (leaf.IsSplit) continue;

                // split if it is too big, or with a 75% chance
                if (leaf.Width > BspLeaf.MaxSize || leaf.Height > BspLeaf.MaxSize || FlxG.Random.Float() > 0.25f)
                {
                    if (leaf.Split())
                    {
                        leafs.Add(leaf.LeftChild!);
                        leafs.Add(leaf.RightChild!);
                        didSplit = true;
                    }
                }
            }
        }

        // make a room in each leaf and hallways between them, then carve them out of the map
        root.CreateRooms();

        foreach (var leaf in leafs)
        {
            if (leaf.Room is { } room) Fill(map, room);
            if (leaf.Hallways != null) foreach (var hall in leaf.Hallways) Fill(map, hall);
        }

        MapData = map;
        UpdateSprite();
    }

    static void Fill(int[] map, Rectangle r)
    {
        for (int y = r.Top; y < r.Bottom; y++)
            for (int x = r.Left; x < r.Right; x++)
                if (x >= 0 && y >= 0 && x < MapWidth && y < MapHeight) map[y * MapWidth + x] = 0;
    }

    void UpdateSprite()
    {
        var pixels = new Color[MapWidth * MapHeight];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = MapData![i] == 0 ? Color.White : Color.Black;
        _mapSprite.Pixels!.SetData(pixels);
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        if (FlxG.Keys.JustPressed(Keys.G)) GenerateMap();
        if (FlxG.Keys.JustPressed(Keys.Space)) Play();
        if (FlxG.Keys.JustPressed(Keys.Escape)) LauncherState.Return();
    }

    static void Play() => FlxG.SwitchState(new BspPlayState());
}
