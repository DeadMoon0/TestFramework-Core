namespace TestFramework.Core.Debugger;

public record VariableState
{
    public required string Key { get; init; }
    public required string TypeName { get; init; }
    public required string Value { get; init; }
}