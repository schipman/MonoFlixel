using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>The Mega Man / tilemap test level. ESC returns to the launcher.</summary>
public class MegaManState : FlxState
{
    const int W = 120, H = 36, T = 8;     // map size in tiles, tile size in pixels (map = 960x288)
    const int Ground = 28;                // first floor row (y = 224)

    static bool _useAlt;                  // T toggles autotiles.png (AUTO) <-> autotiles_alt.png (ALT)

    MegaManPlayer _player = null!;
    FlxTilemap _map = null!, _hazards = null!;
    FlxSpriteGroup _lift = null!;
    readonly FlxTypedGroup<FlxSprite> _coins = new();
    int _collected, _total;
    bool _dying;

    public override void Create()
    {
        FlxG.Camera.BgColor = new Color(24, 24, 40);

        // ---- solid terrain (auto-tiled: any non-zero value = filled)
        var ground = new int[W * H];
        void Fill(int c0, int r0, int c1, int r1)
        {
            for (int r = r0; r <= r1; r++)
                for (int c = c0; c <= c1; c++) ground[r * W + c] = 1;
        }
        Fill(0, Ground, 39, H - 1);       // floor, pit at columns 40-47
        Fill(48, Ground, 87, H - 1);      // floor, big pit at columns 88-103 (crossed by the lift)
        Fill(104, Ground, W - 1, H - 1);
        Fill(0, 0, 1, Ground - 1);        // side walls
        Fill(W - 2, 0, W - 1, Ground - 1);
        Fill(12, 21, 19, 22);             // platform 1
        Fill(26, 16, 33, 17);             // platform 2 (reach it from platform 1)
        Fill(50, 21, 57, 22);             // platform 3
        Fill(58, 25, 61, 27);             // step
        Fill(62, 20, 69, 27);             // block

        string tiles = _useAlt ? "autotiles_alt.png" : "autotiles.png";
        _map = new FlxTilemap().LoadMapFromArray(ground, W, H, FlxAssets.Get(tiles), T, T, _useAlt ? FlxAutoTiling.Alt : FlxAutoTiling.Auto);

        // ---- hazard layer: non-solid tiles with a callback (no auto-tiling, nothing collides)
        var spikes = new int[W * H];
        for (int c = 74; c <= 77; c++) spikes[(Ground - 1) * W + c] = 1;
        _hazards = new FlxTilemap().LoadMapFromArray(spikes, W, H, TilesetFactory.MakeSpikeTile(FlxG.GraphicsDevice, T), T, T,
            FlxAutoTiling.Off, drawIndex: 1, collideIndex: 1000);
        _hazards.FrameOffset = 1;         // tile value 1 draws frame 0 of the single-frame spike texture
        _hazards.SetTileProperties(1, FlxDirection.None, OnSpike, typeof(MegaManPlayer));

        // ---- moving platform: a sprite group that moves as one unit
        _lift = new FlxSpriteGroup(704, Ground * T);
        for (int i = 0; i < 3; i++)
        {
            var part = new FlxSprite(i * 16, 0).MakeGraphic(16, 8, Color.SteelBlue);
            part.Immovable = true;
            _lift.Add(part);
        }
        _lift.Velocity.X = 40;

        // ---- coins (tile coordinates of the cell the coin sits in)
        foreach (var (col, row) in new[]
        {
            (14, 20), (16, 20), (28, 15), (30, 15), (52, 20), (55, 20),
            (64, 19), (66, 19), (96, 25), (108, 27), (112, 27)
        })
        {
            _coins.Add(new FlxSprite(col * T + 1, row * T + 1).MakeGraphic(6, 6, Color.Gold));
        }
        _total = _coins.Members.Count;
        UpdateTitle();

        Add(_map);
        Add(_hazards);
        Add(_lift);
        Add(_coins);
        _player = new MegaManPlayer(4 * T, 180);
        Add(_player);

        _fxText = new FlxText(2, FlxG.Height - 10, FlxG.Width - 4, "", 8) { ScrollFactor = Vector2.Zero };
        _fxText.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black, 1);
        Add(_fxText);

        UpdateTitle();
        FlxG.Camera.Bounds = _map.Bounds;
        FlxG.Camera.Follow(_player, 0.15f);
        FlxG.Camera.Fade(Color.Black, 0.5f, fadeIn: true);
        FlxFlicker.Flicker(_player, 0.8f, 0.06f);
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        FlxG.Collide(_player, _map);
        FlxG.Collide(_player, _lift);
        FlxG.Overlap(_player, _coins, (p, c) => { c.Kill(); _collected++; UpdateTitle(); });
        FlxG.Overlap(_player, _hazards);          // fires the spike callback

        // lift patrols back and forth across the big pit
        if (_lift.X >= 784) { _lift.X = 784; _lift.Velocity.X = -40; }
        else if (_lift.X <= 704) { _lift.X = 704; _lift.Velocity.X = 40; }

        if (_player.Y > _map.Height + 40) Die();

        if (FlxG.Keys.JustPressed(Keys.T))
        {
            _useAlt = !_useAlt;
            FlxG.SwitchState(new MegaManState());
        }

        if (FlxG.Keys.JustPressed(Keys.Escape))
        {
            FlxG.PostProcess.Clear();
            LauncherState.Return();
        }
    }

    void OnSpike(FlxTile tile, FlxObject obj) => Die();

    void Die()
    {
        if (_dying) return;
        _dying = true;
        FlxG.Camera.Shake(0.02f, 0.25f);
        FlxG.Camera.Fade(Color.Black, 0.4f, false, () => FlxG.SwitchState(new MegaManState()));
    }

    FlxText? _fxText;

    void UpdateTitle()
    {
        if (_fxText != null)
            _fxText.Text = "Post-processing effects: the launcher's EFFECTS menu (F)";
        SetTitle();
    }

    void SetTitle() =>
        FlxG.Game.Window.Title =
            $"Mega Man test - coins {_collected}/{_total}   |   T: tileset ({(_useAlt ? "ALT" : "AUTO")})   |   ESC: launcher";
}
