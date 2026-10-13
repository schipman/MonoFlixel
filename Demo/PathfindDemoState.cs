using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// Shows FlxPathfinder and FlxPath: a random cave, a blue unit that walks wherever you click, and a red chaser
/// that finds a new path to the blue one twice a second. Also uses FlxVelocity / FlxMath / FlxAngle for the readout.
/// </summary>
public class PathfindDemoState : FlxState
{
    const int TileSize = 8;

    FlxTilemap _map = null!;
    FlxSprite _player = null!, _chaser = null!;
    FlxText _info = null!, _help = null!;
    PathOverlay _overlay = null!;

    FlxDiagonalPolicy _diagonal = FlxDiagonalPolicy.Wide;
    FlxPathSimplify _simplify = FlxPathSimplify.Straight;
    bool _chase = true;
    double _lastMs;
    float _repath;
    string _message = "";

    public override void Create()
    {
        FlxG.Game.Window.Title = "MonoFlixel - pathfinding";
        FlxG.Cameras.BgColor = new Color(12, 14, 22);
        FlxG.Mouse.ResetCursor();
        FlxG.Mouse.Visible = true;

        _map = new FlxTilemap();
        Add(_map);

        _player = new FlxSprite().MakeGraphic(6, 6, new Color(80, 190, 255));
        _chaser = new FlxSprite().MakeGraphic(6, 6, new Color(240, 80, 80));
        Add(_player);
        Add(_chaser);

        _overlay = new PathOverlay(this);
        Add(_overlay);

        _info = new FlxText(4, 2, FlxG.Width - 8, "");
        _info.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black, 1);
        Add(_info);

        _help = new FlxText(4, FlxG.Height - 11, FlxG.Width - 8,
            "Click: walk there   D: diagonals   S: simplify   C: chaser on/off   R: new cave   ESC: back")
            { Tint = new Color(180, 190, 200) };
        _help.SetBorderStyle(FlxTextBorderStyle.Outline, Color.Black, 1);
        Add(_help);

