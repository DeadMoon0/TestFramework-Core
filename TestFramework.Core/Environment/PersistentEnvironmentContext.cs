using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Environment.Internal;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Environment;

/// <summary>
/// Owns a persistent environment component slice that is created once and reused across later runs.
/// </summary>
public sealed class PersistentEnvironmentContext<TSetup> : IAsyncDisposable
    where TSetup : IPersistentEnvironmentSetup, new()
{
    private readonly TSetup _setup;
    private readonly IServiceProvider _persistentServiceProvider;
    private readonly bool _disposePersistentServiceProvider;
    private readonly IEnvironmentProvider _bootstrapEnvironment;
    private readonly Dictionary<EnvComponentIdentifier, object?> _persistentStates = [];
    private readonly List<EnvComponentIdentifier> _persistentCreationOrder = [];
    private readonly HashSet<EnvComponentIdentifier> _persistentComponents;
    private bool _disposed;

    /// <summary>
    /// Creates and bootstraps the persistent environment slice immediately.
    /// </summary>
    public PersistentEnvironmentContext(IServiceProvider? persistentServiceProvider = null, bool disposePersistentServiceProvider = false)
        : this(new TSetup(), persistentServiceProvider, disposePersistentServiceProvider)
    {
    }

    /// <summary>
    /// Creates and bootstraps the persistent environment slice immediately using an explicit setup instance.
    /// </summary>
    public PersistentEnvironmentContext(TSetup setup, IServiceProvider? persistentServiceProvider = null, bool disposePersistentServiceProvider = false)
    {
        ArgumentNullException.ThrowIfNull(setup);

        _setup = setup;
        _persistentServiceProvider = persistentServiceProvider ?? new EmptyServiceProvider();
        _disposePersistentServiceProvider = disposePersistentServiceProvider;
        _bootstrapEnvironment = _setup.CreateEnvironment();

        IReadOnlyCollection<EnvComponentIdentifier> persistentRoots = ValidatePersistentRoots(_setup.GetPersistentComponentIdentifiers());
        ValidatePersistentClosure(_bootstrapEnvironment, persistentRoots);
        _persistentComponents = [.. EnvComponentGraph.Order(_bootstrapEnvironment, persistentRoots).Select(component => component.Id)];

        try
        {
            BootstrapPersistentComponents(persistentRoots);
        }
        catch
        {
            DisposePersistentComponentsAsync().GetAwaiter().GetResult();
            throw;
        }
    }

    /// <summary>
    /// Creates a fresh environment provider that reuses the persistent component slice.
    /// </summary>
    public IEnvironmentProvider CreateEnvironment()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IEnvironmentProvider environment = _setup.CreateEnvironment();
        if (environment is IPersistentEnvironmentStateSink stateSink)
        {
            foreach ((EnvComponentIdentifier identifier, object? state) in _persistentStates)
                stateSink.SetPersistentState(identifier, state);
        }

        return new PersistentEnvironmentProvider(environment, _persistentComponents, _persistentStates);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await DisposePersistentComponentsAsync();

        if (_disposePersistentServiceProvider && _persistentServiceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (_disposePersistentServiceProvider && _persistentServiceProvider is IDisposable disposable)
            disposable.Dispose();

        _disposed = true;
    }

    private IReadOnlyCollection<EnvComponentIdentifier> ValidatePersistentRoots(IReadOnlyCollection<EnvComponentIdentifier> persistentRoots)
    {
        if (persistentRoots.Count == 0)
            throw new InvalidOperationException("Persistent environment setup must declare at least one persistent component root.");

        HashSet<string> seenIdentifiers = [];
        foreach (EnvComponentIdentifier identifier in persistentRoots)
        {
            if (string.IsNullOrWhiteSpace(identifier.Identifier))
                throw new InvalidOperationException("Persistent environment setup contains an empty component identifier.");

            if (!seenIdentifiers.Add(identifier.Identifier))
                throw new InvalidOperationException($"Persistent environment setup contains duplicate component identifier '{identifier}'.");

            EnvComponent component = _bootstrapEnvironment.GetComponent(identifier);
            if (component.ReuseMode != EnvComponentReuseMode.PersistentContext)
                throw new InvalidOperationException($"Persistent environment component '{identifier}' must opt into '{EnvComponentReuseMode.PersistentContext}'.");
        }

        return persistentRoots;
    }

    private static void ValidatePersistentClosure(IEnvironmentProvider environment, IEnumerable<EnvComponentIdentifier> persistentRoots)
    {
        foreach (EnvComponentIdentifier root in persistentRoots)
            VisitPersistentClosure(environment, current: root, root, visited: []);
    }

    private static void VisitPersistentClosure(IEnvironmentProvider environment, EnvComponentIdentifier current, EnvComponentIdentifier root, HashSet<EnvComponentIdentifier> visited)
    {
        if (!visited.Add(current))
            return;

        EnvComponent component = environment.GetComponent(current);
        if (component.ReuseMode != EnvComponentReuseMode.PersistentContext)
            throw new InvalidOperationException($"Persistent environment component '{root}' depends on per-run component '{current}'. Split the dependency or mark the dependency as persistent.");

        foreach (EnvComponentIdentifier dependency in component.Dependencies)
            VisitPersistentClosure(environment, dependency, root, visited);
    }

    private void BootstrapPersistentComponents(IReadOnlyCollection<EnvComponentIdentifier> persistentRoots)
    {
        ScopedLogger logger = new(outputHelper: null);
        DebuggingRunSession debuggingSession = new(((IRunDebugger?)_persistentServiceProvider.GetService(typeof(IRunDebugger))) ?? CommonDebugger.GetCommon());
        VariableStore variableStore = new(logger, debuggingSession);
        ArtifactStore artifactStore = new(logger, debuggingSession);

        EnvComponentLifecycleRunner.CreateAsync(
                _bootstrapEnvironment,
                persistentRoots,
                _persistentServiceProvider,
                variableStore,
                artifactStore,
                logger,
                CancellationToken.None,
                (identifier, state) =>
                {
                    _persistentStates[identifier] = state;
                    if (!_persistentCreationOrder.Contains(identifier))
                        _persistentCreationOrder.Add(identifier);
                })
            .GetAwaiter()
            .GetResult();
    }

    private async Task DisposePersistentComponentsAsync()
    {
        if (_persistentCreationOrder.Count == 0)
            return;

        ScopedLogger logger = new(outputHelper: null);
        DebuggingRunSession debuggingSession = new(((IRunDebugger?)_persistentServiceProvider.GetService(typeof(IRunDebugger))) ?? CommonDebugger.GetCommon());
        VariableStore variableStore = new(logger, debuggingSession);
        ArtifactStore artifactStore = new(logger, debuggingSession);

        await EnvComponentLifecycleRunner.DeconstructAsync(
            _bootstrapEnvironment,
            _persistentCreationOrder,
            _persistentServiceProvider,
            variableStore,
            artifactStore,
            logger,
            CancellationToken.None,
            identifier => _persistentStates.TryGetValue(identifier, out object? state) ? state : null);

        _persistentCreationOrder.Clear();
        _persistentStates.Clear();
    }

    private sealed class PersistentEnvironmentProvider(IEnvironmentProvider inner, IReadOnlySet<EnvComponentIdentifier> persistentComponents, IReadOnlyDictionary<EnvComponentIdentifier, object?> persistentStates) : IEnvironmentProviderProxy
    {
        public IEnvironmentProvider InnerEnvironment => inner;

        public bool SupportsParallelComponentCreation => inner.SupportsParallelComponentCreation;

        public IReadOnlyCollection<EnvComponentIdentifier> ResolveComponents(IEnumerable<ArtifactInstanceGeneric> artifacts, IEnumerable<EnvironmentRequirement> requirements)
            => inner.ResolveComponents(artifacts, requirements);

        public EnvComponent GetComponent(EnvComponentIdentifier identifier)
        {
            EnvComponent innerComponent = inner.GetComponent(identifier);
            if (!persistentComponents.Contains(identifier))
                return innerComponent;

            if (!persistentStates.TryGetValue(identifier, out object? state))
                throw new InvalidOperationException($"Persistent environment component '{identifier}' was not created during bootstrap.");

            return new PersistentStateEnvComponent(innerComponent, state);
        }
    }

    private sealed class PersistentStateEnvComponent(EnvComponent inner, object? state) : EnvComponent
    {
        public override EnvComponentIdentifier Id => inner.Id;

        public override EnvComponentReuseMode ReuseMode => inner.ReuseMode;

        public override IReadOnlyList<EnvComponentIdentifier> Dependencies => inner.Dependencies;

        public override Task<object?> CreateAsync(IEnvironmentProvider environment, IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.FromResult(state);

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}