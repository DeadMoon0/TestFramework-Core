using System;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFrameworkCore.Exceptions;

namespace TestFramework.Core.Timelines.Assertions;

public class StepAsserter
{
    private readonly StepInstanceGeneric _step;
    private readonly ScopedLogger? _logger;
    private readonly string _display;

    internal StepAsserter(StepInstanceGeneric step, ScopedLogger? logger = null)
    {
        _step = step;
        _logger = logger;
        _display = step.Step.LabelOptions.Label is not null ? $"'{step.Step.LabelOptions.Label}'" : $"'{step.Step.Name}'";
    }

    private StepAsserter Pass(string assertion)
    {
        // Print a single header once per run, then the assertion line
        _logger?.EnsureAssertionHeaderPrinted();
        _logger?.LogInformation($"[ASSERT]  {_display}  {assertion}  [PASS]");
        return this;
    }

    private StepAsserter Fail(string assertion, string reason)
    {
        // Print a single header once per run, then the assertion line
        _logger?.EnsureAssertionHeaderPrinted();
        _logger?.LogInformation($"[ASSERT]  {_display}  {assertion}  [FAIL]  {reason}");
        var message = $"Step {_display}: {assertion} failed \u2014 {reason}";
        if (_logger?.CurrentScope is { } scope)
        {
            scope.RecordFailure(message);
            return this; // don't throw — scope collects it
        }
        throw new StepAssertionException(message);
    }

    public StepAsserter HaveCompleted()
    {
        if (_step.State != StepState.Complete)
            return Fail(nameof(HaveCompleted), $"expected Complete, was {_step.State}");
        return Pass(nameof(HaveCompleted));
    }

    public StepAsserter HaveBeenSkipped()
    {
        if (_step.State != StepState.Skipped)
            return Fail(nameof(HaveBeenSkipped), $"expected Skipped, was {_step.State}");
        return Pass(nameof(HaveBeenSkipped));
    }

    public StepAsserter HaveTimedOut()
    {
        if (_step.State != StepState.Timeout)
            return Fail(nameof(HaveTimedOut), $"expected Timeout, was {_step.State}");
        return Pass(nameof(HaveTimedOut));
    }

    public StepAsserter HaveErrored()
    {
        if (_step.State != StepState.Error)
            return Fail(nameof(HaveErrored), $"expected Error, was {_step.State}");
        return Pass(nameof(HaveErrored));
    }

    public StepAsserter HaveThrown<TException>() where TException : Exception
    {
        var ex = _step.LastResult.Exception;
        if (ex is not TException)
            return Fail(nameof(HaveThrown), $"expected {typeof(TException).Name}, got {ex?.GetType().Name ?? "nothing"}");
        return Pass(nameof(HaveThrown));
    }

    public StepAsserter And() => this;
}
