using System;
using System.Linq;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// The value layer every consumer in the family reads through: what wins, what is refused, and what a
/// value built for one viewpoint may never answer.
/// </summary>
public class ResourceValueTests
{
    private const string Sql = "web.sql";
    private const string Orders = "orders-db";

    [Fact]
    public void WhatTheRunProducedBeatsWhatAFileDeclared()
    {
        // The whole precedence rule, in one place instead of once per package - and the declared value
        // is still on record, so a message can say what was overridden.
        ResourceValueStore store = new ResourceValueStore();

        store.Declare(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), "Server=deployed", "config");
        store.Produce(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), "Server=localhost,32771", "DockerWebEnvironment");

        Assert.True(store.TryGet(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), out ResolvedValue? resolved));
        Assert.Equal("Server=localhost,32771", resolved!.Value);
        Assert.Equal(ValueOrigin.Produced, resolved.Origin);
    }

    [Fact]
    public void ADeclaredValueDoesNotOverwriteAProducedOneWhateverTheOrder()
    {
        // Configuration is loaded before an environment provisions, but nothing guarantees that order
        // for a relay node materialized later - so the rule is about origin, not arrival.
        ResourceValueStore store = new ResourceValueStore();

        store.Produce(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), "Server=localhost,32771", "DockerWebEnvironment");
        store.Declare(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), "Server=deployed", "config");

        Assert.True(store.TryGet(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), out ResolvedValue? resolved));
        Assert.Equal("Server=localhost,32771", resolved!.Value);
    }

    [Fact]
    public void TwoProvidersClaimingOneResourceIsStatedNotRaced()
    {
        ResourceValueStore store = new ResourceValueStore();
        store.Produce(Sql, Orders, new ValueKey(ValueNames.ConnectionString), "Server=first", "FirstEnvironment");

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => store.Produce(Sql, Orders, new ValueKey(ValueNames.ConnectionString), "Server=second", "SecondEnvironment"));

        Assert.Contains("FirstEnvironment", failure.Message, StringComparison.Ordinal);
        Assert.Contains("SecondEnvironment", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AValueBuiltForOneViewpointNeverAnswersForTheOther()
    {
        // The substitution this prevents is the bug that string-rewriting connection strings existed to
        // paper over: a test handed a container-internal alias it cannot route to.
        ResourceValueStore store = new ResourceValueStore();
        store.Produce(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Network), "Server=orders-db,1433", "DockerWebEnvironment");

        ValueResolution resolution = new ValueResolution(store);

        ValueRef connectionString = ValueRef.For(Sql, Orders, ValueNames.ConnectionString);

        Assert.True(resolution.TryGet(connectionString, ResourceVantage.Network, out string? network));
        Assert.Equal("Server=orders-db,1433", network);

        Assert.False(resolution.TryGet(connectionString, ResourceVantage.Host, out string? host));
        Assert.Null(host);
    }

    [Fact]
    public void AValueWithNoViewpointAnswersEveryAsk()
    {
        // A database name is a name, whoever is asking.
        ResourceValueStore store = new ResourceValueStore();
        store.Produce(Sql, Orders, new ValueKey("DatabaseName"), "orders", "DockerWebEnvironment");

        ValueResolution resolution = new ValueResolution(store);

        ValueRef databaseName = ValueRef.For(Sql, Orders, "DatabaseName");

        Assert.True(resolution.TryGet(databaseName, ResourceVantage.Host, out string? fromHost));
        Assert.True(resolution.TryGet(databaseName, ResourceVantage.Network, out string? fromNetwork));
        Assert.Equal("orders", fromHost);
        Assert.Equal("orders", fromNetwork);
    }

    [Fact]
    public void TeardownForgetsWhatWasProducedAndKeepsWhatWasDeclared()
    {
        // Nothing may dial a dead port afterwards; an author's entry does not stop existing because a
        // container stopped.
        ResourceValueStore store = new ResourceValueStore();
        store.Declare(Sql, Orders, new ValueKey("DatabaseName"), "orders", "config");
        store.Produce(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), "Server=localhost,32771", "DockerWebEnvironment");

        store.WithdrawProduced(Sql, Orders);

        Assert.False(store.TryGet(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), out _));
        Assert.True(store.TryGet(Sql, Orders, new ValueKey("DatabaseName"), out _));
    }

    [Fact]
    public void OneNameOnTwoKindsIsAStatedErrorNotACoinToss()
    {
        ResourceValueStore store = new ResourceValueStore();
        store.Produce("web.site", "storefront", new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host), "http://localhost:1/", "DockerWebEnvironment");
        store.Produce("web.restapi", "storefront", new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host), "http://localhost:2/", "DockerWebEnvironment");

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => store.TryGetByIdentifier("storefront", new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host), out _));

        Assert.Contains("names 2 different kinds", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingValueNamesWhatTheRunDoesHave()
    {
        // The fix should be a copy, not an expedition - the family's standing rule for failures.
        ResourceValueStore store = new ResourceValueStore();
        store.Produce("web.site", "storefront", new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host), "http://localhost:1/", "DockerWebEnvironment");

        ValueResolution resolution = new ValueResolution(store);

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => resolution.Require(ValueRef.For("web.stub", "payments", ValueNames.BaseUrl), ResourceVantage.Network));

        Assert.Contains("Nothing in this run supplies BaseUrl (Network) for web.stub/payments", failure.Message, StringComparison.Ordinal);
        Assert.Contains("web.site/storefront", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSnapshotReadsTheSameForTwoRunsOfOneTest()
    {
        // Ordered output is what lets the run snapshot be compared across runs at all.
        ResourceValueStore store = new ResourceValueStore();
        store.Produce("web.stub", "payments", new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host), "http://localhost:2/", "env");
        store.Produce("web.site", "storefront", new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host), "http://localhost:1/", "env");

        Assert.Equal(
            ["web.site/storefront", "web.stub/payments"],
            store.Snapshot().Select(static value => $"{value.ResourceKind}/{value.Identifier}"));
    }

    [Fact]
    public void AReaderCanTellAProducedCoordinateFromADeclaredOne()
    {
        // The one question a coordinate alone cannot answer, and the reason it is asked: a produced value is
        // complete - whatever made it knows the port and the credentials both - while a declared one is
        // qualified by the rest of somebody's entry. A reader that cannot tell applies those qualifications
        // to a container's own connection string and strips its user and password out of it.
        ResourceValueStore store = new ResourceValueStore();
        ValueKey key = new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host);

        store.Declare(Sql, Orders, key, "Server=deployed", "config");

        ValueResolution resolution = new ValueResolution(store);
        ValueRef reference = ValueRef.For(Sql, Orders, ValueNames.ConnectionString);

        Assert.True(resolution.TryResolve(reference, ResourceVantage.Host, out ResolvedValue? declared));
        Assert.Equal(ValueOrigin.Declared, declared!.Origin);

        store.Produce(Sql, Orders, key, "Server=localhost,32771;User Id=sa", "DockerWebEnvironment");

        Assert.True(resolution.TryResolve(reference, ResourceVantage.Host, out ResolvedValue? produced));
        Assert.Equal(ValueOrigin.Produced, produced!.Origin);
        Assert.Equal("Server=localhost,32771;User Id=sa", produced.Value);
    }

    [Fact]
    public void AskingForAValueTheRunDoesNotHaveSaysSoWithoutThrowing()
    {
        // Same contract as TryGet, so the two reads cannot disagree about what "the run does not know it"
        // looks like.
        ValueResolution resolution = new ValueResolution(new ResourceValueStore());

        Assert.False(resolution.TryResolve(ValueRef.For(Sql, Orders, ValueNames.ConnectionString), ResourceVantage.Host, out ResolvedValue? missing));
        Assert.Null(missing);
    }
}
