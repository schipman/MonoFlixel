using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel.Demo.PostApoc;

/// <summary>
/// The road. A survivor walks right through rooms of city and seaside, picking up what lies about (food and drink into the bag, I),
/// shooting bandits and dogs with the pistol (Z), eating and drinking to stay alive (right click an item, or keys 1 to 5 for the quick
/// slots in the equipment window, U). At the end of a room the screen fades and the next one begins. P saves.
/// </summary>
public class PostApocPlayState : FlxTransitionableState
{
    const int Tile = 32;

    readonly bool _load, _newFemale;

    PostApocInput _input = null!;
    PostApocPlayer _player = null!;
    FlxTilemap _back = null!, _level = null!;
    readonly YSortedGroup _layer = new();
    readonly FlxTypedGroup<Bullet> _bullets = new(), _enemyBullets = new();
    FlxEmitter _fire = null!, _blood = null!;
    SlotWindow _hotbar = null!;
    FlxSprite _dim = null!;
    float _campLeft, _fireLeft;
    ItemDef? _campItem;
    FlxGroup _world = null!;
    SlotWindow _bag = null!, _equipment = null!;
    FlxText _room = null!, _gameOver = null!, _ammoText = null!;
    FlxTypeText _message = null!;
    const float HotbarX = 640 - 5 * 36 - 4;
    Biome _biome;

    // the open doors of the buildings (the black ones): up against one, UP slips inside for a rest
    const int OpenDoorTop = 29, OpenDoorBottom = 34;
    readonly List<Rectangle> _doors = new();
    readonly HashSet<int> _usedDoors = new();
    float _doorLeft, _doorHint;
    int _doorIndex = -1;
    int _roomNumber = 1;
    float _messageTime, _deadTime, _autosave;
    bool _changing;
    FlxSound? _wind;

    public PostApocPlayState(bool load, bool female)
    {
        TransIn = TransOut = null;          // no transition unless the survivor dies (see Die)
        _load = load;
        _newFemale = female;
    }

    // ------------------------------------------------------------------ building

    public override void Create()
    {
        FlxG.Game.Window.Title = "POST APOC";
        FlxG.Cameras.BgColor = Color.Black;
        FlxG.Mouse.Visible = true;
        FlxG.Mouse.LoadCursor("postapoc/cursor.png");
        SlotWindow.Take(null);

        _input = new PostApocInput();
        _player = new PostApocPlayer(Tile + 18, 272, _newFemale, _input);
        _player.Fired += OnFired;
        _player.SwingStarted += () => { if (_player.Ammo <= 0 && _messageTime <= 0.5f) Say("Out of ammo: swinging! Enemies drop more bullets.", 1.5f); };
        _player.Swung += OnSwing;

        // the picture, from back to front: the backdrop (moves slowly), the road, then everything standing on it
        _back = new FlxTilemap { ScrollFactor = new Vector2(0.2f, 0.2f) };
        _level = new FlxTilemap();
        _world = new FlxGroup();
        _world.Add(_back);
        _world.Add(_level);
        _world.Add(_layer);
        _world.Add(_bullets);
        _world.Add(_enemyBullets);
        Add(_world);

        for (int i = 0; i < 14; i++) _bullets.Add(new Bullet());
        for (int i = 0; i < 20; i++) _enemyBullets.Add(new Bullet(hostile: true));
        _layer.Add(_player);
        _layer.Add(_player.Gun);

        // saved game, or a new one
        _roomNumber = 1;
        if (_load && PostApocData.Load() is { } save)
        {
            _player.Health = Math.Clamp(save.Health, 1, PostApocPlayer.MaxHealth);
            _player.Hunger = save.Hunger;
            _player.Thirst = save.Thirst;
            _player.Fatigue = save.Fatigue;
            _player.Ammo = save.Ammo;
            _player.Stats.Distance = save.Distance;
            _player.Stats.Eaten = save.Eaten;
            _player.Stats.Drank = save.Drank;
            _player.Stats.Kills = save.Kills;
            _player.Stats.Rests = save.Rests;
            _player.SetFemale(save.Female);
            _roomNumber = Math.Max(1, save.Room);
        }
        else
        {
            PostApocData.ClearItems();
        }

        _biome = RandomBiome();
        LoadRoom(_biome);

        BuildHud();
        BuildWindows();

        FlxG.Camera.Follow(_player, 0.15f);
        FlxG.Camera.Bounds = _level.Bounds;

        _fire = MakeFire();
        _world.Add(_fire);
        _blood = MakeBlood();
        _world.Add(_blood);
        _player.Bled += Spray;

        _wind = FlxG.Sound.Load("postapoc/audio/wind_ambience.wav", 0.5f, looped: true);
        _wind.Persist = false;
        _wind.Play();

        FlxG.Camera.Fade(Color.Black, 0.8f, fadeIn: true);
        Say(_roomNumber == 1 && !_load ? "Arrows / WASD walk   Z shoot   I bag   U equipment   P save" : $"Mile {_roomNumber}", 4f);
        if (!_load || PostApocData.Load() == null) Save();
    }

