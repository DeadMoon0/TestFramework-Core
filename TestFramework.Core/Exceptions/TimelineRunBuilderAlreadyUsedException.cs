namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a run builder returned by <c>Timeline.SetupRun(...)</c> is used a second time.
/// </summary>
/// <remarks>
/// A run builder owns the stores of exactly one run. Once its run has started, those stores belong
/// to that run and are frozen when it finishes, so a second use would either mutate a finished run
/// or silently reuse its state.
/// </remarks>
public class TimelineRunBuilderAlreadyUsedException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception for a run builder that has already started a run.
    /// </summary>
    /// <param name="operation">The operation that was attempted on the spent builder.</param>
    public TimelineRunBuilderAlreadyUsedException(string operation)
        : base(
            $"This run builder has already started a run, so '{operation}' is no longer valid on it.",
            new[]
            {
                "Call timeline.SetupRun(...) again to get a fresh run builder for the next run.",
                "Configure the builder with AddVariable / AddArtifact / SetEnv before calling RunAsync().",
                "Do not hold on to a run builder after RunAsync(); hold on to the returned TimelineRun instead."
            },
            new[]
            {
                "timeline.SetupRun(...) - starts a new, independent run of the same built timeline."
            })
    {
    }
}
