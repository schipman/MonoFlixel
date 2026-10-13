using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo.Drown;

/// <summary>
/// A dive. The site comes from a seed (the same map position always gives the same site), the player mines it with a laser or a
/// drill, harpoons the creatures, collects shards, crafts lanterns, and the deeper it goes the darker it gets. What was changed in a site
/// is saved, so it is as the player left it next time.
/// </summary>
public class DrownDiveState : FlxState
{
    readonly int _siteX, _siteY, _depthType;

    public DiveLevel Level = null!;
    public FlxTilemap Map = null!;
    FlxTilemap _bgMap = null!;
    public DrownPlayer Player = null!;
    public readonly FlxTypedGroup<Mob> Mobs = new();
    public Gibs Gibs = new();
    public readonly BubblePool Bubbles = new(450);

    readonly FlxTypedGroup<Pickup> _pickups = new();
    readonly FlxTypedGroup<Harpoon> _harpoons = new();
    readonly FlxGroup _kelp = new(), _decos = new(), _spawners = new(), _lamps = new(), _geysers = new();
    readonly HashSet<int> _vents = new();      // the rock the geysers are in: it cannot be mined
    readonly Dictionary<int, List<Deco>> _bySupport = new();

    DrownInput _input = null!;
    DrownHud _hud = null!;
    DarknessLayer _dark = null!;
    SiteChanges _changes = null!;
    HashSet<int> _taken = null!;
    readonly Dictionary<int, float> _tileHealth = new();
    readonly List<Vector2> _lanternLights = new();
    int _nextHarpoon;
    float _autosave, _bagWarning;
    FlxSound? _ambience, _miningLoop;
    FlxPostProcess? _wobble, _bloom, _night;
    bool _waterFx = true, _nightVision;
    Vector2 _lastSparks;

    public DrownDiveState(int siteX, int siteY, int depthType)
    {
        _siteX = siteX; _siteY = siteY; _depthType = Math.Clamp(depthType, 1, 3);
    }

    /// <summary>How deep the diver is, in tiles.</summary>
    public int DepthInTiles => Math.Clamp((int)(Player.Y / DiveLevel.Tile), 0, Level.Rows);

    static int SeedFor(int x, int y, int depth) => unchecked(x * 73856093 ^ y * 19349663 ^ depth * 83492791);

    // ------------------------------------------------------------------ building

