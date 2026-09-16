using CoreChoice.Application;
using CoreChoice.Presentation;
using FluentAssertions;

namespace CoreChoice.App.Tests.Application;

/// <summary>
/// <see cref="AnalysisPackCatalog"/> is the single source of truth for the three product ids, so
/// that <c>CoinsViewModel</c> and <c>PlayBillingService</c> cannot list them independently and
/// drift apart. These tests pin both former call sites to the catalog directly, so a hand-edited
/// literal reintroduced at either one (rather than reading the catalog) fails here by name.
/// </summary>
public class AnalysisPackCatalogTests
{
    [Fact]
    public void Entries_ShouldDeclareExactlyTheThreeProductIdsCoinsViewModelExposes()
    {
        // Arrange
        var expected = new[]
        {
            CoinsViewModel.TenAnalysesProductId,
            CoinsViewModel.ThirtyAnalysesProductId,
            CoinsViewModel.HundredAnalysesProductId,
        };

        // Act
        var actual = AnalysisPackCatalog.Entries.Select(e => e.ProductId).ToArray();

        // Assert
        actual.Should().BeEquivalentTo(expected,
            "CoinsViewModel's product ids must be exactly the catalog's, not a second, independent list");
    }

    [Fact]
    public void Entries_ShouldMatchTheProductIdsTheStoreServiceContractDeclaresAsFallbackPacks()
    {
        // Arrange — FakeBillingService.FallbackPacks mirrors PlayBillingService.FallbackPacks'
        // shape (PlayBillingService itself cannot be linked here; it touches Android.* types).
        var billing = new FakeBillingService();

        // Act
        var catalogIds = AnalysisPackCatalog.Entries.Select(e => e.ProductId).ToArray();
        var fallbackIds = billing.FallbackPacks.Select(p => p.ProductId).ToArray();

        // Assert
        catalogIds.Should().BeEquivalentTo(fallbackIds, o => o.WithStrictOrdering(),
            "FallbackPacks must declare exactly the same product ids as the catalog, in the same order");
    }

    [Fact]
    public void Entries_ShouldPairEachProductIdWithTheAnalysisCountItsNameImplies()
    {
        // Assert
        AnalysisPackCatalog.Entries.Should().BeEquivalentTo(
        [
            (AnalysisPackCatalog.TenAnalysesProductId, 10),
            (AnalysisPackCatalog.ThirtyAnalysesProductId, 30),
            (AnalysisPackCatalog.HundredAnalysesProductId, 100),
        ]);
    }
}
