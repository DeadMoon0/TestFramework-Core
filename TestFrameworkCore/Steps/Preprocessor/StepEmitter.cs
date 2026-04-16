using System;
using System.Collections.Generic;
using TestFrameworkCore.Artifacts;
using TestFrameworkCore.Logging;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Steps.Preprocessor;

public record StepEmitterStepResult(StepGeneric Step, bool RedirectToCleanUp = false, bool RunInPreSetupStage = false);

public abstract class StepEmitter
{
    private readonly List<Action<StepGeneric, VariableTracker, ArtifactTracker>> _modifierActions = [];

    public string? Label { get; private set; }
    internal void SetLabel(string label) => Label = label;

    public IEnumerable<StepEmitterStepResult> Emit(ArtifactStore artifactStore, VariableStore variableStore, VariableTracker variableTracker, ArtifactTracker artifactTracker, ScopedLogger? logger = null) => Emit(artifactStore, variableStore, variableTracker, artifactTracker, _modifierActions, logger);
    public abstract IEnumerable<StepEmitterStepResult> Emit(ArtifactStore artifactStore, VariableStore variableStore, VariableTracker variableTracker, ArtifactTracker artifactTracker, List<Action<StepGeneric, VariableTracker, ArtifactTracker>> modifierActions, ScopedLogger? logger = null);
    public void AddModifier(Action<StepGeneric, VariableTracker, ArtifactTracker> action) => _modifierActions.Add(action);
}