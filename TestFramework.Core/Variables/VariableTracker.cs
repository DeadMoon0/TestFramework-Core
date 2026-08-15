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
    /// Returns the recorded reads and writes in the order they were composed.
    /// </summary>
    /// <remarks>
    /// Usage ordering and existence are validated by <c>IOContractValidator</c> from the declared step
    /// contracts. This is what remains that only the tracker knows: which reads demanded immutability.
    /// </remarks>
    internal IEnumerable<TrackedVariableOperation> GetRecordedOperations()
    {
        foreach (VariableIdentifierOperation operation in _referencedIdentifier)
            yield return new TrackedVariableOperation(operation.Operation == VariableOperation.Set, operation.Identifier, operation.NeedsImmutability);
    }

    internal readonly record struct TrackedVariableOperation(bool IsWrite, VariableIdentifier Identifier, bool RequiresImmutability);
}