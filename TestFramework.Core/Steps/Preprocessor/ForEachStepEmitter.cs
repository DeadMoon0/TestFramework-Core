using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;
using TestFramework.Core.Steps.SystemSteps;

namespace TestFramework.Core.Steps.Preprocessor;

//TODO: Track variable and collection var
/// <summary>
/// Emits nested steps once for each item in a collection variable.
/// </summary>
/// <typeparam name="TItem">The collection item type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public class ForEachStepEmitter<TItem>(VariableReference<IEnumerable<TItem>> collection, VariableIdentifier variable, Action<ITimelineBuilder> steps) : StepEmitter
{
    /// <summary>
    /// Emits the nested steps for each resolved collection item.
    /// </summary>
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