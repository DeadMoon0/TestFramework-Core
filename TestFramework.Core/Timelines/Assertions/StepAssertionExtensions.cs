using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines;

namespace TestFramework.Core.Timelines.Assertions;

public static class StepAssertionExtensions
{
    // Raw instance (no logger)
    public static StepAsserter Should(this StepInstanceGeneric step) => new StepAsserter(step);
    public static StepListAsserter Should(this IReadOnlyList<StepInstanceGeneric> steps) => new StepListAsserter(steps);

    // Via StepHandle (logger flows through)
    public static StepAsserter Should(this StepHandle handle) => handle.Should();
    public static StepListAsserter Should(this IReadOnlyList<StepHandle> handles) =>
        new StepListAsserter(
            handles.Select(h => (StepInstanceGeneric)h).ToList(),
            handles.Count > 0 ? handles[0].Label : null,
            handles.Count > 0 ? handles[0].Logger : null);
}
