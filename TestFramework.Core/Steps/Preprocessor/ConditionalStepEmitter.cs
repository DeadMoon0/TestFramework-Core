using System.ComponentModel;
using System;
using System.Collections.Generic;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.Preprocessor;

//TODO: Track shouldRun var
/// <summary>
/// Emits nested steps only when a condition variable resolves to <see langword="true"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class ConditionalStepEmitter(VariableReference<bool> shouldRun, Action<ITimelineBuilder> steps) : StepEmitter
{
    /// <summary>
    /// Emits the nested steps when the condition evaluates to true.
    /// </summary>
    public override IEnumerable<StepEmitterStepResult> Emit(ArtifactStore artifactStore, VariableStore variableStore, VariableTracker variableTracker, ArtifactTracker artifactTracker, List<Action<StepGeneric, VariableTracker, ArtifactTracker>> modifierActions, ScopedLogger? logger = null)
    {
        if (modifierActions.Count != 0) throw new NotSupportedException("Modifier on an ConditionalStepEmitter is not Supported.");

        bool cond = shouldRun.GetValue(variableStore);
        logger?.LogInformation($"Conditional '{shouldRun.Identifier}' = {cond} -> {(cond ? "steps included" : "steps skipped")}");

        if (!cond) yield break;

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