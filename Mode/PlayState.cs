using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Mode;

/// <summary>The actual gameplay: a 640x640 level of random rooms, spawners, bots and a player.</summary>
public class PlayState : FlxState
{
    public const int TileSize = 8;
    public const int MapWidthInTiles = 80;
    public const int MapHeightInTiles = 80;

    int[] _map = null!;
    FlxTilemap _tileMap = null!;

    // Major game object storage
    FlxTypedGroup<Bullet> _bullets = null!;
    Player _player = null!;
    FlxTypedGroup<Enemy> _enemies = null!;
    FlxTypedGroup<Spawner> _spawners = null!;
    FlxTypedGroup<EnemyBullet> _enemyBullets = null!;
    FlxEmitter _littleGibs = null!;
    FlxEmitter _bigGibs = null!;
    FlxSpriteGroup _hud = null!;
    FlxSpriteGroup _gunjam = null!;

    // Meta groups, to help speed up collisions
    FlxGroup _objects = null!;
    FlxGroup _hazards = null!;

    // HUD / user interface
    FlxText _score = null!;
    FlxText? _score2;
    float _scoreTimer;
    float _jamTimer;

    // Just to prevent weirdness during level transition
    bool _fading;

    public override void Create()
    {
        FlxG.Mouse.Visible = false;

        // A pool of 100 little metal bits that can be exploded. We recycle these.
        _littleGibs = new FlxEmitter();
        _littleGibs.Velocity.Set(-150, -200, 150, 0);
        _littleGibs.AngularVelocity.Set(-720);
        _littleGibs.Acceleration.Set(0, 350);
        _littleGibs.Elasticity.Set(0.5f);
        _littleGibs.LoadParticles(ModeAssets.Image("gibs"), 100, 10, true);

        // A smaller pool of larger metal bits
        _bigGibs = new FlxEmitter();
        _bigGibs.Velocity.Set(-200, -300, 200, 0);
        _bigGibs.AngularVelocity.Set(-720, -720);
        _bigGibs.Acceleration.Set(0, 350);
        _bigGibs.Elasticity.Set(0.35f);
        _bigGibs.LoadParticles(ModeAssets.Image("spawner_gibs"), 50, 20, true);

        // The rest of our object groups or pools
        _enemies = new FlxTypedGroup<Enemy>(50);
        _spawners = new FlxTypedGroup<Spawner>();
        _hud = new FlxSpriteGroup();
        _enemyBullets = new FlxTypedGroup<EnemyBullet>(100);
        _bullets = new FlxTypedGroup<Bullet>(20);

        _player = new Player(316, 300, _bullets, _littleGibs);

        // Creates all level geometry (640x640) and the spawners + spawner cameras
        GenerateLevel();

        // Bots and spawners are added after the blocks so they draw on top of the level
        Add(_spawners);
        Add(_littleGibs);
        Add(_bigGibs);
        Add(_enemies);

        // Then the player and the scrolling camera
        Add(_player);

        FlxG.Camera.Bounds = new Rectangle(0, 0, 640, 640);
        FlxG.Camera.Follow(_player, FlxCameraFollowStyle.Platformer);

        // Bullets are drawn on top of pretty much everything
        Add(_enemyBullets);
        Add(_bullets);
        Add(_hud);

        // Helper groups for collisions (not added to the state)
        _hazards = new FlxGroup();
        _hazards.Add(_enemyBullets);
        _hazards.Add(_spawners);
        _hazards.Add(_enemies);
        _objects = new FlxGroup();
        _objects.Add(_enemyBullets);
        _objects.Add(_bullets);
        _objects.Add(_enemies);
        _objects.Add(_player);
        _objects.Add(_littleGibs);
        _objects.Add(_bigGibs);

        // HUD: the current score
        _score = new FlxText(FlxG.Width / 4f, 0, MathF.Floor(FlxG.Width / 2f));
        _score.SetFormat(16, FlxColor.Rgb(0xd8eba2), FlxTextAlign.Center);
        _score.SetBorderStyle(FlxTextBorderStyle.Outline, FlxColor.Rgb(0x131c1b), 2);
        _hud.Add(_score);

        if (Reg.Scores.Count < 2)
        {
            Reg.Scores.Add(0);
            Reg.Scores.Add(0);
        }

        // The player's highest and last scores
        if (Reg.Score > Reg.Scores[0]) Reg.Scores[0] = Reg.Score;

        if (Reg.Scores[0] != 0)
        {
            _score2 = new FlxText(FlxG.Width / 2f, 0, MathF.Floor(FlxG.Width / 2f));
            _score2.SetFormat(8, FlxColor.Rgb(0xd8eba2), FlxTextAlign.Right);
            _score2.SetBorderStyle(_score.BorderStyle, _score.BorderColor, _score.BorderSize);
            _hud.Add(_score2);
            _score2.Text = "HIGHEST: " + Reg.Scores[0] + "\nLAST: " + Reg.Score;
        }

        Reg.Score = 0;
        _scoreTimer = 0;

        // The "gun jammed" notification
        _gunjam = new FlxSpriteGroup();
        _gunjam.Add(new FlxSprite(0, FlxG.Height - 22).MakeGraphic(FlxG.Width, 24, FlxColor.Rgb(0x131c1b)));
        _gunjam.Add(new FlxText(0, FlxG.Height - 22, FlxG.Width, "GUN IS JAMMED").SetFormat(16, FlxColor.Rgb(0xd8eba2), FlxTextAlign.Center));
        _gunjam.Visible = false;
        _hud.Add(_gunjam);

        // The HUD must not scroll with the camera, and only shows on the main camera
        var mainCamera = FlxG.Camera;
        _hud.ForEach(s => PinToScreen(s, mainCamera));

        FlxG.Sound.PlayMusic(ModeAssets.Sound("mode"));

        FlxG.Cameras.Flash(FlxColor.Rgb(0x131c1b));
        _fading = false;
    }

