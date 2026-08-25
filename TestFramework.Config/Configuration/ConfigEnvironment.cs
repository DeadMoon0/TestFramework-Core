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
public sealed class ConfigEnvironment : IResourceNodeSource
{
    private readonly Lazy<IReadOnlyList<ResourceNode>> nodes;

    private ConfigEnvironment(IConfiguration configuration, IReadOnlyList<IConfigShape> shapes)
    {
        // Lazily, because the shapes are registered in whatever order packages happen to be loaded in,
        // and nothing should depend on this being built after the last of them.
        this.nodes = new Lazy<IReadOnlyList<ResourceNode>>(() => Build(configuration, shapes));
    }

    /// <inheritdoc />
    public string SourceName => "configuration";

    /// <inheritdoc />
    public IReadOnlyList<ResourceNode> Nodes => this.nodes.Value;

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

    private static IReadOnlyList<ResourceNode> Build(IConfiguration configuration, IReadOnlyList<IConfigShape> shapes)
    {
        List<ResourceNode> nodes = [];

        foreach (IConfigShape shape in shapes)
        {
            foreach (string identifier in shape.Identifiers(configuration))
            {
                object config = shape.Read(configuration, identifier);
                IReadOnlyDictionary<ValueKey, string> values = shape.Values(config);

                ConfigShapeValidation.EnsureDeclaresOnlyOfferedValues(shape, identifier, values);

                nodes.Add(new DeclaredResourceNode(shape.Kind, identifier, values));
            }
        }

        return nodes;
    }

    /// <summary>
    /// A resource somebody wrote down: values as declared, nothing to start, nothing to tear down.
    /// </summary>
    private sealed class DeclaredResourceNode(
        ResourceKind kind,
        string identifier,
        IReadOnlyDictionary<ValueKey, string> declaredValues) : ResourceNode
    {
        public override ResourceKind Kind => kind;

        public override string Identifier => identifier;

        public override IReadOnlyDictionary<ValueKey, string> DeclaredValues => declaredValues;
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

        services.AddSingleton<IConfigShape>(new TShape());

        // Registered even when the section is empty, so a missing entry reads as "nothing is configured
        // under that name" rather than as a missing package.
        services.AddSingleton(implementationFactory: provider => BuildStore<TShape, TConfig>(provider));

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

    private static ConfigStore<TConfig> BuildStore<TShape, TConfig>(IServiceProvider provider)
        where TShape : ConfigShape<TConfig>, new()
        where TConfig : class
    {
        IConfiguration configuration = provider.GetRequiredService<IConfiguration>();
        TShape shape = new TShape();
        ConfigStore<TConfig> store = new ConfigStore<TConfig>();

        foreach (string identifier in shape.Identifiers(configuration))
        {
            store.Add(identifier, shape.Read(configuration.GetSection(shape.Section).GetSection(identifier), identifier));
        }

        // Declarations are complete the moment the file has been read; anything a run discovers later is
        // a resource value.
        store.Seal();

        return store;
    }
}
