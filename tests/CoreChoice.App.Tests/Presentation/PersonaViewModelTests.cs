using CoreChoice.Application;
using CoreChoice.Domain;
using CoreChoice.Presentation;
using CoreChoice.Services;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CoreChoice.App.Tests.Presentation;

public class PersonaViewModelTests
{
    private static OceanProfile ProfileWithNeuroticism(int score) => new(
        TraitScore.From(50), TraitScore.From(50), TraitScore.From(50), TraitScore.From(50), TraitScore.From(score));

    private static readonly IReadOnlyList<PersonaSummary> AllSixPersonas =
    [
        new("devils-advocate", "Devil's Advocate", "Attacks whichever option you are leaning toward"),
        new("warm-support", "Warm Support", "Names the feeling underneath the question"),
        new("pure-logic", "Pure Logic", "Trade-offs and base rates. No reassurance"),
        new("the-pragmatist", "The Pragmatist", "Cost, time, effort and how easily it is undone"),
        new("the-long-view", "The Long View", "Answers as the person you will be in ten years"),
        new("gut-check", "The Gut Check", "Short and decisive. One answer, no hedging"),
    ];

    // ===== PersonaSuggestion.PersonaId — the full cross-product: weight x profile presence x the
    // neuroticism threshold, sweeping every cell rather than a handful of examples, with the
    // 66/67 boundary and the "override applies only at weights 1-3" rule each represented
    // explicitly so a mutation to either the threshold or the weight bracket fails a specific row.

    public static TheoryData<int, OceanProfile, string> SuggestionCrossProduct()
    {
        var data = new TheoryData<int, OceanProfile, string>();
        OceanProfile none = OceanProfile.None;
        OceanProfile low = ProfileWithNeuroticism(0);
        OceanProfile justBelow = ProfileWithNeuroticism(66);
        OceanProfile atThreshold = ProfileWithNeuroticism(67);
        OceanProfile high = ProfileWithNeuroticism(100);

        // weight 1-2: gut-check, overridden to warm-support only when neuroticism >= 67.
        foreach (var weight in new[] { 1, 2 })
        {
            data.Add(weight, none, "gut-check");
            data.Add(weight, low, "gut-check");
            data.Add(weight, justBelow, "gut-check");
            data.Add(weight, atThreshold, "warm-support");
            data.Add(weight, high, "warm-support");
        }

        // weight 3: the-pragmatist, same override behaviour as 1-2.
        data.Add(3, none, "the-pragmatist");
        data.Add(3, low, "the-pragmatist");
        data.Add(3, justBelow, "the-pragmatist");
        data.Add(3, atThreshold, "warm-support");
        data.Add(3, high, "warm-support");

        // weight 4: pure-logic, NEVER overridden regardless of neuroticism.
        data.Add(4, none, "pure-logic");
        data.Add(4, low, "pure-logic");
        data.Add(4, justBelow, "pure-logic");
        data.Add(4, atThreshold, "pure-logic");
        data.Add(4, high, "pure-logic");

        // weight 5: the-long-view, NEVER overridden regardless of neuroticism.
        data.Add(5, none, "the-long-view");
        data.Add(5, low, "the-long-view");
        data.Add(5, justBelow, "the-long-view");
        data.Add(5, atThreshold, "the-long-view");
        data.Add(5, high, "the-long-view");

        return data;
    }

    [Theory]
    [MemberData(nameof(SuggestionCrossProduct))]
    public void PersonaId_AcrossWeightProfilePresenceAndNeuroticism_ShouldMatchTheDocumentedRule(
        int weight, OceanProfile profile, string expectedId)
    {
        // Act
        var suggestion = PersonaSuggestion.PersonaId(weight, profile);

        // Assert
        suggestion.Should().Be(expectedId);
    }

    [Fact]
    public void PersonaId_ShouldNeverSuggestDevilsAdvocate()
    {
        // Arrange — devils-advocate is a real, selectable persona but is never pushed at someone.
        var profiles = new[]
        {
            OceanProfile.None, ProfileWithNeuroticism(0), ProfileWithNeuroticism(67), ProfileWithNeuroticism(100),
        };

        // Act
        var suggestions = Enumerable.Range(DecisionWeight.Min, DecisionWeight.Max)
            .SelectMany(_ => profiles, (weight, profile) => PersonaSuggestion.PersonaId(weight, profile))
            .ToList();

        // Assert
        suggestions.Should().NotContain("devils-advocate");
    }

