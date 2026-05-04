using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.SystemSteps;

internal enum FindArtifactNamingMode
{
    Single,
    Generated,
    Exact
}

//TODO: Find a way to not have loos identifiers when no Artifact is found
internal class FindArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference> : Step<object?>
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{
    private readonly ArtifactIdentifier[] _identifiers;
    private readonly ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> _finder;
    private readonly FindArtifactNamingMode _namingMode;

    public FindArtifactStep(ArtifactIdentifier identifier, ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> finder)
        : this([identifier], finder, FindArtifactNamingMode.Single)
    {
    }

    public FindArtifactStep(ArtifactIdentifier baseName, ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> finder, FindArtifactNamingMode namingMode)
        : this([baseName], finder, namingMode)
    {
    }

    public FindArtifactStep(IReadOnlyList<ArtifactIdentifier> identifiers, ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> finder)
        : this(identifiers.ToArray(), finder, FindArtifactNamingMode.Exact)
    {
    }

    private FindArtifactStep(ArtifactIdentifier[] identifiers, ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> finder, FindArtifactNamingMode namingMode)
    {
        if (identifiers.Length == 0)
            throw new InvalidOperationException("At least one artifact identifier is required.");

        _identifiers = identifiers;
        _finder = finder;
        _namingMode = namingMode;
    }

    public override bool DoesReturn => false;

    public override string Name => "Find Artifact";
    public override string Description => "Searches and Finds external Artifacts";

    public override Step<object?> Clone()
    {
        return new FindArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(_identifiers, _finder, _namingMode).WithClonedOptions(this);
    }

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        List<TArtifactReference> artifacts = [];
        if (_namingMode == FindArtifactNamingMode.Single)
        {
            ArtifactFinderResult? result = await _finder.FindAsync(serviceProvider, variableStore, logger, cancellationToken);
            if (result is null)
            {
                logger.LogWarning("No Artifact Found.");
                return null;
            }
            artifacts.Add((TArtifactReference)result.Reference);
        }
        else
        {
            artifacts.AddRange((await _finder.FindMultiAsync(serviceProvider, variableStore, logger, cancellationToken)).ArtifactReferences.Select(x => (TArtifactReference)x.Reference));
        }

        EnsureIdentifierCountMatches(artifacts.Count);

        for (int i = 0; i < artifacts.Count; i++)
        {
            ArtifactIdentifier identifier = GetIdentifier(i);
            artifacts[i].PinReference(variableStore, logger);
            artifactStore.AddArtifact(new ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference>(artifacts[i].GetArtifactDescriber(), identifier, artifacts[i], (await artifacts[i].ResolveToDataAsync(serviceProvider, ArtifactVersionIdentifier.Default, variableStore, logger)).Data)
            {
                State = ArtifactState.Setup
            });
        }
        return null;
    }

    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        foreach (var id in _identifiers)
            contract.Outputs.Add(new StepIOEntry(id.Identifier, StepIOKind.Artifact));
    }

    private void EnsureIdentifierCountMatches(int artifactCount)
    {
        if (_namingMode != FindArtifactNamingMode.Exact)
            return;

        if (artifactCount != _identifiers.Length)
            throw new InvalidOperationException($"FindArtifactsAs expected {_identifiers.Length} artifact names but finder produced {artifactCount} results.");
    }

    private ArtifactIdentifier GetIdentifier(int index)
    {
        return _namingMode switch
        {
            FindArtifactNamingMode.Single => _identifiers[0],
            FindArtifactNamingMode.Exact => _identifiers[index],
            FindArtifactNamingMode.Generated => _identifiers[0] + "_" + index,
            _ => throw new InvalidOperationException($"Unknown naming mode {_namingMode}.")
        };
    }
}