using System;
using TestFramework.Core.Debugger;
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
    /// <summary>The name the run knows it by, unquoted: what a consumer matches the check against.</summary>
    /// <remarks>
    /// Kept apart from the quoted form the failure message uses. Reporting the quoted one is what kept a board
    /// from ever tying a check made on a finished run to the value it checked - its values are keyed by name.
    /// </remarks>
    private readonly string _name;

    internal StepAsserter(StepInstanceGeneric step, ScopedLogger? logger = null)
    {
        _step = step;
        _logger = logger;
        _name = step.Step.LabelOptions.Label ?? step.Step.Name;
        _display = $"'{_name}'";
    }

    private StepAsserter Pass(string assertion)
    {
        _logger?.SignalAssertion(DebugAssertionTargetKind.Step, _name, assertion, [], true, ActualFor(assertion));
        return this;
    }

    private StepAsserter Fail(string assertion, string reason)
    {
        _logger?.SignalAssertion(DebugAssertionTargetKind.Step, _name, assertion, [], false, ActualFor(assertion));
        var message = $"Step {_display}: {assertion} failed \u2014 {reason}";
        AssertionFailure.Raise(_logger, message, static (text, site) => new StepAssertionException(text, site));
        return this;
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

    private string ExpectedFor(string assertion)
    {
        return assertion switch
        {
            nameof(HaveCompleted) => StepState.Complete.ToString(),
            nameof(HaveBeenSkipped) => StepState.Skipped.ToString(),
            nameof(HaveTimedOut) => StepState.Timeout.ToString(),
            nameof(HaveErrored) => StepState.Error.ToString(),
            _ => typeof(Exception).Name
        };
    }

    private string ActualFor(string assertion)
    {
        return assertion == nameof(HaveThrown)
            ? _step.LastResult.Exception?.GetType().Name ?? "nothing"
            : _step.State.ToString();
    }
}
