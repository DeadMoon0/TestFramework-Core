using System;
using System.Collections.Generic;

namespace TestFramework.Core.Runner;

/// <summary>
/// One run's own live things: what a package must keep for the length of a run and cannot put in a
/// variable.
/// </summary>
/// <remarks>
/// <para>
/// The isolation rule says components talk through variables and artifacts, and that a need the channels
/// cannot carry is a missing engine capability rather than a licence for a side door. This is that
/// capability. A browser session is the case that asked for it: a timeline reads like a person using an
/// application because the page one step left behind is the page the next step finds, and a live browser
/// context is not something a variable can hold - it is not data, it has to be closed, and serialising it
/// is meaningless.
/// </para>
/// <para>
/// What it replaces is worth naming, because the replacement is the point. UI kept a
/// <c>ConditionalWeakTable</c> keyed on the run's <see cref="Variables.VariableStore"/>, on the reasoning
/// that the store is the one object every step of a run shares. That was true when it was written and
/// stopped being true the moment a step began receiving a per-attempt view of the store: keyed on the
/// object a step is handed, a retry would have opened a second browser, and the cleanup step - handed a
/// third view - would have found no sessions to close and closed none. A run's identity has to be
/// something the engine states, not something a package infers from what it happens to be holding.
/// </para>
/// <para>
/// One slot per type, so two packages cannot collide and neither has to invent a key. It holds what a run
/// owns; it does not own the lifetime. Closing what was opened stays a step in the plan, where a reader
/// can see it happen and a failure to close it is reported like anything else.
/// </para>
/// </remarks>
public sealed class RunState
{
    private readonly object syncRoot = new object();
    private readonly Dictionary<Type, object> slots = [];
    private bool frozen;

    /// <summary>
    /// Ends the run's ability to take new state. What it already holds stays readable.
    /// </summary>
    /// <remarks>
    /// Internal, and deliberately not <see cref="IFreezable"/>: a caller able to freeze a running run's state
    /// would make the next step that opens a browser fail. The run freezes its own state when it ends, as it does
    /// its variables and artifacts.
    /// </remarks>
    internal void FreezeForRunEnd()
    {
        lock (this.syncRoot)
        {
            this.frozen = true;
        }
    }

    /// <summary>
    /// This run's instance of a package's state, created on first ask.
    /// </summary>
    /// <remarks>
    /// The factory runs under the lock, so two steps reaching this at the same moment get the same
    /// instance - which is the entire guarantee. A run that opened two browsers because two steps asked
    /// at once would be worse than one that opened none.
    /// </remarks>
    /// <typeparam name="TState">The package's state type, which is also the slot.</typeparam>
    /// <param name="create">Builds it, when this run does not have one yet.</param>
    /// <returns>The state.</returns>
    public TState GetOrAdd<TState>(Func<TState> create)
        where TState : class
    {
        ArgumentNullException.ThrowIfNull(create);

        lock (this.syncRoot)
        {
            if (this.slots.TryGetValue(typeof(TState), out object? existing))
            {
                return (TState)existing;
            }

            // Reading what a finished run kept is what it is for; opening something new into it is not. The
            // run's cleanup has already run, so a live object created now would belong to a run that can no
            // longer close it.
            if (this.frozen)
            {
                throw new Exceptions.FrameworkStateException(
                    $"The run has finished, so it can take no new state: '{typeof(TState).Name}' was asked for after its Cleanup stage had run.",
                    [
                        "Create live objects inside a step of the run, and close them in a step of the run.",
                        "Use TryGet to read what the run kept.",
                    ]);
            }

            TState created = create()
                ?? throw new InvalidOperationException($"The factory for '{typeof(TState).Name}' returned null, and a run's state slot cannot hold nothing.");

            this.slots[typeof(TState)] = created;

            return created;
        }
    }

    /// <summary>
    /// This run's instance, if it has one, without creating it.
    /// </summary>
    /// <remarks>
    /// For code that only wants to tidy up after something: a cleanup step should not open a browser in
    /// order to discover that no browser was ever opened.
    /// </remarks>
    /// <typeparam name="TState">The package's state type.</typeparam>
    /// <param name="state">The state, or null when nothing asked for it.</param>
    /// <returns>True when this run has one.</returns>
    public bool TryGet<TState>(out TState? state)
        where TState : class
    {
        lock (this.syncRoot)
        {
            if (this.slots.TryGetValue(typeof(TState), out object? existing))
            {
                state = (TState)existing;

                return true;
            }

            state = null;

            return false;
        }
    }
}
