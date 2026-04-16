namespace TestFrameworkCore.Debugger;

public record DebugStageState
{
    public required string Name { get; init; }
    public required string Description { get; init; }

    public required DebugStepState[] Steps { get; init; }
}