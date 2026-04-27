using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Variables;

/// <summary>
/// Tracks variable reads and writes while a timeline is being composed so invalid variable usage can be detected early.
/// </summary>
public class VariableTracker : IFreezable
{
    private enum VariableOperation
    {
        Set,
        Get
    }

    private record VariableIdentifierOperation(VariableOperation Operation, VariableIdentifier Identifier, bool NeedsImmutability = false);

    /// <summary>
    /// Gets a value indicating whether the tracker has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the tracker and its recorded variable operations.
    /// </summary>
    public void Freeze()
    {
        IsFrozen = true;
        _referencedIdentifier.Freeze();
    }

    private readonly FreezableCollection<VariableIdentifierOperation> _referencedIdentifier = [];

    /// <summary>
    /// Records that a variable identifier will be assigned within the composed timeline.
    /// </summary>
    /// <param name="identifier">The variable identifier that will be set.</param>
    public void SetReference(VariableIdentifier identifier)
    {
        _referencedIdentifier.Add(new VariableIdentifierOperation(VariableOperation.Set, identifier));
    }

    /// <summary>
    /// Records that a variable reference will be read within the composed timeline.
    /// </summary>
    /// <param name="variableReference">The variable reference being read.</param>
    public void GetReference(VariableReferenceGeneric variableReference)
    {
        if (variableReference.HasIdentifier) _referencedIdentifier.Add(new VariableIdentifierOperation(VariableOperation.Get, variableReference.Identifier ?? throw new System.ArgumentNullException(nameof(variableReference.Identifier)), variableReference.RequireImmutability));
    }

    /// <summary>
    /// Validates variable usage order and immutability constraints against the external variables and current store.
    /// </summary>
    /// <param name="externalVariables">Variables that are already available before the tracked operations run.</param>
    /// <param name="variableStore">The current variable store.</param>
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