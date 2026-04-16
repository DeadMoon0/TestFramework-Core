using TestFramework.Core.Steps.Options;

namespace TestFramework.Core.Debugger;

public record DebugStepState
{
    public required string Name { get; init; }
    public required string Description { get; init; }

    public required RetryOptions RetryOptions { get; init; }
    public required ErrorHandlingOptions ErrorHandlingOptions { get; init; }
    public required TimeOutOptions TimeOutOptions { get; init; }
    public required LabelOptions LabelOptions { get; init; }
    public required ExecutionOptions ExecutionOptions { get; init; }
    public required StepIOContract IOContract { get; init; }

    public required bool DoesReturn { get; init; }
}