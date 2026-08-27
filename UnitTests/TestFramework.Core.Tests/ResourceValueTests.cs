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

        store.Declare(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), "Server=deployed", "config", secret: false);
        store.Produce(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), "Server=localhost,32771", "DockerWebEnvironment", secret: false);

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

        store.Produce(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), "Server=localhost,32771", "DockerWebEnvironment", secret: false);
        store.Declare(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), "Server=deployed", "config", secret: false);

        Assert.True(store.TryGet(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), out ResolvedValue? resolved));
        Assert.Equal("Server=localhost,32771", resolved!.Value);
    }

    [Fact]
    public void TwoProvidersClaimingOneResourceIsStatedNotRaced()
    {
        ResourceValueStore store = new ResourceValueStore();
        store.Produce(Sql, Orders, new ValueKey(ValueNames.ConnectionString), "Server=first", "FirstEnvironment", secret: false);

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => store.Produce(Sql, Orders, new ValueKey(ValueNames.ConnectionString), "Server=second", "SecondEnvironment", secret: false));

        Assert.Contains("FirstEnvironment", failure.Message, StringComparison.Ordinal);
        Assert.Contains("SecondEnvironment", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AValueBuiltForOneViewpointNeverAnswersForTheOther()
    {
        // The substitution this prevents is the bug that string-rewriting connection strings existed to
        // paper over: a test handed a container-internal alias it cannot route to.
        ResourceValueStore store = new ResourceValueStore();
        store.Produce(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Network), "Server=orders-db,1433", "DockerWebEnvironment", secret: false);

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
        store.Produce(Sql, Orders, new ValueKey("DatabaseName"), "orders", "DockerWebEnvironment", secret: false);

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
        store.Declare(Sql, Orders, new ValueKey("DatabaseName"), "orders", "config", secret: false);
        store.Produce(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), "Server=localhost,32771", "DockerWebEnvironment", secret: false);

        store.WithdrawProduced(Sql, Orders);

        Assert.False(store.TryGet(Sql, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), out _));
        Assert.True(store.TryGet(Sql, Orders, new ValueKey("DatabaseName"), out _));
    }

    [Fact]
    public void OneNameOnTwoKindsIsAStatedErrorNotACoinToss()
    {
        ResourceValueStore store = new ResourceValueStore();
        store.Produce("web.site", "storefront", new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host), "http://localhost:1/", "DockerWebEnvironment", secret: false);
        store.Produce("web.restapi", "storefront", new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host), "http://localhost:2/", "DockerWebEnvironment", secret: false);

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => store.TryGetByIdentifier("storefront", new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host), out _));

        Assert.Contains("names 2 different kinds", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingValueNamesWhatTheRunDoesHave()
    {
        // The fix should be a copy, not an expedition - the family's standing rule for failures.
        ResourceValueStore store = new ResourceValueStore();
        store.Produce("web.site", "storefront", new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host), "http://localhost:1/", "DockerWebEnvironment", secret: false);

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
        store.Produce("web.stub", "payments", new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host), "http://localhost:2/", "env", secret: false);
        store.Produce("web.site", "storefront", new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host), "http://localhost:1/", "env", secret: false);

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

        store.Declare(Sql, Orders, key, "Server=deployed", "config", secret: false);

        ValueResolution resolution = new ValueResolution(store);
        ValueRef reference = ValueRef.For(Sql, Orders, ValueNames.ConnectionString);

        Assert.True(resolution.TryResolve(reference, ResourceVantage.Host, out ResolvedValue? declared));
        Assert.Equal(ValueOrigin.Declared, declared!.Origin);

        store.Produce(Sql, Orders, key, "Server=localhost,32771;User Id=sa", "DockerWebEnvironment", secret: false);

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

    [Fact]
    public void AFrozenRunsValuesAreReadableAndNoLongerWritable()
    {
        // §2: a finished run is a snapshot that can be handed around and trusted. That has to include the
        // coordinates, because they are what the run proved something *against* - and this was the one store
        // a finished run exposed that was still open to writes.
        ResourceValueStore store = new ResourceValueStore();
        ValueKey key = new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host);

        store.Declare(Sql, Orders, key, "Server=deployed", "config", secret: false);
        store.Freeze();

        // Reading stays open - that is the point of keeping them.
        Assert.True(store.TryGet(Sql, Orders, key, out ResolvedValue? kept));
        Assert.Equal("Server=deployed", kept!.Value);
        Assert.Single(store.Snapshot());

        Assert.True(store.IsFrozen);
        Assert.Throws<FrameworkStateException>(() => store.Produce(Sql, Orders, key, "Server=late", "too late", secret: false));
        Assert.Throws<FrameworkStateException>(() => store.Declare(Sql, Orders, key, "Server=late", "too late", secret: false));
        Assert.Throws<FrameworkStateException>(() => store.WithdrawProduced(Sql, Orders));
    }

    [Fact]
    public void ASecretValueIsRedactedInEveryListingAndReadableWhenAsked()
    {
        // The two channels, and why a secret can live in the graph at all. A connection string is a
        // coordinate and a credential in one indivisible string: it has to be here for anything to connect,
        // and it must never appear in the list of what the run knows.
        ResourceKind kind = ResourceKind
            .Named("test.secretsql")
            .OffersPerVantage(ValueNames.ConnectionString, optional: false, secret: true)
            .Offers("DatabaseName")
            .Build();

        ResourceValueStore store = new ResourceValueStore();
        ValueKey connection = new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host);

        store.Produce(kind.Name, Orders, connection, "Server=localhost,1433;User Id=sa;Password=hunter2", "container", secret: kind.IsSecret(ValueNames.ConnectionString));
        store.Declare(kind.Name, Orders, new ValueKey("DatabaseName"), "orders", "config", secret: kind.IsSecret("DatabaseName"));

        // The listing - what reaches failure messages, log lines and the frozen run.
        ResolvedValue listed = store.Snapshot().Single(value => value.Key == connection);

        Assert.True(listed.IsSecret);
        Assert.DoesNotContain("hunter2", listed.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", listed.ToString(), StringComparison.Ordinal);

        // A value nobody declared secret is untouched, so redaction is not a blanket.
        ResolvedValue name = store.Snapshot().Single(value => value.Key == new ValueKey("DatabaseName"));

        Assert.False(name.IsSecret);
        Assert.Equal("orders", name.Value);

        // And the point read still answers, because asking for a connection string is asking for it.
        Assert.True(store.TryGet(kind.Name, Orders, connection, out ResolvedValue? read));
        Assert.Equal("Server=localhost,1433;User Id=sa;Password=hunter2", read!.Value);
    }

    [Fact]
    public void AMissingValueMessageListsWhatIsKnownWithoutLeakingASecret()
    {
        // The exact path that made "the graph cannot hold secrets" true: Require's failure lists everything
        // the run knows, and that list is built from the snapshot.
        ResourceKind kind = ResourceKind
            .Named("test.secretbus")
            .OffersPerVantage(ValueNames.ConnectionString, optional: false, secret: true)
            .Offers("QueueName", optional: true)
            .Build();

        ResourceValueStore store = new ResourceValueStore();
        store.Produce(kind.Name, Orders, new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host), "Endpoint=sb://x/;SharedAccessKey=hunter2", "container", secret: kind.IsSecret(ValueNames.ConnectionString));

        ValueResolution resolution = new ValueResolution(store);

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => resolution.Require(ValueRef.For(kind.Name, Orders, "QueueName"), ResourceVantage.Host));

        Assert.DoesNotContain("hunter2", failure.ToString(), StringComparison.Ordinal);

        // It still says what it does know, which is the whole point of the list.
        Assert.Contains(Orders, failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void TwoPackagesDisagreeingAboutWhetherAValueIsSecretIsRefused()
    {
        // Secrecy is part of a kind's schema, so it is held to the same rule as the rest of it: one meaning
        // per name. Otherwise the package that declared it last would decide whether a password is printed.
        ResourceKind.Named("test.disputedsecret").OffersPerVantage(ValueNames.ConnectionString, optional: false, secret: true).Build();

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => ResourceKind.Named("test.disputedsecret").OffersPerVantage(ValueNames.ConnectionString).Build());

        Assert.Contains("different values", failure.Message, StringComparison.Ordinal);
    }
}
