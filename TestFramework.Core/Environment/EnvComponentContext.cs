using System.Collections.Generic;

namespace TestFramework.Core.Environment;

/// <summary>
/// Tracks the created environment component states for a run.
/// </summary>
public class EnvComponentContext : IFreezable
{
    private readonly FreezableDictionary<EnvComponentIdentifier, object?> _states = [];
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
        _creationOrder.Freeze();
    }

    internal void SetState(EnvComponentIdentifier identifier, object? state)
    {
        ((IFreezable)this).EnsureNotFrozen();
        _states[identifier] = state;

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
    /// Gets the identifiers of all components that were created.
    /// </summary>
    public IReadOnlyCollection<EnvComponentIdentifier> CreatedComponents => [.. _creationOrder];
}