    public override void Create()
    {
        FlxG.Game.Window.Title = $"DROWN - {DiveLevel.DepthNames[_depthType]} ({_siteX}, {_siteY})";
        FlxG.Cameras.BgColor = new Color(4, 12, 28);
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = false;

        Level = DiveLevel.Generate(SeedFor(_siteX, _siteY, _depthType), _depthType);
        _changes = DrownData.LoadSite(_siteX, _siteY, _depthType);
        _taken = new HashSet<int>(_changes.TakenEntities);

        // what the player mined last time
        foreach (int cell in _changes.MinedTiles) if (cell >= 0 && cell < Level.Rock.Length) Level.Rock[cell] = 0;

        // ---- the picture behind: the sea, then a smaller cave that scrolls at half speed
        var sea = new FlxSprite(0, 0).LoadGraphic("drown/backdrop.png");
        sea.ScrollFactor = Vector2.Zero;
        sea.Scale = new Vector2(FlxG.Width / (float)sea.FrameWidth, FlxG.Height / (float)sea.FrameHeight);
        sea.Origin = Vector2.Zero;
        sea.Moves = false;
        Add(sea);

        _bgMap = new FlxTilemap().LoadMapFromArray(Level.Background, Level.BgCols, Level.BgRows, FlxAssets.Get("drown/bg_autotile.png"),
            DiveLevel.Tile, DiveLevel.Tile, FlxAutoTiling.Auto);
        _bgMap.ScrollFactor = new Vector2(0.5f, 0.5f);
        Add(_bgMap);

        // ---- the rock
        Map = new FlxTilemap().LoadMapFromArray(Level.Rock, Level.Cols, Level.Rows, FlxAssets.Get("drown/rock_autotile.png"),
            DiveLevel.Tile, DiveLevel.Tile, FlxAutoTiling.Auto);
        Add(Map);

        // ---- what lives on it
        foreach (var p in Level.Placements)
        {
            if (_taken.Contains(p.Id)) continue;

            if (p.Kind is EntityKind.MobSpawner or EntityKind.SharkSpawner or EntityKind.JellySpawner)
            {
                _spawners.Add(new MobSpawner(this, p.Kind, new Vector2(p.Col * DiveLevel.Tile + 15, p.Row * DiveLevel.Tile + 15)));
                continue;
            }

            if (p.Kind == EntityKind.Geyser)
            {
                _geysers.Add(new Geyser(this, p, Level.GeyserHeadroom(p.Col, p.Row)));
                _vents.Add(p.SupportRow * Level.Cols + p.Col);
                continue;
            }

            var deco = new Deco(this, p);
            (p.Kind == EntityKind.Kelp ? _kelp : _decos).Add(deco);

            int support = p.SupportRow * Level.Cols + p.Col;
            if (!_bySupport.TryGetValue(support, out var list)) _bySupport[support] = list = new List<Deco>();
            list.Add(deco);
        }
        Add(_kelp);
        Add(_decos);
        Add(_spawners);
        Add(_geysers);
        Add(_pickups);
        Add(Mobs);

        for (int i = 0; i < 8; i++) _harpoons.Add(new Harpoon(this));
        Add(_harpoons);

        // lanterns from earlier dives
        foreach (int cell in _changes.Lanterns) PlaceLamp(cell % Level.Cols, cell / Level.Cols, false);
        Add(_lamps);

        // ---- the player (the controls are actions: see DrownInput)
        _input = new DrownInput();
        var start = Level.StartPosition;
        Player = new DrownPlayer(this, _input, start.X, start.Y);
        Add(Bubbles);
        Add(Player);
        Add(Player.Arm);
        Add(Player.Laser);
        Add(Player.Sparks);
        Gibs.AddTo(this);

        FlxG.Camera.Bounds = Map.Bounds;
        FlxG.Camera.Follow(Player, 0.1f);

        // ---- the dark and the screen furniture
        _dark = new DarknessLayer();
        Add(_dark);
        _hud = new DrownHud(this, Player, Level.Rows);
        Add(_hud.Root);

        // ---- the water wobbles a little
        // ---- the look of the dive: water wobble and glow (P), and night vision (N); they stack on top of whatever
        // the player chose in the launcher's effects menu
        _wobble = FlxPostProcess.Load("Wiggle", ("Amplitude", 0.0025f), ("Frequency", 18f), ("Speed", 2.5f));
        _bloom = FlxBloom.Create(threshold: 0.65f, intensity: 0.6f);
        _night = FlxPostProcessLibrary.Create("NightVision");
        ApplyEffects();

        // ---- sound
        _ambience = DrownData.Loop("layered_dive_ambience", 0.5f);
        _ambience?.Play();
        if (_depthType == 1) FlxG.Sound.PlayMusic("drown/audio/music_shallows_1.wav", 0.3f);
        _miningLoop = DrownData.Loop("laser_mining_rock", 0.7f);

        FlxG.Camera.Fade(Color.Black, 1f, fadeIn: true);
        _hud.Say($"{DiveLevel.DepthNames[_depthType]}.  Mouse aims, left button fires.", 4f);
    }

    // ------------------------------------------------------------------ things other objects ask for

    public void TakeEntity(int id)
    {
        if (_taken.Add(id)) _changes.TakenEntities.Add(id);
    }

    /// <summary>A line on the screen (the HUD's message).</summary>
    public void Say(string text, float seconds = 2.5f) => _hud.Say(text, seconds);

    public void SpawnPickup(ItemKind kind, float x, float y, float vx, float vy, float spin) =>
        _pickups.Add(new Pickup(kind, x, y, vx, vy, spin));

    /// <summary>Puts a lantern down where the diver stands (it needs the floor under the feet).</summary>
    public void UseLantern()
    {
        if (!Player.IsTouching(FlxDirection.Down)) { _hud.Say("Stand on the floor to put a lantern down."); DrownData.Sfx("drown_negative"); return; }
        if (DrownData.Bag.Count(ItemKind.Lantern) == 0) { _hud.Say("No lanterns. Craft one: 3 Territe, 1 Oceanite, 2 Magmite."); DrownData.Sfx("drown_negative"); return; }

        int col = (int)(Player.Midpoint.X / DiveLevel.Tile);
        int row = (int)((Player.Y + Player.Height - 1) / DiveLevel.Tile);
        if (_changes.Lanterns.Contains(row * Level.Cols + col)) { _hud.Say("There is a lantern here already."); return; }

        DrownData.Bag.Remove(ItemKind.Lantern);
        _changes.Lanterns.Add(row * Level.Cols + col);
        PlaceLamp(col, row, true);
        DrownData.Sfx("drown_click");
    }

    void PlaceLamp(int col, int row, bool fresh)
    {
        var lamp = new FlxSprite(col * DiveLevel.Tile, row * DiveLevel.Tile - 2).LoadGraphic("drown/lamp.png");
        lamp.Moves = false;
        _lamps.Add(lamp);
        _lanternLights.Add(new Vector2(col * DiveLevel.Tile + 15, row * DiveLevel.Tile + 10));
    }

