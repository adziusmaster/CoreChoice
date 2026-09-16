using System.Reflection;
using CoreChoice.Domain;
using CoreChoice.Presentation;
using FluentAssertions;

namespace CoreChoice.App.Tests.Presentation;

public class WaitingLineComposerTests
{
    private static readonly Trait[] AllTraits = Enum.GetValues<Trait>();

    private static readonly string[] TraitNames =
        AllTraits.Select(t => t.ToString().ToLowerInvariant()).ToArray();

    private static OceanProfile Profile(int o, int c, int e, int a, int n) => new(
        TraitScore.From(o), TraitScore.From(c), TraitScore.From(e),
        TraitScore.From(a), TraitScore.From(n));

    [Fact]
    public void Compose_ForTheArtboardsHighOpennessProfile_ShouldMatchTheDesignedLineVerbatim()
    {
        // Arrange — openness is the dominant trait, matching design/Loading.dc.html's own example.
        var profile = Profile(o: 88, c: 45, e: 50, a: 50, n: 50);

        // Act
        var line = WaitingLineComposer.Compose(profile);

        // Assert
        line.Should().Be("Weighing your openness against the pull of a steady income.");
    }

    [Fact]
    public void Compose_WithNoProfile_ShouldFallBackToANeutralLineNamingNoTrait()
    {
        // Arrange
        var profile = OceanProfile.None;

        // Act
        var line = WaitingLineComposer.Compose(profile);

        // Assert
        line.Should().NotBeNullOrWhiteSpace();
        foreach (var traitName in TraitNames)
            line.ToLowerInvariant().Should().NotContain(traitName);
    }

    [Fact]
    public void Compose_ForALowConscientiousnessDominantProfile_ShouldNameTheLowPullNotCreditTheHighTrait()
    {
        // Arrange — the exact regression this fix closes: the old line said "Weighing your
        // conscientiousness against the pull of walking away while you still can", which credits
        // a low-conscientiousness person with the discipline they are actually short on.
        var profile = Profile(o: 50, c: 5, e: 50, a: 50, n: 50);

        // Act
        var line = WaitingLineComposer.Compose(profile);

        // Assert — the low trait's own pull is the force, weighed against what finishing costs.
        line.Should().Be(
            "Weighing the pull of walking away while you still can against the work of finishing what you have.");
        line.Should().NotContain("your conscientiousness",
            "a low-conscientiousness profile must not be credited with \"your conscientiousness\"");
    }

    [Fact]
    public void Compose_ForAHighOpennessDominantProfile_ShouldNameTheTraitAsTheForce()
    {
        // Arrange — the mirror case: a HIGH trait genuinely is the pull, so naming it "your
        // openness" stays correct here.
        var profile = Profile(o: 95, c: 50, e: 50, a: 50, n: 50);

        // Act
        var line = WaitingLineComposer.Compose(profile);

        // Assert
        line.Should().Be("Weighing your openness against the pull of a steady income.");
    }

    /// <summary>Every trait, both directions, with the exact expected sentence — not just a
    /// grammar check. A purely grammatical sweep would pass even if a low-direction phrase were
    /// swapped for the wrong trait's, or for its own high-direction phrase; only an exact match
    /// per trait/direction pair catches a meaning-level regression like that.</summary>
    public static TheoryData<Trait, bool, string> ExpectedLines() => new()
    {
        { Trait.Openness, true, "Weighing your openness against the pull of a steady income." },
        { Trait.Openness, false, "Weighing the pull of a steady income against what a new opportunity might be worth." },
        { Trait.Conscientiousness, true, "Weighing your conscientiousness against the pull of walking away while you still can." },
        { Trait.Conscientiousness, false, "Weighing the pull of walking away while you still can against the work of finishing what you have." },
        { Trait.Extraversion, true, "Weighing your extraversion against the pull of a quiet room." },
        { Trait.Extraversion, false, "Weighing the pull of a quiet room against what a room full of people might give you." },
        { Trait.Agreeableness, true, "Weighing your agreeableness against the pull of putting yourself first." },
        { Trait.Agreeableness, false, "Weighing the pull of putting yourself first against the ease of keeping the peace." },
        { Trait.Neuroticism, true, "Weighing your neuroticism against the pull of staying calm about it." },
        { Trait.Neuroticism, false, "Weighing the pull of staying calm about it against the signal that something is actually wrong." },
    };

