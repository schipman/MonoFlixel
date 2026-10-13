using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>What a creature in the roguelike demo is: its looks, numbers and how it finds its way to the player.</summary>
sealed record RlKind(string Name, char Glyph, Color Color, int Hp, string Damage, string Brain, int Speed);

/// <summary>A creature (the player included) on the grid. It lives in cells; its glyph slides to where it is.</summary>
sealed class RlActor
{
    public RlKind Kind = null!;
    public int X, Y, Hp, MaxHp;
    public bool IsPlayer;
    public int Cooldown;                 // for slow monsters: turns to skip
    public FlxText Glyph = null!;
    public FlxDice Damage = null!;
    public bool Alive => Hp > 0;
}

/// <summary>
/// A small roguelike made from the engine's grid tools: a dungeon from <see cref="FlxBspGenerator"/> or from the cave generator, what you
/// can see from <see cref="FlxFov"/> (change the algorithm with F), turn based moves and fights with <see cref="FlxDice"/>, and monsters
/// that find you with a <see cref="FlxDijkstraMap"/> (rats and ogres) or A* (goblins).
/// Arrows / WASD / numpad move (QEZC and the numpad for diagonals), bump to attack, SPACE or . waits, click to walk somewhere (A*),
/// The blue steps are the way down. F FOV algorithm, [ ] radius, T light walls, M generator, V shows the distance map, X reveals the map, R new level, ESC back.
/// </summary>
public class RoguelikeDemoState : FlxState
{
    const int Tile = 16, MapW = 70, MapH = 44, HudHeight = 56;

    static readonly RlKind Player = new("You", '@', new Color(255, 230, 90), 24, "1d6+1", "player", 1);
    static readonly RlKind Rat = new("rat", 'r', new Color(190, 140, 100), 4, "1d3", "dijkstra", 1);
    static readonly RlKind Goblin = new("goblin", 'g', new Color(110, 220, 110), 8, "1d4+1", "astar", 1);
    static readonly RlKind Ogre = new("ogre", 'O', new Color(230, 90, 80), 18, "1d8", "dijkstra", 2);

    static readonly FlxFovAlgorithm[] Algorithms = { FlxFovAlgorithm.Shadowcast, FlxFovAlgorithm.SymmetricShadowcast, FlxFovAlgorithm.Permissive, FlxFovAlgorithm.Basic };
    static int _algo, _mapMode;                 // the choices stay from level to level
    static int _radius = 9;
    static bool _lightWalls = true;

    FlxGridMap _map = null!;
    FlxTilemap _tiles = null!;
    FlxSprite _heat = null!, _route = null!;
    readonly List<RlActor> _actors = new();
    RlActor _player = null!;
    readonly List<(int X, int Y)> _potions = new(), _stairsList = new();
    readonly Dictionary<(int X, int Y), FlxText> _potionGlyphs = new();
    (int X, int Y) _stairs;
    FlxDijkstraMap _towardPlayer = null!;

    FlxGroup? _hud;
    FlxBar _hpBar = null!;
    FlxText _status = null!, _info = null!, _help = null!, _banner = null!;
    readonly FlxText[] _log = new FlxText[4];
    readonly List<string> _messages = new();

    int _depth = 1, _turn, _kills;
    bool _dead, _showHeat;
    float _repeat, _travelTimer;
    readonly Queue<(int X, int Y)> _travel = new();
    (int X, int Y)? _hover;

    Texture2D _tileset = null!;

    public override void Create()
    {
        FlxG.Game.Window.Title = "MonoFlixel - roguelike";
        FlxG.Cameras.BgColor = new Color(6, 6, 12);
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = true;

        _tileset = MakeTileset();
        NewLevel(first: true);

        BuildHud();
    }

    // ------------------------------------------------------------------ the picture of the dungeon

