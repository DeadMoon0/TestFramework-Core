using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using TestFramework.Config.Builder;
using TestFramework.Config.Builder.InstanceBuilder;

namespace TestFramework.Config;

public class ConfigInstance
{
    private readonly ConfigServiceDeltaCollection _deltaCollection;

    internal ConfigInstance(ConfigServiceDeltaCollection deltaCollection)
    {
        this._deltaCollection = deltaCollection;
    }

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

    public static IConfigInstanceBuilder Create()
    {
        return new ConfigInstanceBuilder(new ConfigServiceDeltaCollection(null));
    }

    public IConfigInstanceBuilder SetupSubInstance()
    {
        return new ConfigInstanceBuilder(new ConfigServiceDeltaCollection(this._deltaCollection));
    }

    public IServiceProvider BuildServiceProvider()
    {
        Dictionary<string, string?> config = this._deltaCollection.ApplyConfigDeltas();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();

        ServiceCollection services = this._deltaCollection.ApplyServiceDeltas().Resolve(configuration);
        services.AddSingleton(configuration);
        return services.BuildServiceProvider();
    }
}