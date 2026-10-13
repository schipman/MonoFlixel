using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>How the cave is made.</summary>
public enum CaveGenerator
{
    /// <summary>FlxCaveGenerator: cellular automaton (random fill, smoothed with the 4-5 rule).</summary>
    Cellular,
    PerlinBlobs, SimplexBlobs,
    /// <summary>Winding tunnels along the lines where the noise is zero.</summary>
    PerlinTunnels, SimplexTunnels,
    /// <summary>A few sine waves added up: regular, flowing shapes.</summary>
    SineWaves,
    /// <summary>Fractal Brownian motion: six layers of simplex noise, detail at every scale.</summary>
    FbmCaverns,
    /// <summary>Fractal Brownian motion with domain warping: the layers bend themselves into swirls and folds.</summary>
    WarpedFbm,
    /// <summary>A drunkard's walk: one walker stumbling about, digging a connected cave as it goes.</summary>
    RandomWalkCaverns,
    /// <summary>The same walker, holding its heading: long winding tunnels.</summary>
    RandomWalkTunnels
}

/// <summary>
/// A huge cave drawn with rock_autotile.png (30 pixel tiles), five parallax layers behind it (FlxBackdrop) and the spaceman to
/// explore it: run, jump, hold jump for a jetpack. The cave comes from FlxCaveGenerator (cellular automaton) or from FlxNoise
/// (Perlin, Simplex or sine noise, as caverns or tunnels), at Large (400 x 140 tiles) or Huge (800 x 280 tiles = 24000 x 8400 pixels)
/// size; fireflies drift about on FlxNoise.Wander (or on a random walk).
/// G changes the generator, L the size, R builds a new cave, F the firefly motion, M toggles the map.
/// </summary>
public class BigCaveDemoState : FlxState
{
    static string GeneratorName(CaveGenerator g) => g switch
    {
        CaveGenerator.Cellular => "Cellular automaton (FlxCaveGenerator)",
        CaveGenerator.PerlinBlobs => "Perlin noise caverns",
        CaveGenerator.SimplexBlobs => "Simplex noise caverns",
        CaveGenerator.PerlinTunnels => "Perlin noise tunnels",
        CaveGenerator.SimplexTunnels => "Simplex noise tunnels",
        CaveGenerator.SineWaves => "Sine wave noise",
        CaveGenerator.FbmCaverns => "Fractal Brownian motion",
        CaveGenerator.WarpedFbm => "Domain warped fractal Brownian motion",
        CaveGenerator.RandomWalkCaverns => "Random walk (drunkard's) caverns",
        _ => "Random walk tunnels"
    };

    const int Tile = 30;

    // the choices stay when the cave is rebuilt
    static CaveGenerator _generator = CaveGenerator.PerlinBlobs;
    static bool _huge, _walkFlies;


    int _cols, _rows;
    FlxTilemap _map = null!;
    BigSpaceman _player = null!;
    FlxSprite _mini = null!, _miniDot = null!;
    FlxText _info = null!;
    int _openTiles, _miniScale;
    double _generateMs;

    // fireflies: sprites that drift around a home spot by following FlxNoise.Wander
    readonly FlxNoise _flyNoise = new();
    readonly List<(FlxSprite Sprite, Vector2 Home)> _flies = new();
    float _time;

    public override void Create()
    {
        FlxG.Game.Window.Title = "MonoFlixel - big cave";
        FlxG.Cameras.BgColor = new Color(8, 6, 20);
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = false;

        _cols = _huge ? 800 : 400;
        _rows = _huge ? 280 : 140;
        float worldH = _rows * Tile;

        // ---- the cave: generate, close the border, keep only the biggest connected cavern
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var (data, cavern) = MakeCave();
        _generateMs = watch.Elapsed.TotalMilliseconds;
        _openTiles = cavern.Count;

        // ---- parallax background, back to front. ScrollFactor says how much of the camera's movement a layer follows.
        BuildBackground(worldH);

        // ---- the rock
        _map = new FlxTilemap().LoadMapFromArray(data, _cols, _rows, FlxAssets.Get("rock_autotile.png"), Tile, Tile, FlxAutoTiling.Auto);
        Add(_map);

        BuildFireflies(cavern);

        // ---- the spaceman starts on the floor somewhere in the cavern
        int spawn = PickSpawn(data, cavern);
        _player = new BigSpaceman((spawn % _cols) * Tile + 6, (spawn / _cols + 1) * Tile - 22);
        Add(_player.Flame);
        Add(_player);

        FlxG.Camera.Bounds = _map.Bounds;
        FlxG.Camera.Follow(_player, 0.12f);

        BuildHud(data);
    }

