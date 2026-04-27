using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines;

namespace TestFramework.Core.Timelines.Assertions;

/// <summary>
/// Adds assertion-entry helpers for raw step instances and higher-level step handles.
/// </summary>
public static class StepAssertionExtensions
{
    // Raw instance (no logger)
    /// <summary>
    /// Starts an assertion chain for a raw step instance.
    /// </summary>
    /// <param name="step">The step instance to assert on.</param>
    public static StepAsserter Should(this StepInstanceGeneric step) => new StepAsserter(step);

    /// <summary>
    /// Starts a list assertion chain for raw step instances.
    /// </summary>
    /// <param name="steps">The step instances to assert on.</param>
    public static StepListAsserter Should(this IReadOnlyList<StepInstanceGeneric> steps) => new StepListAsserter(steps);

    // Via StepHandle (logger flows through)
    /// <summary>
    /// Starts an assertion chain for a step handle.
    /// </summary>
    /// <param name="handle">The step handle to assert on.</param>
    public static StepAsserter Should(this StepHandle handle) => handle.Should();

    /// <summary>
    /// Starts a list assertion chain for step handles.
    /// </summary>
    /// <param name="handles">The step handles to assert on.</param>
    public static StepListAsserter Should(this IReadOnlyList<StepHandle> handles) =>
        new StepListAsserter(
            handles.Select(h => (StepInstanceGeneric)h).ToList(),
            handles.Count > 0 ? handles[0].Label : null,
            handles.Count > 0 ? handles[0].Logger : null);
}