    /// <summary>The tiles of the dungeon in one strip, drawn here: 0 empty, 1 floor, 2 wall, 3 floor remembered, 4 wall remembered, 5 stairs, 6 stairs remembered.</summary>
    static Texture2D MakeTileset()
    {
        const int count = 7;
        var tex = new Texture2D(FlxG.GraphicsDevice, Tile * count, Tile);
        var px = new Color[Tile * count * Tile];
        var rnd = new Random(3);

        void Put(int tile, int x, int y, Color c) => px[y * Tile * count + tile * Tile + x] = c;

        for (int tile = 1; tile < count; tile++)
        {
            bool remembered = tile is 3 or 4 or 6;
            bool wall = tile is 2 or 4;
            Color baseColor = wall ? new Color(96, 84, 110) : new Color(46, 40, 52);
            Color line = wall ? new Color(52, 44, 66) : new Color(34, 30, 40);
            Color dot = new Color(70, 62, 78);
            if (remembered) { baseColor = Color.Lerp(baseColor, new Color(18, 20, 40), 0.65f); line = Color.Lerp(line, new Color(10, 12, 26), 0.65f); dot = Color.Lerp(dot, new Color(24, 26, 50), 0.6f); }

            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                {
                    Color c = baseColor;
                    if (wall)
                    {
                        // bricks
                        int row = y / 4, offset = (row % 2) * 4;
                        if (y % 4 == 3 || (x + offset) % 8 == 7) c = line;
                        else if (rnd.Next(9) == 0) c = Color.Lerp(baseColor, Color.White, 0.06f);
                    }
                    else
                    {
                        if (x == 0 || y == 0) c = line;
                        else if (rnd.Next(26) == 0) c = dot;
                    }
                    Put(tile, x, y, c);
                }

            if (tile is 5 or 6)
            {
                // the stairs: steps going down
                for (int s = 0; s < 4; s++)
                    for (int y = 3 + s * 3; y < 6 + s * 3 && y < 15; y++)
                        for (int x = 2 + s; x < 14 - s; x++)
                            Put(tile, x, y, remembered ? new Color(40, 44, 70) : Color.Lerp(new Color(70, 110, 150), new Color(20, 30, 50), s / 4f));
            }
        }
        tex.SetData(px);
        return tex;
    }

    // ------------------------------------------------------------------ making a level

    void NewLevel(bool first = false)
    {
        // clear the old one
        foreach (var a in _actors) Remove(a.Glyph);
        _actors.Clear();
        foreach (var g in _potionGlyphs.Values) Remove(g);
        _potionGlyphs.Clear();
        _potions.Clear();
        _travel.Clear();
        if (_tiles != null) Remove(_tiles);
        if (_heat != null) Remove(_heat);
        if (_route != null) Remove(_route);

        int[] data;
        var rooms = new List<Rectangle>();
        int watch = 0;
        while (true)
        {
            if (_mapMode == 0)
            {
                var bsp = FlxBspGenerator.Generate(MapW, MapH, minLeaf: 9, maxLeaf: 20);
                data = bsp.Data;
                rooms = bsp.Rooms;
            }
            else
            {
                data = CaveData();
                rooms.Clear();
            }

            _map = FlxGridMap.FromWalls(data, MapW, MapH);
            if (CountFloor() > MapW * MapH * 0.12 || ++watch > 20) break;
        }

        // the tiles: nothing is seen yet
        _tiles = new FlxTilemap().LoadMapFromArray(new int[MapW * MapH], MapW, MapH, _tileset, Tile, Tile);
        Add(_tiles);

        _heat = MakeOverlay();
        _route = MakeOverlay();
        Add(_heat);
        Add(_route);
        _heat.Visible = _showHeat;

        // the player, the stairs, the monsters and potions
        (int X, int Y) start;
        var floors = FloorCells();
        if (rooms.Count > 0)
        {
            var c = FlxBspMap.Center(rooms[0]);
            start = (c.X, c.Y);
        }
        else start = floors[FlxG.Random.Int(0, floors.Count - 1)];

        var dist = new FlxDijkstraMap(MapW, MapH);
        dist.Compute(new[] { start }, _map, FlxDiagonalPolicy.Normal);

        // the stairs are as far away as can be walked
        _stairs = floors.OrderByDescending(f => dist[f.X, f.Y] is var d && !float.IsPositiveInfinity(d) ? d : -1).First();

        if (_player == null)
        {
            _player = MakeActor(Player, start.X, start.Y);
            _player.IsPlayer = true;
        }
        else
        {
            _player.X = start.X; _player.Y = start.Y;
            _actors.Add(_player);
            Add(_player.Glyph);
        }
        SnapGlyph(_player);

        int monsters = 5 + _depth * 2;
        var candidates = floors.Where(f => dist[f.X, f.Y] > 12 && f != _stairs).OrderBy(_ => FlxG.Random.Float()).ToList();
        for (int i = 0; i < monsters && i < candidates.Count; i++)
        {
            float roll = FlxG.Random.Float();
            var kind = roll < 0.5f ? Rat : roll < 0.85f || _depth < 2 ? Goblin : Ogre;
            if (_depth >= 3 && roll > 0.7f) kind = Ogre;
            var m = MakeActor(kind, candidates[i].X, candidates[i].Y);
            SnapGlyph(m);
        }

        foreach (var cell in candidates.Skip(monsters).Take(3))
        {
            _potions.Add(cell);
            var g = Glyph('!', new Color(240, 100, 230));
            PlaceGlyph(g, cell.X, cell.Y);
            _potionGlyphs[cell] = g;
        }

        _towardPlayer = new FlxDijkstraMap(MapW, MapH);
        _hover = null;

        FlxG.Camera.Bounds = new Rectangle(0, 0, MapW * Tile, MapH * Tile - 0);
        FlxG.Camera.Follow(_player.Glyph, 0.2f);

        Log(first ? "You enter the dungeon. Find the stairs." : $"You go down to level {_depth}.");
        if (_hud != null) { Remove(_hud); Add(_hud); }              // the status strip stays on top
        EndTurn(passTime: false);
    }

