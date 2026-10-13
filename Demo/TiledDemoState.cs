using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// Port of the HaxeFlixel TiledEditor demo (PlayState): a level made in the Tiled editor.
/// Collect all the coins, then find the exit. ESC returns to the launcher.
/// </summary>
public class TiledDemoState : FlxState
{
    public TiledLevel Level = null!;

    public FlxText Score = null!;
    public FlxText Status = null!;
    public FlxTypedGroup<FlxSprite> Coins = new();
    public MegaManPlayer Player = null!;
    public FlxObject Floor = null!;
    public FlxSprite Exit = null!;

    static bool _youDied;

    public override void Create()
    {
        FlxG.Mouse.Visible = false;
        FlxG.Cameras.BgColor = FlxColor.Rgb(0xaaaaaa);

        // Load the level's tilemaps, images and objects from assets/tiled/level.tmx
        Coins = new FlxTypedGroup<FlxSprite>();
        Level = new TiledLevel(FlxAssets.Get("tiled/level.tmx"), this);

        // Add backgrounds
        Add(Level.BackgroundLayer);

        // Draw coins first
        Add(Coins);

        // Add static images
        Add(Level.ImagesLayer);

        // Load player objects
        Add(Level.ObjectsLayer);

        // Add foreground tiles after the level objects, so these tiles render on top of the player
        Add(Level.ForegroundTiles);

        // Create UI
        Score = new FlxText(2, 2, 80);
        Score.ScrollFactor = Vector2.Zero;
        Score.SetBorderStyle(FlxTextBorderStyle.Shadow, Color.Black, 1);
        Score.Text = "SCORE: " + (Coins.CountDead() * 100);
        Add(Score);

        Status = new FlxText(FlxG.Width - 160 - 2, 2, 160);
        Status.ScrollFactor = Vector2.Zero;
        Status.SetBorderStyle(FlxTextBorderStyle.Shadow, Color.Black, 1);
        Status.Alignment = FlxTextAlign.Right;
        Status.Text = _youDied ? "Aww, you died!" : "Collect coins.";
        Add(Status);
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);   // MegaManPlayer handles its own input

        FlxG.Overlap(Coins, Player, GetCoin);

        // Collide with the foreground tile layer(s)
        Level.CollideWithLevel(Player);

        FlxG.Overlap(Exit, Player, Win);

        if (FlxG.Overlap(Player, Floor))
        {
            _youDied = true;
            FlxG.ResetState();
        }

        if (FlxG.Keys.JustPressed(Keys.Escape))
        {
            _youDied = false;
            LauncherState.Return();
        }
    }

    void Win(FlxObject exit, FlxObject player)
    {
        Status.Text = "Yay, you won!";
        Score.Text = "SCORE: 5000";
        Player.Kill();
    }

    void GetCoin(FlxObject coin, FlxObject player)
    {
        coin.Kill();
        Score.Text = "SCORE: " + (Coins.CountDead() * 100);
        if (Coins.CountLiving() == 0)
        {
            Status.Text = "Find the exit";
            Exit.Exists = true;
        }
    }
}
