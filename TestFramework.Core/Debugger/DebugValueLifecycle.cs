using System.ComponentModel;

namespace TestFramework.Core.Debugger;

/// <summary>
/// A value that has a life of its own: a state it is in, and the versions captured of it.
/// </summary>
/// <remarks>
/// <para>
/// Present on artifacts and absent on plain variables — which is the honest way round. A variable is
/// its current value and nothing else; an artifact is set up, changed and torn down, and a reader
/// looking at one needs to know which of those has happened before anything else on the card can be
/// trusted. A cleaned artifact's reference points at something that is gone.
/// </para>
/// <para>
/// Stated as fields rather than left in the envelope's <c>Core</c> JSON, where a consumer had to
/// know the property names, know the shapes, and cope with them being absent. That is the same
/// defect <see cref="DebugValueDescription"/> fixed for presentation: facts pushed through an
/// untyped blob that every consumer then has to parse back out.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record DebugValueLifecycle
{
    /// <summary>Gets the state the value is in, such as <c>Setup</c> or <c>Cleaned</c>.</summary>
    public required string State { get; init; }

    /// <summary>
    /// Gets the identifiers of every captured version, oldest first.
    /// </summary>
    /// <remarks>
    /// The whole history on every update, so a consumer that attached late — or replayed a recording
    /// missing the earlier events — still shows v1 → v2 → v3 rather than only what it witnessed.
    /// </remarks>
    public string[] Versions { get; init; } = [];

    /// <summary>Gets the identifier of the version currently in hand, when there is one.</summary>
    public string? CurrentVersion { get; init; }
}
