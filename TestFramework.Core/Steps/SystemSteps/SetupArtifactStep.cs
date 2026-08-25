using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.SystemSteps;

internal class SetupArtifactStep(ArtifactIdentifier identifier) : Step<EmptyStepResultContext>
{
    internal ArtifactIdentifier Identifier => identifier;

    public override StepExecutionPhase Phase => StepExecutionPhase.Prepare;

    public override bool DoesReturn => false;

    public override string Name => "Setup Artifact";
    public override string Description => "Sets an Artifact externally up";

    public override Step<EmptyStepResultContext> Clone()
    {
        return new SetupArtifactStep(identifier).WithClonedOptions(this);
    }

    public override async Task<EmptyStepResultContext?> Execute(RunContext context)
    {
        ArtifactInstanceGeneric artifactInstance = context.Artifacts.GetArtifact(identifier);
        context.Artifacts.PinReference(artifactInstance, context);
        await artifactInstance.Artifact.SetupGeneric(context, artifactInstance.Last, artifactInstance.Reference);
        context.Artifacts.MarkState(artifactInstance, ArtifactState.Setup);
        return EmptyStepResultContext.Instance;
    }

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    public override void DeclareIO(StepIOContract contract)
    {
        contract.Inputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Artifact));
        contract.Outputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Artifact));
    }
}