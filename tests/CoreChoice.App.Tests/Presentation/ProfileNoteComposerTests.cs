using CoreChoice.Domain;
using CoreChoice.Presentation;
using FluentAssertions;

namespace CoreChoice.App.Tests.Presentation;

public class ProfileNoteComposerTests
{
    private static OceanProfile Profile(int o, int c, int e, int a, int n) => new(
        TraitScore.From(o), TraitScore.From(c), TraitScore.From(e),
        TraitScore.From(a), TraitScore.From(n));

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
}
