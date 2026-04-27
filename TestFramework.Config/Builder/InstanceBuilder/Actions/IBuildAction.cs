using System;

namespace TestFramework.Config.Builder.InstanceBuilder.Actions;

/// <summary>
/// Materializes the fluent configuration builder into reusable runtime objects.
/// </summary>
public interface IBuildAction
{
    /// <summary>
    /// Builds a reusable <see cref="ConfigInstance"/> snapshot.
    /// </summary>
    /// <returns>The materialized configuration instance.</returns>
    public ConfigInstance Build();

    /// <summary>
    /// Builds an <see cref="IServiceProvider"/> directly from the current builder state.
    /// </summary>
    /// <returns>The resolved service provider.</returns>
    public IServiceProvider BuildServiceProvider();
}