    static void PinToScreen(FlxSprite s, FlxCamera camera)
    {
        s.ScrollFactor = Vector2.Zero;
        s.Cameras = new[] { camera };
        if (s is IFlxGroup g)
            foreach (var m in g.Basics)
                if (m is FlxSprite child) PinToScreen(child, camera);
    }

    public override void Update(float elapsed)
    {
        // Save off the current score and update the game state
        int oldScore = Reg.Score;

        base.Update(elapsed);

        // Collisions with environment
        FlxG.Collide(_tileMap, _objects);
        FlxG.Overlap(_hazards, _player, Overlapped);
        FlxG.Overlap(_bullets, _hazards, Overlapped);

        // Did the player score points this frame?
        bool scoreChanged = oldScore != Reg.Score;

        // Jammed message
        if (FlxG.Keys.JustPressed(Keys.C) && _player.Flickering)
        {
            _jamTimer = 1;
            _gunjam.Visible = true;
        }

        if (_jamTimer > 0)
        {
            if (!_player.Flickering) _jamTimer = 0;

            _jamTimer -= elapsed;

            if (_jamTimer < 0) _gunjam.Visible = false;
        }

        if (!_fading)
        {
            // Score + countdown stuffs
            if (scoreChanged) _scoreTimer = 2;

            _scoreTimer -= elapsed;

            if (_scoreTimer < 0)
            {
                if (Reg.Score > 0)
                {
                    if (Reg.Score > 100)
                    {
                        Reg.Score -= 100;
                    }
                    else
                    {
                        Reg.Score = 0;
                        _player.Kill();
                    }

                    _scoreTimer = 1;
                    scoreChanged = true;

                    // Play loud beeps if your score is low
                    float volume = 0.35f;
                    if (Reg.Score < 600) volume = 1.0f;

                    FlxG.Sound.Play(ModeAssets.Sound("countdown"), volume);
                }
            }

            // Fade out to the victory screen
            if (_spawners.CountLiving() <= 0)
            {
                _fading = true;
                FlxG.Cameras.Fade(FlxColor.Rgb(0xd8eba2), 3, false, OnVictory);
            }
        }

        if (_score.Size > 16) _score.Size--;

        // Actually update the score text if it changed
        if (scoreChanged)
        {
            if (!_player.Alive) Reg.Score = 0;

            _score.Size = 24;
            _score.Text = Reg.Score.ToString();
        }

        // Escape to the Mode menu
        if (FlxG.Keys.JustPressed(Keys.Escape))
        {
            FlxG.SwitchState(new MenuState());
        }
    }

    /// <summary>Overlap callback from FlxG.Overlap.</summary>
    void Overlapped(FlxObject attacker, FlxObject victim)
    {
        if (attacker is Bullet || attacker is EnemyBullet) attacker.Kill();

        if (victim is IHurt hurtable) hurtable.Hurt(1);
        else victim.Kill();
    }

    void OnVictory()
    {
        // Reset the sounds for going in between the menu etc
        FlxG.Sound.Destroy(true);
        FlxG.SwitchState(new VictoryState());
    }

    // ================================================================ level generation

