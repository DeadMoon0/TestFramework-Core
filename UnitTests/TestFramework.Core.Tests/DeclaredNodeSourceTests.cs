using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// The socket a package plugs declared resources into, and the guarantees the engine applies to whatever
/// gets plugged in.
/// </summary>
public class DeclaredNodeSourceTests
{
    private static readonly ResourceKind Sql = ResourceKind.Named("socket.tests.sql")
        .OffersPerVantage(ValueNames.ConnectionString)
        .Offers("DatabaseName");

    [Fact]
    public void WhateverAPiecePlugsInBecomesResourcesTheGraphCanAnswerFor()
    {
        // A piece says what it found; the engine turns it into resources. The piece never learns how a
        // graph is composed.
        ResourceGraph graph = ResourceGraph.Compose(
        [
            new HandBuiltSource("a fixture",
            [
                new DeclaredResource(Sql, "orders-db", new Dictionary<ValueKey, string>
                {
                    [new ValueKey("DatabaseName")] = "orders",
                }, "a fixture"),
            ]),
        ]);

        ResourceNode node = Assert.Single(graph.Nodes);

        Assert.Equal("socket.tests.sql/orders-db", node.ToString());
        Assert.Equal("orders", node.DeclaredValues[new ValueKey("DatabaseName")]);
        Assert.IsNotAssignableFrom<ProvisionedResourceNode>(node);
    }

    [Fact]
    public void TheEngineChecksEveryPiece_NotOnlyTheOnesItShipsWith()
    {
        // The point of the socket carrying the guarantee: a value the kind never offered is the same
        // mistake whether it comes from configuration, a fixture, or somebody's own plug-in.
        HandBuiltSource source = new HandBuiltSource("somebody's own plug-in",
        [
            new DeclaredResource(Sql, "orders-db", new Dictionary<ValueKey, string>
            {
                [new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host)] = "https://nope.test/",
            }, "somebody's own plug-in"),
        ]);

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(() => _ = source.Nodes);

        Assert.Contains("somebody's own plug-in declares BaseUrl (Host) for 'orders-db'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("does not offer", failure.Message, StringComparison.Ordinal);
        Assert.Contains("offers ConnectionString", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APieceIsReadOnceRatherThanEveryTimeTheGraphIsAsked()
    {
        // Reading a file or a manifest twice per question would be a surprise a piece cannot defend
        // against, so the engine reads it once.
        HandBuiltSource source = new HandBuiltSource("a fixture",
        [
            new DeclaredResource(Sql, "orders-db", new Dictionary<ValueKey, string>(), "a fixture"),
        ]);

        _ = source.Nodes;
        _ = source.Nodes;

        Assert.Equal(1, source.Reads);
    }

    private sealed class HandBuiltSource(string name, IReadOnlyList<DeclaredResource> declarations) : DeclaredNodeSource
    {
        public int Reads { get; private set; }

        public override string SourceName => name;

        protected override IEnumerable<DeclaredResource> Declarations
        {
            get
            {
                this.Reads++;

                return declarations;
            }
        }
    }
}
