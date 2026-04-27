namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Identifies whether a declared step IO contract entry refers to a variable or an artifact.
/// </summary>
public enum StepIOKind
{
    /// <summary>
    /// The IO entry refers to a variable.
    /// </summary>
    Variable,

    /// <summary>
    /// The IO entry refers to an artifact.
    /// </summary>
    Artifact
}
