using System.ComponentModel;
using System.Collections.Generic;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Represents the debugger-facing structure of a timeline run.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public record TimelineRunStructure
{
    /// <summary>
    /// Gets the stages in the run.
    /// </summary>
    public required DebugStageState[] Stages { get; init; }
    /// <summary>
    /// Gets the variables visible in the run.
    /// </summary>
    public required IReadOnlyDictionary<VariableIdentifier, VariableState> Variables { get; init; }
    /// <summary>
    /// Gets the artifacts visible in the run.
    /// </summary>
    public required IReadOnlyDictionary<ArtifactIdentifier, ArtifactState> Artifacts { get; init; }
}