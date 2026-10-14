using System;
using System.Collections.Generic;

namespace MonoFlixel;

// Port of flixel-addons' FlxFSM: a finite state machine for game objects.
//
//     class Idle : FlxFSMState<Slime> { public override void Update(float dt, Slime o, FlxFSM<Slime> fsm) { ... } }
//
//     fsm = new FlxFSM<Slime>(this);
//     fsm.Transitions.Add(typeof(Idle), typeof(Jump), o => FlxG.Keys.JustPressed(Keys.Up) && o.IsTouching(FlxDirection.Down));
//     fsm.Transitions.Start(typeof(Idle));
//     ...
//     fsm.Update(elapsed);          // from the owner's Update
//
// States are classes. The transition table says which state follows which, when; states are pooled, so a state object
// is reused each time it is entered (reset what you need in Enter).

/// <summary>A state. Override what you need; the owner is whatever object the FSM controls.</summary>
public class FlxFSMState<T> : IDisposable where T : class
{
    /// <summary>Called when the state becomes active.</summary>
    public virtual void Enter(T owner, FlxFSM<T> fsm) { }
    /// <summary>Called every update while active. Use <paramref name="fsm"/> to look at the age of the state.</summary>
    public virtual void Update(float elapsed, T owner, FlxFSM<T> fsm) { }
    /// <summary>Called when the state stops being active.</summary>
    public virtual void Exit(T owner) { }
    public virtual void Dispose() { }
}

/// <summary>When to move from one state to another. Derive and override <see cref="Evaluate"/> to avoid passing a function.</summary>
public class FlxFSMTransition<T> where T : class
{
    /// <summary>The state this applies to, or null from any state (a global transition).</summary>
    public Type? From;
    public Type To = null!;
    public Func<T, bool>? Condition;
    /// <summary>Marks the transition for removal (set by the table).</summary>
    public bool Remove;

    /// <summary>True when the transition should fire.</summary>
    public virtual bool Evaluate(T target) => Condition != null && Condition(target);
}

/// <summary>Which states follow which. Conditions are polled in the order they were added; the first one that holds wins.</summary>
public class FlxFSMTransitionTable<T> where T : class
{
    readonly List<FlxFSMTransition<T>> _table = new();
    Type? _startState;
    bool _garbage;

    /// <summary>The state that is entered first and when the FSM has none.</summary>
    public FlxFSMTransitionTable<T> Start(Type state) { _startState = state; return this; }
    public FlxFSMTransitionTable<T> Start<TState>() where TState : FlxFSMState<T> => Start(typeof(TState));

    /// <summary>The state that should be active: the current one, or the target of the first transition that holds.</summary>
    public Type? Poll(Type? current, T owner)
    {
        if (current == null && _startState != null) return _startState;

        if (_garbage)
        {
            _garbage = false;
            for (int i = _table.Count - 1; i >= 0; i--)
            {
                var t = _table[i];
                if (!t.Remove) continue;
                if (t.From == current) _garbage = true;      // still in that state: remove later
                else _table.RemoveAt(i);
            }
        }

        foreach (var t in _table)
            if ((t.From == current || t.From == null) && t.Evaluate(owner)) return t.To;
        return current;
    }

    public FlxFSMTransitionTable<T> Add(Type from, Type to, Func<T, bool> condition)
    {
        if (!Has(from, to, condition)) _table.Add(new FlxFSMTransition<T> { From = from, To = to, Condition = condition });
        return this;
    }

    public FlxFSMTransitionTable<T> Add<TFrom, TTo>(Func<T, bool> condition) where TFrom : FlxFSMState<T> where TTo : FlxFSMState<T> =>
        Add(typeof(TFrom), typeof(TTo), condition);

    /// <summary>A transition from any state.</summary>
    public FlxFSMTransitionTable<T> AddGlobal(Type to, Func<T, bool> condition)
    {
        if (!Has(null, to, condition)) _table.Add(new FlxFSMTransition<T> { To = to, Condition = condition });
        return this;
    }

    public FlxFSMTransitionTable<T> AddGlobal<TTo>(Func<T, bool> condition) where TTo : FlxFSMState<T> => AddGlobal(typeof(TTo), condition);

