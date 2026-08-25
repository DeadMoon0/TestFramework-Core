using System;

namespace TestFramework.Core.Environment;

/// <summary>
/// Describes a resource requirement that an environment provider can resolve.
/// </summary>
/// <param name="ResourceKind">The logical resource kind, or <see cref="EnvironmentResourceKinds.Any"/>.</param>
/// <param name="ResourceIdentifier">The specific resource identifier.</param>
public record EnvironmentRequirement(string ResourceKind, string ResourceIdentifier)
{
    /// <summary>
    /// Requires a resource by name, whatever kind it turns out to be.
    /// </summary>
    /// <remarks>
    /// For a consumer that genuinely does not care: a browser opens whatever answers at an address,
    /// whether that is a static site or an application's own front end. The graph decides which kind
    /// the name belongs to, so the test never has to say - and a name that two kinds answer to is a
    /// stated error rather than a silent choice.
    /// </remarks>
    /// <param name="resourceIdentifier">The resource identifier.</param>
    /// <returns>The requirement.</returns>
    public static EnvironmentRequirement AnyKind(string resourceIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceIdentifier);

        return new EnvironmentRequirement(EnvironmentResourceKinds.Any, resourceIdentifier);
    }
}

/// <summary>
/// Resource kinds Core itself defines.
/// </summary>
public static class EnvironmentResourceKinds
{
    /// <summary>
    /// Stands for "whatever kind this name belongs to", for a requirement declared with
    /// <see cref="EnvironmentRequirement.AnyKind"/>.
    /// </summary>
    public const string Any = "*";

    /// <summary>
    /// Whether a kind is the stand-in rather than a real kind.
    /// </summary>
    /// <param name="resourceKind">The kind to check.</param>
    /// <returns>True when it is <see cref="Any"/>.</returns>
    public static bool IsAny(string resourceKind) => string.Equals(resourceKind, Any, StringComparison.Ordinal);
}