    // ------------------------------------------------------------------ cave helpers

    /// <summary>
    /// Makes caves with the chosen generator until one has a big connected cavern, closes the border and fills every sealed pocket
    /// with rock. Returns the map (1 = rock, 0 = open) and the cells of the cavern.
    /// </summary>
    (int[] Data, List<int> Cavern) MakeCave()
    {
        // how much of the map must be one connected cavern (tunnel networks are thinner)
        bool tunnels = _generator is CaveGenerator.PerlinTunnels or CaveGenerator.SimplexTunnels or CaveGenerator.RandomWalkTunnels;
        int needed = (int)(_cols * _rows * (tunnels ? 0.10 : 0.22));

        int[] data = Array.Empty<int>();
        List<int> cavern = new();

        for (int attempt = 0; attempt < 10; attempt++)
        {
            data = Generate();
            for (int x = 0; x < _cols; x++) { data[x] = 1; data[(_rows - 1) * _cols + x] = 1; }
            for (int y = 0; y < _rows; y++) { data[y * _cols] = 1; data[y * _cols + _cols - 1] = 1; }
            cavern = LargestOpenArea(data, _cols, _rows);
            if (cavern.Count >= needed) break;
        }

        var keep = new bool[data.Length];
        foreach (int i in cavern) keep[i] = true;
        for (int i = 0; i < data.Length; i++) if (!keep[i]) data[i] = 1;
        return (data, cavern);
    }

    int[] Generate()
    {
        // a new noise (a new random seed) for every try; the scales are in tiles, so a Huge cave simply holds more of the same
        var noise = new FlxNoise();
        switch (_generator)
        {
            case CaveGenerator.PerlinBlobs:
                return noise.GenerateCave(_cols, _rows, FlxNoiseType.Perlin, 0.045f, 4, 0.42f);
            case CaveGenerator.SimplexBlobs:
                return noise.GenerateCave(_cols, _rows, FlxNoiseType.Simplex, 0.045f, 4, 0.42f);
            case CaveGenerator.PerlinTunnels:
                return noise.GenerateCave(_cols, _rows, FlxNoiseType.Perlin, 0.035f, 3, 0.8f, FlxNoiseCaveStyle.Tunnels, widen: 1);
            case CaveGenerator.SimplexTunnels:
                return noise.GenerateCave(_cols, _rows, FlxNoiseType.Simplex, 0.035f, 3, 0.8f, FlxNoiseCaveStyle.Tunnels, widen: 1);
            case CaveGenerator.SineWaves:
                return noise.GenerateCave(_cols, _rows, FlxNoiseType.Sine, 0.09f, 4, 0.45f);
            case CaveGenerator.FbmCaverns:
                return noise.GenerateCave(_cols, _rows, FlxNoiseType.Fbm, 0.03f, 6, 0.42f);
            case CaveGenerator.WarpedFbm:
                noise.FbmWarp = 1.2f;                      // bend the noise by itself
                return noise.GenerateCave(_cols, _rows, FlxNoiseType.Fbm, 0.03f, 6, 0.42f);
            case CaveGenerator.RandomWalkCaverns:
                return noise.GenerateWalkCave(_cols, _rows, 0.42f, 0.8f, 2, 100);
            case CaveGenerator.RandomWalkTunnels:
                return noise.GenerateWalkCave(_cols, _rows, 0.22f, 0.95f, 1, 250);
            default:
                return FlxCaveGenerator.GenerateCaveArray(_cols, _rows, 6, 0.48f);
        }
    }

