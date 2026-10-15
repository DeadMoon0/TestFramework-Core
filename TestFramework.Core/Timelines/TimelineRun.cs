using TestFramework.Core.Runner;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Graph;
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
    /// Freezes the run result and finishes the run, so none of its live state takes another write.
    /// </summary>
    /// <remarks>
    /// One call closes every channel - variables, artifacts, run state, effective settings, resource values,
    /// environment state and widgets - because they all ask the run's one gate. Before, this listed a freeze per
    /// store, and a channel nobody added to the list stayed writable on a finished run.
    /// </remarks>
    public void Freeze()
    {
        IsFrozen = true;
        _writeGate.Finish(_logger ?? new ScopedLogger(null));
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
    /// What this run decided on your behalf: the browser it found, the image it started.
    /// </summary>
    /// <remarks>
    /// The half of §5 the resource values do not carry. A coordinate is in
    /// <see cref="Values"/> with its origin and source; this is for what the framework chose where nobody
    /// stated a choice, so a passing run can still answer "with what".
    /// </remarks>
    public EffectiveSettings EffectiveSettings => this.VariableStore.EffectiveSettings;

    /// <summary>
    /// Gets the environment-component context produced while preparing and executing this run.
    /// </summary>
    public EnvComponentContext EnvironmentContext { get; }

    /// <summary>
    /// Gets the environment this run used, or <see langword="null"/> when it was given none.
    /// </summary>
    /// <remarks>
    /// The instance the run itself used, which for an <see cref="IRunScopedEnvironmentFactory"/> is not the one
    /// handed to <c>SetEnv</c> but the copy that environment made for this run - so what it records describes this
    /// run and no other.
    /// </remarks>
    public IEnvironmentProvider? Environment { get; internal init; }

    /// <summary>
    /// Gets where this run's resources ended up - the same answer the steps read, kept for assertions.
    /// </summary>
    public ValueResolution Values { get; }

    /// <summary>
    /// Gets the executed stages and their step instances.
    /// </summary>
    public IFreezableCollection<StageInstance> Stages { get; }

    private readonly ScopedLogger? _logger;

    internal TimelineRun(Timeline timeline, FreezableCollection<StageInstance> stages, ArtifactStore artifactStore, VariableStore variableStore, EnvComponentContext environmentContext, ValueResolution values, RunWriteGate writeGate, ScopedLogger? logger = null)
    {
        _writeGate = writeGate;
        Timeline = timeline;
        Stages = stages;
        ArtifactStore = artifactStore;
        VariableStore = variableStore;
        EnvironmentContext = environmentContext;
        Values = values;
        _logger = logger;
    }

    private readonly RunWriteGate _writeGate;

    private bool TryGetWithLabel(string label, out List<StepInstanceGeneric> steps)
    {
        steps = [.. Stages.SelectMany(x => x.Steps.Where(x => String.Equals(x.Step.LabelOptions.Label, label, StringComparison.OrdinalIgnoreCase)))];
        return steps.Any();
    }

    private IReadOnlyList<string> GetAvailableLabels()
        => Stages.SelectMany(x => x.Steps)
            .Select(x => x.Step.LabelOptions.Label)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();

    /// <summary>
    /// Returns the single step with the given label.
    /// </summary>
    /// <param name="label">The label assigned through <c>Label(...)</c>.</param>
    /// <returns>A handle for the matched step.</returns>
    /// <exception cref="StepLabelNotFoundException">Thrown when no step matches the label.</exception>
    /// <exception cref="StepLabelAmbiguousException">Thrown when more than one step matches the label.</exception>
    public StepHandle Step(string label)
    {
        if (!TryGetWithLabel(label, out var instances))
            throw new StepLabelNotFoundException(label, GetAvailableLabels());
        if (instances.Count > 1)
            throw new StepLabelAmbiguousException(label, instances.Count);
        return new StepHandle(instances[0], _logger);
    }

    /// <summary>
    /// Returns all step instances with the given label.
    /// </summary>
    /// <param name="label">The label assigned through <c>Label(...)</c>.</param>
    /// <returns>All matching step handles.</returns>
    /// <exception cref="StepLabelNotFoundException">Thrown when no step matches the label.</exception>
    public IReadOnlyList<StepHandle> Steps(string label)
    {
        if (!TryGetWithLabel(label, out var instances))
            throw new StepLabelNotFoundException(label, GetAvailableLabels());
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
        return new VariableHandle<T>(value, exists, identifier.ToString(), _logger, () => VariableStore.Missing(identifier), VariableStore.IsSecret(identifier));
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
                // A step that never ran did not complete either. Passing over it is how a run cancelled
                // between stages - with nothing failed in it - used to pass here.
                if (step.RetryResults.Count == 0)
                {
                    failedSteps.Add(new FailedStepInfo(stage.Stage.Name, step.Step.Name, null) { SkipReason = "It has no result: the run never reached it." });
                    continue;
                }

                var lastResult = step.RetryResults.Last();
                if (lastResult.State == StepState.Complete)
                    continue;

                failedSteps.Add(new FailedStepInfo(stage.Stage.Name, step.Step.Name, lastResult.Exception)
                {
                    SkipReason = lastResult.State == StepState.Skipped ? lastResult.SkipReason ?? "It was skipped." : null,
                });
            }
        }
        if (failedSteps.Count > 0)
            throw new TimelineRunFailedException(failedSteps);
    }
}