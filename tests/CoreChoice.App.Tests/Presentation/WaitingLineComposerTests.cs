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
    public void Compose_ForALowConscientiousnessDominantProfile_ShouldNameConscientiousness()
    {
        // Arrange
        var profile = Profile(o: 50, c: 5, e: 50, a: 50, n: 50);

        // Act
        var line = WaitingLineComposer.Compose(profile);

        // Assert
        line.Should().Contain("conscientiousness");
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
