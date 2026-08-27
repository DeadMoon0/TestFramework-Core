using System.Collections.Generic;
using System.Linq;

namespace TestFramework.Core.Environment;

/// <summary>
/// What a run's environment consists of: each component's state, and whether the run owns it.
/// </summary>
/// <remarks>
/// <para>
/// The scope is here rather than in a type of its own because this is already the object that knows which
/// components a run has. "What is mine and what am I only using" is a property of each component in the run,
/// not a separate ledger to keep beside this one and hope stays in step with it.
/// </para>
/// <para>
/// It is what settles teardown. Before it, a borrowed component was protected only by the stand-in that
/// replaces it having an empty <c>DeconstructAsync</c> - true, but invisible: nothing a reader could ask, and
/// no protection at all for a caller that reached the real component instead. Now the run states what it owns
/// and tears down exactly that.
/// </para>
/// </remarks>
public class EnvComponentContext : IFreezable
{
    private readonly FreezableDictionary<EnvComponentIdentifier, object?> _states = [];
    private readonly FreezableDictionary<EnvComponentIdentifier, EnvComponentScope> _scopes = [];
    private readonly FreezableCollection<EnvComponentIdentifier> _creationOrder = [];

    /// <summary>
    /// Gets a value indicating whether the context has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the component context.
    /// </summary>
    public void Freeze()
    {
        IsFrozen = true;
        _states.Freeze();
        _scopes.Freeze();
        _creationOrder.Freeze();
    }

    internal void SetState(EnvComponentIdentifier identifier, object? state, EnvComponentScope scope)
    {
        ((IFreezable)this).EnsureNotFrozen();
        _states[identifier] = state;
        _scopes[identifier] = scope;

        foreach (EnvComponentIdentifier created in _creationOrder)
        {
            if (created == identifier)
                return;
        }

        _creationOrder.Add(identifier);
    }

    internal IReadOnlyList<EnvComponentIdentifier> GetCreationOrder() => [.. _creationOrder];

    /// <summary>
    /// Returns whether state exists for the given component identifier.
    /// </summary>
    public bool Contains(EnvComponentIdentifier identifier) => _states.ContainsKey(identifier);

    /// <summary>
    /// Gets the raw state for the given component identifier.
    /// </summary>
    public object? GetState(EnvComponentIdentifier identifier) => _states[identifier];

    /// <summary>
    /// Gets the typed state for the given component identifier.
    /// </summary>
    public T? GetState<T>(EnvComponentIdentifier identifier) => (T?)_states[identifier];

    /// <summary>
    /// Attempts to get the raw state for the given component identifier.
    /// </summary>
    public bool TryGetState(EnvComponentIdentifier identifier, out object? state) => _states.TryGetValue(identifier, out state);

    /// <summary>
    /// Attempts to get the typed state for the given component identifier.
    /// </summary>
    public bool TryGetState<T>(EnvComponentIdentifier identifier, out T? state)
    {
        if (_states.TryGetValue(identifier, out object? rawState))
        {
            state = (T?)rawState;
            return true;
        }

        state = default;
        return false;
    }

    /// <summary>
    /// Whether the run owns this component or was only handed it.
    /// </summary>
    /// <param name="identifier">Which component.</param>
    /// <returns>Its scope in this run.</returns>
    /// <exception cref="KeyNotFoundException">The run has no such component.</exception>
    public EnvComponentScope ScopeOf(EnvComponentIdentifier identifier) => _scopes[identifier];

    /// <summary>
    /// Whether the run owns this component, for a caller that may be asking about one it does not have.
    /// </summary>
    /// <param name="identifier">Which component.</param>
    /// <param name="scope">Its scope, when the run has it.</param>
    /// <returns>True when the run has it.</returns>
    public bool TryGetScope(EnvComponentIdentifier identifier, out EnvComponentScope scope) => _scopes.TryGetValue(identifier, out scope);

    /// <summary>
    /// Gets the identifiers of all components that were created.
    /// </summary>
    public IReadOnlyCollection<EnvComponentIdentifier> CreatedComponents => [.. _creationOrder];

    /// <summary>
    /// The components this run created, and so the ones it has to take down.
    /// </summary>
    /// <remarks>
    /// In creation order, so a caller taking them down walks it backwards - the same order teardown uses,
    /// because a dependency has to outlive what depends on it.
    /// </remarks>
    public IReadOnlyList<EnvComponentIdentifier> ComponentsThisRunOwns =>
        [.. _creationOrder.Where(identifier => ScopeOf(identifier) == EnvComponentScope.Run)];

    /// <summary>
    /// The components this run was handed, and so the ones it must leave standing.
    /// </summary>
    public IReadOnlyList<EnvComponentIdentifier> ComponentsThisRunReuses =>
        [.. _creationOrder.Where(identifier => ScopeOf(identifier) == EnvComponentScope.Reused)];
}