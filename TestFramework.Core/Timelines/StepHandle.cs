using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Assertions;

namespace TestFramework.Core.Timelines;

/// <summary>
/// Provides consumer-friendly access to the state and assertions of a single executed step.
/// </summary>
public class StepHandle
{
    private readonly StepInstanceGeneric _instance;
    private readonly ScopedLogger? _logger;

    internal StepHandle(StepInstanceGeneric instance, ScopedLogger? logger)
    {
        _instance = instance;
        _logger = logger;
    }

    /// <summary>
    /// Gets the label assigned to the step, if one was configured.
    /// </summary>
    public string? Label => _instance.Step.LabelOptions.Label;

    /// <summary>
    /// Gets the current terminal state of the step instance.
    /// </summary>
    public StepState State => _instance.State;

    /// <summary>
    /// Gets the last recorded result for the step.
    /// </summary>
    public StepResultGeneric LastResult => _instance.LastResult;

    /// <summary>
    /// Gets all retry-attempt results for the step.
    /// </summary>
    public IFreezableCollection<StepResultGeneric> RetryResults => _instance.RetryResults;

    /// <summary>
    /// Gets the underlying step definition.
    /// </summary>
    public StepGeneric Step => _instance.Step;

    /// <summary>
    /// Creates a step assertion helper for this step.
    /// </summary>
    public StepAsserter Should() => new StepAsserter(_instance, _logger);

    internal ScopedLogger? Logger => _logger;

    /// <summary>
    /// Converts a step handle back to its underlying step instance.
    /// </summary>
    public static implicit operator StepInstanceGeneric(StepHandle handle) => handle._instance;
}
