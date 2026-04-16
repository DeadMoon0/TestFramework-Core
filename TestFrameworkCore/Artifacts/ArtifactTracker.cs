using System.Collections.Generic;
using System.Linq;
using TestFrameworkCore.Exceptions;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Artifacts;

public class ArtifactTracker : IFreezable
{
    private enum ArtifactOperation
    {
        Set,
        Get
    }

    private record ArtifactIdentifierOperation(ArtifactOperation Operation, ArtifactIdentifier Identifier);

    public bool IsFrozen { get; private set; }
    public void Freeze()
    {
        IsFrozen = true;
        _referencedIdentifier.Freeze();
    }

    private readonly FreezableCollection<ArtifactIdentifierOperation> _referencedIdentifier = [];

    public void SetReference(ArtifactIdentifier identifier)
    {
        _referencedIdentifier.Add(new ArtifactIdentifierOperation(ArtifactOperation.Set, identifier));
    }

    public void GetReference(ArtifactIdentifier identifier)
    {
        _referencedIdentifier.Add(new ArtifactIdentifierOperation(ArtifactOperation.Get, identifier));
    }

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