using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Timelines.Assertions;

/// <summary>
/// Provides fluent assertions for an artifact lookup result.
/// </summary>
public class ArtifactAsserter
{
    private readonly ArtifactInstanceGeneric _instance;
    private readonly ScopedLogger? _logger;
    private readonly string _display;
    /// <summary>The name the run knows it by, unquoted: what a consumer matches the check against.</summary>
    /// <remarks>
    /// Kept apart from the quoted form the failure message uses. Reporting the quoted one is what kept a board
    /// from ever tying a check made on a finished run to the value it checked - its values are keyed by name.
    /// </remarks>
    private readonly string _name;

    internal ArtifactAsserter(ArtifactInstanceGeneric instance, ScopedLogger? logger)
    {
        _instance = instance;
        _logger = logger;
        _name = instance.Identifier.Identifier;
        _display = $"'{instance.Identifier}'";
    }

    private ArtifactAsserter Pass(string assertion)
    {
        _logger?.SignalAssertion(DebugAssertionTargetKind.Artifact, _name, assertion, [], true, _instance.VersionCount);
        return this;
    }

    private ArtifactAsserter Fail(string assertion, string reason)
    {
        _logger?.SignalAssertion(DebugAssertionTargetKind.Artifact, _name, assertion, [], false, _instance.VersionCount);
        var message = $"Artifact {_display}: {assertion} failed \u2014 {reason}";
        AssertionFailure.Raise(_logger, message, static (text, site) => new ValueAssertionException(text, site));
        return this;
    }

    /// <summary>
    /// Asserts that the artifact has at least one data version.
    /// </summary>
    public ArtifactAsserter Exist()
    {
        if (_instance.VersionCount == 0)
            return Fail(nameof(Exist), "artifact has no data versions");
        return Pass(nameof(Exist));
    }

    /// <summary>
    /// Asserts that the artifact has no data versions.
    /// </summary>
    public ArtifactAsserter NotExist()
    {
        if (_instance.VersionCount > 0)
            return Fail(nameof(NotExist), $"artifact has {_instance.VersionCount} data version(s)");
        return Pass(nameof(NotExist));
    }

    /// <summary>
    /// Continues the fluent assertion chain.
    /// </summary>
    public ArtifactAsserter And() => this;
}
