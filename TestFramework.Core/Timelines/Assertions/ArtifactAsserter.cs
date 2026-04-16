using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Timelines.Assertions;

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
        _logger?.EnsureAssertionHeaderPrinted();
        _logger?.LogInformation($"[ASSERT]  {_display}  {assertion}  [PASS]");
        return this;
    }

    private ArtifactAsserter Fail(string assertion, string reason)
    {
        _logger?.EnsureAssertionHeaderPrinted();
        _logger?.LogInformation($"[ASSERT]  {_display}  {assertion}  [FAIL]  {reason}");
        var message = $"Artifact {_display}: {assertion} failed \u2014 {reason}";
        if (_logger?.CurrentScope is { } scope)
        {
            scope.RecordFailure(message);
            return this;
        }
        throw new ValueAssertionException(message);
    }

    public ArtifactAsserter Exist()
    {
        if (_instance.VersionCount == 0)
            return Fail(nameof(Exist), "artifact has no data versions");
        return Pass(nameof(Exist));
    }

    public ArtifactAsserter NotExist()
    {
        if (_instance.VersionCount > 0)
            return Fail(nameof(NotExist), $"artifact has {_instance.VersionCount} data version(s)");
        return Pass(nameof(NotExist));
    }

    public ArtifactAsserter And() => this;
}
