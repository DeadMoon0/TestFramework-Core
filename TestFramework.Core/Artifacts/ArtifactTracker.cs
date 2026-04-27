using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Variables;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Artifacts;

/// <summary>
/// Tracks artifact reads and writes while a timeline is being composed so invalid artifact usage can be detected early.
/// </summary>
public class ArtifactTracker : IFreezable
{
    private enum ArtifactOperation
    {
        Set,
        Get
    }

    private record ArtifactIdentifierOperation(ArtifactOperation Operation, ArtifactIdentifier Identifier);

    /// <summary>
    /// Gets a value indicating whether the tracker has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the tracker and its recorded artifact operations.
    /// </summary>
    public void Freeze()
    {
        IsFrozen = true;
        _referencedIdentifier.Freeze();
    }

    private readonly FreezableCollection<ArtifactIdentifierOperation> _referencedIdentifier = [];

    /// <summary>
    /// Records that an artifact identifier will be assigned within the composed timeline.
    /// </summary>
    public void SetReference(ArtifactIdentifier identifier)
    {
        _referencedIdentifier.Add(new ArtifactIdentifierOperation(ArtifactOperation.Set, identifier));
    }

    /// <summary>
    /// Records that an artifact identifier will be read within the composed timeline.
    /// </summary>
    public void GetReference(ArtifactIdentifier identifier)
    {
        _referencedIdentifier.Add(new ArtifactIdentifierOperation(ArtifactOperation.Get, identifier));
    }

    /// <summary>
    /// Validates artifact usage order against external artifacts and current composition state.
    /// </summary>
    public void EnsureValidity(List<ArtifactIdentifier> externalArtifacts, VariableStore variableStore)
    {
        HashSet<ArtifactIdentifier> existingIdentifier = [.. externalArtifacts];
        foreach (var reference in _referencedIdentifier)
        {
            switch (reference.Operation)
            {
                case ArtifactOperation.Set:
                    existingIdentifier.Add(reference.Identifier);
                    break;
                case ArtifactOperation.Get:
                    if (!existingIdentifier.TryGetValue(reference.Identifier, out _))
                    {
                        if (_referencedIdentifier.Count(x => x.Identifier == reference.Identifier) > 1) throw new ArtifactDoesNotYetExistException(reference.Identifier);
                        else throw new ArtifactDoesNotExistException(reference.Identifier);
                    }
                    break;
                default: throw new System.ArgumentOutOfRangeException(nameof(reference.Operation), reference.Operation, null);
            }
        }
    }
}