    [Fact]
    public void Reason_ForEveryWeightAndProfile_ShouldBeNonEmpty()
    {
        // Arrange
        var profiles = new[] { OceanProfile.None, ProfileWithNeuroticism(30), ProfileWithNeuroticism(90) };

        // Act
        var reasons = Enumerable.Range(DecisionWeight.Min, DecisionWeight.Max)
            .SelectMany(_ => profiles, (weight, profile) => PersonaSuggestion.Reason(weight, profile))
            .ToList();

        // Assert
        reasons.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r));
    }

    // ===== PersonaViewModel.LoadAsync =====

    private static PersonaViewModel BuildVm(
        IPersonaCatalog? catalog = null, IProfileRepository? repository = null) =>
        new(catalog ?? StubCatalog(AllSixPersonas), repository ?? new FakeProfileRepository());

    private static IPersonaCatalog StubCatalog(IReadOnlyList<PersonaSummary> personas)
    {
        var catalog = Substitute.For<IPersonaCatalog>();
        catalog.GetPersonasAsync(Arg.Any<CancellationToken>()).Returns(personas);
        return catalog;
    }

    [Fact]
    public async Task LoadAsync_WithWeightFive_ShouldSuggestTheLongViewAndListTheOtherFive()
    {
        // Arrange
        var vm = BuildVm();

        // Act
        await vm.LoadAsync(5);

        // Assert
        vm.Suggested.Should().NotBeNull();
        vm.Suggested!.Id.Should().Be("the-long-view");
        vm.SuggestionReason.Should().Contain("five out of five");
        vm.Others.Should().HaveCount(5);
        vm.Others.Select(p => p.Id).Should().NotContain("the-long-view");
    }

    [Fact]
    public async Task LoadAsync_WithHighNeuroticismAndLowWeight_ShouldSuggestWarmSupport()
    {
        // Arrange
        var repository = new FakeProfileRepository();
        await repository.SaveProfileAsync(ProfileWithNeuroticism(80));
        var vm = BuildVm(repository: repository);

        // Act
        await vm.LoadAsync(2);

        // Assert
        vm.Suggested!.Id.Should().Be("warm-support");
    }

    [Fact]
    public async Task LoadAsync_WithHighNeuroticismButWeightFive_ShouldNotOverrideTheSuggestion()
    {
        // Arrange
        var repository = new FakeProfileRepository();
        await repository.SaveProfileAsync(ProfileWithNeuroticism(90));
        var vm = BuildVm(repository: repository);

        // Act
        await vm.LoadAsync(5);

        // Assert
        vm.Suggested!.Id.Should().Be("the-long-view");
    }

    [Fact]
    public async Task LoadAsync_WhenTheCatalogThrows_ShouldLeaveSuggestedNullAndOthersEmptyRatherThanThrow()
    {
        // Arrange
        var catalog = Substitute.For<IPersonaCatalog>();
        catalog.GetPersonasAsync(Arg.Any<CancellationToken>()).ThrowsForAnyArgs(new HttpRequestException("offline"));
        var vm = BuildVm(catalog: catalog);

        // Act
        await vm.LoadAsync(3);

        // Assert
        vm.Suggested.Should().BeNull();
        vm.Others.Should().BeEmpty();
        vm.HasSuggestion.Should().BeFalse();
        vm.HasOthers.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WhenTheCallerCancels_ShouldNotSwallowTheCancellation()
    {
        // Arrange
        var catalog = Substitute.For<IPersonaCatalog>();
        using var cts = new CancellationTokenSource();
        catalog.GetPersonasAsync(Arg.Any<CancellationToken>()).ThrowsForAnyArgs(_ =>
        {
            cts.Cancel();
            return new OperationCanceledException(cts.Token);
        });
        var vm = BuildVm(catalog: catalog);

        // Act
        Func<Task> act = async () => await vm.LoadAsync(3, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