    // ------------------------------------------------------------------ mining

    void DamageTile(int col, int row, float amount, Vector2 at)
    {
        // the frame of the site stays
        if (col <= 0 || row <= 0 || col >= Level.Cols - 1 || row >= Level.Rows - 1) return;
        if (!Level.IsRock(col, row)) return;

        int cell = row * Level.Cols + col;
        if (_vents.Contains(cell)) return;           // a geyser's vent
        float health = _tileHealth.GetValueOrDefault(cell, 100f) - amount;
        _tileHealth[cell] = health;

        _lastSparks = at;
        if (health <= 0) BreakTile(col, row);
    }

    void BreakTile(int col, int row)
    {
        int cell = row * Level.Cols + col;
        _tileHealth.Remove(cell);
        Level.Rock[cell] = 0;
        Map.SetTile(col, row, 0);
        _changes.MinedTiles.Add(cell);

        Gibs.Rock.Burst(new Vector2(col * DiveLevel.Tile + 15, row * DiveLevel.Tile + 15), Color.SandyBrown, 8);
        DrownData.Sfx("rocktile_break", 0.7f);

        // whatever stood on it falls apart
        if (_bySupport.TryGetValue(cell, out var list))
            foreach (var d in list.ToArray()) d.Break(true);
    }

    void Mine(float elapsed)
    {
        bool mining = false;

        if (Player.LaserOn && Player.LaserHitsRock)
        {
            // a little way into the rock the beam ended in
            var inside = Player.LaserHit + Player.AimVector * 2f;
            DamageTile((int)MathF.Floor(inside.X / DiveLevel.Tile), (int)MathF.Floor(inside.Y / DiveLevel.Tile), elapsed * 50f, Player.LaserHit);
            mining = true;

            Player.Sparks.X = Player.LaserHit.X;
            Player.Sparks.Y = Player.LaserHit.Y;
            Player.Sparks.Start(explode: false, frequency: 0.05f);
        }
        else if (Player.Sparks.Emitting) Player.Sparks.Emitting = false;

        // the suit drill digs the rock you push against
        if (Player.Drill && !Player.IsDead)
        {
            var dir = Player.DrillDirection;
            bool touching = (dir.X < 0 && Player.IsTouching(FlxDirection.Left)) || (dir.X > 0 && Player.IsTouching(FlxDirection.Right)) ||
                            (dir.Y < 0 && Player.IsTouching(FlxDirection.Up)) || (dir.Y > 0 && Player.IsTouching(FlxDirection.Down));
            if (touching)
            {
                var mid = Player.Midpoint;
                var probe = mid + new Vector2(dir.X * (Player.Width * 0.5f + 6), dir.Y * (Player.Height * 0.5f + 6));
                DamageTile((int)MathF.Floor(probe.X / DiveLevel.Tile), (int)MathF.Floor(probe.Y / DiveLevel.Tile), elapsed * 120f, probe);
                // the second tile for the tall diver when digging sideways
                if (dir.X != 0) DamageTile((int)MathF.Floor(probe.X / DiveLevel.Tile), (int)MathF.Floor((probe.Y + 28) / DiveLevel.Tile), elapsed * 120f, probe);
                mining = true;
            }
        }

        if (_miningLoop != null)
        {
            if (mining && !_miningLoop.Playing) _miningLoop.Play();
            else if (!mining && _miningLoop.Playing) _miningLoop.Pause();
        }
    }

    // ------------------------------------------------------------------ the update

    public override void Update(float elapsed)
    {
        // ---- a dead diver starts again at the surface, without the things carried
        if (Player.IsDead && Player.CanRevive)
        {
            DrownData.Bag.Clear();
            DrownData.Components.Clear();
            Player.Respawn(Level.StartPosition);
            _hud.Say("The suit is gone... you are back at the surface, with empty hands.", 4f);
        }

        HandleControls();
        base.Update(elapsed);

        FlxG.Collide(Map, Player);
        FlxG.Collide(Map, Mobs);
        FlxG.Collide(Map, _pickups);
        FlxG.Collide(Map, _harpoons);

        // harpoons hurt creatures
        foreach (var h in _harpoons.Members)
        {
            if (!h.Exists) continue;
            foreach (var m in Mobs.Members)
                if (m.Exists && m.Alive && FlxObject.OverlapsPair(h, m)) { m.HitBy(h); break; }
        }

        // shards into the bag
        foreach (var p in _pickups.Members)
        {
            if (!p.Exists || Player.IsDead || !FlxObject.OverlapsPair(p, Player)) continue;
            if (DrownData.Bag.Add(p.Kind) > 0)
            {
                p.Kill();
                _hud.Say("+1 " + DrownData.Items[p.Kind].Name, 1.2f);
            }
            else if (_bagWarning <= 0)
            {
                _hud.Say("Your bag is full or too heavy: make room (I).", 2f);
                _bagWarning = 2f;
            }
        }
        _bagWarning -= elapsed;

        Mine(elapsed);
        UpdateLights();
        _hud.Update(elapsed);

        _autosave += elapsed;
        if (_autosave > 20f) { _autosave = 0; Save(); }
    }

