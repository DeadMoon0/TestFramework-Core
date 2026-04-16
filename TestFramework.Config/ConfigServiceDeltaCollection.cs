using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;

namespace TestFramework.Config;

internal class ConfigServiceDeltaCollection(ConfigServiceDeltaCollection? baseCollection)
{
    private readonly ResolvableServiceCollection _serviceDeltas = [];
    private readonly Dictionary<string, string?> _configDeltas = [];

    internal void AddServiceDelta(Action<IServiceCollection, IConfiguration> deltaFunc)
    {
        this._serviceDeltas.Add(deltaFunc);
    }

    internal void AddConfigDelta(string key, string? value)
    {
        _configDeltas[key] = value;
    }

    internal ResolvableServiceCollection ApplyServiceDeltas()
    {
        ResolvableServiceCollection result = baseCollection?.ApplyServiceDeltas() ?? new ResolvableServiceCollection();
        foreach (var action in _serviceDeltas)
        {
            result.Add(action);
        }
        return result;
    }

    internal Dictionary<string, string?> ApplyConfigDeltas()
    {
        Dictionary<string, string?> result = baseCollection?.ApplyConfigDeltas() ?? new Dictionary<string, string?>();
        foreach (KeyValuePair<string, string?> kvp in _configDeltas)
        {
            result[kvp.Key] = kvp.Value;
        }
        return result;
    }
}

internal class ResolvableServiceCollection : IEnumerable<Action<IServiceCollection, IConfiguration>>
{
    private readonly List<Action<IServiceCollection, IConfiguration>> _serviceActions = [];

    internal ServiceCollection Resolve(IConfiguration configuration)
    {
        ServiceCollection result = new ServiceCollection();
        foreach (Action<IServiceCollection, IConfiguration> action in _serviceActions)
        {
            action(result, configuration);
        }
        return result;
    }

    internal void Add(Action<IServiceCollection, IConfiguration> action) { _serviceActions.Add(action); }

    public IEnumerator<Action<IServiceCollection, IConfiguration>> GetEnumerator()
    {
        return this._serviceActions.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}