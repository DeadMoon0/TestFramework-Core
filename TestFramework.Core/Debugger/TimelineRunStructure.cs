using System.Collections.Generic;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Debugger;

public record TimelineRunStructure
{
    public required DebugStageState[] Stages { get; init; }
    public required IReadOnlyDictionary<VariableIdentifier, VariableState> Variables { get; init; }
    public required IReadOnlyDictionary<ArtifactIdentifier, ArtifactState> Artifacts { get; init; }
}