    static List<int> LargestOpenArea(int[] data, int cols, int rows)
    {
        var seen = new bool[data.Length];
        var best = new List<int>();
        var stack = new Stack<int>();

        for (int start = 0; start < data.Length; start++)
        {
            if (data[start] != 0 || seen[start]) continue;

            var area = new List<int>();
            stack.Push(start);
            seen[start] = true;
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                area.Add(c);
                int x = c % cols, y = c / cols;
                if (x > 0 && data[c - 1] == 0 && !seen[c - 1]) { seen[c - 1] = true; stack.Push(c - 1); }
                if (x < cols - 1 && data[c + 1] == 0 && !seen[c + 1]) { seen[c + 1] = true; stack.Push(c + 1); }
                if (y > 0 && data[c - cols] == 0 && !seen[c - cols]) { seen[c - cols] = true; stack.Push(c - cols); }
                if (y < rows - 1 && data[c + cols] == 0 && !seen[c + cols]) { seen[c + cols] = true; stack.Push(c + cols); }
            }
            if (area.Count > best.Count) best = area;
        }
        return best;
    }

    /// <summary>A random open cell with rock under it and room above (a floor to stand on).</summary>
    int PickSpawn(int[] data, List<int> cavern)
    {
        for (int tries = 0; tries < 5000; tries++)
        {
            int c = cavern[FlxG.Random.Int(0, cavern.Count - 1)];
            if (c / _cols < 3 || c / _cols > _rows - 3) continue;
            if (data[c + _cols] != 0 && data[c - _cols] == 0 && data[c - 2 * _cols] == 0) return c;
        }
        return cavern[0];
    }

    // ------------------------------------------------------------------ fireflies

    void BuildFireflies(List<int> cavern)
    {
        int count = _huge ? 260 : 130;
        for (int i = 0; i < count; i++)
        {
            int cell = cavern[FlxG.Random.Int(0, cavern.Count - 1)];
            var home = new Vector2((cell % _cols) * Tile + Tile / 2f, (cell / _cols) * Tile + Tile / 2f);

            var fly = new FlxSprite().LoadGraphic(FlxAssets.Get("atlas/glow.png"));
            fly.Tint = i % 3 == 0 ? new Color(120, 255, 200) : new Color(255, 230, 110);
            fly.Blend = FlxBlend.Add;
            fly.Scale = new Vector2(0.28f, 0.28f);
            fly.UpdateHitbox();
            Add(fly);
            _flies.Add((fly, home));
        }
    }

    void UpdateFireflies()
    {
        for (int i = 0; i < _flies.Count; i++)
        {
            var (fly, home) = _flies[i];

            // Wander: a smooth random offset that never repeats; every firefly gets its own path (id).
            // WalkWander drifts like a random walk instead: it can linger far to one side. (F switches.)
            // Smooth with sine noise makes each one pulse at its own pace.
            var offset = _walkFlies ? _flyNoise.WalkWander(_time, 0.5f, 60f, i) : _flyNoise.Wander(_time, 0.3f, 34f, i);
            fly.X = home.X + offset.X - fly.Width / 2f;
            fly.Y = home.Y + offset.Y - fly.Height / 2f;
            fly.Alpha = Math.Clamp(0.55f + _flyNoise.Smooth(_time, 0.9f, 0.45f, i, FlxNoiseType.Sine), 0.1f, 1f);
        }
    }

    // ------------------------------------------------------------------ parallax background

    void BuildBackground(float worldH)
    {
        // a sky-like gradient that never moves
        var sky = FlxGradient.CreateSprite(FlxG.Width, FlxG.Height, new[] { new Color(8, 6, 26), new Color(34, 16, 58), new Color(14, 28, 52) });
        sky.ScrollFactor = Vector2.Zero;
        Add(sky);

        // drifting spores (additive, so they glow), almost fixed to the screen
        var spores = new FlxBackdrop(Spores(256, 256)) { ScrollFactor = new Vector2(0.03f, 0.03f), AutoScroll = new Vector2(-4, 2), Blend = FlxBlend.Add };
        Add(spores);

        // ridges: the further away, the slower they move and the lighter they are
        float half = (worldH - FlxG.Height) / 2f;
        Add(Ridge(512, 130, 70, smooth: true, new Color(96, 52, 150), new Color(190, 130, 230), new Vector2(0.10f, 0.03f), screenBase: 140, half));
        Add(Ridge(640, 120, 80, smooth: false, new Color(48, 70, 150), new Color(120, 170, 235), new Vector2(0.20f, 0.05f), screenBase: 175, half));
        Add(Ridge(768, 140, 100, smooth: false, new Color(22, 40, 96), new Color(80, 130, 200), new Vector2(0.36f, 0.08f), screenBase: 215, half));
    }

    /// <summary>A repeating ridge: a silhouette that fills everything below its top line.</summary>
    static FlxBackdrop Ridge(int width, int baseline, int amplitude, bool smooth, Color fill, Color edge, Vector2 factor, float screenBase, float halfRange)
    {
        // tall enough to still cover the screen when the layer has moved all the way up or down
        int height = baseline + (int)(FlxG.Height + halfRange * factor.Y + 60);
        var tex = RidgeTexture(width, height, baseline, amplitude, smooth, fill, edge, FlxG.Random.Int(1, 99999));

        return new FlxBackdrop(tex, repeatX: true, repeatY: false)
        {
            ScrollFactor = factor,
            // at the middle of the cave the ridge's top line sits at screenBase on the screen
            Y = screenBase - baseline + halfRange * factor.Y
        };
    }

    // The textures are made of sine waves with whole numbers of periods across the width, so they repeat without a seam.
    static Texture2D RidgeTexture(int w, int h, int baseline, int amplitude, bool smooth, Color fill, Color edge, int seed)
    {
        var rnd = new Random(seed);
        int[] k = { 1, 2, 3, 5, 8, 13 };
        var phase = new float[k.Length];
        for (int i = 0; i < k.Length; i++) phase[i] = (float)(rnd.NextDouble() * Math.PI * 2);

        var data = new Color[w * h];
        for (int x = 0; x < w; x++)
        {
            float v = 0, total = 0;
            for (int i = 0; i < k.Length; i++)
            {
                float weight = 1f / (i + 1);
                float s = MathF.Sin(MathF.PI * 2 * k[i] * x / w + phase[i]);
                v += weight * (smooth ? s : 1f - 2f * MathF.Abs(s));        // sharp peaks like stalagmites for the near layers
                total += weight;
            }
            int top = baseline - (int)(amplitude * 0.5f * (v / total + 1f));
            top = Math.Clamp(top, 0, h - 1);

            for (int y = top; y < h; y++)
            {
                // solid at the rim, fading into mist further down, so the layers behind stay visible
                float fade = MathF.Max(0f, 1f - (y - top) / 230f);
                var c = new Color(fill.R, fill.G, fill.B, (int)(255 * fade * fade));
                if (y - top < 2) c = edge;                                         // a lit rim along the top
                data[y * w + x] = c;
            }
        }

        var tex = new Texture2D(FlxG.GraphicsDevice, w, h);
        tex.SetData(data);
        return tex;
    }

    static Texture2D Spores(int w, int h)
    {
        var rnd = new Random(11);
        var data = new Color[w * h];
        for (int i = 0; i < 260; i++)
        {
            int x = rnd.Next(2, w - 2), y = rnd.Next(2, h - 2);
            int b = rnd.Next(60, 200);
            var c = rnd.Next(3) == 0 ? new Color(b / 2, b, b, 255) : new Color(b, b / 2, b, 255);
            data[y * w + x] = c;
            if (rnd.Next(5) == 0)         // a few bigger ones with a soft halo
            {
                data[y * w + x + 1] = c; data[(y + 1) * w + x] = c; data[(y + 1) * w + x + 1] = c;
                var halo = new Color(c.R / 4, c.G / 4, c.B / 4, 255);
                data[(y - 1) * w + x] = halo; data[y * w + x - 1] = halo; data[(y + 2) * w + x] = halo; data[y * w + x + 2] = halo;
            }
        }
        var tex = new Texture2D(FlxG.GraphicsDevice, w, h);
        tex.SetData(data);
        return tex;
    }

    // ------------------------------------------------------------------ HUD and map

    void BuildHud(int[] data)
    {
        // the whole cave in one small picture, 200 pixels wide whatever the size of the cave
        _miniScale = Math.Max(1, _cols / 200);
        int f = _miniScale, mw = _cols / f, mh = _rows / f;
        var px = new Color[mw * mh];
        for (int y = 0; y < mh; y++)
            for (int x = 0; x < mw; x++)
            {
                int walls = 0;
                for (int dy = 0; dy < f; dy++)
                    for (int dx = 0; dx < f; dx++)
                        walls += data[(y * f + dy) * _cols + x * f + dx];
                px[y * mw + x] = walls * 2 >= f * f ? new Color(96, 80, 130, 220) : new Color(10, 8, 20, 200);
            }
        var tex = new Texture2D(FlxG.GraphicsDevice, mw, mh);
        tex.SetData(px);

        _mini = new FlxSprite(FlxG.Width - mw - 4, 4).LoadGraphic(tex);
        _mini.ScrollFactor = Vector2.Zero;
        Add(_mini);

        _miniDot = new FlxSprite().MakeGraphic(3, 3, new Color(255, 80, 80));
        _miniDot.ScrollFactor = Vector2.Zero;
        Add(_miniDot);

        var fuel = new FlxBar(6, FlxG.Height - 14, FlxBarFillDirection.LeftToRight, 70, 6, () => _player.Fuel, 0, 100, showBorder: true)
            .CreateFilledBar(new Color(30, 24, 40), new Color(255, 170, 60), true, new Color(200, 200, 220));
        fuel.ScrollFactor = Vector2.Zero;
        Add(fuel);

        _info = new FlxText(6, 4, 270, "") { Tint = new Color(220, 225, 240) };
        _info.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black);
        _info.ScrollFactor = Vector2.Zero;
        Add(_info);

        var help = new FlxText(80, FlxG.Height - 14, 400, "A/D  SPACE jump/jet  G gen  L size  R new  F flies  M map") { Tint = new Color(190, 195, 215) };
        help.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black);
        help.ScrollFactor = Vector2.Zero;
        Add(help);
    }

    // ------------------------------------------------------------------ update

    public override void Update(float elapsed)
    {
        base.Update(elapsed);
        FlxG.Collide(_map, _player);

        _player.PlaceFlame();

        _time += elapsed;
        UpdateFireflies();

        // map marker
        _miniDot.X = _mini.X + (_player.X / Tile) / _miniScale - 1;
        _miniDot.Y = _mini.Y + (_player.Y / Tile) / _miniScale - 1;

        _info.Text = $"{GeneratorName(_generator)}\n{_cols}x{_rows}  {_openTiles * 100 / (_cols * _rows)}% open  {_generateMs:0} ms  tile {(int)(_player.X / Tile)},{(int)(_player.Y / Tile)}";

        if (FlxG.Keys.JustPressed(Keys.M)) _mini.Visible = _miniDot.Visible = !_mini.Visible;
        if (FlxG.Keys.JustPressed(Keys.R)) FlxG.SwitchState(new BigCaveDemoState());
        if (FlxG.Keys.JustPressed(Keys.G))
        {
            _generator = (CaveGenerator)(((int)_generator + 1) % Enum.GetValues<CaveGenerator>().Length);
            FlxG.SwitchState(new BigCaveDemoState());
        }
        if (FlxG.Keys.JustPressed(Keys.L))
        {
            _huge = !_huge;
            FlxG.SwitchState(new BigCaveDemoState());
        }
        if (FlxG.Keys.JustPressed(Keys.F)) _walkFlies = !_walkFlies;
        if (FlxG.Keys.JustPressed(Keys.Escape))
        {
            FlxG.PostProcess.Clear();
            LauncherState.Return();
        }
    }
}

