using System.Collections.Generic;
using TestFrameworkCore.Artifacts;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Debugger;

public record TimelineRunStructure
{
    public required DebugStageState[] Stages { get; init; }
    public required IReadOnlyDictionary<VariableIdentifier, VariableState> Variables { get; init; }
    public required IReadOnlyDictionary<ArtifactIdentifier, ArtifactState> Artifacts { get; init; }
}