using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel.Demo.PostApoc;

/// <summary>Where a slot is in its window and whether anything can go in it.</summary>
public readonly record struct SlotSpec(int X, int Y, bool Enabled = true, string Label = "");

/// <summary>
/// A window of item slots: the bag, the equipment. One class for both (the original had two near copies). Click an item to pick it
/// up (it follows the mouse as the cursor), click a slot to put it down or swap, right click to use it, drop it with the drop slot.
/// The slots are sprites and the clicks come through FlxMouseEvent. What is in the slots is an array outside the window, so it
/// stays when the window is closed or the room changes.
/// </summary>
public sealed class SlotWindow
{
    const int Cell = 32;

    /// <summary>The item on the mouse, shared by all windows.</summary>
    public static ItemDef? Held { get; private set; }

    readonly ItemDef?[] _data;
    readonly List<SlotSpec> _specs;
    readonly List<FlxBasic> _parts = new();
    readonly FlxSprite[] _frames, _icons;
    readonly FlxText _tooltip;
    readonly Func<int, ItemDef?, bool> _accepts;
    readonly Func<ItemDef, bool> _use;
    readonly Action<ItemDef>? _drop;
    readonly Texture2D _normal, _lit, _off;
    int _hover = -1;

    public readonly FlxGroup Root = new();
    public bool Open { get; private set; }
    public readonly float X, Y;

    public SlotWindow(float x, float y, Texture2D? background, string title, ItemDef?[] data, IReadOnlyList<SlotSpec> specs,
        Func<int, ItemDef?, bool> accepts, Func<ItemDef, bool> use, Action<ItemDef>? drop = null, SlotSpec? dropSlot = null, Point? closeAt = null)
    {
        X = x; Y = y;
        _data = data;
        _specs = new List<SlotSpec>(specs);
        _accepts = accepts;
        _use = use;
        _drop = drop;
        _normal = Frame(new Color(255, 255, 255, 0), new Color(255, 255, 255, 28));
        _lit = Frame(new Color(255, 230, 120, 255), new Color(255, 230, 120, 60));
        _off = Frame(new Color(0, 0, 0, 120), new Color(0, 0, 0, 120));

        if (background != null)
        {
            Part(new FlxSprite(x, y).LoadGraphic(background));

            if (title.Length > 0)
                Part(new FlxText(x + 14, y + 10, background.Width - 60, title) { Tint = new Color(220, 205, 170) });
        }

        _frames = new FlxSprite[_specs.Count + (dropSlot != null ? 1 : 0)];
        _icons = new FlxSprite[_specs.Count];
        for (int i = 0; i < _specs.Count; i++)
        {
            var s = _specs[i];
            _frames[i] = new FlxSprite(x + s.X, y + s.Y).LoadGraphic(s.Enabled ? _normal : _off);
            Part(_frames[i]);
            _icons[i] = new FlxSprite(x + s.X, y + s.Y).LoadGraphic("postapoc/items/dropItem.png");
            _icons[i].Visible = false;
            Part(_icons[i]);

            if (s.Label.Length > 0)
                Part(new FlxText(x + s.X, y + s.Y + Cell + 1, 48, s.Label) { Tint = new Color(150, 140, 120) });

            if (!s.Enabled) continue;
            int index = i;
            FlxMouseEvent.Add(_frames[i], onMouseOver: _ => Hover(index), onMouseOut: _ => Unhover(index), pixelPerfect: false,
                mouseButtons: new[] { FlxMouseButtonID.Left, FlxMouseButtonID.Right });
            FlxMouseEvent.SetDownCallback(_frames[i], _ => Click(index, FlxMouseEvent.CurrentButton == FlxMouseButtonID.Right));
        }

        if (dropSlot is { } d && drop != null)
        {
            int di = _frames.Length - 1;
            var tex = FlxAssets.GetTexture("postapoc/items/dropItem.png");
            _frames[di] = new FlxSprite(x + d.X, y + d.Y).LoadGraphic(tex);
            Part(_frames[di]);
            FlxMouseEvent.Add(_frames[di], onMouseOver: _ => ShowTip(_frames[di], "DROP ITEM"), onMouseOut: _ => _tooltip!.Visible = false, pixelPerfect: false);
            FlxMouseEvent.SetDownCallback(_frames[di], _ => DropHeld());
        }

        // closing (a window without a picture, like the hotbar, is always there)
        if (background != null)
        {
            var close = new FlxSprite(x + (closeAt?.X ?? background.Width - 36), y + (closeAt?.Y ?? 6)).LoadGraphic("postapoc/items/closeButton.jpg");
            close.Scale = new Vector2(0.5f, 0.5f);
            close.Origin = Vector2.Zero;
            Part(close);
            FlxMouseEvent.Add(close, pixelPerfect: false);
            FlxMouseEvent.SetClickCallback(close, _ => SetOpen(false));
        }

        _tooltip = new FlxText(x, y, 150, "") { Tint = Color.Cyan, Visible = false };
        _parts.Add(_tooltip);
        Root.Add(_tooltip);

        foreach (var p in _parts) { if (p is FlxObject o) o.ScrollFactor = Vector2.Zero; p.DrawAfterPostProcess = true; }
        SetOpen(false);
    }

