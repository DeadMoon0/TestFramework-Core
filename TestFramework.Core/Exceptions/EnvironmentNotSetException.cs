using System;
using System.Collections.Generic;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a timeline tries to use an extension without setting an environment.
/// </summary>
public class EnvironmentNotSetException : TimelineFrameworkException
{
    private readonly Type? _expectedEnvironmentType;
    private readonly IReadOnlyList<string> _availableEnvironments;

    /// <summary>
    /// Initializes a new instance of the EnvironmentNotSetException class.
    /// </summary>
    /// <param name="expectedType">Expected environment type</param>
    /// <param name="availableEnvs">List of available environment options</param>
    public EnvironmentNotSetException(
        Type? expectedType = null,
        IReadOnlyList<string>? availableEnvs = null)
        : base(
            "No environment is configured for this timeline. Call SetEnv() before using extensions.",
            new[]
            {
                "Call .SetEnv() after Timeline.Create()",
                "Example: timeline.SetEnv(new AzureEnvironment(...))",
                "Or use default: timeline.SetEnv(AzureExt.DefaultEnvironment())",
                "For local testing: timeline.SetEnv(LocalIOExt.DefaultEnvironment())"
            },
            new[]
            {
                "AzureExt.DefaultEnvironment() - for Azure triggers/bindings",
                "ContainerExt.ForFunctionAppWithStorage() - for Docker emulation",
                "LocalIOExt.DefaultEnvironment() - for file-based testing"
            })
    {
        _expectedEnvironmentType = expectedType;
        _availableEnvironments = availableEnvs ?? new List<string>();
    }
}
