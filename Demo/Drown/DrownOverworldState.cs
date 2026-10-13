using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo.Drown;

/// <summary>
/// The sea above. Sail the ship (WASD, arrows or the left stick) across the water between the islands and dive where you like: the
/// place on the map is the dive site, so coming back to the same place gives the same site, as you left it. 1, 2 and 3 choose how
/// deep the site goes (Shallows, Deep, Abyss).
/// </summary>
public class DrownOverworldState : FlxState
{
    const int Tile = 16, Water = 31;

    readonly int? _startX, _startY;
    FlxTilemap _map = null!;
    FlxSprite _ship = null!;
    FlxText _info = null!, _help = null!;
    FlxActionAnalog _move = null!;
    FlxActionDigital _dive = null!;
    int _depth = 1;
    bool _diving;

    public DrownOverworldState() { }
    public DrownOverworldState(int tileX, int tileY) { _startX = tileX; _startY = tileY; }

    public override void Create()
    {
        FlxG.Game.Window.Title = "DROWN - the sea";
        FlxG.Cameras.BgColor = new Color(10, 40, 90);
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = false;

        // the map of islands (60 x 60 tiles of 16 pixels)
        _map = new FlxTilemap().LoadMapFromCsvFile(FlxAssets.Get("drown/islands.csv"), FlxAssets.Get("drown/overworld.png"), Tile, Tile);
        Add(_map);

        var start = new Vector2((_startX ?? 30) * Tile, (_startY ?? 30) * Tile);
        _ship = new FlxSprite(start.X, start.Y).MakeGraphic(14, 14, new Color(230, 70, 60));
        _ship.Moves = false;
        Add(_ship);
        if (!IsWater(_ship.X, _ship.Y, _ship.Width, _ship.Height)) FindWater();

        FlxG.Camera.Bounds = _map.Bounds;
        FlxG.Camera.Follow(_ship, 0.15f);

        _info = new FlxText(8, 6, 0, "") { Tint = Color.White };
        _help = new FlxText(8, FlxG.Height - 12, 0, "WASD / arrows: sail    1 2 3: depth    ENTER: dive    X: forget this site    ESC: back")
        {
            Tint = new Color(190, 220, 240)
        };
        _info.ScrollFactor = _help.ScrollFactor = Vector2.Zero;
        Add(_info);
        Add(_help);

        _move = FlxG.Actions.Activate(new FlxActionSet("sea",
            new FlxActionAnalog("move").AddKeys(Keys.W, Keys.S, Keys.A, Keys.D).AddKeys(Keys.Up, Keys.Down, Keys.Left, Keys.Right).AddGamepad(FlxGamepadInputID.LeftAnalogStick),
            new FlxActionDigital("dive").AddKey(Keys.Enter).AddKey(Keys.Z).AddGamepad(FlxGamepadInputID.A))).Analog("move")!;
        _dive = FlxG.Actions.Get("sea").Digital("dive")!;

        FlxG.Camera.Fade(Color.Black, 0.8f, fadeIn: true);
    }

    bool IsWater(float x, float y, float w, float h)
    {
        foreach (var (px, py) in new[] { (x, y), (x + w, y), (x, y + h), (x + w, y + h) })
            if (_map.GetTileAt(px, py) != Water) return false;
        return true;
    }

    void FindWater()
    {
        // look outwards for the nearest water to put the ship on
        for (int r = 1; r < 40; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    float x = _ship.X + dx * Tile, y = _ship.Y + dy * Tile;
                    if (IsWater(x, y, _ship.Width, _ship.Height)) { _ship.X = x; _ship.Y = y; return; }
                }
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        if (_diving) return;

        // sailing: each axis on its own so the ship slides along coasts
        var v = _move.Value * 110f * elapsed;
        if (v.X != 0 && IsWater(_ship.X + v.X, _ship.Y, _ship.Width, _ship.Height)) _ship.X += v.X;
        if (v.Y != 0 && IsWater(_ship.X, _ship.Y + v.Y, _ship.Width, _ship.Height)) _ship.Y += v.Y;

        if (FlxG.Keys.JustPressed(Keys.D1)) _depth = 1;
        if (FlxG.Keys.JustPressed(Keys.D2)) _depth = 2;
        if (FlxG.Keys.JustPressed(Keys.D3)) _depth = 3;

        int tx = (int)(_ship.Midpoint.X / Tile), ty = (int)(_ship.Midpoint.Y / Tile);
        bool visited = FlxG.Save.Has($"drown.site.{tx}_{ty}_{_depth}");
        _info.Text = $"Position {tx}, {ty}     Dive site: {DiveLevel.DepthNames[_depth]}{(visited ? "  (explored before)" : "")}     Bag: {DrownData.Bag.Weight:0.0} lbs";

        if (FlxG.Keys.JustPressed(Keys.X) && visited)
        {
            DrownData.ForgetSite(tx, ty, _depth);
            DrownData.Sfx("drown_negative");
        }

        if (_dive.Triggered) Dive(tx, ty);
        if (FlxG.Keys.JustPressed(Keys.Escape)) DrownMenuState.Open();
    }

    void Dive(int tx, int ty)
    {
        _diving = true;
        DrownData.Sfx("air_intake", 0.7f);
        FlxG.Camera.Fade(Color.Black, 0.8f, onComplete: () => FlxG.SwitchState(new DrownDiveState(tx, ty, _depth), 640, 480, 1));
    }
}
