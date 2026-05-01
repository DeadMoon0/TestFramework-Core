using System;
using TestFramework.Core.Environment;

namespace TestFramework.Core.Tests;

public class ComponentGraphValidatorTests
{
    [Fact]
    public void Validate_ThrowsWhenExclusiveDependenciesResolveToSameIdentity()
    {
        ComponentGraphNode[] nodes =
        [
            new(
                typeof(TestOwnerA),
                "functionapp:fa1",
                [new ComponentGraphDependency(typeof(TestSharedDependency), "servicebus:bus", DependencyOwnership.Exclusive)],
                [],
                []),
            new(
                typeof(TestOwnerB),
                "functionapp:fa2",
                [new ComponentGraphDependency(typeof(TestSharedDependency), "servicebus:bus", DependencyOwnership.Exclusive)],
                [],
                []),
            new(
                typeof(TestSharedDependency),
                "servicebus:bus",
                [],
                [],
                [])
        ];

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => ComponentGraphValidator.Validate(nodes));

        Assert.Contains("exclusive", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("servicebus:bus", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bind_ThrowsWhenMultipleCompatibleProvidersExist()
    {
        TestContract requirement = new("trigger", "bus", "queue");
        ComponentGraphNode[] nodes =
        [
            new(typeof(TestOwnerA), "consumer", [], [], [requirement]),
            new(typeof(TestProviderA), "provider-a", [], [new TestContract("trigger", "bus", "queue")], []),
            new(typeof(TestProviderB), "provider-b", [], [new TestContract("trigger", "bus", "queue")], [])
        ];

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => ContractBindingPass.Bind(nodes, static (required, provided) => object.Equals(required, provided)));

        Assert.Contains("compatible providers", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestOwnerA;
    private sealed class TestOwnerB;
    private sealed class TestProviderA;
    private sealed class TestProviderB;
    private sealed class TestSharedDependency;

    private sealed record TestContract(string ContractKey, string Scope, string Name) : IEnvironmentResourceContract;
}
