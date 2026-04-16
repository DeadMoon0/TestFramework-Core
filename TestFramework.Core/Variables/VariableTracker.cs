using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Variables;

public class VariableTracker : IFreezable
{
    private enum VariableOperation
    {
        Set,
        Get
    }

    private record VariableIdentifierOperation(VariableOperation Operation, VariableIdentifier Identifier, bool NeedsImmutability = false);

    public bool IsFrozen { get; private set; }
    public void Freeze()
    {
        IsFrozen = true;
        _referencedIdentifier.Freeze();
    }

    private readonly FreezableCollection<VariableIdentifierOperation> _referencedIdentifier = [];

    public void SetReference(VariableIdentifier identifier)
    {
        _referencedIdentifier.Add(new VariableIdentifierOperation(VariableOperation.Set, identifier));
    }

    public void GetReference(VariableReferenceGeneric variableReference)
    {
        if (variableReference.HasIdentifier) _referencedIdentifier.Add(new VariableIdentifierOperation(VariableOperation.Get, variableReference.Identifier ?? throw new System.ArgumentNullException(nameof(variableReference.Identifier)), variableReference.RequireImmutability));
    }

    public void EnsureValidity(List<VariableIdentifier> externalVariables, VariableStore variableStore)
    {
        // Check definition order
        HashSet<VariableIdentifier> existingIdentifier = [.. externalVariables];
        foreach (var reference in _referencedIdentifier)
        {
            switch (reference.Operation)
            {
                case VariableOperation.Set:
                    existingIdentifier.Add(reference.Identifier);
                    break;
                case VariableOperation.Get:
                    if (!existingIdentifier.TryGetValue(reference.Identifier, out _))
                    {
                        if (_referencedIdentifier.Count(x => x.Identifier == reference.Identifier) > 1) throw new VariableDoesNotYetExistException(reference.Identifier);
                        else throw new VariableDoesNotExistException(reference.Identifier);
                    }
                    break;
                default: throw new System.ArgumentOutOfRangeException(nameof(reference.Operation), reference.Operation, null);
            }
        }

        // Check Immutability restrained
        HashSet<VariableIdentifier> immutableVars = [.. _referencedIdentifier.Where(x => x.Operation == VariableOperation.Get && x.NeedsImmutability).Select(x => x.Identifier)];
        foreach (var reference in _referencedIdentifier)
        {
            switch (reference.Operation)
            {
                case VariableOperation.Set:
                    if (immutableVars.Contains(reference.Identifier)) throw new CannotSetImmutableVariableException(reference.Identifier);
                    break;
                case VariableOperation.Get: break;
                default: throw new System.ArgumentOutOfRangeException(nameof(reference.Operation), reference.Operation, null);
            }
        }
    }
}