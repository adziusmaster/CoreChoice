namespace CoreChoice.Presentation;

/// <summary>
/// Local, offline fallback for the six known personas' display names, keyed by id. Exists because
/// <see cref="PersonaSuggestion.PersonaId"/> already computes the suggested persona's id purely
/// locally — weight and the stored profile, no network — but until now only the human-readable
/// name required a round trip to <see cref="Application.IPersonaCatalog"/>. The dilemma screen's
/// footer must name the suggested advisor ("Answering as The Long View") the moment it renders,
/// with no signal and without the person ever having visited the persona picker — a suggestion
/// that must be sought before it can be seen is not a suggestion, it is the same "choose who
/// answers" decision restated. This table is what lets the footer answer that immediately.
///
/// The ids mirror the ones <see cref="PersonaSuggestion"/> already hardcodes (plus
/// devils-advocate, which is never suggested but is a real, explicitly selectable persona), so
/// this adds no new duplication — both already assume the six personas are fixed. It is a
/// fallback, not a second source of truth: <see cref="DilemmaViewModel"/> prefers the catalog's
/// own spelling the moment <see cref="Application.IPersonaCatalog.GetPersonasAsync"/> answers, so
/// a wording change on the server reaches the footer without a client release.
///
/// No MAUI type appears here, same as the rest of this file's siblings: <c>CoreChoice.App.Tests</c>
/// links it in by source — see <c>PresentationMath.cs</c>'s doc comment for why that matters.
/// </summary>
public static class PersonaDisplayNames
{
    private static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        ["devils-advocate"] = "Devil's Advocate",
        ["warm-support"] = "Warm Support",
        ["pure-logic"] = "Pure Logic",
        ["the-pragmatist"] = "The Pragmatist",
        ["the-long-view"] = "The Long View",
        ["gut-check"] = "Gut Check",
    };

    /// <summary>
    /// The known display name for <paramref name="personaId"/>. Falls back to the id itself when
    /// it is not one of the six known personas, rather than an empty string — so a future persona
    /// added to <see cref="PersonaSuggestion"/> but forgotten here shows an ugly-but-honest id in
    /// the footer instead of a blank "Answering as ".
    /// </summary>
    public static string DisplayName(string personaId) =>
        Names.TryGetValue(personaId, out var name) ? name : personaId;
}
