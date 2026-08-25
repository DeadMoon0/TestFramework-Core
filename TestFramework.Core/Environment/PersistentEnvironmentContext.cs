using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Steps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;
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
    private readonly IReadOnlyCollection<EnvComponentIdentifier> _persistentRoots;
    private readonly TimeSpan _persistentSetupTimeout;
    private bool _disposed;

    private const string BlockingConstructorObsoleteMessage =
        "This constructor blocks the calling thread for the entire bootstrap — up to the persistent setup timeout, two minutes by default — and deadlocks under a SynchronizationContext. Use PersistentEnvironmentContext<TSetup>.CreateAsync(...) instead.";

    /// <summary>
    /// Creates and bootstraps the persistent environment slice immediately.
    /// </summary>
    [Obsolete(BlockingConstructorObsoleteMessage)]
    public PersistentEnvironmentContext(IServiceProvider? persistentServiceProvider = null, bool disposePersistentServiceProvider = false)
        : this(new TSetup(), persistentServiceProvider, disposePersistentServiceProvider)
    {
    }

    /// <summary>
    /// Creates and bootstraps the persistent environment slice immediately using an explicit setup instance.
    /// </summary>
    [Obsolete(BlockingConstructorObsoleteMessage)]
    public PersistentEnvironmentContext(TSetup setup, IServiceProvider? persistentServiceProvider = null, bool disposePersistentServiceProvider = false)
        : this(setup, persistentServiceProvider, disposePersistentServiceProvider, bootstrap: false)
    {
        try
        {
            BootstrapPersistentComponentsAsync(_persistentRoots, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
            DisposePersistentComponentsAsync().GetAwaiter().GetResult();
            throw;
        }
    }

    private PersistentEnvironmentContext(TSetup setup, IServiceProvider? persistentServiceProvider, bool disposePersistentServiceProvider, bool bootstrap)
    {
        ArgumentNullException.ThrowIfNull(setup);

        _setup = setup;
        _persistentServiceProvider = persistentServiceProvider ?? new EmptyServiceProvider();
        _disposePersistentServiceProvider = disposePersistentServiceProvider;
        _persistentSetupTimeout = ValidatePersistentSetupTimeout(_setup.GetPersistentSetupTimeout());
        _bootstrapEnvironment = _setup.CreateEnvironment();

        _persistentRoots = ValidatePersistentRoots(_setup.GetPersistentComponentIdentifiers());
        ValidatePersistentClosure(_bootstrapEnvironment, _persistentRoots);
        _persistentComponents = [.. EnvComponentGraph.Order(_bootstrapEnvironment, _persistentRoots).Select(component => component.Id)];

        // 'bootstrap' exists only so CreateAsync can build the object and then await the bootstrap.
        // A constructor cannot await, which is the whole reason the blocking overloads are obsolete.
        _ = bootstrap;
    }

    /// <summary>
    /// Creates the persistent environment slice and awaits its bootstrap.
    /// </summary>
    /// <param name="persistentServiceProvider">Services available to the persistent components.</param>
    /// <param name="disposePersistentServiceProvider">Whether disposing this context also disposes the provider.</param>
    /// <param name="cancellationToken">What stops the bootstrap.</param>
    /// <returns>The bootstrapped context.</returns>
    public static Task<PersistentEnvironmentContext<TSetup>> CreateAsync(
        IServiceProvider? persistentServiceProvider = null,
        bool disposePersistentServiceProvider = false,
        CancellationToken cancellationToken = default)
        => CreateAsync(new TSetup(), persistentServiceProvider, disposePersistentServiceProvider, cancellationToken);

    /// <summary>
    /// Creates the persistent environment slice from an explicit setup instance and awaits its bootstrap.
    /// </summary>
    /// <param name="setup">The persistent environment setup.</param>
    /// <param name="persistentServiceProvider">Services available to the persistent components.</param>
    /// <param name="disposePersistentServiceProvider">Whether disposing this context also disposes the provider.</param>
    /// <param name="cancellationToken">What stops the bootstrap.</param>
    /// <returns>The bootstrapped context.</returns>
    public static async Task<PersistentEnvironmentContext<TSetup>> CreateAsync(
        TSetup setup,
        IServiceProvider? persistentServiceProvider = null,
        bool disposePersistentServiceProvider = false,
        CancellationToken cancellationToken = default)
    {
        PersistentEnvironmentContext<TSetup> context = new(setup, persistentServiceProvider, disposePersistentServiceProvider, bootstrap: false);

        try
        {
            await context.BootstrapPersistentComponentsAsync(context._persistentRoots, cancellationToken);
        }
        catch
        {
            // Whatever was already created has to come back down, or the next attempt inherits it.
            await context.DisposePersistentComponentsAsync();
            throw;
        }

        return context;
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
            throw new FrameworkConfigurationException("Persistent environment setup must declare at least one persistent component root.");

        HashSet<string> seenIdentifiers = [];
        foreach (EnvComponentIdentifier identifier in persistentRoots)
        {
            if (string.IsNullOrWhiteSpace(identifier.Identifier))
                throw new FrameworkConfigurationException("Persistent environment setup contains an empty component identifier.");

            if (!seenIdentifiers.Add(identifier.Identifier))
                throw new FrameworkConfigurationException($"Persistent environment setup contains duplicate component identifier '{identifier}'.");

            EnvComponent component = _bootstrapEnvironment.GetComponent(identifier);
            if (component.ReuseMode != EnvComponentReuseMode.PersistentContext)
                throw new FrameworkConfigurationException($"Persistent environment component '{identifier}' must opt into '{EnvComponentReuseMode.PersistentContext}'.");
        }

        return persistentRoots;
    }

    private static TimeSpan ValidatePersistentSetupTimeout(TimeSpan timeout)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
            return timeout;

        if (timeout <= TimeSpan.Zero)
            throw new FrameworkConfigurationException("Persistent environment setup timeout must be greater than zero or Timeout.InfiniteTimeSpan.");

        return timeout;
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
            throw new FrameworkConfigurationException($"Persistent environment component '{root}' depends on per-run component '{current}'. Split the dependency or mark the dependency as persistent.");

        foreach (EnvComponentIdentifier dependency in component.Dependencies)
            VisitPersistentClosure(environment, dependency, root, visited);
    }

    private async Task BootstrapPersistentComponentsAsync(IReadOnlyCollection<EnvComponentIdentifier> persistentRoots, CancellationToken cancellationToken)
    {
        DebuggingRunSession debuggingSession = new(CommonDebugger.GetCommon(_persistentServiceProvider, null));
        ScopedLogger logger = ScopedLogger.CreateWithDebuggerSession(debuggingSession);
        VariableStore variableStore = new(logger, debuggingSession);
        ArtifactStore artifactStore = new(logger, debuggingSession);

        using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_persistentSetupTimeout != Timeout.InfiniteTimeSpan)
            cancellationTokenSource.CancelAfter(_persistentSetupTimeout);

        try
        {
            RunContext context = RunContext.Ambient(
                _persistentServiceProvider,
                variableStore,
                artifactStore,
                logger,

                // Empty on purpose: a persistent bootstrap declares no resources of its own, and saying so
                // is better than handing its components a resolution belonging to some later run.
                ValueResolution.Empty,
                cancellationTokenSource.Token,
                _persistentSetupTimeout == Timeout.InfiniteTimeSpan ? null : _persistentSetupTimeout);

            await EnvComponentLifecycleRunner.CreateAsync(_bootstrapEnvironment, persistentRoots, context, (identifier, state) =>
                {
                    _persistentStates[identifier] = state;
                    if (!_persistentCreationOrder.Contains(identifier))
                        _persistentCreationOrder.Add(identifier);
                });
        }
        catch (OperationCanceledException exception) when (cancellationTokenSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Only the configured timeout fired; a caller-driven cancellation stays an OperationCanceledException.
            throw new FrameworkTimeoutException($"Persistent environment setup exceeded the configured timeout of {_persistentSetupTimeout} while bootstrapping roots: {string.Join(", ", persistentRoots)}.", exception);
        }
    }

    private async Task DisposePersistentComponentsAsync()
    {
        if (_persistentCreationOrder.Count == 0)
            return;

        DebuggingRunSession debuggingSession = new(CommonDebugger.GetCommon(_persistentServiceProvider, null));
        ScopedLogger logger = ScopedLogger.CreateWithDebuggerSession(debuggingSession);
        VariableStore variableStore = new(logger, debuggingSession);
        ArtifactStore artifactStore = new(logger, debuggingSession);

        // Teardown has no deadline: cutting a release short is how a resource is stranded, which is the
        // thing this path exists to prevent.
        RunContext context = RunContext.Ambient(
            _persistentServiceProvider,
            variableStore,
            artifactStore,
            logger,
            ValueResolution.Empty);

        await EnvComponentLifecycleRunner.DeconstructAsync(
            _bootstrapEnvironment,
            _persistentCreationOrder,
            context,
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
                throw new FrameworkStateException($"Persistent environment component '{identifier}' was not created during bootstrap.");

            return new PersistentStateEnvComponent(innerComponent, state);
        }
    }

    private sealed class PersistentStateEnvComponent(EnvComponent inner, object? state) : EnvComponent
    {
        public override EnvComponentIdentifier Id => inner.Id;

        public override EnvComponentReuseMode ReuseMode => inner.ReuseMode;

        public override IReadOnlyList<EnvComponentIdentifier> Dependencies => inner.Dependencies;

        public override Task<object?> CreateAsync(IEnvironmentProvider environment, RunContext context)
            => Task.FromResult(state);

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, RunContext context)
            => Task.CompletedTask;
    }
}