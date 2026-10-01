using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Environment;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Tests;

public class ComponentGraphValidatorTests
{
    [Fact]
    public void Validate_ThrowsWhenExclusiveDependenciesResolveToSameIdentity()
    {
        ComponentGraphNode[] nodes =
        [
            new ComponentGraphNode
            {
                ComponentType = typeof(TestOwnerA),
                RealizedComponentIdentity = "functionapp:fa1",
                Dependencies = [new ComponentGraphDependency { ComponentType = typeof(TestSharedDependency), RealizedComponentIdentity = "servicebus:bus", Ownership = DependencyOwnership.Exclusive }],
                Provides = [],
                Requires = [],
            },
            new ComponentGraphNode
            {
                ComponentType = typeof(TestOwnerB),
                RealizedComponentIdentity = "functionapp:fa2",
                Dependencies = [new ComponentGraphDependency { ComponentType = typeof(TestSharedDependency), RealizedComponentIdentity = "servicebus:bus", Ownership = DependencyOwnership.Exclusive }],
                Provides = [],
                Requires = [],
            },
            new ComponentGraphNode
            {
                ComponentType = typeof(TestSharedDependency),
                RealizedComponentIdentity = "servicebus:bus",
                Dependencies = [],
                Provides = [],
                Requires = [],
            }
        ];

        DependencyGraphException exception = Assert.Throws<DependencyGraphException>(() => ComponentGraphValidator.Validate(nodes));

        Assert.Contains("exclusive", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("servicebus:bus", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bind_ThrowsWhenMultipleCompatibleProvidersExist()
    {
        TestContract requirement = new("trigger", "bus", "queue");
        ComponentGraphNode[] nodes =
        [
            new ComponentGraphNode { ComponentType = typeof(TestOwnerA), RealizedComponentIdentity = "consumer", Dependencies = [], Provides = [], Requires = [requirement] },
            new ComponentGraphNode { ComponentType = typeof(TestProviderA), RealizedComponentIdentity = "provider-a", Dependencies = [], Provides = [new TestContract("trigger", "bus", "queue")], Requires = [] },
            new ComponentGraphNode { ComponentType = typeof(TestProviderB), RealizedComponentIdentity = "provider-b", Dependencies = [], Provides = [new TestContract("trigger", "bus", "queue")], Requires = [] }
        ];

        DependencyGraphException exception = Assert.Throws<DependencyGraphException>(() => ContractBindingPass.Bind(nodes, static (required, provided) => object.Equals(required, provided)));

        Assert.Contains("compatible providers", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestOwnerA;
    private sealed class TestOwnerB;
    private sealed class TestProviderA;
    private sealed class TestProviderB;
    private sealed class TestSharedDependency;

    private sealed record TestContract(string ContractKey, string Scope, string Name) : IEnvironmentResourceContract;
}
