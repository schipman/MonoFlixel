using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoFlixel.Flixel;

namespace MonoFlixel.Demo;

/// <summary>
/// Port of the HaxeFlixel FlxCaveGenerator demo: a cave made with a cellular automaton, auto-tiled, with a
/// little player to run around in it. R regenerates, ESC returns to the launcher.
/// </summary>
public class CaveDemoState : FlxState
{
    const int TileSize = 8;

    FlxTilemap _tilemap = null!;
    CavePlayer _player = null!;
    FlxText _generationTime = null!;
    FlxSlider _smoothing = null!, _wallRatio = null!;

    public override void Create()
    {
        FlxG.Game.Window.Title = "MonoFlixel - FlxCaveGenerator";
        FlxG.Cameras.BgColor = Color.Black;
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = true;

        _tilemap = new FlxTilemap();
        _player = new CavePlayer(0, 0);

        // UI
        const int uiWidth = 200;
        int uiX = FlxG.Width - uiWidth;

        var background = new FlxSprite(uiX, 0).MakeGraphic(uiWidth, 245, Color.White);
        background.Alpha = 0.85f;

        var title = new FlxText(uiX, 2, uiWidth, "FlxCaveGenerator", 16) { Alignment = FlxTextAlign.Center, Tint = new Color(150, 75, 20) };
        title.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black, 1);

        _smoothing = new FlxSlider("Smoothing Iterations", uiX + 20, 62, 150, 0, 15, 6, wholeNumbers: true);
        _wallRatio = new FlxSlider("Wall Ratio", uiX + 20, 132, 150, 0.35f, 0.65f, 0.5f);

        var button = new FlxButton(uiX + 60, 190, "[R]egenerate", GenerateCave);

        _generationTime = new FlxText(uiX, 220, uiWidth) { Tint = Color.Black, Alignment = FlxTextAlign.Center };

        // add everything in drawing order
        Add(_tilemap);
        Add(_player);
        Add(background);
        Add(title);
        Add(_smoothing);
        Add(_wallRatio);
        Add(button);
        Add(_generationTime);

        GenerateCave();
    }

    public override void Update(float elapsed)
    {
        if (FlxG.Keys.JustPressed(Keys.R)) GenerateCave();

        if (FlxG.Keys.JustPressed(Keys.Escape))
        {
            LauncherState.Return();
            return;
        }

        // a little fading effect for the text
        if (_generationTime.Alpha < 1) _generationTime.Alpha = MathF.Min(1, _generationTime.Alpha + 0.05f);

        // Touching is read before Update resets it, so collide after the player moved this frame
        base.Update(elapsed);
        FlxG.Collide(_tilemap, _player);

        // the player can't leave the screen area: wrap around
        if (_player.X + _player.Width < 0) _player.X = FlxG.Width;
        else if (_player.X > FlxG.Width) _player.X = -_player.Width;
        if (_player.Y + _player.Height < 0) _player.Y = FlxG.Height;
        else if (_player.Y > FlxG.Height) _player.Y = -_player.Height;
    }

    void GenerateCave()
    {
        // width and height in tiles needed to fill the screen with 8x8 tiles
        int width = FlxG.Width / TileSize;
        int height = FlxG.Height / TileSize;

        var watch = Stopwatch.StartNew();
        string caveData = FlxCaveGenerator.GenerateCaveString(width, height, (int)_smoothing.Value, _wallRatio.Value);
        watch.Stop();

        _generationTime.Text = $"Generation time: {watch.Elapsed.TotalSeconds:0.0000}s";
        _generationTime.Alpha = 0;

        _tilemap.LoadMapFromCsv(caveData, FlxAssets.Get("cave/caveWallTiles.png"), TileSize, TileSize, FlxAutoTiling.Auto);

        // find an empty tile for the player
        var empty = _tilemap.FindTiles(0);
        if (empty.Count == 0) return;
        var spawn = _tilemap.GetTileCoords(empty[FlxG.Random.Int(0, empty.Count - 1)], midpoint: false);
        _player.Reset(spawn.X, spawn.Y);
    }
}
