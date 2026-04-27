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

/// <summary>
/// Represents the immutable result of executing a built timeline.
/// </summary>
public class TimelineRun : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the run result has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the run result and all runtime stores it exposes.
    /// </summary>
    public void Freeze()
    {
        IsFrozen = true;
        ArtifactStore.Freeze();
        VariableStore.Freeze();
        EnvironmentContext.Freeze();
        Stages.Freeze();
    }

    /// <summary>
    /// Gets the timeline definition that produced this run.
    /// </summary>
    public Timeline Timeline { get; }

    /// <summary>
    /// Gets the artifact store captured during this run.
    /// </summary>
    public ArtifactStore ArtifactStore { get; }

    /// <summary>
    /// Gets the variable store captured during this run.
    /// </summary>
    public VariableStore VariableStore { get; }

    /// <summary>
    /// Gets the environment-component context produced while preparing and executing this run.
    /// </summary>
    public EnvComponentContext EnvironmentContext { get; }

    /// <summary>
    /// Gets the executed stages and their step instances.
    /// </summary>
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

    /// <summary>
    /// Returns the single step with the given label.
    /// </summary>
    /// <param name="label">The label assigned through <c>Name(...)</c>.</param>
    /// <returns>A handle for the matched step.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no step or more than one step matches the label.</exception>
    public StepHandle Step(string label)
    {
        if (!TryGetWithLabel(label, out var instances))
            throw new InvalidOperationException($"No step with label '{label}' was found. Make sure you called .Name(\"{label}\") on the step in the builder.");
        if (instances.Count > 1)
            throw new InvalidOperationException($"Label '{label}' matches {instances.Count} step instances (e.g. from a ForEach). Use Steps(\"{label}\") to get all of them.");
        return new StepHandle(instances[0], _logger);
    }

    /// <summary>
    /// Returns all step instances with the given label.
    /// </summary>
    /// <param name="label">The label assigned through <c>Name(...)</c>.</param>
    /// <returns>All matching step handles.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no step matches the label.</exception>
    public IReadOnlyList<StepHandle> Steps(string label)
    {
        if (!TryGetWithLabel(label, out var instances))
            throw new InvalidOperationException($"No step with label '{label}' was found. Make sure you called .Name(\"{label}\") on the step in the builder.");
        return instances.ConvertAll(i => new StepHandle(i, _logger));
    }

    /// <summary>
    /// Starts an assertion scope that groups multiple assertion failures together when supported by the logger.
    /// </summary>
    public AssertionScope AssertionScope() => new AssertionScope(_logger);

    /// <summary>
    /// Wraps a direct value in a Core assertion handle.
    /// </summary>
    /// <typeparam name="T">The asserted value type.</typeparam>
    /// <param name="value">The value to wrap.</param>
    /// <param name="expression">The captured source expression for the asserted value.</param>
    public ValueHandle<T> Assert<T>(T value, [CallerArgumentExpression(nameof(value))] string expression = "")
        => new ValueHandle<T>(value, expression, _logger);

    /// <summary>
    /// Returns an assertion handle for an artifact in the run artifact store.
    /// </summary>
    /// <param name="identifier">The artifact identifier to resolve.</param>
    public ArtifactHandle Artifact(ArtifactIdentifier identifier)
        => new ArtifactHandle(ArtifactStore.GetArtifact(identifier), _logger);

    /// <summary>
    /// Returns a typed assertion handle for an artifact in the run artifact store.
    /// </summary>
    /// <typeparam name="TData">The artifact data type.</typeparam>
    /// <param name="identifier">The artifact identifier to resolve.</param>
    public ArtifactHandle<TData> Artifact<TData>(ArtifactIdentifier identifier) where TData : ArtifactDataGeneric
        => new ArtifactHandle<TData>(ArtifactStore.GetArtifact(identifier), _logger);

    /// <summary>
    /// Returns a typed variable handle for a variable in the run variable store.
    /// </summary>
    /// <typeparam name="T">The expected variable value type.</typeparam>
    /// <param name="identifier">The variable identifier to resolve.</param>
    public VariableHandle<T> Variable<T>(VariableIdentifier identifier)
    {
        bool exists = VariableStore.TryGetVariable<T>(identifier, out T? value);
        return new VariableHandle<T>(value, exists, identifier.ToString(), _logger);
    }

    /// <summary>
    /// Throws when one or more executed steps did not finish successfully.
    /// </summary>
    /// <exception cref="TimelineRunFailedException">Thrown when at least one step ended in a non-complete state.</exception>
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