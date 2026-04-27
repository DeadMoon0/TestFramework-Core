using System.ComponentModel;
using System;
using System.Collections.Generic;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.Preprocessor;

/// <summary>
/// Represents the emitted step together with any stage-routing flags.
/// </summary>
/// <param name="Step">The emitted step.</param>
/// <param name="RedirectToCleanUp">Whether the step should be routed to the cleanup stage.</param>
/// <param name="RunInPreSetupStage">Whether the step should be routed to the pre-setup stage.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
public record StepEmitterStepResult(StepGeneric Step, bool RedirectToCleanUp = false, bool RunInPreSetupStage = false);

/// <summary>
/// Provides the base contract for objects that emit concrete steps during preprocessing.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class StepEmitter
{
    private readonly List<Action<StepGeneric, VariableTracker, ArtifactTracker>> _modifierActions = [];

    /// <summary>
    /// Gets the label assigned to the emitter.
    /// </summary>
    public string? Label { get; private set; }
    internal void SetLabel(string label) => Label = label;

    /// <summary>
    /// Emits steps using the emitter's stored modifier actions.
    /// </summary>
    public IEnumerable<StepEmitterStepResult> Emit(ArtifactStore artifactStore, VariableStore variableStore, VariableTracker variableTracker, ArtifactTracker artifactTracker, ScopedLogger? logger = null) => Emit(artifactStore, variableStore, variableTracker, artifactTracker, _modifierActions, logger);

    /// <summary>
    /// Emits steps using the provided modifier actions.
    /// </summary>
    public abstract IEnumerable<StepEmitterStepResult> Emit(ArtifactStore artifactStore, VariableStore variableStore, VariableTracker variableTracker, ArtifactTracker artifactTracker, List<Action<StepGeneric, VariableTracker, ArtifactTracker>> modifierActions, ScopedLogger? logger = null);

    /// <summary>
    /// Adds a modifier that will be applied to emitted steps.
    /// </summary>
    public void AddModifier(Action<StepGeneric, VariableTracker, ArtifactTracker> action) => _modifierActions.Add(action);
}