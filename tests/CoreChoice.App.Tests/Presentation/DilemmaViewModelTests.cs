using CoreChoice.Application;
using CoreChoice.Domain;
using CoreChoice.Presentation;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CoreChoice.App.Tests.Presentation;

public class DilemmaViewModelTests
{
    private static IVoiceDictation AvailableDictation(string? heard = "quit my job") =>
        BuildDictation(true, heard);

    private static IVoiceDictation UnavailableDictation() => BuildDictation(false, null);

    private static IVoiceDictation BuildDictation(bool available, string? heard)
    {
        var dictation = Substitute.For<IVoiceDictation>();
        dictation.IsAvailable.Returns(available);
        dictation.ListenAsync(Arg.Any<CancellationToken>()).Returns(heard);
        return dictation;
    }

    /// <summary>Throws on every call — the default catalog for these tests, since the important
    /// case for the footer is that it names the suggested advisor with no network at all.</summary>
    private static IPersonaCatalog OfflineCatalog()
    {
        var catalog = Substitute.For<IPersonaCatalog>();
        catalog.GetPersonasAsync(Arg.Any<CancellationToken>()).ThrowsForAnyArgs(new HttpRequestException("offline"));
        return catalog;
    }

    private static IPersonaCatalog StubCatalog(IReadOnlyList<PersonaSummary> personas)
    {
        var catalog = Substitute.For<IPersonaCatalog>();
        catalog.GetPersonasAsync(Arg.Any<CancellationToken>()).Returns(personas);
        return catalog;
    }

    private static DilemmaViewModel BuildVm(
        IProfileRepository? repository = null, IVoiceDictation? dictation = null, IPersonaCatalog? personaCatalog = null) =>
        new(repository ?? new FakeProfileRepository(), dictation ?? AvailableDictation(), personaCatalog ?? OfflineCatalog());

    // ===== CanSubmit =====

    [Fact]
    public void CanSubmit_WhenBothOptionsAreEmpty_ShouldBeFalse()
    {
        // Arrange
        var vm = BuildVm();

        // Act & Assert
        vm.CanSubmit.Should().BeFalse();
    }