    static Biome RandomBiome() => FlxG.Random.Bool() ? Biome.City : Biome.Seaside;

    void LoadRoom(Biome biome)
    {
        _biome = biome;
        string name = biome == Biome.City ? "city" : "seaside";
        string atlas = biome == Biome.City ? "blank_road.png" : "Seaside_tileset.png";

        string Path(string part) => FlxAssets.Get($"postapoc/maps/mapCSV_{name}_mile_1_{part}.csv");
        string tiles = FlxAssets.Get("postapoc/" + atlas);

        // new tilemaps take the places of the old ones
        var back = new FlxTilemap().LoadMapFromCsvFile(Path("backdrop"), tiles, Tile, Tile);
        back.ScrollFactor = new Vector2(0.2f, 0.2f);
        var level = new FlxTilemap().LoadMapFromCsvFile(Path("level"), tiles, Tile, Tile);
        _world.Members[_world.Members.IndexOf(_back)] = back;
        _world.Members[_world.Members.IndexOf(_level)] = level;
        _back = back;
        _level = level;

        // the things of the room, from the spawner tiles of the entity map
        for (int i = _layer.Members.Count - 1; i >= 0; i--)
            if (_layer.Members[i] != _player && _layer.Members[i] != _player.Gun) _layer.Members.RemoveAt(i);
        foreach (var b in _bullets.Members) b.Kill();

        var rows = File.ReadAllLines(Path("entities")).Where(l => l.Trim().Length > 0).ToArray();
        for (int ty = 0; ty < rows.Length; ty++)
        {
            var cells = rows[ty].Trim().TrimEnd(',').Split(',');
            for (int tx = 0; tx < cells.Length; tx++)
                if (cells[tx].Trim() == "1") Spawn(tx * Tile, ty * Tile);
        }

        FlxG.Camera.Bounds = _level.Bounds;

        // find the open doors: a door is a tile of the open-door picture (the upper half is enough)
        _doors.Clear();
        _usedDoors.Clear();
        for (int ty = 0; ty < _level.HeightInTiles; ty++)
            for (int tx = 0; tx < _level.WidthInTiles; tx++)
                if (_level.GetTile(tx, ty) == OpenDoorTop && _level.GetTile(tx, ty + 1) == OpenDoorBottom)
                    _doors.Add(new Rectangle(tx * Tile, ty * Tile, Tile, Tile * 2));
    }

