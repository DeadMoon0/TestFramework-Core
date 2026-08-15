using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.SystemSteps;

internal enum FindArtifactNamingMode
{
    Single,
    Generated,
    Exact
}

// Current behaviour: when the finder returns nothing in Single naming mode the step logs a warning
// and completes, so the declared identifier is left with no artifact behind it. Later steps that
// read that identifier fail at IO-contract validation, not here.
internal class FindArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference> : Step<EmptyStepResultContext>
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
            throw new ArtifactIdentifierRequiredException("FindArtifactsAs(...)");

        _identifiers = identifiers;
        _finder = finder;
        _namingMode = namingMode;
    }

    public override bool DoesReturn => false;

    public override string Name => "Find Artifact";
    public override string Description => "Searches and Finds external Artifacts";

    public override Step<EmptyStepResultContext> Clone()
    {
        return new FindArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(_identifiers, _finder, _namingMode).WithClonedOptions(this);
    }

    public override async Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        List<TArtifactReference> artifacts = [];
        if (_namingMode == FindArtifactNamingMode.Single)
        {
            ArtifactFinderResult? result = await _finder.FindAsync(serviceProvider, variableStore, logger, cancellationToken);
            if (result is null)
            {
                logger.LogWarning("No Artifact Found.");
                return EmptyStepResultContext.Instance;
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
            ArtifactResolveResult<TArtifactDescriber, TArtifactData, TArtifactReference> artifactDataResult = await artifacts[i].ResolveToDataAsync(serviceProvider, ArtifactVersionIdentifier.Default, variableStore, logger);
            if (artifactDataResult.Found && artifactDataResult.Data is null)
                throw new ArtifactResolutionInvariantException(identifier, "artifact discovery");

            artifactStore.AddArtifact(new ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference>(artifacts[i].GetArtifactDescriber(), identifier, artifacts[i], artifactDataResult.Data)
            {
                State = artifactDataResult.Found ? ArtifactState.Setup : ArtifactState.NotFound
            });
        }
        return EmptyStepResultContext.Instance;
    }

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

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
            throw new ArtifactCountMismatchException(_identifiers.Length, artifactCount);
    }

    private ArtifactIdentifier GetIdentifier(int index)
    {
        return _namingMode switch
        {
            FindArtifactNamingMode.Single => _identifiers[0],
            FindArtifactNamingMode.Exact => _identifiers[index],
            FindArtifactNamingMode.Generated => _identifiers[0] + "_" + index,
            _ => throw new FindArtifactNamingModeInvalidException(_namingMode)
        };
    }
}