/// <summary>The spaceman from the cave demo, drawn 3 times bigger to suit the 30 pixel tiles, with a jetpack.</summary>
sealed class BigSpaceman : FlxSprite
{
    const float RunSpeed = 150f, JumpSpeed = 270f, Gravity = 700f, JetSpeed = 170f, JetPush = 1400f;

    /// <summary>Jetpack fuel, 0 to 100: used while holding jump in the air, refilled on the ground.</summary>
    public float Fuel = 100f;

    /// <summary>A glowing flame under the feet (added to the state by the demo).</summary>
    public readonly FlxSprite Flame;

    public BigSpaceman(float x, float y) : base(x, y)
    {
        LoadGraphic(FlxAssets.Get("cave/spaceman.png"), animated: true, frameWidth: 8);

        // draw the 8x8 frames at 3x from their top left corner; the hitbox is the original 6x7 box at 3x
        Scale = new Vector2(3, 3);
        Origin = Vector2.Zero;
        Width = 18; Height = 21;
        Offset = new Vector2(3, 3);

        Acceleration.Y = Gravity;
        MaxVelocity = new Vector2(RunSpeed, 380);
        Drag.X = RunSpeed * 8;

        Animation.Add("idle", new[] { 0 }, 1);
        Animation.Add("run", new[] { 1, 2, 3, 0 }, 12);
        Animation.Add("jump", new[] { 4 }, 1);
        Animation.Play("idle");

        Flame = new FlxSprite().LoadGraphic(FlxAssets.Get("atlas/glow.png"));
        Flame.Tint = new Color(255, 150, 40);
        Flame.Blend = FlxBlend.Add;
        Flame.Scale = new Vector2(0.7f, 0.7f);
        Flame.UpdateHitbox();
        Flame.Visible = false;
    }