    /// <summary>One spawner tile: nothing, something to pick up, a bandit or a dog.</summary>
    void Spawn(float x, float y)
    {
        if (x < 320) return;                              // keep the start clear
        float feet = Math.Clamp(y + 8, Road.MinY, Road.MaxY);
        float roll = FlxG.Random.Float();

        if (roll < 0.10f)
        {
            // something on the ground: now and then a few bullets, mostly what the road leaves
            if (FlxG.Random.Bool(8)) _layer.Add(new AmmoPickup(x, feet, FlxG.Random.Int(3, 4)));
            else _layer.Add(new ItemPickup(x, feet, PostApocData.RandomItem()));
        }
        else if (x < 480) return;                         // ... and more so for enemies: they notice you from far off
        else if (roll < 0.19f) _layer.Add(new PostApocEnemy(x, feet, EnemyKind.Human, _player, OnEnemyDied));
        else if (roll < 0.28f) _layer.Add(new PostApocEnemy(x, feet, EnemyKind.Dog, _player, OnEnemyDied));
        else if (roll < 0.33f) _layer.Add(new PostApocEnemy(x, feet, EnemyKind.Gunman, _player, OnEnemyDied, OnEnemyShoots));
    }

    void OnEnemyDied(PostApocEnemy enemy)
    {
        _player.Stats.Kills++;
        // the ones with guns leave bullets (that is where you get more), now and then something else
        var (chance, count) = enemy.AmmoDrop;
        if (count > 0 && FlxG.Random.Bool(chance * 100))
            _layer.Add(new AmmoPickup(enemy.X + 4, enemy.Y, count));
        if (FlxG.Random.Bool(20))
            _layer.Add(new ItemPickup(enemy.X + 16, enemy.Y, PostApocData.RandomItem()));
    }

    void OnEnemyShoots(Vector2 from, int direction, float lane)
    {
        var bullet = _enemyBullets.Members.FirstOrDefault(b => !b.Exists);
        bullet?.Shoot(from, direction, lane, 300f);
    }

    /// <summary>The survivor's swing (with no ammo): hits whatever is close in front, in the same lane.</summary>
    void OnSwing(Vector2 at, int direction)
    {
        foreach (var m in _layer.Members.ToArray())
        {
            if (m is not PostApocEnemy e || e.IsDead) continue;
            float dx = (e.X + e.Width * 0.5f - _player.X - _player.Width * 0.5f) * direction;       // how far in front
            if (dx > -6 && dx < 52 && MathF.Abs(e.Y - _player.Y) < 16)
            {
                e.Shot(15, direction);
                Spray(new Vector2(at.X, e.Y - (e.Kind == EnemyKind.Dog ? 14 : 30)), direction);
            }
        }
    }

    void OnFired(Vector2 from, int direction)
    {
        var bullet = _bullets.Members.FirstOrDefault(b => !b.Exists);
        bullet?.Shoot(from, direction, _player.Y);
    }

    /// <summary>Blood: a few small red specks that fly out and fall.</summary>
    static FlxEmitter MakeBlood()
    {
        const int count = 90;
        var blood = new FlxEmitter(0, 0, count);
        blood.Acceleration.Set(0, 380, 0, 380);
        blood.Lifespan.Set(0.35f, 0.8f);
        for (int i = 0; i < count; i++)
        {
            var p = new FlxParticle();
            int size = FlxG.Random.Int(2, 3);
            p.MakeGraphic(size, size, FlxG.Random.Bool(30) ? new Color(200, 30, 28) : new Color(140, 12, 14));
            blood.Add(p);
        }
        return blood;
    }

    /// <summary>A small spray of blood at a hit. <paramref name="direction"/> is the way the blow was going (0: all around).</summary>
    void Spray(Vector2 at, int direction)
    {
        _blood.X = at.X;
        _blood.Y = at.Y;
        if (direction == 0) _blood.Velocity.Set(-70, -110, 70, -20);
        else if (direction > 0) _blood.Velocity.Set(20, -100, 120, -10);
        else _blood.Velocity.Set(-120, -100, -20, -10);
        _blood.Start(explode: true, quantity: 9);
    }

    /// <summary>The flames of a camp: soft orange dots that rise, added together.</summary>
    static FlxEmitter MakeFire()
    {
        const int size = 12, count = 50;
        var dot = new Texture2D(FlxG.GraphicsDevice, size, size);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = MathF.Sqrt((x - 5.5f) * (x - 5.5f) + (y - 5.5f) * (y - 5.5f)) / 6f;
                float a = Math.Clamp(1f - d, 0f, 1f);
                px[y * size + x] = new Color(255, 255, 255, (int)(255 * a * a));
            }
        dot.SetData(px);

