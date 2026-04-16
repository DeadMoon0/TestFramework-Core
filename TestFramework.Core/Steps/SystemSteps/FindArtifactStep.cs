using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFrameworkCore.Steps.Options;

namespace TestFramework.Core.Steps.SystemSteps;

//TODO: Find a way to not have loos identifiers when no Artifact is found
internal class FindArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(ArtifactIdentifier[] identifiers, ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> finder, bool single) : Step<object?>
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{
    public override bool DoesReturn => false;

    public override string Name => "Find Artifact";
    public override string Description => "Searches and Finds external Artifacts";

    public override Step<object?> Clone()
    {
        return new FindArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(identifiers, finder, single).WithClonedOptions(this);
    }

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        List<TArtifactReference> artifacts = [];
        if (single)
        {
            ArtifactFinderResult? result = await finder.FindAsync(serviceProvider, variableStore, logger, cancellationToken);
            if (result is null)
            {
                logger.LogWarning("No Artifact Found.");
                return null;
            }
            artifacts.Add((TArtifactReference)result.Reference);
        }
        else
        {
            artifacts.AddRange((await finder.FindMultiAsync(serviceProvider, variableStore, logger, cancellationToken)).ArtifactReferences.Select(x => (TArtifactReference)x.Reference));
        }

        for (int i = 0; i < artifacts.Count; i++)
        {
            ArtifactIdentifier identifier = (identifiers.Length - 1) > i ? identifiers[i] : (identifiers[identifiers.Length - 1] + "_" + (i - identifiers.Length + 1));
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
        foreach (var id in identifiers)
            contract.Outputs.Add(new StepIOEntry(id.Identifier, StepIOKind.Artifact));
    }
}