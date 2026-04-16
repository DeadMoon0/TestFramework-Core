using System.Collections.Generic;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Timelines.Assertions;

public class StepListAsserter
{
    private readonly IReadOnlyList<StepInstanceGeneric> _steps;
    private readonly ScopedLogger? _logger;
    private readonly string _label;

    internal StepListAsserter(IReadOnlyList<StepInstanceGeneric> steps, string? label = null, ScopedLogger? logger = null)
    {
        _steps = steps;
        _logger = logger;
        _label = label is not null ? $"'{label}'" : $"{steps.Count} step(s)";
    }

    public StepListAsserter AllHaveCompleted()
    {
        for (int i = 0; i < _steps.Count; i++)
        {
            var step = _steps[i];
            if (step.State != StepState.Complete)
            {
                var reason = $"step {i + 1} of {_steps.Count} ('{step.Step.Name}') was {step.State}";
                var message = $"Steps {_label}: AllHaveCompleted failed \u2014 {reason}.";
                _logger?.EnsureAssertionHeaderPrinted();
                _logger?.LogInformation($"[ASSERT]  {_label}  AllHaveCompleted  [FAIL]  {reason}");
                if (_logger?.CurrentScope is { } scope)
                {
                    scope.RecordFailure(message);
                    continue; // keep checking remaining steps
                }
                throw new StepAssertionException(message);
            }
        }
        _logger?.EnsureAssertionHeaderPrinted();
        _logger?.LogInformation($"[ASSERT]  {_label}  AllHaveCompleted  [PASS]");
        return this;
    }

    public StepListAsserter And() => this;
}
