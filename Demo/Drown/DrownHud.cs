using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel.Demo.Drown;

/// <summary>
/// The screen furniture of a dive: air, suit and depth read-outs, the depth meter, the tool, a message line, the animated cursor, and the
/// inventory with its crafting window. Everything here is drawn after the post-process shaders so the water wobble doesn't touch it.
/// The windows are plain sprites; hovering and clicking go through FlxMouseEvent.
/// </summary>
public sealed class DrownHud
{
    const int InvX = 10, InvY = 110, InvRows = 10, CraftRows = 5;

    readonly DrownDiveState _world;
    readonly DrownPlayer _player;
    public readonly FlxGroup Root = new();

    readonly FlxText _oxygen, _health, _depth, _tool, _weight, _recipeHint;
    readonly FlxTypeText _message;
    readonly FlxSprite _meterTicker, _cursor;
    float _messageTime, _cursorAngle;
    readonly int _mapRows;

    // the windows
    readonly List<FlxBasic> _inventoryParts = new(), _craftParts = new();
    readonly List<Row> _invRows = new(), _craftRows = new();
    Row _resultRow = null!;
    FlxSprite _craftToggle = null!, _scrollUp = null!, _scrollDown = null!;
    int _scroll;

    public bool InventoryOpen { get; private set; }
    public bool CraftOpen { get; private set; }

    public DrownHud(DrownDiveState world, DrownPlayer player, int mapRows)
    {
        _world = world;
        _player = player;
        _mapRows = mapRows;

        // ---- read-outs
        FlxText Label(float x, float y, string text, Color? color = null)
        {
            var t = new FlxText(x, y, 0, text) { Tint = color ?? Color.White };
            Root.Add(t);
            return t;
        }

        Label(12, 8, "Oxygen:");
        _oxygen = Label(100, 8, "");
        Root.Add(new FlxBar(12, 20, FlxBarFillDirection.LeftToRight, 130, 6, () => _player.Oxygen, 0, DrownPlayer.MaxOxygen, showBorder: true)
            .CreateFilledBar(new Color(10, 30, 50), new Color(90, 190, 255), true, new Color(120, 160, 190)));

        Label(12, 32, "Suit integrity:");
        _health = Label(100 + 40, 32, "");
        Root.Add(new FlxBar(12, 44, FlxBarFillDirection.LeftToRight, 130, 6, () => _player.Health, 0, DrownPlayer.MaxHealth, showBorder: true)
            .CreateFilledBar(new Color(50, 10, 10), new Color(230, 90, 70), true, new Color(190, 140, 130)));

        Label(12, 56, "Depth:");
        _depth = Label(100, 56, "");

        _tool = Label(12, FlxG.Height - 22, "");
        _weight = Label(12, FlxG.Height - 12, "", new Color(150, 190, 210));
        _message = new FlxTypeText(0, FlxG.Height - 40, FlxG.Width, "") { Tint = new Color(255, 230, 140), UseDefaultSound = true, DefaultSoundVolume = 0.1f, Delay = 0.012f };
        Root.Add(_message);
        _recipeHint = new FlxText(0, 0, 0, "") { Tint = new Color(200, 210, 220) };

        // ---- depth meter (the bottom of the picture is the "DEPTH" label, so it sits a bit higher than the original)
        float meterX = FlxG.Width - 90, meterY = FlxG.Height - 290;
        var meter = new FlxSprite(meterX, meterY).LoadGraphic("drown/depth_meter.png");
        meter.ScrollFactor = Vector2.Zero;
        Root.Add(meter);
        _meterTicker = new FlxSprite(meterX + 35, meterY + 2).LoadGraphic("drown/depth_ticker.png");
        _meterTicker.ScrollFactor = Vector2.Zero;
        Root.Add(_meterTicker);

        BuildInventory();

        // ---- the cursor spins around the mouse
        _cursor = new FlxSprite().LoadGraphic("drown/cursor_anim.png", animated: true, frameWidth: 30, frameHeight: 30);
        _cursor.Animation.Add("spin", FlxAnimationController.Range(0, 19), 20);
        _cursor.Animation.Play("spin");
        _cursor.ScrollFactor = Vector2.Zero;
        Root.Add(_cursor);

        // everything here stays where it is on the screen, whatever the camera does
        foreach (var m in Root.Members) if (m is FlxObject o) o.ScrollFactor = Vector2.Zero;
        MarkOverlay(Root);
        SetInventory(false);
    }

