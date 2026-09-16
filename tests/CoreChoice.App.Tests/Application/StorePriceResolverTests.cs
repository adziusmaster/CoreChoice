using CoreChoice.Application;
using FluentAssertions;

namespace CoreChoice.App.Tests.Application;

/// <summary>
/// <see cref="StorePriceResolver"/> is the all-or-nothing decision behind the coins screen's
/// placeholder-prices banner, pulled out of <c>PlayBillingService</c> specifically so it can be
/// tested here — that class touches <c>Android.*</c> types throughout and cannot be linked into
/// this project. A made-up price shown next to real ones, with nothing to tell them apart, is
/// worse than no real price at all, because the person believes it — so every test below checks
/// that anything short of a complete, real price for every pack falls back to the complete
/// placeholder set instead of a partial mix.
/// </summary>
public class StorePriceResolverTests
{
    private static readonly IReadOnlyList<AnalysisPack> FallbackPacks =
    [
        new AnalysisPack("corechoice.analyses.10", 10, "€2.99"),
        new AnalysisPack("corechoice.analyses.30", 30, "€5.99"),
        new AnalysisPack("corechoice.analyses.100", 100, "€14.99"),
    ];

    [Fact]
    public void Resolve_WhenEveryPackHasARealPrice_ShouldReturnTheResolvedPacksAndTrue()
    {
        // Arrange
        var resolutions = new PackPriceResolution[]
        {
            new("corechoice.analyses.10", "€2,99"),
            new("corechoice.analyses.30", "€5,99"),
            new("corechoice.analyses.100", "€14,99"),
        };

        // Act
        var (packs, pricesAreFromStore) = StorePriceResolver.Resolve(FallbackPacks, resolutions);

        // Assert
        pricesAreFromStore.Should().BeTrue();
        packs.Should().BeEquivalentTo(
        [
            new AnalysisPack("corechoice.analyses.10", 10, "€2,99"),
            new AnalysisPack("corechoice.analyses.30", 30, "€5,99"),
            new AnalysisPack("corechoice.analyses.100", 100, "€14,99"),
        ], o => o.WithStrictOrdering());
    }

    [Fact]
    public void Resolve_WhenOnePackHasNoPrice_ShouldReturnTheCompleteFallbackSetRatherThanAPartialMix()
    {
        // Arrange — exactly the CRITICAL case: two packs price fine, the third's offer list came
        // back empty (e.g. that product is not live in the Play Console yet). The result must be
        // the whole fallback set, never two real prices next to one placeholder.
        var resolutions = new PackPriceResolution[]
        {
            new("corechoice.analyses.10", "€2,99"),
            new("corechoice.analyses.30", null),
            new("corechoice.analyses.100", "€14,99"),
        };

        // Act
        var (packs, pricesAreFromStore) = StorePriceResolver.Resolve(FallbackPacks, resolutions);

        // Assert
        pricesAreFromStore.Should().BeFalse();
        packs.Should().BeEquivalentTo(FallbackPacks, o => o.WithStrictOrdering(),
            "a partial mix of real and placeholder prices must never reach the screen");
    }

    [Fact]
    public void Resolve_WhenNoPackHasAPrice_ShouldReturnTheCompleteFallbackSet()
    {
        // Arrange
        var resolutions = new PackPriceResolution[]
        {
            new("corechoice.analyses.10", null),
            new("corechoice.analyses.30", null),
            new("corechoice.analyses.100", null),
        };

        // Act
        var (packs, pricesAreFromStore) = StorePriceResolver.Resolve(FallbackPacks, resolutions);

        // Assert
        pricesAreFromStore.Should().BeFalse();
        packs.Should().BeEquivalentTo(FallbackPacks, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Resolve_WhenOnePacksFormattedPriceIsBlank_ShouldReturnTheCompleteFallbackSet()
    {
        // Arrange — an empty string is not a real price either.
        var resolutions = new PackPriceResolution[]
        {
            new("corechoice.analyses.10", "€2,99"),
            new("corechoice.analyses.30", string.Empty),
            new("corechoice.analyses.100", "€14,99"),
        };

        // Act
        var (packs, pricesAreFromStore) = StorePriceResolver.Resolve(FallbackPacks, resolutions);

        // Assert
        pricesAreFromStore.Should().BeFalse();
        packs.Should().BeEquivalentTo(FallbackPacks, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Resolve_WhenOnePacksFormattedPriceIsWhitespaceOnly_ShouldReturnTheCompleteFallbackSet()
    {
        // Arrange
        var resolutions = new PackPriceResolution[]
        {
            new("corechoice.analyses.10", "€2,99"),
            new("corechoice.analyses.30", "   "),
            new("corechoice.analyses.100", "€14,99"),
        };

        // Act
        var (packs, pricesAreFromStore) = StorePriceResolver.Resolve(FallbackPacks, resolutions);

        // Assert
        pricesAreFromStore.Should().BeFalse();
        packs.Should().BeEquivalentTo(FallbackPacks, o => o.WithStrictOrdering());
    }
}
