using System;
using System.Collections.Generic;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.Preprocessor;

public class SingleStepEmitter(StepGeneric step) : StepEmitter
{
    public override IEnumerable<StepEmitterStepResult> Emit(ArtifactStore artifactStore, VariableStore variableStore, VariableTracker variableTracker, ArtifactTracker artifactTracker, List<Action<StepGeneric, VariableTracker, ArtifactTracker>> modifierActions, ScopedLogger? logger = null)
    {
        StepGeneric modifiedStep = step.CloneGeneric();
        foreach (var modifier in modifierActions)
        {
            modifier(modifiedStep, variableTracker, artifactTracker);
        }

        // If the step needs a pre-step (e.g. create temp subscription), emit it into
        // the Pre-Setup Stage so it runs before any Main Stage step.
        if (modifiedStep is IHasPreStep preProvider && preProvider.CreatePreStep(variableStore) is { } preStep)
            yield return new StepEmitterStepResult(preStep, RunInPreSetupStage: true);

        // If the step needs a cleanup step, redirect it to the Cleanup Stage.
        if (modifiedStep is IHasCleanupStep cleanupProvider && cleanupProvider.CreateCleanupStep(variableStore) is { } cleanupStep)
            yield return new StepEmitterStepResult(cleanupStep, RedirectToCleanUp: true);

        yield return new StepEmitterStepResult(modifiedStep);
    }
}