    public override void Update(float elapsed)
    {
        // Touching is read before Update resets it (the state collides after updating)
        bool onFloor = IsTouching(FlxDirection.Floor);

        Acceleration.X = 0;
        if (FlxG.Keys.AnyPressed(Keys.Left, Keys.A)) { Acceleration.X -= Drag.X; FlipX = true; }
        else if (FlxG.Keys.AnyPressed(Keys.Right, Keys.D)) { Acceleration.X += Drag.X; FlipX = false; }

        bool jumpHeld = FlxG.Keys.AnyPressed(Keys.Space, Keys.W, Keys.Up);
        bool jumpPressed = FlxG.Keys.AnyJustPressed(Keys.Space, Keys.W, Keys.Up);

        if (onFloor)
        {
            Fuel = MathF.Min(100f, Fuel + 60f * elapsed);
            if (jumpPressed) Velocity.Y = -JumpSpeed;
        }

        // hold jump in the air: the jetpack pushes up until the climb speed is reached
        bool jetting = !onFloor && jumpHeld && Fuel > 0f;
        if (jetting)
        {
            Velocity.Y = MathF.Max(Velocity.Y - JetPush * elapsed, -JetSpeed);
            Fuel = MathF.Max(0f, Fuel - 45f * elapsed);
        }
        Flame.Visible = jetting;

        if (!onFloor) Animation.Play("jump");
        else if (Velocity.X != 0 || Acceleration.X != 0) Animation.Play("run");
        else Animation.Play("idle");

        base.Update(elapsed);
    }

    /// <summary>Puts the flame under the feet, flickering.</summary>
    public void PlaceFlame()
    {
        Flame.X = X + Width / 2f - Flame.Width / 2f;
        Flame.Y = Y + Height - 6;
        Flame.Alpha = 0.7f + FlxG.Random.Float() * 0.3f;
    }
}