    int CountFloor()
    {
        int n = 0;
        for (int y = 0; y < MapH; y++) for (int x = 0; x < MapW; x++) if (_map.IsWalkable(x, y)) n++;
        return n;
    }

    List<(int X, int Y)> FloorCells()
    {
        var list = new List<(int, int)>();
        for (int y = 0; y < MapH; y++) for (int x = 0; x < MapW; x++) if (_map.IsWalkable(x, y)) list.Add((x, y));
        return list;
    }

    /// <summary>A cave: cellular automaton, only its biggest open area kept, a wall round the edge.</summary>
    static int[] CaveData()
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            var data = FlxCaveGenerator.GenerateCaveArray(MapW, MapH, 6, 0.44f);
            for (int x = 0; x < MapW; x++) { data[x] = 1; data[(MapH - 1) * MapW + x] = 1; }
            for (int y = 0; y < MapH; y++) { data[y * MapW] = 1; data[y * MapW + MapW - 1] = 1; }

            // flood the open cells, keep the biggest area
            var area = new int[data.Length];
            var sizes = new List<int> { 0 };
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != 0 || area[i] != 0) continue;
                int id = sizes.Count, size = 0;
                var stack = new Stack<int>();
                stack.Push(i); area[i] = id;
                while (stack.Count > 0)
                {
                    int c = stack.Pop(); size++;
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = c % MapW + dx, ny = c / MapW + dy;
                        if (nx < 0 || ny < 0 || nx >= MapW || ny >= MapH) continue;
                        int n = ny * MapW + nx;
                        if (data[n] == 0 && area[n] == 0) { area[n] = id; stack.Push(n); }
                    }
                }
                sizes.Add(size);
            }

            int best = 1;
            for (int s = 1; s < sizes.Count; s++) if (sizes[s] > sizes[best]) best = s;
            if (sizes.Count <= 1 || sizes[best] < MapW * MapH * 0.25) continue;

            for (int i = 0; i < data.Length; i++) if (data[i] == 0 && area[i] != best) data[i] = 1;
            return data;
        }
        return FlxBspGenerator.Generate(MapW, MapH).Data;
    }

    FlxSprite MakeOverlay()
    {
        var s = new FlxSprite(0, 0).MakeGraphic(MapW, MapH, Color.Transparent);
        s.Scale = new Vector2(Tile, Tile);
        s.Origin = Vector2.Zero;
        s.Moves = false;
        return s;
    }

    // ------------------------------------------------------------------ actors and glyphs

    FlxText Glyph(char c, Color color)
    {
        var t = new FlxText(0, 0, 0, c.ToString(), 16) { Tint = color };
        t.SetBorderStyle(FlxTextBorderStyle.Outline, new Color(8, 6, 12), 1);
        Add(t);
        return t;
    }

    void PlaceGlyph(FlxText t, int cx, int cy)
    {
        t.X = cx * Tile + (Tile - t.TextWidth) / 2f;
        t.Y = cy * Tile + (Tile - t.TextHeight) / 2f - 1;
    }

    void SnapGlyph(RlActor a) => PlaceGlyph(a.Glyph, a.X, a.Y);

    RlActor MakeActor(RlKind kind, int x, int y)
    {
        var a = new RlActor { Kind = kind, X = x, Y = y, Hp = kind.Hp, MaxHp = kind.Hp, Damage = FlxDice.Parse(kind.Damage), Glyph = Glyph(kind.Glyph, kind.Color) };
        _actors.Add(a);
        return a;
    }

    RlActor? ActorAt(int x, int y) => _actors.FirstOrDefault(a => a.Alive && a.X == x && a.Y == y);

    // ------------------------------------------------------------------ the status strip

    void BuildHud()
    {
        var hud = new FlxGroup();
        var strip = new FlxSprite(0, FlxG.Height - HudHeight).MakeGraphic(FlxG.Width, HudHeight, new Color(10, 8, 16));
        strip.ScrollFactor = Vector2.Zero;
        hud.Add(strip);

        _hpBar = new FlxBar(8, FlxG.Height - HudHeight + 6, FlxBarFillDirection.LeftToRight, 120, 8, () => _player.Hp, 0, _player.MaxHp, showBorder: true)
            .CreateFilledBar(new Color(50, 12, 12), new Color(210, 60, 50), true, new Color(150, 110, 100));
        _hpBar.ScrollFactor = Vector2.Zero;
        hud.Add(_hpBar);

        FlxText Label(float x, float y, float width, Color color)
        {
            var t = new FlxText(x, y, width, "") { Tint = color };
            t.ScrollFactor = Vector2.Zero;
            hud.Add(t);
            return t;
        }

        _status = Label(136, FlxG.Height - HudHeight + 5, 160, new Color(230, 200, 190));
        _info = Label(8, FlxG.Height - HudHeight + 18, 280, new Color(150, 160, 190));
        _help = Label(8, FlxG.Height - 12, FlxG.Width - 16, new Color(100, 108, 140));
        _help.Text = "arrows/WASD/QEZC move   SPACE wait   click: walk (A*)   F fov   [ ] radius   T walls   M map   V distances   X reveal   R new   ESC";

        for (int i = 0; i < _log.Length; i++) _log[i] = Label(300, FlxG.Height - HudHeight + 4 + i * 10, FlxG.Width - 310, Color.White);

        _banner = new FlxText(0, 100, FlxG.Width, "") { Alignment = FlxTextAlign.Center, Tint = new Color(230, 70, 60) };
        _banner.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black, 2);
        _banner.ScrollFactor = Vector2.Zero;
        hud.Add(_banner);

        foreach (var m in hud.Members) m.DrawAfterPostProcess = true;
        _hud = hud;
        Add(hud);
    }

    void Log(string text)
    {
        _messages.Add(text);
        if (_messages.Count > 60) _messages.RemoveAt(0);
    }

    // ------------------------------------------------------------------ a turn

    /// <summary>The player's action is done: see, then the monsters move.</summary>
    void EndTurn(bool passTime = true)
    {
        if (passTime) _turn++;

        _map.ComputeFov(_player.X, _player.Y, _radius, Algorithms[_algo], _lightWalls);
        _towardPlayer.Compute(new[] { (_player.X, _player.Y) }, _map, FlxDiagonalPolicy.Normal, maxDistance: 40f);

        if (passTime) foreach (var m in _actors.Where(a => !a.IsPlayer && a.Alive).ToList()) Think(m);
        _towardPlayer.Compute(new[] { (_player.X, _player.Y) }, _map, FlxDiagonalPolicy.Normal, maxDistance: 40f);

        RefreshPicture();
    }

    void Think(RlActor m)
    {
        if (m.Kind.Speed > 1 && m.Cooldown-- > 0) return;
        m.Cooldown = m.Kind.Speed - 1;

        bool noticed = _map.IsInFov(m.X, m.Y) || _towardPlayer[m.X, m.Y] <= 10;      // seen by the player (so it sees the player), or close
        if (!noticed)
        {
            // wanders a little
            if (FlxG.Random.Bool(25)) StepTo(m, m.X + FlxG.Random.Int(-1, 1), m.Y + FlxG.Random.Int(-1, 1));
            return;
        }

        if (FlxGridUtil.ChebyshevDistance(m.X, m.Y, _player.X, _player.Y) <= 1 && CanStep(m.X, m.Y, _player.X, _player.Y)) { Attack(m, _player); return; }

        if (m.Kind.Brain == "astar")
        {
            // each goblin works out its own way with A*
            var path = _map.FindPath(m.X, m.Y, _player.X, _player.Y, FlxPathAlgorithm.AStar, FlxDiagonalPolicy.Normal);
            if (path is { Count: > 1 })
            {
                var (nx, ny) = path[1];
                if (!(nx == _player.X && ny == _player.Y)) StepTo(m, nx, ny);
            }
        }
        else
        {
            // rats and ogres just walk downhill on the distance map: one map for all of them
            if (_towardPlayer.NextStep(m.X, m.Y, (x, y) => ActorAt(x, y) != null) is { } step) StepTo(m, step.X, step.Y);
        }
    }

    /// <summary>A step of one cell is possible if it is open and not squeezing between two walls.</summary>
    bool CanStep(int x, int y, int tx, int ty)
    {
        if (!_map.IsWalkable(tx, ty) && !(tx == _player.X && ty == _player.Y)) return false;
        if (tx != x && ty != y) return _map.IsWalkable(tx, y) || _map.IsWalkable(x, ty);
        return true;
    }

    void StepTo(RlActor a, int x, int y)
    {
        if (x == a.X && y == a.Y) return;
        if (!_map.IsWalkable(x, y) || ActorAt(x, y) != null || !CanStep(a.X, a.Y, x, y)) return;
        a.X = x; a.Y = y;
    }

    void Attack(RlActor attacker, RlActor target)
    {
        bool hit = FlxG.Random.Int(1, 20) >= (attacker.IsPlayer ? 4 : 7);
        string who = attacker.IsPlayer ? "You" : "The " + attacker.Kind.Name;
        string whom = target.IsPlayer ? "you" : "the " + target.Kind.Name;

        if (!hit) { Log($"{who} {(attacker.IsPlayer ? "miss" : "misses")} {whom}."); return; }

        int damage = Math.Max(1, attacker.Damage.Roll());
        target.Hp -= damage;
        Log($"{who} {(attacker.IsPlayer ? "hit" : "hits")} {whom} for {damage}.");

        FlxFlicker.Flicker(target.Glyph, 0.15f);
        if (target.IsPlayer) FlxG.Camera.Shake(0.004f, 0.1f);

        if (target.Hp > 0) return;

        if (target.IsPlayer)
        {
            _dead = true;
            Log("You die...");
            _banner.Text = "YOU DIED\nR: try again";
        }
        else
        {
            Log($"The {target.Kind.Name} dies.");
            _kills++;
            target.Glyph.Visible = false;
            if (FlxG.Random.Bool(25))
            {
                _potions.Add((target.X, target.Y));
                var g = Glyph('!', new Color(240, 100, 230));
                PlaceGlyph(g, target.X, target.Y);
                _potionGlyphs[(target.X, target.Y)] = g;
            }
        }
    }

    // ------------------------------------------------------------------ the player moves

    void PlayerMove(int dx, int dy)
    {
        if (_dead || (dx == 0 && dy == 0)) return;
        int tx = _player.X + dx, ty = _player.Y + dy;

        if (ActorAt(tx, ty) is { IsPlayer: false } target && CanStep(_player.X, _player.Y, tx, ty))
        {
            Attack(_player, target);
            _travel.Clear();
            EndTurn();
            return;
        }

        if (!_map.IsWalkable(tx, ty) || !CanStep(_player.X, _player.Y, tx, ty)) return;

        _player.X = tx; _player.Y = ty;

        if (_potions.Remove((tx, ty)))
        {
            int heal = FlxDice.Roll("1d6+3");
            _player.Hp = Math.Min(_player.MaxHp, _player.Hp + heal);
            Log($"You drink a potion and heal {heal}.");
            if (_potionGlyphs.Remove((tx, ty), out var g)) Remove(g);
        }

        if ((tx, ty) == _stairs)
        {
            _depth++;
            _player.Hp = Math.Min(_player.MaxHp, _player.Hp + 4);
            NewLevel();
            return;
        }

        EndTurn();
    }

    void PlayerWait()
    {
        if (_dead) return;
        EndTurn();
    }

    // ------------------------------------------------------------------ drawing what is known

    void RefreshPicture()
    {
        // tiles: what is in view is bright, what was seen is dim, the rest is empty
        for (int y = 0; y < MapH; y++)
            for (int x = 0; x < MapW; x++)
            {
                int i = y * MapW + x;
                int value = 0;
                if (_map.IsInFov(x, y) || _map.IsExplored(x, y))
                {
                    bool lit = _map.IsInFov(x, y);
                    if ((x, y) == _stairs) value = lit ? 5 : 6;
                    else value = _map.IsWalkable(x, y) ? (lit ? 1 : 3) : (lit ? 2 : 4);
                }
                if (_tiles.GetTileByIndex(i) != value) _tiles.SetTileByIndex(i, value);
            }

        foreach (var a in _actors)
        {
            a.Glyph.Visible = a.Alive && (a.IsPlayer || _map.IsInFov(a.X, a.Y));
        }
        foreach (var (cell, g) in _potionGlyphs) g.Visible = _map.IsExplored(cell.X, cell.Y);

        UpdateOverlays();
    }

    void UpdateOverlays()
    {
        if (_showHeat)
        {
            var px = new Color[MapW * MapH];
            float max = Math.Max(1f, 22f);
            for (int i = 0; i < px.Length; i++)
            {
                float d = _towardPlayer.Distance[i];
                if (float.IsPositiveInfinity(d) || !(_map.IsExplored(i % MapW, i / MapW) || _map.IsInFov(i % MapW, i / MapW))) continue;
                float t = Math.Clamp(d / max, 0f, 1f);
                px[i] = FlxColor.FromHsb(240f - 240f * (1f - t), 0.8f, 1f, 0.35f) * 0.5f;
            }
            _heat.Pixels!.SetData(px);
            _heat.Visible = true;
        }
        else _heat.Visible = false;

        // the way the mouse would walk (A*)
        var route = new Color[MapW * MapH];
        if (_hover is { } h && !_dead && _map.IsExplored(h.X, h.Y) && _map.IsWalkable(h.X, h.Y))
        {
            var path = _map.FindPath(_player.X, _player.Y, h.X, h.Y, FlxPathAlgorithm.AStar, FlxDiagonalPolicy.Normal);
            if (path != null) foreach (var (x, y) in path.Skip(1)) route[y * MapW + x] = new Color(255, 230, 120) * 0.35f;
        }
        foreach (var (x, y) in _travel) route[y * MapW + x] = new Color(255, 230, 120) * 0.45f;
        _route.Pixels!.SetData(route);
    }

    // ------------------------------------------------------------------ every frame

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        // the glyphs slide to where their creatures are
        foreach (var a in _actors)
        {
            float tx = a.X * Tile + (Tile - a.Glyph.TextWidth) / 2f, ty = a.Y * Tile + (Tile - a.Glyph.TextHeight) / 2f - 1;
            float k = Math.Min(1f, elapsed * 22f);
            a.Glyph.X += (tx - a.Glyph.X) * k;
            a.Glyph.Y += (ty - a.Glyph.Y) * k;
        }

        HandleInput(elapsed);
        UpdateHud();

        if (FlxG.Keys.JustPressed(Keys.Escape)) { FlxG.Camera.Bounds = null; LauncherState.Return(); }
    }

    void HandleInput(float elapsed)
    {
        var k = FlxG.Keys;

        // restart / options
        if (k.JustPressed(Keys.R))
        {
            if (_dead) { _depth = 1; _kills = 0; _turn = 0; _player.Hp = _player.MaxHp; _dead = false; _banner.Text = ""; }
            NewLevel(first: _depth == 1);
            return;
        }
        if (k.JustPressed(Keys.F)) { _algo = (_algo + 1) % Algorithms.Length; Log($"Field of view: {Algorithms[_algo]}."); EndTurn(false); }
        if (k.JustPressed(Keys.OemCloseBrackets)) { _radius = Math.Min(20, _radius + 1); EndTurn(false); }
        if (k.JustPressed(Keys.OemOpenBrackets)) { _radius = Math.Max(2, _radius - 1); EndTurn(false); }
        if (k.JustPressed(Keys.T)) { _lightWalls = !_lightWalls; EndTurn(false); }
        if (k.JustPressed(Keys.V)) { _showHeat = !_showHeat; EndTurn(false); }
        if (k.JustPressed(Keys.X)) { _map.ExploreAll(); EndTurn(false); }
        if (k.JustPressed(Keys.M)) { _mapMode = 1 - _mapMode; NewLevel(); return; }

        if (_dead) return;

        // walking where the mouse was clicked: one step each moment, until something comes into view
        var mouse = FlxG.Mouse;
        var world = mouse.WorldPosition;
        int cx = (int)MathF.Floor(world.X / Tile), cy = (int)MathF.Floor(world.Y / Tile);
        var newHover = mouse.Y < FlxG.Height - HudHeight && cx >= 0 && cy >= 0 && cx < MapW && cy < MapH ? ((int, int)?)(cx, cy) : null;
        if (newHover != _hover) { _hover = newHover; UpdateOverlays(); }

        if (mouse.Left.JustPressed && _hover is { } target && _map.IsExplored(target.X, target.Y) && _map.IsWalkable(target.X, target.Y))
        {
            var path = _map.FindPath(_player.X, _player.Y, target.X, target.Y, FlxPathAlgorithm.AStar, FlxDiagonalPolicy.Normal);
            _travel.Clear();
            if (path != null) foreach (var c in path.Skip(1)) _travel.Enqueue(c);
            UpdateOverlays();
        }

        if (_travel.Count > 0)
        {
            bool danger = _actors.Any(a => !a.IsPlayer && a.Alive && _map.IsInFov(a.X, a.Y));
            if (danger || k.AnyKey) { _travel.Clear(); UpdateOverlays(); }
            else
            {
                _travelTimer -= elapsed;
                if (_travelTimer <= 0)
                {
                    _travelTimer = 0.07f;
                    var (nx, ny) = _travel.Dequeue();
                    PlayerMove(nx - _player.X, ny - _player.Y);
                }
                return;
            }
        }

        // keys: a step, and held keys repeat
        int dx = 0, dy = 0;
        bool any = false;
        void Pick(bool pressed, int ddx, int ddy) { if (pressed) { dx += ddx; dy += ddy; any = true; } }
        bool Held(params Keys[] keys) => k.AnyPressed(keys);
        Pick(Held(Keys.Left, Keys.A, Keys.NumPad4), -1, 0);
        Pick(Held(Keys.Right, Keys.D, Keys.NumPad6), 1, 0);
        Pick(Held(Keys.Up, Keys.W, Keys.NumPad8), 0, -1);
        Pick(Held(Keys.Down, Keys.S, Keys.NumPad2), 0, 1);
        Pick(Held(Keys.NumPad7, Keys.Q), -1, -1);
        Pick(Held(Keys.NumPad9, Keys.E), 1, -1);
        Pick(Held(Keys.NumPad1, Keys.Z), -1, 1);
        Pick(Held(Keys.NumPad3, Keys.C), 1, 1);
        dx = Math.Clamp(dx, -1, 1); dy = Math.Clamp(dy, -1, 1);

        bool fresh = k.AnyJustPressed(Keys.Left, Keys.A, Keys.NumPad4, Keys.Right, Keys.D, Keys.NumPad6, Keys.Up, Keys.W, Keys.NumPad8,
            Keys.Down, Keys.S, Keys.NumPad2, Keys.NumPad7, Keys.Q, Keys.NumPad9, Keys.E, Keys.NumPad1, Keys.Z, Keys.NumPad3, Keys.C);
        if (any)
        {
            _repeat -= elapsed;
            if (fresh) { _repeat = 0.28f; PlayerMove(dx, dy); }
            else if (_repeat <= 0) { _repeat = 0.09f; PlayerMove(dx, dy); }
        }
        else _repeat = 0;

        if (k.AnyJustPressed(Keys.Space, Keys.OemPeriod, Keys.NumPad5)) PlayerWait();
    }

    void UpdateHud()
    {
        _status.Text = $"HP {Math.Max(0, _player.Hp)}/{_player.MaxHp}   Depth {_depth}";
        _info.Text = $"Turn {_turn}   Kills {_kills}   {(_mapMode == 0 ? "BSP rooms" : "Cave")}\nFOV {Algorithms[_algo]}  radius {_radius}  walls {(_lightWalls ? "lit" : "dark")}";

        for (int i = 0; i < _log.Length; i++)
        {
            int index = _messages.Count - _log.Length + i;
            _log[i].Text = index >= 0 ? _messages[index] : "";
            float fade = 0.45f + 0.55f * (i + 1) / _log.Length;
            _log[i].Tint = Color.White * fade;
        }
    }
}
