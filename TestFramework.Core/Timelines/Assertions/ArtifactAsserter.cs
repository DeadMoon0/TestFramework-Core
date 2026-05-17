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

    internal ArtifactAsserter(ArtifactInstanceGeneric instance, ScopedLogger? logger)
    {
        _instance = instance;
        _logger = logger;
        _display = $"'{instance.Identifier}'";
    }

    private ArtifactAsserter Pass(string assertion)
    {
        _logger?.SignalAssertion(DebugAssertionTargetKind.Artifact, _display, assertion, assertion, true, assertion == nameof(NotExist) ? "0 versions" : ">= 1 version", _instance.VersionCount.ToString());
        return this;
    }

    private ArtifactAsserter Fail(string assertion, string reason)
    {
        _logger?.SignalAssertion(DebugAssertionTargetKind.Artifact, _display, assertion, assertion, false, assertion == nameof(NotExist) ? "0 versions" : ">= 1 version", _instance.VersionCount.ToString(), reason);
        var message = $"Artifact {_display}: {assertion} failed \u2014 {reason}";
        if (_logger?.CurrentScope is { } scope)
        {
            scope.RecordFailure(message);
            return this;
        }
        throw new ValueAssertionException(message);
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