    static void MarkOverlay(FlxBasic b) => b.DrawAfterPostProcess = true;

    public void Say(string text, float seconds = 2.5f)
    {
        // typed out; the same line again is just kept up
        if (text == _message.FinalText && _messageTime > 0) { _messageTime = seconds; return; }
        _message.ResetText(text);
        _message.Start();
        // typed text is left aligned (centered text would slide about as letters are added): put the box where the finished line is centered
        float width = 0;
        foreach (var line in _message.FinalText.Split('\n')) width = Math.Max(width, _message.MeasureLine(line));
        _message.X = (FlxG.Width - width) / 2f;
        _messageTime = seconds + text.Length * 0.012f;
    }

    // ------------------------------------------------------------------ the inventory window

    sealed class Row
    {
        public readonly FlxSprite Bg, Icon;
        public readonly FlxText Name, Count, Weight;
        public ItemKind? Kind;
        public int Amount;
        public bool Hover;

        public Row(float x, float y, List<FlxBasic> parts, FlxGroup root)
        {
            Bg = new FlxSprite(x, y).LoadGraphic("drown/GUI/inventoryElement.png");
            Icon = new FlxSprite(x + 5, y + 4).LoadGraphic("drown/GUI/null.png");
            Name = new FlxText(x + 28, y + 8, 100, "Empty");
            Count = new FlxText(x + 128, y + 8, 30, "");
            Weight = new FlxText(x + 158, y + 8, 30, "");
            foreach (FlxBasic p in new FlxBasic[] { Bg, Icon, Name, Count, Weight })
            {
                if (p is FlxObject o) o.ScrollFactor = Vector2.Zero;
                parts.Add(p);
                root.Add(p);
                p.DrawAfterPostProcess = true;
            }
        }

        public void Show(ItemKind? kind, int amount)
        {
            Kind = kind;
            Amount = amount;
            if (kind is { } k)
            {
                var info = DrownData.Items[k];
                Name.Text = info.Name.Replace(" Shard", "");
                Count.Text = "x" + amount;
                Weight.Text = (info.Weight * amount).ToString("0.0");
                Icon.LoadGraphic(info.Icon);
            }
            else
            {
                Name.Text = "Empty";
                Count.Text = Weight.Text = "";
                Icon.LoadGraphic("drown/GUI/null.png");
            }
        }

        public void SetHover(bool hover)
        {
            if (Hover == hover) return;
            Hover = hover;
            Bg.LoadGraphic(hover ? "drown/GUI/inventoryHighlight.png" : "drown/GUI/inventoryElement.png");
        }
    }

