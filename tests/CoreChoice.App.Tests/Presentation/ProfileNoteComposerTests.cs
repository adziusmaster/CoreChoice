using System.Reflection;
using CoreChoice.Domain;
using CoreChoice.Presentation;
using FluentAssertions;

namespace CoreChoice.App.Tests.Presentation;

public class ProfileNoteComposerTests
{
    private static readonly Trait[] AllTraits = Enum.GetValues<Trait>();

    private static readonly string[] TraitNames =
        AllTraits.Select(t => t.ToString().ToLowerInvariant()).ToArray();

    private static OceanProfile Profile(int o, int c, int e, int a, int n) => new(
        TraitScore.From(o), TraitScore.From(c), TraitScore.From(e),
        TraitScore.From(a), TraitScore.From(n));

    /// <summary>Reads one of <see cref="ProfileNoteComposer"/>'s private phrase tables by field
    /// name via reflection. There is no production reason to expose these publicly — reflection
    /// here buys the completeness test in <see cref="PhraseTable_ForEveryTraitInBothDirections_ShouldHaveANonEmptyEntry"/>
    /// without widening the composer's public surface just for tests.</summary>
    private static Dictionary<Trait, string> GetPhraseTable(string fieldName)
    {
        var field = typeof(ProfileNoteComposer).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"ProfileNoteComposer no longer has a private static field named '{fieldName}'. " +
                "Update this test to match, or restore the field.");