    void GenerateLevel()
    {
        int r = 160;

        _map = new int[MapHeightInTiles * MapWidthInTiles];

        // The walls, ceiling and floors
        FillRect(0, 0, 640, 16, 1, 6);
        FillRect(0, 16, 16, 640 - 16, 1, 6);
        FillRect(640 - 16, 16, 16, 640 - 16, 1, 6);
        FillRect(16, 640 - 24, 640 - 32, 8, 16, 17);
        FillRect(16, 640 - 16, 640 - 32, 16, 32, 47);

        // Split the world into a 4x4 grid and generate some blocks in each area.
        // Some grid spaces also get a spawner.
        BuildRoom(r * 0, r * 0, true);
        BuildRoom(r * 1, r * 0);
        BuildRoom(r * 2, r * 0);
        BuildRoom(r * 3, r * 0, true);
        BuildRoom(r * 0, r * 1, true);
        BuildRoom(r * 1, r * 1);
        BuildRoom(r * 2, r * 1);
        BuildRoom(r * 3, r * 1, true);
        BuildRoom(r * 0, r * 2);
        BuildRoom(r * 1, r * 2);
        BuildRoom(r * 2, r * 2);
        BuildRoom(r * 3, r * 2);
        BuildRoom(r * 0, r * 3, true);
        BuildRoom(r * 1, r * 3);
        BuildRoom(r * 2, r * 3);
        BuildRoom(r * 3, r * 3, true);

        _tileMap = new FlxTilemap();
        _tileMap.LoadMapFromArray(_map, MapWidthInTiles, MapHeightInTiles, ModeAssets.Image("img_tiles"), 8, 8);
        Add(_tileMap);
    }

    /// <summary>Plops down a spawner and some blocks: haphazard but functional.</summary>
    void BuildRoom(int rx, int ry, bool spawners = false)
    {
        // First place the spawn point (if necessary)
        int rw = 20;
        int sx = 0;
        int sy = 0;

        if (spawners)
        {
            sx = FlxG.Random.Int(2, rw - 6);
            sy = FlxG.Random.Int(2, rw - 6);
        }

        // Then place a bunch of blocks
        int numBlocks = FlxG.Random.Int(3, 6);
        int maxW = 10;
        int minW = 2;
        int maxH = 8;
        int minH = 1;

        if (!spawners) numBlocks++;

        for (int i = 0; i < numBlocks; i++)
        {
            int bx, by, bw, bh;
            bool check;
            do
            {
                // Keep generating different specs if they overlap the spawner
                bw = FlxG.Random.Int(minW, maxW);
                bh = FlxG.Random.Int(minH, maxH);
                bx = FlxG.Random.Int(-1, rw - bw);
                by = FlxG.Random.Int(-1, rw - bh);

                if (spawners)
                    check = (sx > bx + bw) || (sx + 3 < bx) || (sy > by + bh) || (sy + 3 < by);
                else
                    check = true;
            }
            while (!check);

            FillRect(rx + bx * 8, ry + by * 8, bw * 8, bh * 8, 1, 6);

            // If the block has room, add some "dirt" graphics for variety
            if (bw >= 4 && bh >= 5)
            {
                FillRect(rx + bx * 8 + 8, ry + by * 8, bw * 8 - 16, 8, 16, 17);
                FillRect(rx + bx * 8 + 8, ry + by * 8 + 8, bw * 8 - 16, bh * 8 - 24, 32, 47);
            }
        }

        if (spawners)
        {
            // Finally actually add the spawner
            var sp = new Spawner(rx + sx * 8, ry + sy * 8, _bigGibs, _enemies, _enemyBullets, _littleGibs, _player);
            _spawners.Add(sp);
            int n = _spawners.Length - 1;

            // A little frame in the HUD, with a dedicated camera watching the spawner inside it
            _hud.Add(new FlxSprite(3 + n * 16, 3, ModeAssets.Image("miniframe")));

            var camera = new FlxCamera(5 + n * 16, 5, 12, 12, 0.5f);
            camera.Follow(sp, FlxCameraFollowStyle.NoDeadZone);
            FlxG.Cameras.Add(camera);
        }
    }

    void FillRect(int x, int y, int width, int height, int startTile, int endTile)
    {
        int numCols = width / TileSize;
        int numRows = height / TileSize;
        int xStart = x / TileSize;
        int yStart = y / TileSize;

        for (int i = 0; i < numRows; i++)
        {
            for (int j = 0; j < numCols; j++)
            {
                int randomTile = FlxG.Random.Int(startTile, endTile);
                int index = (xStart + j) + (yStart + i) * MapWidthInTiles;
                if (index >= 0 && index < _map.Length) _map[index] = randomTile;
            }
        }
    }
}