    void BuildInventory()
    {
        FlxSprite Panel(string image, float x, float y, List<FlxBasic> parts)
        {
            var s = new FlxSprite(x, y).LoadGraphic(image);
            s.ScrollFactor = Vector2.Zero;
            s.DrawAfterPostProcess = true;
            parts.Add(s);
            Root.Add(s);
            return s;
        }

        Panel("drown/GUI/inventory_basic.png", InvX, InvY, _inventoryParts);
        Panel("drown/GUI/crafting_basic.png", InvX + 221, InvY, _craftParts);

        _craftToggle = new FlxSprite(InvX + 184, InvY - 1).LoadGraphic("drown/GUI/craft_button.png");
        _craftToggle.ScrollFactor = Vector2.Zero;
        _craftToggle.DrawAfterPostProcess = true;
        _inventoryParts.Add(_craftToggle);
        Root.Add(_craftToggle);
        FlxMouseEvent.Add(_craftToggle,
            onMouseOver: _ => { DrownData.Sfx("drown_highlight", 0.6f); _craftToggle.LoadGraphic("drown/GUI/craft_button_highlight.png"); },
            onMouseOut: _ => _craftToggle.LoadGraphic(CraftOpen ? "drown/GUI/craft_button_highlight.png" : "drown/GUI/craft_button.png"));
        FlxMouseEvent.SetClickCallback(_craftToggle, _ => SetCraft(!CraftOpen));

        _scrollUp = new FlxSprite(InvX + 118, InvY + 26).LoadGraphic("drown/GUI/up_button.png");
        _scrollDown = new FlxSprite(InvX + 118, InvY + 311).LoadGraphic("drown/GUI/down_button.png");
        foreach (var b in new[] { _scrollUp, _scrollDown })
        {
            b.ScrollFactor = Vector2.Zero;
            b.DrawAfterPostProcess = true;
            _inventoryParts.Add(b);
            Root.Add(b);
        }
        FlxMouseEvent.Add(_scrollUp, onMouseOver: _ => _scrollUp.LoadGraphic("drown/GUI/up_button_hl.png"), onMouseOut: _ => _scrollUp.LoadGraphic("drown/GUI/up_button.png"));
        FlxMouseEvent.SetClickCallback(_scrollUp, _ => { _scroll = Math.Max(0, _scroll - 1); DrownData.Sfx("drown_click", 0.6f); });
        FlxMouseEvent.Add(_scrollDown, onMouseOver: _ => _scrollDown.LoadGraphic("drown/GUI/down_button_hl.png"), onMouseOut: _ => _scrollDown.LoadGraphic("drown/GUI/down_button.png"));
        FlxMouseEvent.SetClickCallback(_scrollDown, _ => { _scroll++; DrownData.Sfx("drown_click", 0.6f); });

        for (int i = 0; i < InvRows; i++)
        {
            var row = new Row(InvX + 32, InvY + 46 + i * 26, _inventoryParts, Root);
            _invRows.Add(row);
            WireRow(row, RowOwner.Bag);
        }

        for (int i = 0; i < CraftRows; i++)
        {
            var row = new Row(InvX + 258, InvY + 39 + i * 26, _craftParts, Root);
            _craftRows.Add(row);
            WireRow(row, RowOwner.Components);
        }

        _resultRow = new Row(InvX + 259, InvY + 211, _craftParts, Root);
        WireRow(_resultRow, RowOwner.Result);

        var weightLabel = new FlxText(InvX + 176, InvY + 336, 60, "") { Tint = Color.White };
        weightLabel.ScrollFactor = Vector2.Zero;
        _inventoryParts.Add(weightLabel);
        Root.Add(weightLabel);
        _weightLabel = weightLabel;
    }

    FlxText _weightLabel = null!;

    enum RowOwner { Bag, Components, Result }

    void WireRow(Row row, RowOwner owner)
    {
        FlxMouseEvent.Add(row.Bg,
            onMouseOver: _ =>
            {
                row.SetHover(true);
                if (row.Kind != null) DrownData.Sfx("drown_highlight", 0.5f);
                ShowHint(row);
            },
            onMouseOut: _ => { row.SetHover(false); _recipeHint.Text = ""; },
            mouseButtons: new[] { FlxMouseButtonID.Left, FlxMouseButtonID.Right });
        FlxMouseEvent.SetDownCallback(row.Bg, _ => ClickRow(row, owner));
    }

    void ShowHint(Row row)
    {
        if (row.Kind is { } k) Say(DrownData.Items[k].Description, 4f);
    }

    void ClickRow(Row row, RowOwner owner)
    {
        if (row.Kind is not { } kind) return;

        bool right = FlxMouseEvent.CurrentButton == FlxMouseButtonID.Right;
        bool shift = FlxG.Keys.AnyPressed(Microsoft.Xna.Framework.Input.Keys.LeftShift, Microsoft.Xna.Framework.Input.Keys.RightShift);

        if (owner == RowOwner.Result)
        {
            if (DrownData.Craftable() is { } recipe && DrownData.Craft(recipe)) DrownData.Sfx("drown_click");
            else DrownData.Sfx("drown_negative");
            return;
        }

        var from = owner == RowOwner.Bag ? DrownData.Bag : DrownData.Components;

        if (right)
        {
            from.Remove(kind);                       // throw one away
            DrownData.Sfx("drown_click", 0.6f);
            return;
        }

        if (owner == RowOwner.Bag && !CraftOpen)
        {
            if (DrownData.Items[kind].Usable) _world.UseLantern();
            else DrownData.Sfx("drown_negative");
            return;
        }

        // with the crafting window open, clicking moves items between the bag and the components (shift moves the whole stack)
        var to = owner == RowOwner.Bag ? DrownData.Components : DrownData.Bag;
        int n = shift ? row.Amount : 1;
        int moved = to.Add(kind, n);
        if (moved > 0) { from.Remove(kind, moved); DrownData.Sfx("drown_click"); }
        else DrownData.Sfx("drown_negative");
    }

