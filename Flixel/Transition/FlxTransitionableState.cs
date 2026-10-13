using System;

namespace MonoFlixel;

/// <summary>
/// A state that plays a transition when it starts and when it ends. Port of flixel-addons' FlxTransitionableState.
///
/// Method 1, per state:
/// <code>
/// FlxG.SwitchState(new GameState(transIn: new TransitionData(...), transOut: new TransitionData(...)));
/// </code>
/// Method 2, for every state that doesn't set its own:
/// <code>
/// FlxTransitionableState.DefaultTransIn = new TransitionData(TransitionType.Fade, Color.Black, 0.5f);
/// FlxTransitionableState.DefaultTransOut = new TransitionData(TransitionType.Fade, Color.Black, 0.5f);
/// FlxG.SwitchState(new GameState());
/// </code>
/// Derive from this class and call <c>base.Create()</c> at the start of your Create() so the in-transition can begin.
/// The state is paused (it is under a substate) while a transition plays; FlxG.SwitchState waits for the out-transition.
/// </summary>
public class FlxTransitionableState : FlxState
{
    /// <summary>Used by every state whose own TransIn / TransOut is null when it is constructed.</summary>
    public static TransitionData? DefaultTransIn, DefaultTransOut;

    /// <summary>Skips the next in / out transition, once.</summary>
    public static bool SkipNextTransIn, SkipNextTransOut;

    /// <summary>Plays when this state begins / ends.</summary>
    public TransitionData? TransIn, TransOut;

    public bool HasTransIn => TransIn != null && TransIn.Type != TransitionType.None;
    public bool HasTransOut => TransOut != null && TransOut.Type != TransitionType.None;

    /// <summary>True once the out-transition has finished.</summary>
    public bool TransOutFinished { get; private set; }

    bool _exiting;
    Action? _onExit;

    public FlxTransitionableState(TransitionData? transIn = null, TransitionData? transOut = null)
    {
        TransIn = transIn ?? DefaultTransIn;
        TransOut = transOut ?? DefaultTransOut;
    }

    public override void Destroy()
    {
        TransIn = null;
        TransOut = null;
        _onExit = null;
        base.Destroy();
    }

    public override void Create()
    {
        base.Create();
        TransitionIn();
    }

    /// <summary>Called by FlxG.SwitchState: plays the out-transition, and the switch happens when it ends.</summary>
    public override void StartOutro(Action onOutroComplete)
    {
        if (!HasTransOut)
        {
            onOutroComplete();
        }
        else if (!_exiting)
        {
            _exiting = true;
            TransitionOut(onOutroComplete);

            if (SkipNextTransOut)
            {
                SkipNextTransOut = false;
                FinishTransOut();
            }
        }
        // else: already leaving, the second request is ignored
    }

    /// <summary>Starts the in-transition (the screen goes from covered to visible). Can also be called by hand.</summary>
    public void TransitionIn()
    {
        if (!HasTransIn) return;

        if (SkipNextTransIn)
        {
            SkipNextTransIn = false;
            return;
        }

        var trans = new Transition(TransIn!);
        trans.SetStatus(TransitionStatus.Full);
        OpenSubState(trans);

        trans.FinishCallback = FinishTransIn;
        trans.Start(TransitionStatus.Out);
    }

    /// <summary>Starts the out-transition (the screen goes from visible to covered); <paramref name="onExit"/> runs when it is done.</summary>
    public void TransitionOut(Action? onExit = null)
    {
        _onExit = onExit;

        if (!HasTransOut)
        {
            _onExit?.Invoke();
            return;
        }

        var trans = new Transition(TransOut!);
        trans.SetStatus(TransitionStatus.Empty);
        OpenSubState(trans);

        trans.FinishCallback = FinishTransOut;
        trans.Start(TransitionStatus.In);
    }

    void FinishTransIn() => CloseSubState();

    void FinishTransOut()
    {
        TransOutFinished = true;

        // when leaving, the substate stays until the state is destroyed (the screen must stay covered)
        if (!_exiting) CloseSubState();

        _onExit?.Invoke();
    }
}
