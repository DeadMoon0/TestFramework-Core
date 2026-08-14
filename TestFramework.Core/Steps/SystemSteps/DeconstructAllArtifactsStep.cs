using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.SystemSteps;

internal class DeconstructAllArtifactsStep : Step<EmptyStepResultContext>
{
    public override bool DoesReturn => false;

    public override string Name => "Deconstruct All Artifacts";
    public override string Description => "Deconstructs all setuped Artifacts";

    public override Step<EmptyStepResultContext> Clone()
    {
        return new DeconstructAllArtifactsStep().WithClonedOptions(this);
    }

    public override async Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        foreach (var artifactInstance in artifactStore.GetAll())
        {
            try
            {
                logger.LogInformation("Artifact: '{0}' of Type: '{1}'", artifactInstance.Identifier, artifactInstance.Artifact.GetType());

                // Skipped, not returned: one artifact that was never set up must not stop the
                // artifacts after it from being cleaned up.
                if (artifactInstance.State != ArtifactState.Setup) continue;

                // An observed artifact cannot be deconstructed by design, so passing over it is the
                // expected outcome rather than a failure worth reporting as one.
                if (!artifactInstance.Reference.CanDeconstruct)
                {
                    logger.LogInformation("Artifact '{0}' is observed rather than owned, so it is left in place.", artifactInstance.Identifier);
                    continue;
                }

                await artifactInstance.Artifact.DeconstructGeneric(serviceProvider, artifactInstance.Reference, variableStore, logger);
                artifactInstance.State = ArtifactState.Cleaned;
            }
            catch (Exception e)
            {
                logger.LogError("Could not deconstruct artifact '{0}' due to an error:\n{1}", artifactInstance.Identifier, e.ToString());
            }
        }
        return EmptyStepResultContext.Instance;
    }

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    public override void DeclareIO(StepIOContract contract) { }
}