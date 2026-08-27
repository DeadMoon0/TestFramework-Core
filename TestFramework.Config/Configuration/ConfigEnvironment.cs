using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Core.Environment.Graph;

namespace TestFramework.Config.Configuration;

/// <summary>
/// The environment every run composes first: what an author declared, relayed as resources.
/// </summary>
/// <remarks>
/// <para>
/// It edits nothing and starts nothing. Each configured entry becomes a node with no lifecycle, holding
/// the values that entry declares, so a run that provisions nothing keeps exactly the plan shape it
/// always had - and a run that does provision stacks its environments on top, shadowing per value.
/// </para>
/// <para>
/// This is what removes the branch every consumer used to carry. "Ask the environment, else read the
/// file" is gone because the file <em>is</em> in the graph: a step asks once, and never learns whether a
/// container or a person answered.
/// </para>
/// </remarks>
public sealed class ConfigEnvironment : DeclaredNodeSource
{
    private readonly IConfiguration configuration;
    private readonly IReadOnlyList<IConfigShape> shapes;

    private ConfigEnvironment(IConfiguration configuration, IReadOnlyList<IConfigShape> shapes)
    {
        this.configuration = configuration;
        this.shapes = shapes;
    }

    /// <inheritdoc />
    public override string SourceName => "configuration";

    /// <summary>
    /// Every configured entry, as a declared resource.
    /// </summary>
    /// <remarks>
    /// Read when the graph is first composed rather than at construction, because shapes are registered
    /// in whatever order packages happen to load in and nothing should depend on being last.
    /// </remarks>
    protected override IEnumerable<DeclaredResource> Declarations
    {
        get
        {
            foreach (IConfigShape shape in this.shapes)
            {
                foreach (string identifier in shape.Identifiers(this.configuration))
                {
                    yield return new DeclaredResource(
                        shape.Kind,
                        identifier,
                        shape.Values(shape.Read(this.configuration, identifier)),
                        $"section '{shape.Section}'");
                }
            }
        }
    }

    /// <summary>
    /// Builds the relay from the registered shapes.
    /// </summary>
    /// <param name="services">The run's services, holding the configuration and the shapes.</param>
    /// <returns>The relay.</returns>
    public static ConfigEnvironment From(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new ConfigEnvironment(
            services.GetRequiredService<IConfiguration>(),
            [.. services.GetServices<IConfigShape>()]);
    }

}

/// <summary>
/// Registers configuration shapes and the relay that turns them into resources.
/// </summary>
public static class ConfigShapeRegistration
{
    /// <summary>
    /// Registers one shape, and the relay if it is not registered yet.
    /// </summary>
    /// <remarks>
    /// Called by each package's own <c>Load…Config</c> extension, once per configuration record it owns.
    /// The store for that record is registered here too, so a package never has to remember to.
    /// </remarks>
    /// <typeparam name="TShape">The shape.</typeparam>
    /// <typeparam name="TConfig">The configuration record it reads.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddConfigShape<TShape, TConfig>(this IServiceCollection services)
        where TShape : ConfigShape<TConfig>, new()
        where TConfig : class
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddConfigShape(new TShape());
    }

    /// <summary>
    /// Registers a shape that had to be built by its package.
    /// </summary>
    /// <remarks>
    /// A shape usually reads a section by delegating to whatever its package already uses for that, and that
    /// reader is often supplied by the caller - a custom provider, a different validation. Such a shape cannot
    /// be constructed by the engine, which is why this overload exists: without it a package's only way in was
    /// a parameterless shape, so its shape and its own loader would have read the same section by two
    /// different routes and been free to disagree.
    /// </remarks>
    /// <typeparam name="TConfig">The configuration record it reads.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="shape">The shape.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddConfigShape<TConfig>(this IServiceCollection services, ConfigShape<TConfig> shape)
        where TConfig : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(shape);

        services.AddSingleton<IConfigShape>(shape);

        // Registered even when the section is empty, so a missing entry reads as "nothing is configured
        // under that name" rather than as a missing package. The same instance reads it - a shape built twice
        // is a shape whose two copies can be configured differently.
        services.AddSingleton(implementationFactory: provider => BuildStore(shape, provider));

        return services.AddConfigRelay();
    }

    /// <summary>
    /// Registers the relay, once.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddConfigRelay(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(static service => service.ServiceType == typeof(ConfigEnvironment)))
        {
            return services;
        }

        services.AddSingleton(ConfigEnvironment.From);
        services.AddSingleton<IResourceNodeSource>(provider => provider.GetRequiredService<ConfigEnvironment>());

        return services;
    }

    private static ConfigStore<TConfig> BuildStore<TConfig>(ConfigShape<TConfig> shape, IServiceProvider provider)
        where TConfig : class
    {
        IConfiguration configuration = provider.GetRequiredService<IConfiguration>();
        ConfigStore<TConfig> store = new ConfigStore<TConfig>();

        foreach (string identifier in shape.Identifiers(configuration))
        {
            store.Add(identifier, shape.Read(configuration, identifier));
        }

        // Declarations are complete the moment the file has been read; anything a run discovers later is
        // a resource value.
        store.Seal();

        return store;
    }
}
