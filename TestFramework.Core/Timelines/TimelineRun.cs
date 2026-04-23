using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Stages;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Assertions;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Timelines;

public class TimelineRun : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze()
    {
        IsFrozen = true;
        ArtifactStore.Freeze();
        VariableStore.Freeze();
        EnvironmentContext.Freeze();
        Stages.Freeze();
    }

    public Timeline Timeline { get; }

    public ArtifactStore ArtifactStore { get; }
    public VariableStore VariableStore { get; }
    public EnvComponentContext EnvironmentContext { get; }

    public IFreezableCollection<StageInstance> Stages { get; }

    private readonly ScopedLogger? _logger;

    internal TimelineRun(Timeline timeline, FreezableCollection<StageInstance> stages, ArtifactStore artifactStore, VariableStore variableStore, EnvComponentContext environmentContext, ScopedLogger? logger = null)
    {
        Timeline = timeline;
        Stages = stages;
        ArtifactStore = artifactStore;
        VariableStore = variableStore;
        EnvironmentContext = environmentContext;
        _logger = logger;
    }

    private bool TryGetWithLabel(string label, out List<StepInstanceGeneric> steps)
    {
        steps = [.. Stages.SelectMany(x => x.Steps.Where(x => String.Equals(x.Step.LabelOptions.Label, label, StringComparison.OrdinalIgnoreCase)))];
        return steps.Any();
    }

    public StepHandle Step(string label)
    {
        if (!TryGetWithLabel(label, out var instances))
            throw new InvalidOperationException($"No step with label '{label}' was found. Make sure you called .Name(\"{label}\") on the step in the builder.");
        if (instances.Count > 1)
            throw new InvalidOperationException($"Label '{label}' matches {instances.Count} step instances (e.g. from a ForEach). Use Steps(\"{label}\") to get all of them.");
        return new StepHandle(instances[0], _logger);
    }

    public IReadOnlyList<StepHandle> Steps(string label)
    {
        if (!TryGetWithLabel(label, out var instances))
            throw new InvalidOperationException($"No step with label '{label}' was found. Make sure you called .Name(\"{label}\") on the step in the builder.");
        return instances.ConvertAll(i => new StepHandle(i, _logger));
    }

    public AssertionScope AssertionScope() => new AssertionScope(_logger);

    public ValueHandle<T> Assert<T>(T value, [CallerArgumentExpression(nameof(value))] string expression = "")
        => new ValueHandle<T>(value, expression, _logger);

    public ArtifactHandle Artifact(ArtifactIdentifier identifier)
        => new ArtifactHandle(ArtifactStore.GetArtifact(identifier), _logger);

    public ArtifactHandle<TData> Artifact<TData>(ArtifactIdentifier identifier) where TData : ArtifactDataGeneric
        => new ArtifactHandle<TData>(ArtifactStore.GetArtifact(identifier), _logger);

    public VariableHandle<T> Variable<T>(VariableIdentifier identifier)
    {
        bool exists = VariableStore.TryGetVariable<T>(identifier, out T? value);
        return new VariableHandle<T>(value, exists, identifier.ToString(), _logger);
    }

    public void EnsureRanToCompletion()
    {
        var failedSteps = new List<FailedStepInfo>();
        foreach (var stage in Stages)
        {
            foreach (var step in stage.Steps)
            {
                if (!step.RetryResults.Any()) continue;
                var lastResult = step.RetryResults.Last();
                if (lastResult.State != StepState.Complete)
                    failedSteps.Add(new FailedStepInfo(stage.Stage.Name, step.Step.Name, lastResult.Exception));
            }
        }
        if (failedSteps.Count > 0)
            throw new TimelineRunFailedException(failedSteps);
    }
}