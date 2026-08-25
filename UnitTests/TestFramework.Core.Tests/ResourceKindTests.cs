using System;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// Kind names mean one thing per process - the guarantee an enum would give, on a set that stays open.
/// </summary>
/// <remarks>
/// These declare conflicting kinds on purpose, so they run alone and clear the declarations afterwards.
/// </remarks>
[Collection(nameof(ResourceKindTests))]
public class ResourceKindTests : IDisposable
{
    public void Dispose() => ResourceKindRegistry.Reset();

    [Fact]
    public void DeclaringTheSameKindTwiceTheSameWayIsTheSameKind()
    {
        // Static initialization order across packages is nobody's to control, so declaring a kind twice
        // has to be harmless rather than a race.
        ResourceKind first = ResourceKind.Named("test.twice").OffersPerVantage(ValueNames.BaseUrl);
        ResourceKind second = ResourceKind.Named("test.twice").OffersPerVantage(ValueNames.BaseUrl);

        Assert.Same(first, second);
    }

    [Fact]
    public void TwoPackagesCannotMeanDifferentThingsByOneName()
    {
        ResourceKind unused = ResourceKind.Named("test.conflict").OffersPerVantage(ValueNames.BaseUrl);

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => ResourceKind.Named("test.conflict").Offers(ValueNames.ConnectionString).Build());

        Assert.Contains("already declared as a resource kind with different values", failure.Message, StringComparison.Ordinal);

        // Both schemas are named, so the reader can see which declaration to change.
        Assert.Contains("BaseUrl per-vantage", failure.Message, StringComparison.Ordinal);
        Assert.Contains("ConnectionString", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AKindCannotOfferOneValueTwice()
        => Assert.ThrowsAny<ArgumentException>(
            () => ResourceKind.Named("test.duplicate").Offers(ValueNames.BaseUrl).Offers(ValueNames.BaseUrl).Build());
}
