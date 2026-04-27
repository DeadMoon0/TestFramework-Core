using System;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Timelines.Assertions;

/// <summary>
/// Wraps an artifact lookup result so consumers can assert on existence or project its latest data version.
/// </summary>
public class ArtifactHandle
{
    private readonly ArtifactInstanceGeneric _instance;
    private readonly ScopedLogger? _logger;

    internal ArtifactHandle(ArtifactInstanceGeneric instance, ScopedLogger? logger)
    {
        _instance = instance;
        _logger = logger;
    }

    /// <summary>
    /// Starts an assertion chain for the artifact.
    /// </summary>
    public ArtifactAsserter Should() => new ArtifactAsserter(_instance, _logger);

    /// <summary>
    /// Projects the artifact's latest data version to another value while preserving the logging context.
    /// </summary>
    /// <typeparam name="T">The projected value type.</typeparam>
    /// <param name="selector">Maps the latest artifact data version to a new value.</param>
    public ValueHandle<T> Select<T>(Func<ArtifactDataGeneric, T> selector)
        => new ValueHandle<T>(selector(_instance.Last), _instance.Identifier.ToString(), _logger);
}

/// <summary>
/// Wraps a typed artifact lookup result so consumers can work against a known artifact data type.
/// </summary>
/// <typeparam name="TData">The expected artifact data type.</typeparam>
public class ArtifactHandle<TData> where TData : ArtifactDataGeneric
{
    private readonly ArtifactInstanceGeneric _instance;
    private readonly ScopedLogger? _logger;

    internal ArtifactHandle(ArtifactInstanceGeneric instance, ScopedLogger? logger)
    {
        _instance = instance;
        _logger = logger;
    }

    /// <summary>
    /// Starts an assertion chain for the artifact.
    /// </summary>
    public ArtifactAsserter Should() => new ArtifactAsserter(_instance, _logger);

    /// <summary>
    /// Projects the typed latest artifact data version to another value while preserving the logging context.
    /// </summary>
    /// <typeparam name="TNew">The projected value type.</typeparam>
    /// <param name="selector">Maps the latest typed artifact data version to a new value.</param>
    public ValueHandle<TNew> Select<TNew>(Func<TData, TNew> selector)
        => new ValueHandle<TNew>(selector((TData)_instance.Last), _instance.Identifier.ToString(), _logger);
}
