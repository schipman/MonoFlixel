using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>BSPMapGen play screen: the generated map as an auto-tiled tilemap, with a player to walk around it.</summary>
public class BspPlayState : FlxState
{
    FlxTilemap _map = null!;
    BspPlayer _player = null!;

    public override void Create()
    {
        FlxG.Cameras.BgColor = Color.Black;
        FlxG.Mouse.Visible = true;

        const int ts = BspGenerateState.TileSize;
        _map = new FlxTilemap();
        _map.LoadMapFromArray(BspGenerateState.MapData!, BspGenerateState.MapWidth, BspGenerateState.MapHeight,
            FlxAssets.Get("bsp/tiles.png"), ts, ts, FlxAutoTiling.Auto);
        Add(_map);

        // Randomly pick a floor tile (inside a room or hallway) for the player to start on
        var empty = _map.FindTiles(0);
        var pos = _map.GetTileCoords(empty[FlxG.Random.Int(0, empty.Count - 1)], midpoint: false);
        _player = new BspPlayer(pos.X, pos.Y);
        Add(_player);

        Add(new FlxButton(10, 10, "Back (Space)", Back));
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);
        FlxG.Collide(_player, _map);

        if (FlxG.Keys.JustPressed(Keys.Space) || FlxG.Keys.JustPressed(Keys.Escape)) Back();
    }

    static void Back() => FlxG.SwitchState(new BspGenerateState());
}

/// <summary>Top-down player (16x16 sprite, 12x12 hitbox so it fits narrow hallways easily).</summary>
public class BspPlayer : FlxSprite
{
    const float Speed = 100f;

    public BspPlayer(float x, float y) : base(x, y, FlxAssets.Get("bsp/player.png"))
    {
        Width = 12;
        Height = 12;
        CenterOffsets();
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        if (FlxG.Keys.AnyPressed(Keys.A, Keys.Left)) Velocity.X = -Speed;
        else if (FlxG.Keys.AnyPressed(Keys.D, Keys.Right)) Velocity.X = Speed;
        else Velocity.X = 0;

        if (FlxG.Keys.AnyPressed(Keys.W, Keys.Up)) Velocity.Y = -Speed;
        else if (FlxG.Keys.AnyPressed(Keys.S, Keys.Down)) Velocity.Y = Speed;
        else Velocity.Y = 0;
    }
}