        return (Dictionary<Trait, string>)field.GetValue(null)!;
    }

    [Fact]
    public void Compose_ForAClearlyHighOpennessLowConscientiousnessProfile_ShouldNameThatExactPair()
    {
        // Arrange — the artboard's own shape: openness pulls up hard, conscientiousness pulls down
        // hard, everything else sits at the midpoint.
        var profile = Profile(o: 90, c: 15, e: 50, a: 50, n: 50);

        // Act
        var note = ProfileNoteComposer.Compose(profile);

        // Assert — matches design/ProfileResult.dc.html's note card verbatim.
        note.Should().Be(
            "You are drawn to what is new and possible, and less drawn to the systems that make " +
            "new things survive. That combination tends to produce a great many beginnings.");
    }

    [Fact]
    public void Compose_ForTheOppositePairing_ShouldNameConscientiousnessHighAndOpennessLowNotTheOtherProfile()
    {
        // Arrange — the mirror image of the artboard shape: conscientiousness pulls up hard,
        // openness pulls down hard.
        var profile = Profile(o: 15, c: 90, e: 50, a: 50, n: 50);

        // Act
        var note = ProfileNoteComposer.Compose(profile);

        // Assert
        note.Should().Be(
            "You are drawn to finishing what you start, and less drawn to the untested or the " +
            "strange. That combination tends to produce work that outlasts the person who made it.");
        // And it must not be the high-openness note reused for the low-openness profile.
        note.Should().NotContain("drawn to what is new and possible");
    }

    [Fact]
    public void Compose_ForAFlatProfile_ShouldNotClaimAnyPeak()
    {
        // Arrange — nothing here is even 15 points from the midpoint.
        var profile = Profile(o: 50, c: 52, e: 48, a: 55, n: 45);

        // Act
        var note = ProfileNoteComposer.Compose(profile);

        // Assert — no trait name, no "drawn to"/"less drawn to" pairing language.
        note.Should().Contain("even profile");
        note.Should().NotContain("drawn to");
        note.Should().NotContain("combination");
    }

    [Theory]
    [InlineData(64, 36)] // both just inside "Moderate" (dev 14) — still flat
    [InlineData(50, 50)]
    public void Compose_WhenNoTraitReachesTheSalientThreshold_ShouldStillReadAsFlat(int nearHigh, int nearLow)
    {
        // Arrange — dev of 14 must not be treated as "meaningfully far from the middle"; only 15+
        // qualifies (this pins the boundary the display band also uses).
        var profile = Profile(o: nearHigh, c: nearLow, e: 50, a: 50, n: 50);

        // Act
        var note = ProfileNoteComposer.Compose(profile);

        // Assert
        note.Should().Contain("even profile");
    }

    [Fact]
    public void Compose_CalledTwiceOnSeparatelyConstructedButIdenticalProfiles_ShouldReturnTheIdenticalNote()
    {
        // Arrange — two distinct OceanProfile instances with the same values, not the same
        // reference, proving there is no hidden randomness or instance state.
        var first = Profile(o: 90, c: 15, e: 42, a: 61, n: 33);
        var second = Profile(o: 90, c: 15, e: 42, a: 61, n: 33);

        // Act
        var noteOne = ProfileNoteComposer.Compose(first);
        var noteTwo = ProfileNoteComposer.Compose(second);

        // Assert
        noteOne.Should().Be(noteTwo);
    }

    [Fact]
    public void Compose_ForAClearPeak_ShouldNotBeAGenericTemplateNamingTheTraits()
    {
        // Arrange — this is the failure the task explicitly calls out: "You are high in openness
        // and low in conscientiousness" is not an acceptable note. Guard against a regression to a
        // generic trait-name template by asserting the trait names themselves never appear.
        var profile = Profile(o: 90, c: 15, e: 50, a: 50, n: 50);

        // Act
        var note = ProfileNoteComposer.Compose(profile);

        // Assert
        note.Should().NotContainAny("Openness", "openness", "Conscientiousness", "conscientiousness", "high in", "low in");
    }

    [Fact]
    public void Compose_ForDifferentDominantTraits_ShouldProduceDifferentNotesRatherThanOneTemplateFilledIn()
    {
        // Arrange — two profiles whose dominant pairing differs (openness/conscientiousness vs.
        // extraversion/neuroticism). A generic template parameterized only by trait name would
        // still pass the two tests above; this pins that the actual wording differs, not just the
        // names slotted into it.
        var opennessLed = Profile(o: 90, c: 15, e: 50, a: 50, n: 50);
        var extraversionLed = Profile(o: 50, c: 50, e: 90, a: 50, n: 15);

        // Act
        var opennessNote = ProfileNoteComposer.Compose(opennessLed);
        var extraversionNote = ProfileNoteComposer.Compose(extraversionLed);

        // Assert
        opennessNote.Should().NotBe(extraversionNote);
        extraversionNote.Should().Contain("energized by people and noise");
    }

    [Fact]
    public void Compose_WhenOnlyOneTraitIsSalientAndNothingPullsTheOppositeWay_ShouldUseTheLeadOnlyFallback()
    {
        // Arrange — openness alone is salient; every other trait sits exactly at the midpoint, so
        // there is no opposite-pulling secondary trait to name.
        var profile = Profile(o: 90, c: 50, e: 50, a: 50, n: 50);

        // Act
        var note = ProfileNoteComposer.Compose(profile);

        // Assert — the fallback sentence, not the two-clause "{lead}, {trail}." shape.
        note.Should().Be("You are drawn to what is new and possible. The rest of your profile stays close to the middle.");
    }

    /// <summary>Every combination the composer's own logic can select between: which trait leads,
    /// which direction it leads in, which trait (if any) trails in the opposite direction, plus the
    /// flat case. A handful of hand-picked example profiles — the tests above — can prove those
    /// specific notes read correctly, but they cannot prove a phrase table has no holes: the
    /// reviewer blanked <c>LowTrail[Neuroticism]</c> and every existing test, including the ones
    /// above, kept passing. This sweep exists to catch exactly that class of gap.</summary>
    public static IEnumerable<object[]> SweptProfiles()
    {
        const int Mid = 50, High = 90, Low = 10, SecondaryHigh = 85, SecondaryLow = 15;

        yield return new object[] { "flat profile", Mid, Mid, Mid, Mid, Mid };

        foreach (var primary in AllTraits)
        {
            foreach (var primaryHigh in new[] { true, false })
            {
                var baseScores = AllTraits.ToDictionary(t => t, _ => Mid);
                baseScores[primary] = primaryHigh ? High : Low;

                yield return new object[]
                {
                    $"{primary} {(primaryHigh ? "high" : "low")}, lead only",
                    baseScores[Trait.Openness], baseScores[Trait.Conscientiousness],
                    baseScores[Trait.Extraversion], baseScores[Trait.Agreeableness], baseScores[Trait.Neuroticism],
                };

                foreach (var secondary in AllTraits.Where(t => t != primary))
                {
                    var secondaryHigh = !primaryHigh; // must pull the opposite way to register as a trail.
                    var scores = new Dictionary<Trait, int>(baseScores)
                    {
                        [secondary] = secondaryHigh ? SecondaryHigh : SecondaryLow,
                    };

                    yield return new object[]
                    {
                        $"{primary} {(primaryHigh ? "high" : "low")} / {secondary} {(secondaryHigh ? "high" : "low")}",
                        scores[Trait.Openness], scores[Trait.Conscientiousness],
                        scores[Trait.Extraversion], scores[Trait.Agreeableness], scores[Trait.Neuroticism],
                    };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(SweptProfiles))]
    public void Compose_AcrossAWideSweepOfLeadAndTrailPairings_ShouldAlwaysProduceAWellFormedNote(
        string description, int o, int c, int e, int a, int n)
    {
        // Arrange
        var profile = Profile(o, c, e, a, n);

        // Act
        var note = ProfileNoteComposer.Compose(profile);

        // Assert — every invariant a well-formed note must satisfy, named to the profile that
        // produced it so a failure points straight at the broken cell rather than an anonymous string.
        note.Should().NotBeNullOrWhiteSpace($"the note for [{description}] must not be blank");

        note.Should().NotContain(", .", $"the note for [{description}] has an empty trail clause: \"{note}\"");
        note.Should().NotContain(" .", $"the note for [{description}] has a dangling space before its full stop: \"{note}\"");
        note.Should().NotContain(",,", $"the note for [{description}] has a doubled comma: \"{note}\"");
        note.Should().NotContain("  ", $"the note for [{description}] has a doubled space: \"{note}\"");

        char.IsUpper(note[0]).Should().BeTrue($"the note for [{description}] must start with a capital letter: \"{note}\"");
        note.Should().EndWith(".", $"the note for [{description}] must end with a full stop: \"{note}\"");

        var lowered = note.ToLowerInvariant();
        foreach (var traitName in TraitNames)
        {
            lowered.Should().NotContain(traitName,
                $"the note for [{description}] fell back to naming the trait \"{traitName}\" instead of describing it: \"{note}\"");
        }

        note.Should().NotContain("{", $"the note for [{description}] left placeholder residue: \"{note}\"");
        note.Should().NotContain("}", $"the note for [{description}] left placeholder residue: \"{note}\"");
    }

    [Theory]
    [InlineData("HighLead")]
    [InlineData("LowLead")]
    [InlineData("HighTrail")]
    [InlineData("LowTrail")]
    [InlineData("HighConsequence")]
    [InlineData("LowConsequence")]
    public void PhraseTable_ForEveryTraitInBothDirections_ShouldHaveANonEmptyEntry(string tableName)
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
}
