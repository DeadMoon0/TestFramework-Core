using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.SystemSteps;

internal class DeconstructArtifactStep(ArtifactIdentifier identifier) : Step<EmptyStepResultContext>
{
    public override bool DoesReturn => false;

    public override string Name => "Deconstruct Artifact";
    public override string Description => "Deconstructs a setuped Artifact";

    public override Step<EmptyStepResultContext> Clone()
    {
        return new DeconstructArtifactStep(identifier).WithClonedOptions(this);
    }

    public override async Task<EmptyStepResultContext?> Execute(RunContext context)
    {
        ArtifactInstanceGeneric artifactInstance = context.Artifacts.GetArtifact(identifier);
        context.Logger.LogInformation("Artifact: '{0}' of Type: '{1}'", identifier, artifactInstance.Artifact.GetType());
        if (artifactInstance.State != ArtifactState.Setup) return EmptyStepResultContext.Instance;
        // Contradictory instructions, so this fails loudly rather than picking one silently.
        if (artifactInstance.IsReadonly) throw new ArtifactMarkedReadonlyException(identifier);
        if (!artifactInstance.Reference.CanDeconstruct) throw new ArtifactDeconstructionUnavailableException(identifier);
        await artifactInstance.Artifact.DeconstructGeneric(context, artifactInstance.Reference);
        context.Artifacts.MarkState(artifactInstance, ArtifactState.Cleaned);
        return EmptyStepResultContext.Instance;
    }

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    public override void DeclareIO(StepIOContract contract)
    {
        contract.Inputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Artifact));
        contract.Outputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Artifact));
    }
}