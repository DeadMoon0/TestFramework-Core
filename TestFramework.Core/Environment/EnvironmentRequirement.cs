namespace TestFramework.Core.Environment;

/// <summary>
/// Describes a resource requirement that an environment provider can resolve.
/// </summary>
/// <param name="ResourceKind">The logical resource kind.</param>
/// <param name="ResourceIdentifier">The specific resource identifier.</param>
public record EnvironmentRequirement(string ResourceKind, string ResourceIdentifier);