        NewCave();
    }

    /// <summary>Open tiles the unit and the chaser may start on: all in one connected area.</summary>
    List<int> _region = new();

    void NewCave()
    {
        int cols = FlxG.Width / TileSize, rows = FlxG.Height / TileSize;

        // keep making caves until one has a big connected open area to walk around in
        int[] data;
        do
        {
            data = FlxCaveGenerator.GenerateCaveArray(cols, rows, 5, 0.5f);
            _region = LargestOpenArea(data, cols, rows);
        } while (_region.Count < cols * rows / 4);

        _map.LoadMapFromArray(data, cols, rows, FlxAssets.Get("cave/caveWallTiles.png"), TileSize, TileSize, FlxAutoTiling.Auto);

        PlaceOnOpenTile(_player);
        PlaceOnOpenTile(_chaser);
        _player.Path = null;
        _chaser.Path = null;
        _message = "";
        _repath = 0;
    }

    /// <summary>The biggest group of connected empty cells (value 0), as map indices.</summary>
    static List<int> LargestOpenArea(int[] data, int cols, int rows)
    {
        var seen = new bool[data.Length];
        var best = new List<int>();

        for (int start = 0; start < data.Length; start++)
        {
            if (data[start] != 0 || seen[start]) continue;

            var area = new List<int>();
            var stack = new Stack<int>();
            stack.Push(start);
            seen[start] = true;

            while (stack.Count > 0)
            {
                int c = stack.Pop();
                area.Add(c);
                int x = c % cols, y = c / cols;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= cols || ny >= rows) continue;
                    int ni = ny * cols + nx;
                    if (data[ni] != 0 || seen[ni]) continue;
                    seen[ni] = true;
                    stack.Push(ni);
                }
            }
            if (area.Count > best.Count) best = area;
        }
        return best;
    }

    void PlaceOnOpenTile(FlxSprite s)
    {
        var p = _map.GetTileCoords(_region[FlxG.Random.Int(0, _region.Count - 1)], midpoint: true);
        s.X = p.X - s.Width / 2;
        s.Y = p.Y - s.Height / 2;
        s.Velocity = Vector2.Zero;
    }

    // ------------------------------------------------------------------ pathing

    List<Vector2>? Find(Vector2 from, Vector2 to)
    {
        var watch = Stopwatch.StartNew();
        var path = _map.FindPath(from, to, _diagonal, _simplify);
        watch.Stop();
        _lastMs = watch.Elapsed.TotalMilliseconds;
        return path;
    }

    void WalkTo(Vector2 target)
    {
        if (_map.IsSolidAt(target.X, target.Y)) { _message = "That's a wall."; return; }

        var path = Find(_player.Midpoint, target);
        if (path == null) { _message = "No path there."; return; }

        _message = "";
        _player.FollowPath(path, 75);
    }

    void Chase()
    {
        var path = Find(_chaser.Midpoint, _player.Midpoint);
        if (path == null) return;
        _chaser.FollowPath(path, 55);
    }

    // ------------------------------------------------------------------ update

    public override void Update(float elapsed)
    {
        if (FlxG.Mouse.JustPressed) WalkTo(FlxG.Mouse.WorldPosition);

        if (FlxG.Keys.JustPressed(Keys.D))
        {
            _diagonal = (FlxDiagonalPolicy)(((int)_diagonal + 1) % 3);
            RePathNow();
        }
        if (FlxG.Keys.JustPressed(Keys.S))
        {
            _simplify = (FlxPathSimplify)(((int)_simplify + 1) % 3);
            RePathNow();
        }
        if (FlxG.Keys.JustPressed(Keys.C)) { _chase = !_chase; if (!_chase) _chaser.Path?.Cancel(); }
        if (FlxG.Keys.JustPressed(Keys.R)) NewCave();
        if (FlxG.Keys.JustPressed(Keys.Escape))
        {
            LauncherState.Return();
            return;
        }

        if (_chase)
        {
            _repath -= elapsed;
            if (_repath <= 0) { _repath = 0.5f; Chase(); }
        }

        base.Update(elapsed);
        UpdateInfo();
    }

    /// <summary>After changing the settings, the player's current walk is planned again with them.</summary>
    void RePathNow()
    {
        if (_player.Path is { Active: true } p && p.Nodes.Count > 0)
            WalkTo(p.Nodes[^1]);
    }

    void UpdateInfo()
    {
        int playerNodes = _player.Path?.Nodes.Count ?? 0, chaserNodes = _chaser.Path?.Nodes.Count ?? 0;
        float dist = FlxVelocity.DistanceBetween(_player, _chaser);
        float angle = FlxAngle.AngleBetween(_chaser, _player);

        _info.Text = $"diag {_diagonal}   simplify {_simplify}   nodes {playerNodes}/{chaserNodes}   search {_lastMs:0.000} ms   " +
                     $"gap {dist:0}px @ {FlxMath.RoundDecimal(angle, 0):0}deg   {_message}";
    }

    /// <summary>Draws both paths as lines with a dot on every node.</summary>
    sealed class PathOverlay : FlxBasic
    {
        readonly PathfindDemoState _s;
        public PathOverlay(PathfindDemoState s) { _s = s; }

        public override void Draw()
        {
            Draw(_s._player, new Color(250, 220, 70, 200));
            if (_s._chase) Draw(_s._chaser, new Color(255, 120, 120, 200));
        }

        static void Draw(FlxObject o, Color color)
        {
            if (o.Path is not { Active: true } path) return;

            // only the part still ahead of the object
            var rest = new FlxPath();
            rest.Nodes.Add(o.Midpoint);
            for (int i = path.NodeIndex; i < path.Nodes.Count; i++) rest.Nodes.Add(path.Nodes[i]);
            rest.DebugDraw(color, Color.White);
        }
    }
}
