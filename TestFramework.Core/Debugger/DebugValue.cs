using System.ComponentModel;

namespace TestFramework.Core.Debugger;

/// <summary>
/// One value a run holds, whatever kind of thing it is.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public record DebugValue
{
    /// <summary>Gets the identifier the value is known by within the run.</summary>
    public required string Key { get; init; }

    /// <summary>Gets what the value is and what it is worth showing.</summary>
    public required DebugValueEnvelope Envelope { get; init; }
}