    void HandleControls()
    {
        // the mouse and the player's menu: nothing is fired while the inventory is open
        if (_input.Inventory.Triggered && !Player.IsDead)
        {
            _hud.ToggleInventory();
            DrownData.Sfx(_hud.InventoryOpen ? "drown_invopen" : "drown_invclose", 0.7f);
        }

        if (_input.Light.Triggered && !Player.IsDead) UseLantern();

        if (_input.Fire.Triggered && Player.Tool == Tool.Harpoon && !Player.MenuOpen && !Player.IsDead) FireHarpoon();

        if (_input.Back.Triggered)
        {
            if (_hud.CraftOpen) _hud.SetCraft(false);
            else if (_hud.InventoryOpen) { _hud.SetInventory(false); DrownData.Sfx("drown_invclose", 0.7f); }
            else _hud.Say("Swim to the surface and press R to go up.");
        }

        if (_input.Surface.Triggered && !Player.IsDead)
        {
            if (DepthInTiles < 10) Surface();
            else _hud.Say("Too deep: get nearer the surface first.");
        }

        if (FlxG.Keys.JustPressed(Keys.P)) { _waterFx = !_waterFx; ApplyEffects(); }
        if (FlxG.Keys.JustPressed(Keys.N))
        {
            _nightVision = !_nightVision;
            ApplyEffects();
            _hud.Say(_nightVision ? "Night vision on." : "Night vision off.", 1.5f);
        }
    }

    /// <summary>Puts the dive's effects on the screen's stack: water wobble, then glow, then night vision.</summary>
    void ApplyEffects()
    {
        FlxG.PostProcess.Clear();
        if (_waterFx) { FlxG.PostProcess.Add(_wobble!); FlxG.PostProcess.Add(_bloom!); }
        if (_nightVision) FlxG.PostProcess.Add(_night!);
    }

    void FireHarpoon()
    {
        var h = _harpoons.Members[_nextHarpoon];
        _nextHarpoon = (_nextHarpoon + 1) % _harpoons.Members.Count;
        h.Shoot(Player.GunPoint, Player.AimVector * 300f, Player.AimAngle);
        Bubbles.Burst(Player.GunPoint, Player.AimVector, 9, 90f, 30f);       // the water the shot pushes out of the way
        DrownData.Sfx("harpoon_shot", 0.7f);
    }

    // ------------------------------------------------------------------ the dark

    void UpdateLights()
    {
        // the deeper, the darker, and the smaller the diver's own light
        float depth = Math.Clamp((Player.Y / DiveLevel.Tile - DiveLevel.SurfaceRows) / (Level.Rows * 0.65f), 0f, 1f);
        _dark.Ambient = Vector3.Lerp(new Vector3(0.75f, 0.9f, 1f), new Vector3(0.025f, 0.04f, 0.08f), MathF.Pow(depth, 0.7f));

        _dark.Lights.Clear();
        _dark.Lights.Add(new DarknessLayer.Light(Player.Midpoint, MathHelper.Lerp(520f, 170f, depth), new Color(235, 245, 255)));

        foreach (var l in _lanternLights)
            _dark.Lights.Add(new DarknessLayer.Light(l, 190f, new Color(150, 255, 150)));

        foreach (var g in _geysers.Members.OfType<Geyser>())
            _dark.Lights.Add(new DarknessLayer.Light(g.Mouth - new Vector2(0, 30), 150f, new Color(110, 215, 255)));

        if (Player.LaserOn) _dark.Lights.Add(new DarknessLayer.Light(Player.LaserHit, 70f, new Color(255, 190, 120)));
    }

    // ------------------------------------------------------------------ leaving

    void Save()
    {
        DrownData.SaveSite(_siteX, _siteY, _depthType, _changes);
        DrownData.SaveBag();
    }

    void Leave(FlxState next, int w, int h, int zoom)
    {
        Save();
        Player.ReleaseSounds();
        _ambience?.Stop();
        _miningLoop?.Stop();
        FlxG.Sound.Music?.Stop();
        FlxG.PostProcess.Clear();
        FlxG.Camera.Bounds = null;
        FlxG.SwitchState(next, w, h, zoom);
    }

    void Surface() => Leave(new DrownOverworldState(_siteX, _siteY), 640, 480, 1);

    public override void Destroy()
    {
        base.Destroy();
    }
}