        var fire = new FlxEmitter(0, 0, count) { Width = 10 };
        fire.Velocity.Set(-8, -45, 8, -20);
        fire.Lifespan.Set(0.4f, 0.9f);
        for (int i = 0; i < count; i++)
        {
            var p = new FlxParticle();
            p.LoadGraphic(dot);
            p.Blend = FlxBlend.Add;
            p.Tint = FlxG.Random.Bool(40) ? new Color(255, 200, 70) : new Color(255, 110, 30);
            float scale = FlxG.Random.Float(0.5f, 1.2f);
            p.Scale = new Vector2(scale, scale);
            fire.Add(p);
        }
        return fire;
    }

    // ------------------------------------------------------------------ the screen furniture

    void BuildHud()
    {
        var hud = new FlxGroup();

        // the bottom 40 pixels (the map is 320 high) are the status strip
        // the dark when resting at a camp
        _dim = new FlxSprite(0, 0).MakeGraphic(FlxG.Width, 320, new Color(4, 6, 20));
        _dim.ScrollFactor = Vector2.Zero;
        _dim.Alpha = 0;
        _dim.DrawAfterPostProcess = true;
        Add(_dim);

        var strip = new FlxSprite(0, 320).MakeGraphic(FlxG.Width, 40, new Color(12, 10, 10));
        strip.ScrollFactor = Vector2.Zero;
        hud.Add(strip);

        FlxBar Bar(float x, float y, Func<float> value, float max, Color fill, string label)
        {
            var text = new FlxText(x, y - 1, 0, label) { Tint = new Color(200, 190, 170) };
            text.ScrollFactor = Vector2.Zero;
            hud.Add(text);
            var bar = new FlxBar(x + 52, y, FlxBarFillDirection.LeftToRight, 110, 8, value, 0, max, showBorder: true)
                .CreateFilledBar(new Color(40, 34, 30), fill, true, new Color(110, 100, 85));
            bar.ScrollFactor = Vector2.Zero;
            hud.Add(bar);
            return bar;
        }

        Bar(8, 326, () => _player.Health, PostApocPlayer.MaxHealth, new Color(190, 50, 40), "Health");
        Bar(8, 340, () => _player.Hunger, PostApocPlayer.MaxHunger, new Color(210, 140, 40), "Hunger");
        Bar(178, 326, () => _player.Fatigue, PostApocPlayer.MaxFatigue, new Color(150, 90, 190), "Fatigue");
        Bar(178, 340, () => _player.Thirst, PostApocPlayer.MaxThirst, new Color(70, 140, 210), "Thirst");

        _room = new FlxText(350, 326, 100, "") { Tint = new Color(220, 205, 170) };
        _room.ScrollFactor = Vector2.Zero;
        hud.Add(_room);
        _ammoText = new FlxText(350, 340, 100, "") { Tint = new Color(230, 190, 80) };
        _ammoText.ScrollFactor = Vector2.Zero;
        hud.Add(_ammoText);

        // the quick-use slots (keys 1 to 5): the frames and numbers here, the slots themselves are a window (see BuildWindows)
        for (int i = 0; i < PostApocData.QuickSlots; i++)
        {
            float x = HotbarX + i * 36;
            var frame = new FlxSprite(x, 324).MakeGraphic(32, 32, new Color(40, 34, 30));
            frame.ScrollFactor = Vector2.Zero;
            hud.Add(frame);
            var key = new FlxText(x + 2, 324, 0, (i + 1).ToString()) { Tint = new Color(150, 140, 110) };
            key.ScrollFactor = Vector2.Zero;
            hud.Add(key);
        }

        _message = new FlxTypeText(0, 12, FlxG.Width, "") { Tint = new Color(255, 230, 140), UseDefaultSound = true, DefaultSoundVolume = 0.12f, Delay = 0.014f };
        _message.ScrollFactor = Vector2.Zero;
        hud.Add(_message);

        _gameOver = new FlxText(0, 120, FlxG.Width, "") { Alignment = FlxTextAlign.Center, Tint = new Color(220, 70, 60) };
        _gameOver.ScrollFactor = Vector2.Zero;
        hud.Add(_gameOver);

        foreach (var m in hud.Members) m.DrawAfterPostProcess = true;
        Add(hud);
    }

    void BuildWindows()
    {
        // the bag: a 4 x 4 grid and a drop slot
        var bagSlots = new List<SlotSpec>();
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                bagSlots.Add(new SlotSpec(14 + c * 36, 30 + r * 36));
        _bag = new SlotWindow(60, 30, SlotWindow.MakePanel(172, 224), "BAG", PostApocData.Bag, bagSlots,
            accepts: (_, item) => true, use: UseItem, drop: DropItem, dropSlot: new SlotSpec(122, 180), closeAt: new Point(172 - 26, 6));

        // equipment: five quick-use slots work; weapon and armor slots are there (as in the original) but nothing fits yet
        var specs = new List<SlotSpec>();
        for (int i = 0; i < 5; i++) specs.Add(new SlotSpec(20, 48 + i * 44, true, ""));
        specs.Add(new SlotSpec(96, 92, false, "melee"));
        specs.Add(new SlotSpec(96, 180, false, "ranged"));
        string[] armor = { "head", "body", "hands", "legs", "feet" };
        for (int i = 0; i < 5; i++) specs.Add(new SlotSpec(172, 48 + i * 44, false, armor[i]));

        _equipment = new SlotWindow(260, 30, SlotWindow.MakePanel(232, 284), "EQUIPMENT    quick use: keys 1-5", PostApocData.Equipment, specs,
            accepts: (i, item) => i < PostApocData.QuickSlots && item.Usable, use: UseItem);

        // the hotbar: the first five equipment slots, always on the screen: drag food and drink onto them, then press 1 to 5
        var hot = new List<SlotSpec>();
        for (int i = 0; i < PostApocData.QuickSlots; i++) hot.Add(new SlotSpec((int)(i * 36), 0));
        _hotbar = new SlotWindow(HotbarX, 324, null, "", PostApocData.Equipment, hot,
            accepts: (i, item) => item.Usable, use: UseItem);
        _hotbar.SetOpen(true);

        Add(_bag.Root);
        Add(_equipment.Root);
        Add(_hotbar.Root);
    }

    /// <summary>Uses an item. Returns false if it was refused (no camp with enemies near), so it is not used up.</summary>
    bool UseItem(ItemDef item)
    {
        if (_player.IsDead || _player.Resting) return false;

        if (item.Camp)
        {
            // a camp needs a quiet spot: nothing alive and awake nearby
            bool danger = _layer.Members.OfType<PostApocEnemy>().Any(e => !e.IsDead && MathF.Abs(e.Midpoint.X - _player.Midpoint.X) < e.Aggro + 60);
            if (danger) { Say("Too dangerous to rest here.", 2f); return false; }
            StartCamp(item);
            return true;
        }

        _player.Use(item);
        if (item.Hunger < 0 || item.Health > 0 && item.Thirst >= 0) _player.Stats.Eaten++;
        if (item.Thirst < 0) _player.Stats.Drank++;
        Say(item.Name + (item.Hunger < 0 ? ": less hungry" : item.Thirst < 0 ? ": less thirsty" : item.Health > 0 ? ": better" : ""));
        return true;
    }

    /// <summary>Making camp: a fire, the screen goes dark for a few seconds, and the survivor is a lot less tired (but hungrier and thirstier).</summary>
    void StartCamp(ItemDef wood)
    {
        _player.Resting = true;
        _player.Velocity = Vector2.Zero;
        _campItem = wood;
        _campLeft = 4f;
        _fireLeft = 14f;

        _fire.X = _player.X + (_player.Facing_Left ? -26 : 34);
        _fire.Y = _player.Y + 4;
        _fire.Start(explode: false, frequency: 0.03f);
        FlxTween.Tween(_dim, new { Alpha = 0.55f }, 1f);
        Say("Making camp...", 3.5f);
        PostApocData.Sfx("pickup", 0.4f);
    }

    /// <summary>
    /// Up against the wall in front of an open door (at the top edge of the road), UP slips the survivor inside: gone for a second,
    /// resting, then back out. Each door is good for one rest.
    /// </summary>
    void UpdateDoors(float elapsed)
    {
        if (_doorLeft > 0)
        {
            _doorLeft -= elapsed;
            if (_doorLeft <= 0) Emerge();
            return;
        }
        if (_player.IsDead || _player.Resting || !_player.Control) return;

        // which door are we at?
        int at = -1;
        for (int i = 0; i < _doors.Count; i++)
            if (MathF.Abs(_player.Midpoint.X - (_doors[i].X + _doors[i].Width / 2f)) < 22) { at = i; break; }
        if (at < 0 || _player.Y > Road.MinY + 3) return;

        bool used = _usedDoors.Contains(_doors[at].X);
        _doorHint -= elapsed;
        if (_input.Move.Y < -0.5f)
        {
            if (used) { if (_doorHint <= 0) { Say("You already rested here.", 1.5f); _doorHint = 1.5f; } }
            else EnterDoor(at);
        }
        else if (!used && _messageTime <= 0.4f) Say("UP: rest inside", 0.5f);
    }

    void EnterDoor(int index)
    {
        _doorIndex = index;
        _usedDoors.Add(_doors[index].X);
        _doorLeft = 1.4f;
        _player.Control = false;
        _player.Hidden = true;
        _player.Velocity = Vector2.Zero;
        _player.Gun.Visible = false;
        _player.X = _doors[index].X + _doors[index].Width / 2f - _player.Width / 2f;      // right in front of the door
        FlxTween.Tween(_player, new { Alpha = 0f }, 0.25f);
        PostApocData.Sfx("pickup", 0.3f);
    }

    void Emerge()
    {
        FlxTween.Tween(_player, new { Alpha = 1f }, 0.25f);
        _player.Hidden = false;
        _player.Control = true;
        _player.Fatigue = Math.Max(0, _player.Fatigue - 25);
        _player.Health = PostApocPlayer.MaxHealth;
        _player.Stats.Rests++;
        _player.Hunger = Math.Min(PostApocPlayer.MaxHunger, _player.Hunger + 2);
        _player.Thirst = Math.Min(PostApocPlayer.MaxThirst, _player.Thirst + 3);
        // sometimes there is something useful in the building
        if (FlxG.Random.Bool(70))
        {
            int found = FlxG.Random.Int(4, 6);
            _player.Ammo += found;
            Say($"You rested inside: health restored, and found {found} bullets.", 3.5f);
        }
        else Say("You rested inside. Health restored; less tired.", 3f);
        PostApocData.Sfx("pickup", 0.3f);
        _doorIndex = -1;
    }

    void UpdateCamp(float elapsed)
    {
        if (_campLeft > 0)
        {
            _campLeft -= elapsed;
            if (_campLeft <= 0 && _campItem != null)
            {
                // the rest is done
                _player.Use(_campItem);
                _player.Health = PostApocPlayer.MaxHealth;
                _player.Stats.Rests++;
                _player.Hunger = Math.Min(PostApocPlayer.MaxHunger, _player.Hunger + 8);
                _player.Thirst = Math.Min(PostApocPlayer.MaxThirst, _player.Thirst + 12);
                _player.Resting = false;
                _campItem = null;
                FlxTween.Tween(_dim, new { Alpha = 0f }, 1f);
                Say("You rested. Health restored; less tired, hungrier and thirstier.", 3.5f);
                Save();
            }
        }

        // the fire burns for a while after
        if (_fireLeft > 0)
        {
            _fireLeft -= elapsed;
            if (_fireLeft <= 0) _fire.Emitting = false;
        }
    }

    void DropItem(ItemDef item)
    {
        float feet = Math.Clamp(_player.Y + 4, Road.MinY, Road.MaxY);
        _layer.Add(new ItemPickup(_player.X + (_player.Facing_Left ? -36 : 40), feet, item));
    }

    void Say(string text, float seconds = 2.5f)
    {
        // the message types itself out
        if (text == _message.FinalText && _messageTime > 0) { _messageTime = seconds; return; }       // the same line again: just keep it up
        _message.ResetText(text);
        _message.Start();
        // typed text is left aligned (centered text would slide about as letters are added): put the box where the finished line is centered
        float width = 0;
        foreach (var line in _message.FinalText.Split('\n')) width = Math.Max(width, _message.MeasureLine(line));
        _message.X = (FlxG.Width - width) / 2f;
        _messageTime = seconds + text.Length * 0.014f;
    }

    // ------------------------------------------------------------------ saving

    void Save()
    {
        PostApocData.Save(new PostApocData.SaveData
        {
            Health = _player.Health,
            Hunger = (int)_player.Hunger,
            Thirst = (int)_player.Thirst,
            Fatigue = (int)_player.Fatigue,
            Ammo = _player.Ammo,
            Distance = _player.Stats.Distance,
            Eaten = _player.Stats.Eaten,
            Drank = _player.Stats.Drank,
            Kills = _player.Stats.Kills,
            Rests = _player.Stats.Rests,
            Female = _player.Female,
            Room = _roomNumber
        });
    }

    // ------------------------------------------------------------------ every frame

    public override void Update(float elapsed)
    {
        // the player cannot walk while the bag is open... the original let you; keeping that (no pause) so the road stays dangerous
        HandleControls();

        base.Update(elapsed);

        // ---- what lies about, and what flies
        foreach (var b in _bullets.Members)
        {
            if (!b.Exists) continue;
            foreach (var m in _layer.Members)
            {
                if (m is not PostApocEnemy e || e.IsDead) continue;
                if (MathF.Abs(e.Y - b.Lane) < 14 && b.X + 7 > e.X - 6 && b.X < e.X + e.Width + 6)
                {
                    int dir = b.Velocity.X < 0 ? -1 : 1;
                    e.Shot(20, dir);
                    Spray(new Vector2(b.X, e.Y - (e.Kind == EnemyKind.Dog ? 14 : 30)), dir);
                    b.Kill();
                    break;
                }
            }
        }

        // what the raiders shoot: it hurts if the survivor is in its lane
        foreach (var b in _enemyBullets.Members)
        {
            if (!b.Exists || _player.IsDead) continue;
            if (MathF.Abs(_player.Y - b.Lane) < 12 && b.X + 7 > _player.X - 4 && b.X < _player.X + _player.Width + 4)
            {
                _player.Hurt(10, "You were shot.", b.Velocity.X < 0 ? -1 : 1);
                b.Kill();
            }
        }

        foreach (var m in _layer.Members.ToArray())
        {
            if (m is AmmoPickup a && a.Exists && !_player.IsDead &&
                MathF.Abs(a.Midpoint.X - _player.Midpoint.X) < 26 && MathF.Abs(a.Y - _player.Y) < 16)
            {
                _player.Ammo += a.Count;
                a.Kill();
                _layer.Remove(a);
                PostApocData.Sfx("pickup", 0.6f);
                Say($"+{a.Count} bullets", 1.2f);
                continue;
            }

            if (m is not ItemPickup p || !p.Exists) continue;
            if (MathF.Abs(p.Midpoint.X - _player.Midpoint.X) < 26 && MathF.Abs(p.Y - _player.Y) < 16 && !_player.IsDead)
            {
                if (PostApocData.AddToBag(p.Def))
                {
                    p.Kill();
                    _layer.Remove(p);
                    PostApocData.Sfx("pickup", 0.6f);
                    Say("Picked up " + p.Def.Name, 1.2f);
                }
                else Say("The bag is full (I)", 1.2f);
            }
        }

        // ---- the end of the room
        if (!_changing && !_player.IsDead && _player.X > _level.Width - 4)
        {
            _changing = true;
            _player.Control = false;
            _player.Velocity = Vector2.Zero;
            FlxG.Camera.Fade(Color.Black, 0.5f, onComplete: NextRoom);
        }

        // ---- the screen
        _room.Text = $"Mile {_roomNumber}  {(_biome == Biome.City ? "City" : "Seaside")}";
        _ammoText.Text = $"Ammo {_player.Ammo}";
        UpdateCamp(elapsed);
        UpdateDoors(elapsed);
        if (_player.Fatigue >= 80 && _player.Fatigue < 100 && _messageTime <= 0) Say("You are exhausted: make camp with firewood!", 2f);
        _hotbar.Update();
        _bag.Update();
        _equipment.Update();

        if (_messageTime > 0 && (_messageTime -= elapsed) <= 0) { _message.ResetText(""); _message.Text = ""; }

        // ---- the end of the survivor
        if (_player.IsDead)
        {
            _deadTime += elapsed;
            if (_deadTime > 1.8f && !_ending) EndRun();
        }

        _autosave += elapsed;
        if (_autosave > 30f && !_player.IsDead) { _autosave = 0; Save(); }
    }

    void HandleControls()
    {
        if (_input.Inventory.Triggered) { _bag.Toggle(); DrownLikeSound(_bag.Open); }
        if (_input.Equipment.Triggered) { _equipment.Toggle(); DrownLikeSound(_equipment.Open); }

        for (int i = 0; i < _input.Quick.Length; i++)
            if (_input.Quick[i].Triggered && PostApocData.Equipment[i] is { Usable: true } item && UseItem(item))
                PostApocData.Equipment[i] = null;

        if (_input.Save.Triggered && !_player.IsDead) { Save(); Say("Game saved."); }

        if (_input.Back.Triggered)
        {
            if (SlotWindow.Held != null) { /* an item on the mouse: put it back first */ var held = SlotWindow.Held; if (PostApocData.AddToBag(held)) SlotWindow.Take(null); }
            else if (_bag.Open || _equipment.Open) { _bag.SetOpen(false); _equipment.SetOpen(false); }
            else
            {
                if (!_player.IsDead) Save();
                Leave(new PostApocMenuState());
            }
        }
    }

    static void DrownLikeSound(bool open) => PostApocData.Sfx("pickup", open ? 0.3f : 0.2f);

    void NextRoom()
    {
        _fire.Emitting = false;
        _doorLeft = 0;
        _player.Hidden = false;
        _player.Alpha = 1f;
        _roomNumber++;
        LoadRoom(RandomBiome());
        _player.ResetPosition(Tile + 18, 272);
        FlxG.Camera.Scroll = Vector2.Zero;
        Save();
        _changing = false;
        _player.Control = true;
        FlxG.Camera.Fade(Color.Black, 0.5f, fadeIn: true);       // the fade out holds the screen black until a fade in replaces it
        Say($"Mile {_roomNumber}", 2f);
    }

    bool _ending;

    /// <summary>The run is over: the save goes, and tiles of dark red close over the screen before the game over screen.</summary>
    void EndRun()
    {
        _ending = true;
        PostApocData.EraseSave();
        TransOut = new TransitionData(TransitionType.Tiles, new Color(110, 8, 8), 1.1f, new Vector2(1, 1), new TransitionTileData(FlxTransitionAssets.Diamond, 32, 32));
        Leave(new PostApocGameOverState(_player.Stats, _player.Cause, _player.Female));
    }

    void Leave(FlxState next)
    {
        _wind?.Stop();
        SlotWindow.Take(null);
        FlxG.Mouse.ResetCursor();
        FlxG.Camera.Bounds = null;
        FlxG.SwitchState(next, PostApocMenuState.Width, PostApocMenuState.Height, PostApocMenuState.Zoom);
    }
}
