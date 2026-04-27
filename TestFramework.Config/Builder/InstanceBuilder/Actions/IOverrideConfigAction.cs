using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace TestFramework.Config.Builder.InstanceBuilder.Actions;

/// <summary>
/// Adds or overrides configuration values on a configuration instance builder.
/// </summary>
public interface IOverrideConfigAction
{
    /// <summary>
    /// Adds or replaces a single configuration value.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The configuration value. Use <see langword="null"/> to remove the effective value.</param>
    /// <returns>The current builder for further fluent configuration.</returns>
    public IConfigInstanceBuilder OverrideConfig(string key, string? value);

    /// <summary>
    /// Adds or replaces multiple configuration values.
    /// </summary>
    /// <param name="pairs">The configuration key-value pairs to merge into the builder.</param>
    /// <returns>The current builder for further fluent configuration.</returns>
    public IConfigInstanceBuilder OverrideConfig(IDictionary<string, string?> pairs);

    /// <summary>
    /// Adds or replaces configuration values from an existing <see cref="IConfiguration"/> instance.
    /// </summary>
    /// <param name="configuration">The configuration values to merge into the builder.</param>
    /// <returns>The current builder for further fluent configuration.</returns>
    public IConfigInstanceBuilder OverrideConfig(IConfiguration configuration);
}