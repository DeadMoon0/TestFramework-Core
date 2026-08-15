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
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Steps.Preprocessor;

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
        if (modifierActions.Count != 0)
            throw new UnsupportedFrameworkValueException(
                $"ForEach over '{variable.Identifier}' cannot carry step modifiers: it emits a whole group of steps, so there is no single step for a modifier to apply to.",
                new[]
                {
                    $"Move the modifier onto the individual steps inside the ForEach('{variable.Identifier}') body.",
                    "Use Trigger(...) for a single step when you want to modify exactly one step."
                });

        // Record both sides before resolving anything: the loop reads the collection and writes the
        // item variable once per iteration, and validation has to see that in composition order.
        variableTracker.GetReference(collection);
        variableTracker.SetReference(variable);

        var items = (collection.GetValue(variableStore)
            ?? throw new FrameworkStateException(
                $"The ForEach collection variable '{collection.Identifier?.Identifier ?? "<constant>"}' resolved to null, so there is nothing to iterate for '{variable.Identifier}'.",
                new[]
                {
                    $"Set '{collection.Identifier?.Identifier ?? "the collection"}' to a non-null collection before this ForEach runs.",
                    "Pass an empty collection instead of null when the loop is meant to do nothing."
                })).ToList();
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