    public void AddTransition(FlxFSMTransition<T> transition)
    {
        if (!_table.Contains(transition)) _table.Add(transition);
    }

    /// <summary>Everywhere <paramref name="target"/> is used in the table, use <paramref name="replacement"/> instead.</summary>
    /// <param name="removeNow">false: the old transitions go away once the owner is not in their From state.</param>
    public void Replace(Type target, Type replacement, bool removeNow = false)
    {
        for (int i = _table.Count - 1; i >= 0; i--)
        {
            var t = _table[i];
            if (t.To == target)
            {
                if (t.From == null) AddGlobal(replacement, t.Condition!);
                else Add(t.From, replacement, t.Condition!);
                RemoveTransition(t, removeNow);
            }
            else if (t.From == target)
            {
                Add(replacement, t.To, t.Condition!);
                RemoveTransition(t, removeNow);
            }
        }
    }

    /// <summary>Removes transitions matching all the given (non-null) parts.</summary>
    public void Remove(Type? from = null, Type? to = null, Func<T, bool>? condition = null, bool removeNow = false)
    {
        if (from == null && to == null && condition == null) throw new ArgumentException("Give at least one of from, to or condition.");
        for (int i = _table.Count - 1; i >= 0; i--)
        {
            var t = _table[i];
            if ((from == null || from == t.From) && (to == null || to == t.To) && (condition == null || condition == t.Condition))
                RemoveTransition(t, removeNow);
        }
    }

    void RemoveTransition(FlxFSMTransition<T> t, bool now)
    {
        if (now) _table.Remove(t);
        else { t.Remove = true; _garbage = true; }
    }

    /// <summary>True if a transition matches all the given (non-null) parts.</summary>
    public bool Has(Type? from = null, Type? to = null, Func<T, bool>? condition = null)
    {
        if (from == null && to == null && condition == null) return false;
        for (int i = _table.Count - 1; i >= 0; i--)
        {
            var t = _table[i];
            if ((from == null || from == t.From) && (to == null || to == t.To) && (condition == null || condition == t.Condition)) return true;
        }
        return false;
    }
}

/// <summary>A state machine: one active state, changed by its transition table.</summary>
public class FlxFSM<T> : IDisposable where T : class
{
    /// <summary>Seconds the current state has been active.</summary>
    public float Age;
    /// <summary>Used by a stack for locking.</summary>
    public string? Name;
    /// <summary>Bit flags, used by a stack for locking by type.</summary>
    public int TypeFlags;
    public FlxFSMStack<T>? Stack;
    public FlxFSMTransitionTable<T> Transitions = new();

    readonly Dictionary<Type, Stack<FlxFSMState<T>>> _pools = new();
    T? _owner;
    FlxFSMState<T>? _state;

    public FlxFSM(T? owner = null, FlxFSMState<T>? state = null)
    {
        _owner = owner;
        if (state != null) State = state;
    }

    /// <summary>The object the states control.</summary>
    public T? Owner
    {
        get => _owner;
        set
        {
            if (_owner == value) return;
            if (_owner != null) _state?.Exit(_owner);
            _owner = value;
            if (_owner != null && _state != null) { Age = 0; _state.Enter(_owner, this); }
        }
    }

    /// <summary>The active state. Setting a state of another class exits the old one and enters the new one.</summary>
    public FlxFSMState<T>? State
    {
        get => _state;
        set
        {
            var newClass = value?.GetType();
            if (StateClass == newClass) return;
            if (_owner != null) _state?.Exit(_owner);
            _state = value;
            if (_state != null && _owner != null) { Age = 0; _state.Enter(_owner, this); }
            StateClass = newClass;
        }
    }

    /// <summary>The class of the active state.</summary>
    public Type? StateClass { get; private set; }

    public void Update(float elapsed)
    {
        if (_state != null && _owner != null)
        {
            Age += elapsed;
            _state.Update(elapsed, _owner, this);
        }

        if (_owner == null) return;
        var next = Transitions.Poll(StateClass, _owner);
        if (next != null && next != StateClass)
        {
            if (!_pools.TryGetValue(next, out var pool)) _pools[next] = pool = new Stack<FlxFSMState<T>>();
            var fresh = pool.Count > 0 ? pool.Pop() : (FlxFSMState<T>)Activator.CreateInstance(next)!;
            var old = _state;
            Type? oldClass = StateClass;
            State = fresh;
            // the old state goes back to its pool for next time
            if (old != null && oldClass != null)
            {
                if (!_pools.TryGetValue(oldClass, out var oldPool)) _pools[oldClass] = oldPool = new Stack<FlxFSMState<T>>();
                oldPool.Push(old);
            }
        }
    }

