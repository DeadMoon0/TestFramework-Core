using System;
using System.Collections.Generic;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when configuration contains invalid values or types.
/// </summary>
public class ConfigurationInvalidException : TimelineFrameworkException
{
    private readonly string _configKey;
    private readonly string _expectedFormat;
    private readonly string? _actualValue;

    /// <summary>
    /// Gets the configuration key that had the invalid value.
    /// </summary>
    public string ConfigKey => _configKey;

    /// <summary>
    /// Gets the expected format for the configuration value.
    /// </summary>
    public string ExpectedFormat => _expectedFormat;

    /// <summary>
    /// Gets the actual value that was provided (if any).
    /// </summary>
    public string? ActualValue => _actualValue;

    /// <summary>
    /// Initializes a new instance of the ConfigurationInvalidException class.
    /// </summary>
    /// <param name="configKey">Name of the configuration key</param>
    /// <param name="expectedFormat">Expected format or type for the value</param>
    /// <param name="actualValue">Actual value that was provided</param>
    public ConfigurationInvalidException(
        string configKey,
        string expectedFormat,
        string? actualValue = null)
        : base(
            $"Configuration '{configKey}' has invalid value: {actualValue ?? "null"}",
            new[]
            {
                $"Fix the value format to: {expectedFormat}",
                $"Example: ConfigInstance.Set(\"{configKey}\", 5000)",
                "Validation occurs at Build() time, not RunAsync()",
                "Check ConfigStore<T> generic type matches value type"
            },
            new[] { expectedFormat })
    {
        _configKey = configKey;
        _expectedFormat = expectedFormat;
        _actualValue = actualValue;
    }
}
