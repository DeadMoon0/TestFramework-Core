using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using TestFramework.Config.Builder;
using TestFramework.Config.Builder.InstanceBuilder;

namespace TestFramework.Config;

/// <summary>
/// Represents a reusable configuration and service-registration definition for timeline runs.
/// </summary>
public class ConfigInstance
{
    private readonly ConfigServiceDeltaCollection _deltaCollection;

    internal ConfigInstance(ConfigServiceDeltaCollection deltaCollection)
    {
        this._deltaCollection = deltaCollection;
    }

    /// <summary>
    /// Creates a configuration builder from a JSON file.
    /// </summary>
    /// <param name="path">The path to the JSON file.</param>
    /// <returns>A builder seeded with the JSON values from the file.</returns>
    /// <exception cref="System.IO.FileNotFoundException">Thrown when the JSON file does not exist.</exception>
    /// <exception cref="System.IO.InvalidDataException">Thrown when the JSON file cannot be parsed.</exception>
    public static IConfigInstanceBuilder FromJsonFile(string path)
    {
        IConfiguration jsonConfig = new ConfigurationBuilder().AddJsonFile(path).Build();
        ConfigServiceDeltaCollection rootDeltas = new ConfigServiceDeltaCollection(null);
        foreach (KeyValuePair<string, string?> kvp in jsonConfig.AsEnumerable())
        {
            if (kvp.Value is not null)
            {
                rootDeltas.AddConfigDelta(kvp.Key, kvp.Value);
            }
        }
        return new ConfigInstanceBuilder(rootDeltas);
    }

    /// <summary>
    /// Creates an empty configuration builder.
    /// </summary>
    /// <returns>A builder with no initial configuration or service registrations.</returns>
    public static IConfigInstanceBuilder Create()
    {
        return new ConfigInstanceBuilder(new ConfigServiceDeltaCollection(null));
    }

    /// <summary>
    /// Creates a child builder that inherits this instance's configuration and service registrations.
    /// </summary>
    /// <returns>A builder that can override configuration and add services on top of the current instance.</returns>
    public IConfigInstanceBuilder SetupSubInstance()
    {
        return new ConfigInstanceBuilder(new ConfigServiceDeltaCollection(this._deltaCollection));
    }

    /// <summary>
    /// Builds an <see cref="IServiceProvider"/> from the current configuration and service registrations.
    /// </summary>
    /// <returns>The resolved service provider.</returns>
    /// <exception cref="Exception">Propagates exceptions thrown by service registration delegates.</exception>
    public IServiceProvider BuildServiceProvider()
    {
        Dictionary<string, string?> config = this._deltaCollection.ApplyConfigDeltas();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();

        ServiceCollection services = this._deltaCollection.ApplyServiceDeltas().Resolve(configuration);
        services.AddSingleton(configuration);
        return services.BuildServiceProvider();
    }
}