    public void Dispose()
    {
        // exit the current state before letting go of the owner
        if (_owner != null) _state?.Exit(_owner);
        _owner = null;
        _state = null;
        StateClass = null;
        Stack = null;
        Name = null;
        _pools.Clear();
    }
}

/// <summary>Several FSMs on one owner (for example movement and combat), updated together; some can be locked for a frame.</summary>
public class FlxFSMStack<T> : IDisposable where T : class
{
    // shared by every stack of every owner type, so a lock can reach all of them
    static event Action<int>? LockSignal;

    List<FlxFSM<T>> _stack = new();
    List<FlxFSM<T>>? _altered;
    readonly List<string> _lockedNames = new();
    int _lockedTypes;
    bool _hasLocks, _lockRemaining;

    public bool IsEmpty => _stack.Count == 0;

    public FlxFSMStack() { LockSignal += LockType; }

    /// <summary>Updates the FSMs that are not locked this frame.</summary>
    public void Update(float elapsed)
    {
        if (_altered != null) { _stack = new List<FlxFSM<T>>(_altered); _altered = null; }

        foreach (var fsm in _stack)
        {
            if (!_hasLocks || (!_lockRemaining && (fsm.TypeFlags & _lockedTypes) == 0 && (fsm.Name == null || !_lockedNames.Contains(fsm.Name))))
                fsm.Update(elapsed);
        }

        _lockedNames.Clear();
        _lockRemaining = false;
        _lockedTypes = 0;
        _hasLocks = false;
    }

    /// <summary>Skip the FSM with this name for the rest of this update.</summary>
    public void Lock(string name)
    {
        if (!_lockedNames.Contains(name)) { _lockedNames.Add(name); _hasLocks = true; }
    }

    /// <summary>Skip all FSMs not yet updated this frame.</summary>
    public void LockRemaining() { _lockRemaining = true; _hasLocks = true; }

    /// <summary>Skip FSMs whose TypeFlags has any of these bits.</summary>
    public void LockType(int bitflag) { _lockedTypes |= bitflag; _hasLocks = true; }

    /// <summary>Lock a type in every stack (e.g. freeze all "input" FSMs while a cutscene plays).</summary>
    public static void GlobalLock(int bitflag) => LockSignal?.Invoke(bitflag);

    List<FlxFSM<T>> Edit() => _altered ??= new List<FlxFSM<T>>(_stack);

    /// <summary>Add to the front. Changes show up on the next update.</summary>
    public void Unshift(FlxFSM<T> fsm) { fsm.Stack = this; Edit().Insert(0, fsm); }
    public void Push(FlxFSM<T> fsm) { fsm.Stack = this; Edit().Add(fsm); }

    public FlxFSM<T>? Shift()
    {
        var list = Edit();
        if (list.Count == 0) return null;
        var fsm = list[0];
        list.RemoveAt(0);
        fsm.Dispose();
        return fsm;
    }

    public FlxFSM<T>? Pop()
    {
        var list = Edit();
        if (list.Count == 0) return null;
        var fsm = list[^1];
        list.RemoveAt(list.Count - 1);
        if (fsm.Name != null) Lock(fsm.Name);   // not updated for the rest of this loop
        fsm.Dispose();
        return fsm;
    }

    public void Remove(FlxFSM<T> fsm)
    {
        if (!Edit().Remove(fsm)) return;
        if (fsm.Name != null) Lock(fsm.Name);
        fsm.Dispose();
    }

    public void RemoveByName(string name)
    {
        foreach (var fsm in _stack.ToArray()) if (fsm.Name == name) Remove(fsm);
    }

    public void Dispose()
    {
        foreach (var fsm in _stack) fsm.Dispose();
        LockRemaining();
        LockSignal -= LockType;
    }
}
