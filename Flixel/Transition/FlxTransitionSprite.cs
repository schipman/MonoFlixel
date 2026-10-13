using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>The state of one transition tile (or of a whole transition).</summary>
public enum TransitionStatus
{
    Null = -1,
    /// <summary>Growing from empty to full.</summary>
    In = 0,
    /// <summary>Shrinking from full to empty.</summary>
    Out = 1,
    Empty = 2,
    Full = 3
}

/// <summary>The pictures that come with the transitions (Assets/transitions).</summary>
public static class FlxTransitionAssets
{
    static Texture2D? _circle, _diamond, _square, _gradient;

    /// <summary>17 frames of 32x32, from an empty tile to a full one.</summary>
    public static Texture2D Circle => _circle ??= FlxAssets.GetTexture(FlxAssets.Get("transitions/circle.png"));
    public static Texture2D Diamond => _diamond ??= FlxAssets.GetTexture(FlxAssets.Get("transitions/diamond.png"));
    public static Texture2D Square => _square ??= FlxAssets.GetTexture(FlxAssets.Get("transitions/square.png"));

    /// <summary>A white diagonal alpha gradient used by diagonal Fade wipes.</summary>
    public static Texture2D DiagonalGradient => _gradient ??= FlxAssets.GetTexture(FlxAssets.Get("transitions/diagonal_gradient.png"));
}

/// <summary>
/// One tile of a Tiles transition: plays a strip of frames from empty to full ("in") or full to empty ("out"),
/// after waiting for its delay (so the tiles can ripple across the screen). Port of flixel-addons' FlxTransitionSprite.
/// </summary>
public class FlxTransitionSprite : FlxSprite
{
    readonly float _delay;
    float _count;
    bool _starting, _finished;
    TransitionStatus _newStatus = TransitionStatus.Null;

    public TransitionStatus Status { get; private set; } = TransitionStatus.In;

    public FlxTransitionSprite(float x, float y, float delay, Texture2D? graphic = null, int graphicWidth = 32, int graphicHeight = 32, int frameRate = 40)
        : base(x, y)
    {
        if (graphic == null)
        {
            graphic = FlxTransitionAssets.Diamond;
            graphicWidth = graphicHeight = 32;
        }
        _delay = delay;
        LoadGraphic(graphic, animated: true, graphicWidth, graphicHeight);

        int frames = Math.Max(1, graphic.Width / FrameWidth) * Math.Max(1, graphic.Height / FrameHeight);

        // frame 0 is empty, the last frame is full, the ones between are the growing animation
        var grow = new int[Math.Max(0, frames - 2)];
        for (int i = 0; i < grow.Length; i++) grow[i] = i + 1;
        var shrink = (int[])grow.Clone();
        Array.Reverse(shrink);

        Animation.Add("empty", new[] { 0 }, 0, false);
        if (grow.Length > 0)
        {
            Animation.Add("in", grow, frameRate, false);
            Animation.Add("out", shrink, frameRate, false);
        }
        else
        {
            Animation.Add("in", new[] { frames - 1 }, frameRate, false);
            Animation.Add("out", new[] { 0 }, frameRate, false);
        }
        Animation.Add("full", new[] { frames - 1 }, 0, false);
        Animation.OnFinish = OnFinishAnim;

        SetStatus(TransitionStatus.Full);
    }

    /// <summary>Waits for this tile's delay, then goes to <paramref name="newStatus"/> (In or Out).</summary>
    public void Start(TransitionStatus newStatus)
    {
        _starting = true;
        _finished = false;
        _count = 0;
        _newStatus = newStatus;
    }

    public void SetStatus(TransitionStatus status)
    {
        string anim = status switch
        {
            TransitionStatus.In => "in",
            TransitionStatus.Out => "out",
            TransitionStatus.Full => "full",
            _ => "empty"
        };

        Animation.Play(anim, force: true);
        Status = status;
    }

    void OnFinishAnim(string name)
    {
        if (_finished) return;
        _finished = true;

        if (Status == TransitionStatus.In) SetStatus(TransitionStatus.Full);
        else if (Status == TransitionStatus.Out) SetStatus(TransitionStatus.Empty);
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        if (_starting)
        {
            _count += elapsed;
            if (_count >= _delay)
            {
                _starting = false;
                _count = 0;
                SetStatus(_newStatus);
                _newStatus = TransitionStatus.Null;
            }
        }
    }
}