    void Part(FlxBasic p)
    {
        _parts.Add(p);
        Root.Add(p);
    }

    /// <summary>A plain dark panel with a lighter edge, in the colors of the original bag.</summary>
    public static Texture2D MakePanel(int w, int h)
    {
        var tex = new Texture2D(FlxG.GraphicsDevice, w, h);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool border = x < 3 || y < 3 || x >= w - 3 || y >= h - 3;
                bool corner = (x < 6 || x >= w - 6) && (y < 6 || y >= h - 6);
                px[y * w + x] = corner ? Color.Transparent : border ? new Color(95, 80, 60) : new Color(34, 26, 18, 245);
            }
        tex.SetData(px);
        return tex;
    }

    static Texture2D Frame(Color border, Color fill)
    {
        var tex = new Texture2D(FlxG.GraphicsDevice, Cell, Cell);
        var px = new Color[Cell * Cell];
        for (int y = 0; y < Cell; y++)
            for (int x = 0; x < Cell; x++)
                px[y * Cell + x] = x == 0 || y == 0 || x == Cell - 1 || y == Cell - 1 ? border : fill;
        tex.SetData(px);
        return tex;
    }

    public void SetOpen(bool open)
    {
        Open = open;
        foreach (var p in _parts) p.Visible = open;
        _tooltip.Visible = false;
        if (open) Refresh(); else _hover = -1;
    }

    public void Toggle() => SetOpen(!Open);

    // ------------------------------------------------------------------ the slots

    void Hover(int i)
    {
        _hover = i;
        _frames[i].LoadGraphic(_lit);
        if (_data[i] is { } item) ShowTip(_frames[i], item.Name.ToUpperInvariant());
    }

    void Unhover(int i)
    {
        if (_hover == i) _hover = -1;
        _frames[i].LoadGraphic(_normal);
        _tooltip.Visible = false;
    }

    void ShowTip(FlxSprite near, string text)
    {
        _tooltip.Text = text;
        _tooltip.X = near.X;
        _tooltip.Y = near.Y - 10;
        _tooltip.Visible = true;
    }

    void Click(int i, bool right)
    {
        if (right)
        {
            // use what is in the slot
            if (Held == null && _data[i] is { Usable: true } item && _use(item))
            {
                _data[i] = null;                     // used up (the use can be refused: no camp with a bandit about)
                PostApocData.Sfx("pickup", 0.5f);
            }
            return;
        }

        var slot = _data[i];
        if (Held == null)
        {
            if (slot == null) return;
            Take(slot);
            _data[i] = null;
        }
        else
        {
            if (!_accepts(i, Held)) return;
            var put = Held;
            _data[i] = put;
            Take(slot);                  // what was there is now in hand (or nothing)
        }
        Refresh();
    }

    void DropHeld()
    {
        if (Held == null || _drop == null) return;
        _drop(Held);
        Take(null);
    }

    /// <summary>Puts an item on the mouse (null: the mouse is empty again).</summary>
    public static void Take(ItemDef? item)
    {
        Held = item;
        if (item == null) FlxG.Mouse.LoadCursor("postapoc/cursor.png");
        else FlxG.Mouse.LoadCursor(item.Texture, 1f, -16, -16);
    }

    /// <summary>Shows what is in the slots.</summary>
    public void Refresh()
    {
        for (int i = 0; i < _icons.Length; i++)
        {
            var item = _data[i];
            _icons[i].Visible = Open && item != null;
            if (item != null) _icons[i].LoadGraphic(item.Texture);
        }
    }

    public void Update()
    {
        if (Open) Refresh();
    }
}
