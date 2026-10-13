using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// Showcases FlxQuadTree: lots of bouncing squares, with the tree's cells drawn over them. Squares that touch
/// another square turn red; squares under the mouse box (a second list, B) turn yellow.
/// Compare the pair tests and time against brute force with Q.
/// </summary>
public class QuadTreeDemoState : FlxState
{
    static readonly Color Idle = new(90, 200, 230), Hit = new(240, 70, 70), Picked = new(250, 220, 60);
    const int Size = 5;

    readonly FlxTypedGroup<FlxSprite> _balls = new();
    FlxSprite _mouseBox = null!;
    FlxText _info = null!;
    FlxQuadTree? _tree;
    readonly Stopwatch _watch = new();

    bool _useTree = true, _showGrid = true;
    int _divisions = 3;
    int _pairTests, _hits;
    double _ms;

    public override void Create()
    {
        FlxG.Game.Window.Title = "MonoFlixel - quad tree";
        FlxG.Cameras.BgColor = new Color(16, 18, 28);
        FlxG.Mouse.Visible = true;

        Add(_balls);
        AddBalls(300);

        _mouseBox = new FlxSprite(0, 0).MakeGraphic(48, 48, new Color(250, 220, 60, 50));
        _mouseBox.Moves = false;
        Add(_mouseBox);

        Add(new TreeOverlay(this));

        _info = new FlxText(4, 3, FlxG.Width - 8, "", 8);
        _info.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black, 1);
        Add(_info);

        var help = new FlxText(4, FlxG.Height - 11, FlxG.Width - 8,
            "UP/DOWN: objects   LEFT/RIGHT: divisions   Q: tree / brute force   G: grid   ESC: back");
        help.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black, 1);
        help.Tint = new Color(180, 190, 200);
        Add(help);
    }

    void AddBalls(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var b = new FlxSprite(FlxG.Random.Float(0, FlxG.Width - Size), FlxG.Random.Float(0, FlxG.Height - Size))
                .MakeGraphic(Size, Size, Color.White);
            b.Tint = Idle;
            float speed = FlxG.Random.Float(15, 70);
            float dir = FlxG.Random.Float(0, MathF.Tau);
            b.Velocity = new Vector2(MathF.Cos(dir), MathF.Sin(dir)) * speed;
            _balls.Add(b);
        }
    }

    void RemoveBalls(int count)
    {
        count = Math.Min(count, _balls.Length);
        _balls.Members.RemoveRange(_balls.Length - count, count);
    }

    public override void Update(float elapsed)
    {
        if (FlxG.Keys.AnyJustPressed(Keys.Up, Keys.W)) AddBalls(_balls.Length >= 1000 ? 500 : 100);
        if (FlxG.Keys.AnyJustPressed(Keys.Down, Keys.S)) RemoveBalls(_balls.Length > 1000 ? 500 : 100);
        if (FlxG.Keys.AnyJustPressed(Keys.Right, Keys.D)) _divisions = Math.Min(8, _divisions + 1);
        if (FlxG.Keys.AnyJustPressed(Keys.Left, Keys.A)) _divisions = Math.Max(1, _divisions - 1);
        if (FlxG.Keys.JustPressed(Keys.Q)) _useTree = !_useTree;
        if (FlxG.Keys.JustPressed(Keys.G)) _showGrid = !_showGrid;
        if (FlxG.Keys.JustPressed(Keys.Escape))
        {
            LauncherState.Return();
            return;
        }

        _mouseBox.X = FlxG.Mouse.X - _mouseBox.Width / 2;
        _mouseBox.Y = FlxG.Mouse.Y - _mouseBox.Height / 2;

        base.Update(elapsed);

        // bounce off the screen edges
        foreach (var b in _balls.Members)
        {
            if (b.X < 0) { b.X = 0; b.Velocity.X = MathF.Abs(b.Velocity.X); }
            else if (b.X + b.Width > FlxG.Width) { b.X = FlxG.Width - b.Width; b.Velocity.X = -MathF.Abs(b.Velocity.X); }
            if (b.Y < 0) { b.Y = 0; b.Velocity.Y = MathF.Abs(b.Velocity.Y); }
            else if (b.Y + b.Height > FlxG.Height) { b.Y = FlxG.Height - b.Height; b.Velocity.Y = -MathF.Abs(b.Velocity.Y); }
            b.Tint = Idle;
        }

        Collide();
        UpdateInfo();
    }

    void Collide()
    {
        _hits = 0;
        _watch.Restart();

        // Mark touching squares red. The callback is all the tree (or brute force) needs.
        bool Touch(FlxObject a, FlxObject b)
        {
            ((FlxSprite)a).Tint = Hit;
            ((FlxSprite)b).Tint = Hit;
            _hits++;
            return true;
        }

        if (_useTree)
        {
            _tree = new FlxQuadTree(0, 0, FlxG.Width, FlxG.Height, _divisions);
            _tree.Add(_balls, FlxQuadTree.AList);
            _tree.Overlap(false, Touch);
            _pairTests = _tree.PairTests;

            // A second tree compares list A (the squares) against list B (the mouse box).
            var pick = new FlxQuadTree(0, 0, FlxG.Width, FlxG.Height, _divisions);
            pick.Add(_balls, FlxQuadTree.AList);
            pick.Add(_mouseBox, FlxQuadTree.BList);
            pick.Overlap(true, (a, b) => { ((FlxSprite)a).Tint = Picked; return true; });
        }
        else
        {
            _tree = null;
            _pairTests = 0;
            var m = _balls.Members;
            for (int i = 0; i < m.Count; i++)
            {
                for (int j = i + 1; j < m.Count; j++)
                {
                    _pairTests++;
                    if (m[i].Overlaps(m[j])) Touch(m[i], m[j]);
                }
                if (m[i].Overlaps(_mouseBox)) m[i].Tint = Picked;
            }
        }

        _watch.Stop();
        _ms = _watch.Elapsed.TotalMilliseconds;
    }

    void UpdateInfo()
    {
        int cells = 0;
        _tree?.ForEachNode(_ => cells++);
        _info.Text = $"{(_useTree ? "QUAD TREE" : "BRUTE FORCE")}   objects {_balls.Length}   pair tests {_pairTests}   " +
                     $"touching {_hits}   {_ms:0.00} ms" + (_useTree ? $"   divisions {_divisions}   cells {cells}" : "");
    }

    /// <summary>Draws the tree's cells on top of the squares.</summary>
    sealed class TreeOverlay : FlxBasic
    {
        readonly QuadTreeDemoState _s;
        public TreeOverlay(QuadTreeDemoState s) { _s = s; }

        public override void Draw()
        {
            if (!_s._showGrid || _s._tree == null) return;
            var sb = FlxG.SpriteBatch;
            var px = FlxG.Pixel;

            _s._tree.ForEachNode(n =>
            {
                var c = n.Depth switch
                {
                    0 => new Color(255, 255, 255, 60),
                    1 => new Color(120, 220, 120, 70),
                    2 => new Color(120, 160, 255, 70),
                    _ => new Color(255, 160, 100, 70)
                };
                int x = (int)MathF.Round(n.X), y = (int)MathF.Round(n.Y);
                int w = (int)MathF.Round(n.Width), h = (int)MathF.Round(n.Height);
                sb.Draw(px, new Rectangle(x, y, w, 1), c);
                sb.Draw(px, new Rectangle(x, y + h - 1, w, 1), c);
                sb.Draw(px, new Rectangle(x, y, 1, h), c);
                sb.Draw(px, new Rectangle(x + w - 1, y, 1, h), c);
            });
        }
    }
}
