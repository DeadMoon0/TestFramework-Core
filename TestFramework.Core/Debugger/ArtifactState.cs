namespace TestFramework.Core.Debugger;

public record ArtifactState
{
    public required string Key { get; init; }
    public required string KindName { get; init; }
    public required string Reference { get; init; }
    public required string Data { get; init; }
}