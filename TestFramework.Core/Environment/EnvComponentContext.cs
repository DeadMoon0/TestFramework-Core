using System.Collections.Generic;

namespace TestFramework.Core.Environment;

public class EnvComponentContext : IFreezable
{
    private readonly FreezableDictionary<EnvComponentIdentifier, object?> _states = [];
    private readonly FreezableCollection<EnvComponentIdentifier> _creationOrder = [];

    public bool IsFrozen { get; private set; }

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

    public bool Contains(EnvComponentIdentifier identifier) => _states.ContainsKey(identifier);

    public object? GetState(EnvComponentIdentifier identifier) => _states[identifier];

    public T? GetState<T>(EnvComponentIdentifier identifier) => (T?)_states[identifier];

    public bool TryGetState(EnvComponentIdentifier identifier, out object? state) => _states.TryGetValue(identifier, out state);

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

    public IReadOnlyCollection<EnvComponentIdentifier> CreatedComponents => [.. _creationOrder];
}