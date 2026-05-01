using System;

namespace TestFramework.Core.Environment;

/// <summary>
/// Controls whether a dependency may be shared or must stay isolated per dependent component.
/// </summary>
public enum DependencyOwnership
{
    /// <summary>
    /// The dependency may be reused by multiple dependents when the realized identity is compatible.
    /// </summary>
    Shared,

    /// <summary>
    /// The dependency must remain isolated for the dependent component.
    /// </summary>
    Exclusive
}

/// <summary>
/// Describes a dependency edge from one environment component to another.
/// </summary>
/// <param name="ComponentType">The concrete component definition type that should be resolved.</param>
/// <param name="Ownership">How the dependency may be shared across dependents.</param>
public sealed record ComponentDependency(Type ComponentType, DependencyOwnership Ownership = DependencyOwnership.Shared);

/// <summary>
/// Marker interface for resource contracts used to match compatible providers and consumers.
/// </summary>
public interface IEnvironmentResourceContract
{
    /// <summary>
    /// Logical slot key used to disambiguate multiple contracts of the same type.
    /// </summary>
    string ContractKey { get; }
}
