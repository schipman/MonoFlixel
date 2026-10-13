using System;

namespace MonoFlixel;

/// <summary>
/// A screen/scene. Override Create() to build it and Update() for logic.
/// A state can open a <see cref="FlxSubState"/> on top of itself (pause menu, dialog, game over...).
/// </summary>
public class FlxState : FlxTypedGroup<FlxBasic>
{
    public virtual void Create() { }

    /// <summary>
    /// Called when something asks to leave this state (FlxG.SwitchState / ResetState). Play an outro (a fade, a
    /// transition...) and call <paramref name="onOutroComplete"/> when it is done; the switch happens then.
    /// The default switches immediately. See FlxTransitionableState.
    /// </summary>
    public virtual void StartOutro(Action onOutroComplete) => onOutroComplete();

    /// <summary>Keep updating this state while a substate is open (default false: the state is paused).</summary>
    public bool PersistentUpdate;

    /// <summary>Keep drawing this state underneath an open substate (default true).</summary>
    public bool PersistentDraw = true;

    /// <summary>Destroy a substate when it is closed (default true). Set false to keep it and reopen the same instance.</summary>
    public bool DestroySubStates = true;

    /// <summary>The substate currently open on top of this state, if any.</summary>
    public FlxSubState? SubState { get; private set; }

    /// <summary>Called on this state right after a substate was opened / closed.</summary>
    public Action<FlxSubState>? SubStateOpened, SubStateClosed;

    FlxSubState? _requested;
    bool _requestClose;

    /// <summary>
    /// Opens a substate on top of this state. Like Flixel, it takes effect at the start of the next update.
    /// Opening one while another is open closes the first.
    /// </summary>
    public void OpenSubState(FlxSubState sub)
    {
        if (sub == null) throw new ArgumentNullException(nameof(sub));
        _requested = sub;
        _requestClose = false;
    }

    /// <summary>Closes the open substate (at the start of the next update).</summary>
    public void CloseSubState()
    {
        _requested = null;
        if (SubState != null) _requestClose = true;
    }

    /// <summary>Update this state, or its substate. Called by the game, which is why states override Update() and not this.</summary>
    internal void TryUpdate(float elapsed)
    {
        ApplySubStateRequests();

        if (PersistentUpdate || SubState == null) Update(elapsed);
        SubState?.TryUpdate(elapsed);
    }

    internal void TryDraw()
    {
        if (PersistentDraw || SubState == null) Draw();
        SubState?.TryDraw();
    }

    void ApplySubStateRequests()
    {
        if (_requestClose || _requested != null)
        {
            if (SubState != null) CloseNow();
            _requestClose = false;
        }

        if (_requested != null)
        {
            var sub = _requested;
            _requested = null;

            SubState = sub;
            sub.ParentState = this;
            if (!sub.Created) { sub.Created = true; sub.Create(); }
            sub.Opened();
            SubStateOpened?.Invoke(sub);
        }
    }

    void CloseNow()
    {
        var sub = SubState!;
        SubState = null;
        sub.ParentState = null;
        sub.Closed();
        SubStateClosed?.Invoke(sub);
        if (DestroySubStates) sub.Destroy();
    }

    public override void Destroy()
    {
        SubState?.Destroy();
        SubState = null;
        _requested?.Destroy();
        _requested = null;
        base.Destroy();
    }
}