    [Theory]
    [InlineData("Quit my job", "")]
    [InlineData("", "Keep my job")]
    [InlineData("   ", "Keep my job")]
    public void CanSubmit_WhenEitherOptionIsEmptyOrWhitespace_ShouldBeFalse(string a, string b)
    {
        // Arrange
        var vm = BuildVm();

        // Act
        vm.OptionA = a;
        vm.OptionB = b;

        // Assert
        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void CanSubmit_WhenBothOptionsAreValid_ShouldBeTrue()
    {
        // Arrange
        var vm = BuildVm();

        // Act
        vm.OptionA = "Quit my job";
        vm.OptionB = "Keep my job";

        // Assert
        vm.CanSubmit.Should().BeTrue();
    }

    [Fact]
    public void CanSubmit_WhenAnOptionIsExactlyAtMaxLength_ShouldBeTrue()
    {
        // Arrange
        var vm = BuildVm();

        // Act
        vm.OptionA = new string('a', Dilemma.MaxOptionLength);
        vm.OptionB = "Keep my job";

        // Assert
        vm.CanSubmit.Should().BeTrue();
    }

    [Fact]
    public void CanSubmit_WhenAnOptionExceedsMaxLength_ShouldBeFalseRatherThanThrow()
    {
        // Arrange — the domain constructor would throw here; this must block submission instead.
        var vm = BuildVm();

        // Act
        var act = () =>
        {
            vm.OptionA = new string('a', Dilemma.MaxOptionLength + 1);
            vm.OptionB = "Keep my job";
        };

        // Assert
        act.Should().NotThrow();
        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void CanSubmit_WhenContextExceedsMaxLength_ShouldBeFalse()
    {
        // Arrange
        var vm = BuildVm();
        vm.OptionA = "Quit my job";
        vm.OptionB = "Keep my job";

        // Act
        vm.Context = new string('c', Dilemma.MaxContextLength + 1);

        // Assert
        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void CanSubmit_WhenContextIsWhitespaceOnly_ShouldStillBeTrue()
    {
        // Arrange
        var vm = BuildVm();
        vm.OptionA = "Quit my job";
        vm.OptionB = "Keep my job";

        // Act
        vm.Context = "   ";

        // Assert
        vm.CanSubmit.Should().BeTrue();
    }

    // ===== BuildRequestAsync =====

    private static readonly PersonaSummary SamplePersona =
        new("the-pragmatist", "The Pragmatist", "Cost, time, effort and how easily it is undone");

    [Fact]
    public async Task BuildRequestAsync_WhenCanSubmitIsFalse_ShouldReturnNull()
    {
        // Arrange
        var vm = BuildVm();
        vm.Persona = SamplePersona;

        // Act
        var request = await vm.BuildRequestAsync();

        // Assert
        request.Should().BeNull();
    }

    [Fact]
    public async Task BuildRequestAsync_WhenNoPersonaIsChosen_ShouldReturnNull()
    {
        // Arrange
        var vm = BuildVm();
        vm.OptionA = "Quit my job";
        vm.OptionB = "Keep my job";

        // Act
        var request = await vm.BuildRequestAsync();

        // Assert
        request.Should().BeNull();
    }

    [Fact]
    public async Task BuildRequestAsync_WhenTheTestWasNeverTaken_ShouldMapOceanProfileNone()
    {
        // Arrange — FakeProfileRepository's LoadProfileAsync defaults to OceanProfile.None.
        var vm = BuildVm();
        vm.OptionA = "Quit my job";
        vm.OptionB = "Keep my job";
        vm.Persona = SamplePersona;

        // Act
        var request = await vm.BuildRequestAsync();

        // Assert
        request.Should().NotBeNull();
        request!.Profile.Should().Be(OceanProfile.None);
        request.Profile.IsPresent.Should().BeFalse();
    }

    [Fact]
    public async Task BuildRequestAsync_WhenAProfileIsStored_ShouldMapIt()
    {
        // Arrange
        var repository = new FakeProfileRepository();
        var stored = new OceanProfile(
            TraitScore.From(80), TraitScore.From(60), TraitScore.From(40),
            TraitScore.From(30), TraitScore.From(70));
        await repository.SaveProfileAsync(stored);
        var vm = BuildVm(repository: repository);
        vm.OptionA = "Quit my job";
        vm.OptionB = "Keep my job";
        vm.Persona = SamplePersona;

        // Act
        var request = await vm.BuildRequestAsync();

        // Assert
        request!.Profile.Should().Be(stored);
    }

    [Fact]
    public async Task BuildRequestAsync_WithWhitespaceOnlyContext_ShouldMapToNullContext()
    {
        // Arrange
        var vm = BuildVm();
        vm.OptionA = "Quit my job";
        vm.OptionB = "Keep my job";
        vm.Context = "   ";
        vm.Persona = SamplePersona;

        // Act
        var request = await vm.BuildRequestAsync();

        // Assert
        request!.Dilemma.Context.Should().BeNull();
    }

    [Fact]
    public async Task BuildRequestAsync_ShouldMapOptionsWeightAndPersona()
    {
        // Arrange
        var vm = BuildVm();
        vm.OptionA = "Quit my job";
        vm.OptionB = "Keep my job";
        vm.Context = "I have savings";
        vm.Weight = 4;
        vm.Persona = SamplePersona;

        // Act
        var request = await vm.BuildRequestAsync();

        // Assert
        request!.Dilemma.OptionA.Should().Be("Quit my job");
        request.Dilemma.OptionB.Should().Be("Keep my job");
        request.Dilemma.Context.Should().Be("I have savings");
        request.Weight.Value.Should().Be(4);
        request.Persona.Value.Should().Be("the-pragmatist");
    }

    // ===== Dictation =====

    [Fact]
    public void IsDictationAvailable_WhenPortReportsUnsupported_ShouldBeFalse()
    {
        // Arrange
        var vm = BuildVm(dictation: UnavailableDictation());

        // Act & Assert
        vm.IsDictationAvailable.Should().BeFalse();
    }

    [Fact]
    public void IsDictationAvailable_WhenPortReportsSupported_ShouldBeTrue()
    {
        // Arrange
        var vm = BuildVm(dictation: AvailableDictation());

        // Act & Assert
        vm.IsDictationAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task DictateAsync_WhenDictationIsUnavailable_ShouldBeANoOpAndNeverListen()
    {
        // Arrange
        var dictation = UnavailableDictation();
        var vm = BuildVm(dictation: dictation);

        // Act
        await vm.DictateAsync(DilemmaField.OptionA);

        // Assert
        vm.OptionA.Should().BeEmpty();
        await dictation.DidNotReceive().ListenAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(DilemmaField.OptionA)]
    [InlineData(DilemmaField.OptionB)]
    [InlineData(DilemmaField.Context)]
    public async Task DictateAsync_WhenAvailableAndSomethingIsHeard_ShouldFillTheRequestedField(DilemmaField field)
    {
        // Arrange
        var vm = BuildVm(dictation: AvailableDictation("quit my job"));

        // Act
        await vm.DictateAsync(field);

        // Assert
        var actual = field switch
        {
            DilemmaField.OptionA => vm.OptionA,
            DilemmaField.OptionB => vm.OptionB,
            DilemmaField.Context => vm.Context,
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };
        actual.Should().Be("quit my job");
    }

    [Fact]
    public async Task DictateAsync_WhenNothingWasHeard_ShouldLeaveTheFieldUnchanged()
    {
        // Arrange — cancellation or silence: ListenAsync returns null rather than throwing.
        var vm = BuildVm(dictation: AvailableDictation(heard: null));
        vm.OptionA = "existing text";

        // Act
        await vm.DictateAsync(DilemmaField.OptionA);

        // Assert
        vm.OptionA.Should().Be("existing text");
    }

    // ===== WeightLabel =====

    [Theory]
    [InlineData(1, "Barely anything")]
    [InlineData(2, "Not much")]
    [InlineData(3, "A fair amount")]
    [InlineData(4, "Quite a lot")]
    [InlineData(5, "A great deal")]
    public void WeightLabel_ForEachDocumentedWeight_ShouldReturnItsLabel(int weight, string expected)
    {
        // Arrange
        var vm = BuildVm();

        // Act
        vm.Weight = weight;

        // Assert
        vm.WeightLabel.Should().Be(expected);
    }

    // ===== FooterDisplayName / SuggestedDisplayName — the footer must name the suggested advisor,
    // not ask "choose who answers": a suggestion that has to be sought before it is seen is not a
    // suggestion, it hands the choice straight back to someone who came here to avoid one. =====

    [Fact]
    public void FooterDisplayName_OnConstructionWithNoNetworkAndNoPersonaChosen_ShouldNameTheSuggestedAdvisor()
    {
        // Arrange — no InitializeAsync call at all: this must already be correct the instant the
        // view model exists, before storage or the network have had any chance to answer.
        var vm = BuildVm();

        // Act
        vm.Weight = 5;

        // Assert — matches the approved design (design/Dilemma.dc.html): weight "a great deal"
        // suggests the-long-view.
        vm.FooterDisplayName.Should().Be("The Long View");
    }

    [Fact]
    public async Task InitializeAsync_WithNoCatalogAvailable_ShouldStillNameTheSuggestedAdvisor()
    {
        // Arrange — the offline case: the catalog throws, exactly as it would with no signal.
        var vm = BuildVm(personaCatalog: OfflineCatalog());
        vm.Weight = 5;

        // Act
        await vm.InitializeAsync();

        // Assert — the local fallback table named it; the catalog failure never blanked it out.
        vm.FooterDisplayName.Should().Be("The Long View");
    }

    [Theory]
    [InlineData("devils-advocate", "Devil's Advocate")]
    [InlineData("warm-support", "Warm Support")]
    [InlineData("pure-logic", "Pure Logic")]
    [InlineData("the-pragmatist", "The Pragmatist")]
    [InlineData("the-long-view", "The Long View")]
    [InlineData("gut-check", "Gut Check")]
    public void PersonaDisplayNames_DisplayName_ForEachOfTheSixKnownIds_ShouldReturnItsDocumentedName(
        string id, string expectedName)
    {
        // Act
        var name = PersonaDisplayNames.DisplayName(id);

        // Assert — exact match is stronger than merely non-empty, but non-empty is the invariant
        // that matters: a future id added to PersonaSuggestion but forgotten here must never leave
        // the footer blank.
        name.Should().Be(expectedName);
        name.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void FooterDisplayName_WhenTheWeightSliderChanges_ShouldFollowTheNewSuggestion()
    {
        // Arrange
        var vm = BuildVm();

        // Act & Assert — the suggestion rule cross-references the weight directly, so the footer
        // must track it live as the slider moves, not just at load time.
        vm.Weight = 4;
        vm.FooterDisplayName.Should().Be("Pure Logic");

        vm.Weight = 1;
        vm.FooterDisplayName.Should().Be("Gut Check");

        vm.Weight = 5;
        vm.FooterDisplayName.Should().Be("The Long View");
    }

    [Fact]
    public async Task FooterDisplayName_WhenTheWeightChangesAfterAnExplicitPersonaChoice_ShouldClearItAndShowTheNewSuggestion()
    {
        // Arrange — the person picked a persona on PersonaPage; SamplePersona ("the-pragmatist")
        // differs from whatever weight 5 would suggest ("the-long-view"), so a cleared choice is
        // detectable. A changed weight is a changed question: the explicit pick must not survive
        // it, and the slider is what moved here, so it must win back over.
        var vm = BuildVm();
        vm.Persona = SamplePersona;

        // Act — moving the slider after the explicit choice.
        vm.Weight = 5;

        // Assert — the explicit choice is gone; the new suggestion is shown instead.
        vm.Persona.Should().BeNull();
        vm.FooterDisplayName.Should().Be("The Long View");

        // A full re-initialize (fresh catalog fetch) afterwards must not resurrect the old pick.
        await vm.InitializeAsync();
        vm.FooterDisplayName.Should().Be("The Long View");
    }

    [Fact]
    public void Persona_WhenSetExplicitlyFromThePickerScreen_ShouldNotChangeTheWeight()
    {
        // Arrange — returning from PersonaPage must leave "how much rides on it" exactly where the
        // person set it; only moving the slider itself may change Weight.
        var vm = BuildVm();
        vm.Weight = 4;

        // Act
        vm.Persona = SamplePersona;

        // Assert
        vm.Weight.Should().Be(4);
    }

    [Fact]
    public async Task InitializeAsync_WhenTheCatalogAnswers_ShouldPreferTheCatalogsNameOverTheLocalTable()
    {
        // Arrange — the catalog's own spelling deliberately differs from PersonaDisplayNames' so a
        // pass here proves the catalog wins rather than the local table racing it or being final.
        var catalogPersonas = new[]
        {
            new PersonaSummary("the-long-view", "The Long View (from catalog)", "..."),
        };
        var vm = BuildVm(personaCatalog: StubCatalog(catalogPersonas));
        vm.Weight = 5;

        // Act
        await vm.InitializeAsync();

        // Assert
        vm.FooterDisplayName.Should().Be("The Long View (from catalog)");
    }
}
