using System;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>
/// The substate that plays a transition on top of a FlxTransitionableState. FlxTransitionableState creates it for
/// you; you only need this class to run a transition by hand.
/// </summary>
public class Transition : FlxSubState
{
    readonly TransitionEffect _effect;

    public Transition(TransitionData data) : base(Color.Transparent)
    {
        _effect = data.Type switch
        {
            TransitionType.Tiles => new TransitionTiles(data),
            TransitionType.Fade => new TransitionFade(data),
            _ => throw new ArgumentException("A Transition needs a Tiles or Fade type.", nameof(data))
        };
        Add(_effect);
    }

    /// <summary>Called once when the transition has finished.</summary>
    public Action? FinishCallback
    {
        get => _effect.FinishCallback;
        set => _effect.FinishCallback = value;
    }

    /// <summary>Starts moving to Full (In) or Empty (Out).</summary>
    public void Start(TransitionStatus newStatus) => _effect.Start(newStatus);

    /// <summary>Jumps to Full or Empty without animating.</summary>
    public void SetStatus(TransitionStatus newStatus) => _effect.SetStatus(newStatus);

    public override void Destroy()
    {
        FinishCallback = null;
        base.Destroy();
    }
}
