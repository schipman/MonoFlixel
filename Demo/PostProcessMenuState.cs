using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// The effects menu: switches post-process shaders on for every demo at once. The effects are a stack (FlxG.PostProcess.Global):
/// the one switched on first runs first and each next one works on the result, so the order changes the picture (try VHS before CRT,
/// then CRT before VHS). What is chosen is saved and comes back the next time the game starts.
/// UP / DOWN choose, SPACE or ENTER (or a click) switch on and off, SHIFT + UP / DOWN (or [ and ]) move it in the stack, C clears all,
/// ESC closes.
/// </summary>
public class PostProcessMenuState : FlxSubState
{
    const float RowHeight = 12, ListX = 10, ListY = 30;

    int _selected;
    readonly List<FlxText> _rows = new();
    FlxText _stack = null!, _description = null!, _count = null!;
    FlxSprite _cursor = null!;

    public PostProcessMenuState() : base(new Color(8, 10, 22, 215)) { }

    public override void Create()
    {
        Add(new FlxText(0, 4, FlxG.Width, "POST-PROCESSING  (for every demo)", 8) { Alignment = FlxTextAlign.Center, Tint = new Color(216, 235, 162) });

        _cursor = new FlxSprite(ListX - 4, ListY).MakeGraphic(150, 11, new Color(60, 80, 130, 150));
        _cursor.Moves = false;
        _cursor.DrawAfterPostProcess = true;           // drawn with the text, below the dimming of nothing else
        Add(_cursor);

        foreach (var _ in FlxPostProcessLibrary.All)
        {
            var row = new FlxText(ListX, 0, 150, "") { Tint = new Color(190, 200, 220) };
            _rows.Add(row);
            Add(row);
        }

        Add(new FlxText(176, ListY - 11, 140, "STACK (runs top to bottom)") { Tint = new Color(150, 170, 120) });
        _stack = new FlxText(176, ListY, 140, "");
        Add(_stack);

        _description = new FlxText(8, FlxG.Height - 40, FlxG.Width - 16, "") { Tint = new Color(255, 230, 140) };
        Add(_description);

        _count = new FlxText(8, FlxG.Height - 24, FlxG.Width - 16,
            "UP/DOWN choose   SPACE on/off   C clear all\nSHIFT+UP/DOWN (or [ ]) move in the stack   ESC close")
        { Tint = new Color(110, 120, 150) };
        Add(_count);

        Refresh();
    }

    static FlxPostProcessChain Chain => FlxG.PostProcess;

    bool IsOn(string name) => Chain.FindGlobal(name) != null;

    void Toggle(string name)
    {
        var existing = Chain.FindGlobal(name);
        if (existing != null) Chain.RemoveGlobal(existing);
        else Chain.AddGlobal(FlxPostProcessLibrary.Create(name));
    }

    void Refresh()
    {
        var all = FlxPostProcessLibrary.All;
        for (int i = 0; i < all.Count; i++)
        {
            bool on = IsOn(all[i].Name);
            _rows[i].X = ListX;
            _rows[i].Y = ListY + i * RowHeight + 1;
            _rows[i].Text = $"[{(on ? "x" : " ")}] {all[i].Name}";
            _rows[i].Tint = on ? new Color(150, 240, 150) : new Color(160, 170, 195);
        }

        _cursor.Y = ListY + _selected * RowHeight - 1;
        _description.Text = all[_selected].Description;

        var stack = Chain.Global;
        _stack.Text = stack.Count == 0 ? "(nothing: switch some on)" : string.Join("\n", stack.Select((p, i) => $"{i + 1}. {p.Name}"));
        _stack.Tint = stack.Count == 0 ? new Color(110, 120, 150) : new Color(190, 220, 255);
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        var keys = FlxG.Keys;
        int n = FlxPostProcessLibrary.All.Count;
        bool shift = keys.AnyPressed(Keys.LeftShift, Keys.RightShift);
        string selectedName = FlxPostProcessLibrary.All[_selected].Name;

        if (shift && keys.AnyJustPressed(Keys.Up, Keys.Down) || keys.AnyJustPressed(Keys.OemOpenBrackets, Keys.OemCloseBrackets))
        {
            // move the chosen effect up or down in the stack
            bool up = keys.AnyJustPressed(Keys.Up, Keys.OemOpenBrackets);
            if (Chain.FindGlobal(selectedName) is { } p)
            {
                int index = Chain.Global.ToList().IndexOf(p);
                Chain.MoveGlobal(p, index + (up ? -1 : 1));
            }
        }
        else if (keys.JustPressed(Keys.Down)) _selected = (_selected + 1) % n;
        else if (keys.JustPressed(Keys.Up)) _selected = (_selected + n - 1) % n;

        if (keys.AnyJustPressed(Keys.Space, Keys.Enter)) Toggle(selectedName);
        if (keys.JustPressed(Keys.C)) Chain.ClearGlobal();

        // the mouse: click a row to switch it
        var m = FlxG.Mouse;
        if (m.X >= ListX - 4 && m.X < ListX + 146 && m.Y >= ListY - 1 && m.Y < ListY - 1 + n * RowHeight)
        {
            int row = (int)((m.Y - (ListY - 1)) / RowHeight);
            if (m.Left.JustPressed) { _selected = row; Toggle(FlxPostProcessLibrary.All[row].Name); }
            else if (m.JustMoved) _selected = row;
        }

        if (FlxG.Gamepad.JustPressed(FlxGamepadInputID.B) || keys.AnyJustPressed(Keys.Escape, Keys.F)) { Close(); return; }

        Refresh();
    }
}
