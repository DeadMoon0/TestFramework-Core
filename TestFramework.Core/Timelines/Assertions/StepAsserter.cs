using System;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Timelines.Assertions;

/// <summary>
/// Provides fluent assertions for a single executed step.
/// </summary>
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

    /// <summary>
    /// Asserts that the step completed successfully.
    /// </summary>
    public StepAsserter HaveCompleted()
    {
        if (_step.State != StepState.Complete)
            return Fail(nameof(HaveCompleted), $"expected Complete, was {_step.State}");
        return Pass(nameof(HaveCompleted));
    }

    /// <summary>
    /// Asserts that the step was skipped.
    /// </summary>
    public StepAsserter HaveBeenSkipped()
    {
        if (_step.State != StepState.Skipped)
            return Fail(nameof(HaveBeenSkipped), $"expected Skipped, was {_step.State}");
        return Pass(nameof(HaveBeenSkipped));
    }

    /// <summary>
    /// Asserts that the step timed out.
    /// </summary>
    public StepAsserter HaveTimedOut()
    {
        if (_step.State != StepState.Timeout)
            return Fail(nameof(HaveTimedOut), $"expected Timeout, was {_step.State}");
        return Pass(nameof(HaveTimedOut));
    }

    /// <summary>
    /// Asserts that the step ended in the error state.
    /// </summary>
    public StepAsserter HaveErrored()
    {
        if (_step.State != StepState.Error)
            return Fail(nameof(HaveErrored), $"expected Error, was {_step.State}");
        return Pass(nameof(HaveErrored));
    }

    /// <summary>
    /// Asserts that the step threw an exception of the specified type.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    public StepAsserter HaveThrown<TException>() where TException : Exception
    {
        var ex = _step.LastResult.Exception;
        if (ex is not TException)
            return Fail(nameof(HaveThrown), $"expected {typeof(TException).Name}, got {ex?.GetType().Name ?? "nothing"}");
        return Pass(nameof(HaveThrown));
    }

    /// <summary>
    /// Continues the fluent assertion chain.
    /// </summary>
    public StepAsserter And() => this;
}
