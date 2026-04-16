using System;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Timelines.Assertions;

public class ArtifactHandle
{
    private readonly ArtifactInstanceGeneric _instance;
    private readonly ScopedLogger? _logger;

    internal ArtifactHandle(ArtifactInstanceGeneric instance, ScopedLogger? logger)
    {
        _instance = instance;
        _logger = logger;
    }

    public ArtifactAsserter Should() => new ArtifactAsserter(_instance, _logger);

    public ValueHandle<T> Select<T>(Func<ArtifactDataGeneric, T> selector)
        => new ValueHandle<T>(selector(_instance.Last), _instance.Identifier.ToString(), _logger);
}

public class ArtifactHandle<TData> where TData : ArtifactDataGeneric
{
    private readonly ArtifactInstanceGeneric _instance;
    private readonly ScopedLogger? _logger;

    internal ArtifactHandle(ArtifactInstanceGeneric instance, ScopedLogger? logger)
    {
        _instance = instance;
        _logger = logger;
    }

    public ArtifactAsserter Should() => new ArtifactAsserter(_instance, _logger);

    public ValueHandle<TNew> Select<TNew>(Func<TData, TNew> selector)
        => new ValueHandle<TNew>(selector((TData)_instance.Last), _instance.Identifier.ToString(), _logger);
}
