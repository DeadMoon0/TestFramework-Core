using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace TestFramework.Config.Builder.InstanceBuilder.Actions;

/// <summary>
/// Adds service registrations to a configuration instance builder.
/// </summary>
public interface IAddServiceAction
{
    /// <summary>
    /// Adds a service registration delegate that can inspect the merged configuration.
    /// </summary>
    /// <param name="action">The registration delegate to run during provider construction.</param>
    /// <returns>The current builder for further fluent configuration.</returns>
    public IConfigInstanceBuilder AddService(Action<IServiceCollection, IConfiguration> action);

    /// <summary>
    /// Adds a service registration delegate that only needs the service collection.
    /// </summary>
    /// <param name="action">The registration delegate to run during provider construction.</param>
    /// <returns>The current builder for further fluent configuration.</returns>
    public IConfigInstanceBuilder AddService(Action<IServiceCollection> action);
}