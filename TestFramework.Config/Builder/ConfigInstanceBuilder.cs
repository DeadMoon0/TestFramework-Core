using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using TestFramework.Config.Builder.InstanceBuilder;

namespace TestFramework.Config.Builder;

internal class ConfigInstanceBuilder : IConfigInstanceBuilder
{
    private readonly ConfigServiceDeltaCollection _deltaCollection;

    internal ConfigInstanceBuilder(ConfigServiceDeltaCollection deltaCollection)
    {
        _deltaCollection = deltaCollection;
    }

    public IConfigInstanceBuilder AddService(Action<IServiceCollection, IConfiguration> action)
    {
        _deltaCollection.AddServiceDelta(action);
        return this;
    }

    public IConfigInstanceBuilder AddService(Action<IServiceCollection> action)
    {
        _deltaCollection.AddServiceDelta((s, _) => action(s));
        return this;
    }

    public ConfigInstance Build()
    {
        return new ConfigInstance(_deltaCollection);
    }

    public IServiceProvider BuildServiceProvider()
    {
        return Build().BuildServiceProvider();
    }

    public IConfigInstanceBuilder OverrideConfig(string key, string? value)
    {
        _deltaCollection.AddConfigDelta(key, value);
        return this;
    }

    public IConfigInstanceBuilder OverrideConfig(IDictionary<string, string?> pairs)
    {
        foreach (var item in pairs)
        {
            _deltaCollection.AddConfigDelta(item.Key, item.Value);
        }
        return this;
    }

    public IConfigInstanceBuilder OverrideConfig(IConfiguration configuration)
    {
        foreach (KeyValuePair<string, string?> kvp in configuration.AsEnumerable())
        {
            if (kvp.Value is not null)
            {
                _deltaCollection.AddConfigDelta(kvp.Key, kvp.Value);
            }
        }
        return this;
    }
}