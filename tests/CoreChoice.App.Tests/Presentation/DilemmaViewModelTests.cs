using CoreChoice.Application;
using CoreChoice.Domain;
using CoreChoice.Presentation;
using CoreChoice.Services;
using FluentAssertions;
using NSubstitute;

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

    private static DilemmaViewModel BuildVm(
        IProfileRepository? repository = null, IVoiceDictation? dictation = null) =>
        new(repository ?? new FakeProfileRepository(), dictation ?? AvailableDictation());

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
}