    [Theory]
    [MemberData(nameof(ExpectedLines))]
    public void Compose_ForEveryTraitInBothDirections_ShouldProduceTheExactSemanticallyCorrectLine(
        Trait trait, bool high, string expected)
    {
        // Arrange
        var scores = AllTraits.ToDictionary(t => t, _ => 50);
        scores[trait] = high ? 95 : 5;
        var profile = Profile(
            scores[Trait.Openness], scores[Trait.Conscientiousness], scores[Trait.Extraversion],
            scores[Trait.Agreeableness], scores[Trait.Neuroticism]);

        // Act
        var line = WaitingLineComposer.Compose(profile);

        // Assert
        line.Should().Be(expected);
    }

    /// <summary>Reads one of <see cref="WaitingLineComposer"/>'s private phrase tables by field
    /// name via reflection — the same approach <c>ProfileNoteComposerTests</c> uses — so the
    /// completeness test below can name the exact missing cell without widening the composer's
    /// public surface just for tests.</summary>
    private static Dictionary<Trait, string> GetPhraseTable(string fieldName)
    {
        var field = typeof(WaitingLineComposer).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"WaitingLineComposer no longer has a private static field named '{fieldName}'. " +
                "Update this test to match, or restore the field.");

        return (Dictionary<Trait, string>)field.GetValue(null)!;
    }

    [Theory]
    [InlineData("HighCounterPull")]
    [InlineData("LowCounterPull")]
    public void PhraseTable_ForEveryTrait_ShouldHaveANonEmptyEntry(string tableName)
    {
        // Arrange
        var table = GetPhraseTable(tableName);

        // Act & Assert — check every trait individually so a missing or blank cell is named, not
        // just "some entry somewhere is missing".
        foreach (var trait in AllTraits)
        {
            table.Should().ContainKey(trait, $"{tableName}[{trait}] must exist");
            table[trait].Should().NotBeNullOrWhiteSpace($"{tableName}[{trait}] must not be blank");
        }
    }

    [Fact]
    public void Compose_CalledTwiceOnIdenticalButDistinctProfileInstances_ShouldReturnTheSameLine()
    {
        // Arrange
        var first = Profile(o: 30, c: 88, e: 40, a: 61, n: 33);
        var second = Profile(o: 30, c: 88, e: 40, a: 61, n: 33);

        // Act
        var lineOne = WaitingLineComposer.Compose(first);
        var lineTwo = WaitingLineComposer.Compose(second);

        // Assert
        lineOne.Should().Be(lineTwo);
    }

    /// <summary>Every trait as the dominant one, both pulling high and pulling low, plus the
    /// no-profile case — swept the same way <c>ProfileNoteComposerTests</c> sweeps its own phrase
    /// tables, so a blanked cell in <c>WaitingLineComposer</c>'s tables cannot hide behind a
    /// handful of hand-picked examples.</summary>
    public static IEnumerable<object[]> SweptProfiles()
    {
        yield return new object[] { "no profile", null! };

        foreach (var dominant in AllTraits)
        {
            foreach (var high in new[] { true, false })
            {
                var scores = AllTraits.ToDictionary(t => t, _ => 50);
                scores[dominant] = high ? 95 : 5;

                yield return new object[]
                {
                    $"{dominant} {(high ? "high" : "low")}",
                    Profile(scores[Trait.Openness], scores[Trait.Conscientiousness],
                        scores[Trait.Extraversion], scores[Trait.Agreeableness], scores[Trait.Neuroticism]),
                };
            }
        }
    }

    [Theory]
    [MemberData(nameof(SweptProfiles))]
    public void Compose_AcrossEveryDominantTraitAndTheNoProfileCase_ShouldAlwaysBeWellFormed(
        string description, OceanProfile? profile)
    {
        // Arrange
        var input = profile ?? OceanProfile.None;

        // Act
        var line = WaitingLineComposer.Compose(input);

        // Assert
        line.Should().NotBeNullOrWhiteSpace($"the line for [{description}] must not be blank");
        char.IsUpper(line[0]).Should().BeTrue($"the line for [{description}] must start with a capital letter: \"{line}\"");
        line.Should().EndWith(".", $"the line for [{description}] must end with a full stop: \"{line}\"");
        line.Should().NotContain("  ", $"the line for [{description}] has a doubled space: \"{line}\"");
        line.Should().NotContain(" .", $"the line for [{description}] has a dangling space before its full stop: \"{line}\"");
        line.Should().NotContain("{", $"the line for [{description}] left placeholder residue: \"{line}\"");
        line.Should().NotContain("}", $"the line for [{description}] left placeholder residue: \"{line}\"");

        if (description == "no profile")
        {
            var lowered = line.ToLowerInvariant();
            foreach (var traitName in TraitNames)
                lowered.Should().NotContain(traitName, $"no profile means no trait should be named: \"{line}\"");
        }
    }
}
