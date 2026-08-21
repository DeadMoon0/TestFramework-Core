namespace TestFramework.Core.Steps.SystemSteps;

/// <summary>
/// Implemented by the steps that put artifact instances into the store, so the builder can carry a
/// <c>MarkReadonly()</c> choice from the call site down to the instance.
/// </summary>
/// <remarks>
/// Only the two producing steps implement this. Verbs that operate on an artifact which already
/// exists - setup, version capture, removal - have nothing to mark, and the run-level
/// <c>AddArtifact</c> seed is owned by the run that created it.
/// </remarks>
internal interface IMarkArtifactsReadonly
{
    /// <summary>
    /// Gets or sets a value indicating whether every artifact this step produces is readonly.
    /// </summary>
    bool MarkArtifactsReadonly { get; set; }
}
