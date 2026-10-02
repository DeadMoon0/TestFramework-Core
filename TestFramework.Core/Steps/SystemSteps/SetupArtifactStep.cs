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
        contract.InputArtifact(identifier);
        contract.OutputArtifact(identifier);
    }

    /// <summary>
    /// Declares what the reference of the artifact this step sets up reads and writes.
    /// </summary>
    /// <remarks>
    /// Setup pins the reference and runs its setup, so the reference's variables - the key values it is pinned
    /// from, a key it publishes once setup assigns one - are this step's inputs and outputs. Only the run's store
    /// knows which reference that is, so the run declares them when it plans, for an artifact it already holds.
    /// One a step adds while the run is under way is unknown at plan time, and its variables are checked when
    /// they are read.
    /// </remarks>
    /// <param name="store">The run's store, as it stands when the run is planned.</param>
    internal void DeclareHeldReferenceIO(ArtifactStore store)
    {
        if (store.TryGetArtifact(identifier, out ArtifactInstanceGeneric? held))
            held.Reference.DeclareIO(IOContract);
    }
}