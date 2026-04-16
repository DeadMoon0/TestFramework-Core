using System;
using System.Collections.Generic;
using System.Linq;
using TestFrameworkCore.Artifacts;
using TestFrameworkCore.Logging;
using TestFrameworkCore.Steps.SystemSteps;
using TestFrameworkCore.Timelines;
using TestFrameworkCore.Timelines.Builder.TimelineBuilder;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Steps.Preprocessor;

//TODO: Track variable and collection var
public class ForEachStepEmitter<TItem>(VariableReference<IEnumerable<TItem>> collection, VariableIdentifier variable, Action<ITimelineBuilder> steps) : StepEmitter
{
    public override IEnumerable<StepEmitterStepResult> Emit(ArtifactStore artifactStore, VariableStore variableStore, VariableTracker variableTracker, ArtifactTracker artifactTracker, List<Action<StepGeneric, VariableTracker, ArtifactTracker>> modifierActions, ScopedLogger? logger = null)
    {
        if (modifierActions.Count != 0) throw new NotSupportedException("Modifier on an ConditionalStepEmitter is not Supported.");

        var items = (collection.GetValue(variableStore) ?? throw new ArgumentNullException(nameof(collection), "The ForEach collection variable resolved to null.")).ToList();
        logger?.LogInformation($"ForEach '{variable.Identifier}': {items.Count} item(s)");

        foreach (TItem var in items)
        {
            yield return new StepEmitterStepResult(new SetVariableStep(variable, new ConstVariable<TItem>(var)));

            TimelineBuilder builder = new();
            steps(builder);
            foreach (var item in builder._mainStageEmitters.Steps)
            {
                foreach (var step in item.Emit(artifactStore, variableStore, variableTracker, artifactTracker, logger))
                {
                    yield return step;
                }
            }
        }
    }
}