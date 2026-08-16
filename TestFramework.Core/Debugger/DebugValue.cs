using System;
using System.ComponentModel;

namespace TestFramework.Core.Debugger;

/// <summary>
/// One value a run holds, whatever kind of thing it is.
/// </summary>
/// <remarks>
/// <para>
/// Variables and artifacts were two records with identical members and different names. Nothing
/// downstream could act on the difference, because the difference was already in the envelope's
/// <see cref="DebugValueEnvelope.Kind"/> — so the two types bought a consumer nothing and cost it a
/// second code path for every value it handled.
/// </para>
/// <para>
/// The old artifact type also collided with <c>TestFramework.Core.Artifacts.ArtifactState</c>, the
/// enum for an artifact's lifecycle. Two unrelated things called <c>ArtifactState</c> in one library
/// meant nearly every use of either had to be written out in full to say which was meant.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public record DebugValue
{
    /// <summary>Gets the identifier the value is known by within the run.</summary>
    public required string Key { get; init; }

    /// <summary>Gets what the value is and what it is worth showing.</summary>
    public required DebugValueEnvelope Envelope { get; init; }
}

/// <summary>
/// Represents the debugger-facing state snapshot of a variable.
/// </summary>
/// <remarks>
/// Kept so code built against an earlier package still compiles and still passes where a
/// <see cref="DebugValue"/> is wanted. It adds nothing of its own.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[Obsolete("Use DebugValue. A variable and an artifact are the same shape, and the envelope already says which is which.")]
public record VariableState : DebugValue;

/// <summary>
/// Represents the debugger-facing state snapshot of an artifact.
/// </summary>
/// <remarks>
/// Kept so code built against an earlier package still compiles and still passes where a
/// <see cref="DebugValue"/> is wanted. It adds nothing of its own.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[Obsolete("Use DebugValue. It does not collide with Artifacts.ArtifactState, which is the lifecycle enum.")]
public record ArtifactState : DebugValue;