    // ------------------------------------------------------------------ opening and closing

    public void ToggleInventory() => SetInventory(!InventoryOpen);

    public void SetInventory(bool open)
    {
        InventoryOpen = open;
        if (!open) CraftOpen = false;
        _player.MenuOpen = open;
        UpdateVisibility();
    }

    public void SetCraft(bool open)
    {
        if (CraftOpen == open) return;
        CraftOpen = open;
        DrownData.Sfx(open ? "drown_invopen" : "drown_invclose", 0.7f);
        _craftToggle.LoadGraphic(open ? "drown/GUI/craft_button_highlight.png" : "drown/GUI/craft_button.png");
        UpdateVisibility();
    }

    void UpdateVisibility()
    {
        foreach (var p in _inventoryParts) p.Visible = InventoryOpen;
        foreach (var p in _craftParts) p.Visible = InventoryOpen && CraftOpen;
        if (!InventoryOpen) { foreach (var r in _invRows) r.SetHover(false); foreach (var r in _craftRows) r.SetHover(false); _resultRow.SetHover(false); }
    }

    // ------------------------------------------------------------------ every frame

    public void Update(float elapsed)
    {
        var bag = DrownData.Bag;

        _oxygen.Text = ((int)_player.Oxygen).ToString();
        _health.Text = ((int)Math.Max(0, _player.Health)).ToString();
        _depth.Text = _world.DepthInTiles.ToString();
        _tool.Text = $"[1] Harpoon{(_player.Tool == Tool.Harpoon ? " <" : "")}   [2] Laser{(_player.Tool == Tool.Laser ? " <" : "")}   [Z] Drill {(_player.Drill ? "ON" : "off")}   [TAB] Float {(_player.Floating ? "ON" : "off")}";
        _weight.Text = $"Carrying {bag.Weight:0.0} / {bag.MaxWeight:0} lbs    [I] Inventory   [L] Lantern   [R] Surface (near the top)   [N] Night vision   [P] Water fx";

        float fraction = Math.Clamp(_player.Y / (_mapRows * DiveLevel.Tile), 0f, 1f);
        _meterTicker.Y = FlxG.Height - 290 + 2 + fraction * 225;

        if (_messageTime > 0)
        {
            _messageTime -= elapsed;
            if (_messageTime <= 0) { _message.ResetText(""); _message.Text = ""; }
        }

        // the cursor: spinning around the mouse
        _cursorAngle = (_cursorAngle + elapsed * 200f) % 180f;
        _cursor.Angle = _cursorAngle;
        _cursor.X = FlxG.Mouse.X - 15;
        _cursor.Y = FlxG.Mouse.Y - 15;

        if (!InventoryOpen) return;

        // the bag, scrolled
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, bag.Stacks.Count - InvRows));
        for (int i = 0; i < InvRows; i++)
        {
            int index = i + _scroll;
            var s = index < bag.Stacks.Count ? bag.Stacks[index] : null;
            _invRows[i].Show(s?.Kind, s?.Count ?? 0);
        }
        _scrollUp.Visible = _scroll > 0;
        _scrollDown.Visible = bag.Stacks.Count > InvRows + _scroll;
        _weightLabel.Text = bag.Weight.ToString("0.0") + " lbs";

        if (CraftOpen)
        {
            var parts = DrownData.Components.Stacks;
            for (int i = 0; i < CraftRows; i++)
                _craftRows[i].Show(i < parts.Count ? parts[i].Kind : null, i < parts.Count ? parts[i].Count : 0);

            var recipe = DrownData.Craftable();
            _resultRow.Show(recipe?.Result, recipe != null ? 1 : 0);
        }
    }
}
