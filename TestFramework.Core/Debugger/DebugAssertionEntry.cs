using System;
using Newtonsoft.Json;

namespace TestFramework.Core.Debugger;

/// <summary>
/// One check the run made, and what it found.
/// </summary>
/// <remarks>
/// <para>
/// A check is its name and its arguments — <c>Be</c> with <c>expected: "Ada"</c> — and that pair states the
/// expectation exactly. This record used to carry three renderings of it instead: a display string
/// (<c>Be("Ada")</c>), an <c>Expected</c> string, and a <c>FailureReason</c> sentence assembled from both plus
/// the observed value. All three are recoverable from the facts here, and none of them could be compared,
/// grouped or diffed.
/// </para>
/// <para>
/// The observed value is described rather than stringified, so a check on a four-thousand-element collection
/// arrives as a collection, with its shape and a preview, instead of as a cut-off sentence.
/// </para>
/// </remarks>
public sealed record DebugAssertionEntry
{
    /// <summary>Gets when the assertion was evaluated.</summary>
    public DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>Gets what kind of thing was checked.</summary>
    public DebugAssertionTargetKind TargetKind { get; init; }

    /// <summary>Gets what was checked, by name — the expression, variable or artifact.</summary>
    public string Target { get; init; } = "";

    /// <summary>Gets the check's name, such as <c>Be</c>, <c>Contain</c> or <c>NotExist</c>.</summary>
    public string AssertionName { get; init; } = "";

    /// <summary>
    /// Gets the check's own parameters, typed as they were passed.
    /// </summary>
    /// <remarks>
    /// Empty for a check that takes none — <c>BeNull</c> expects null and says so by its name. A parameter that
    /// cannot travel, such as the delegate behind <c>Match</c>, is left out rather than described.
    /// </remarks>
    public DebugLogField[] Arguments { get; init; } = [];

    /// <summary>Gets whether the check held.</summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the value as it actually was, described.
    /// </summary>
    /// <remarks>
    /// Replaced rather than populated on deserialization. The default here is the shared
    /// <see cref="DebugValueDescription.Empty"/> singleton, and a deserializer left to its own devices writes
    /// each arriving description <em>into</em> it — corrupting the one object that is supposed to mean "nothing
    /// was described" for every other reader in the process.
    /// </remarks>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public DebugValueDescription Actual { get; init; } = DebugValueDescription.Empty;

    /// <summary>Gets the assertion scope that was open, when there was one.</summary>
    public string AssertionScope { get; init; } = "";
}
