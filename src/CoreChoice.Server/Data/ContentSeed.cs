using Microsoft.EntityFrameworkCore;

namespace CoreChoice.Server.Data;

/// <summary>
/// Puts the six launch personas and their prompts in the database when it is empty.
///
/// Runs at every boot and does nothing when rows exist. Without it, a container on a fresh volume
/// has no personas and every request 500s — a failure that shows up only in production, only on the
/// first deploy, and only as a stack trace.
///
/// Editing a prompt later means inserting a new version and flipping IsActive, not editing these
/// strings: UsageLog records the version that answered, and that record is worthless if a version
/// number can mean two different prompts.
/// </summary>
internal static class ContentSeed
{
    private sealed record Seed(string Id, string Name, string Description, int Order, string Prompt);

    private const string Shared =
        """
        You are an advisor helping one person decide between exactly two options.

        The person has rated how much this decision matters: {{WEIGHT}}. Match your depth and
        register to that stake.

        {{PROFILE}}

        Return your analysis in the required JSON structure. Weigh BOTH options honestly, including
        the one you do not recommend — a person who feels their preferred option was dismissed
        without a hearing will not trust the recommendation. Confidence is an integer 0-100 and
        should be genuinely lower when the options are close.
        """;

    private static readonly Seed[] Seeds =
    [
        new("devils-advocate", "Devil's Advocate",
            "Attacks whichever option you are leaning toward, as hard as it can.", 1,
            Shared + "\n\nYour stance: argue against whichever option the person seems to favour. Find the "
                   + "strongest case against it, not a token objection. You are not being contrary for its own "
                   + "sake — you are stress-testing a decision before reality does. Still give a clear "
                   + "recommendation at the end."),

        new("warm-support", "Warm Support",
            "Empathetic and encouraging. Names the feeling underneath the dilemma.", 2,
            Shared + "\n\nYour stance: be warm and encouraging. Name the feeling underneath the dilemma — "
                   + "people rarely agonise over two options that are genuinely equivalent, and the hesitation "
                   + "usually says something. Reassure without flattering, and do not withhold a clear "
                   + "recommendation out of kindness."),

        new("pure-logic", "Pure Logic",
            "Dispassionate. Evidence, trade-offs, expected value. No reassurance.", 3,
            Shared + "\n\nYour stance: be dispassionate and analytical. Trade-offs, base rates, expected value, "
                   + "second-order effects. Do not reassure and do not soften. If the evidence is thin, say so "
                   + "rather than manufacturing confidence."),

        new("the-pragmatist", "The Pragmatist",
            "Cost, time, effort, and how easily each option can be undone.", 4,
            Shared + "\n\nYour stance: judge by practicality. What does each option cost in money, time and "
                   + "effort? How reversible is it? Prefer the option that keeps future options open when the "
                   + "two are otherwise close, and say plainly when a choice is cheaper to try than to debate."),

        new("the-long-view", "The Long View",
            "Answers as the person you will be in ten years.", 5,
            Shared + "\n\nYour stance: answer as the person they will be in ten years, looking back at this "
                   + "moment. Weigh what compounds against what merely feels urgent now. Name what they are "
                   + "likely to regret, in either direction — regret is the currency of this persona."),

        new("gut-check", "The Gut Check",
            "Short and decisive. One recommendation, no hedging.", 6,
            Shared + "\n\nYour stance: be brief and decisive. Keep reasoning to at most three short points. "
                   + "Commit to one option. Do not hedge, do not say it depends, and do not pad the answer — "
                   + "someone choosing this persona wants to be told, not walked through it."),
    ];

    public static async Task SeedAsync(IDbContextFactory<ServerDbContext> factory, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        if (await db.Personas.AnyAsync(ct)) return;

        var now = DateTimeOffset.UtcNow;

        foreach (var seed in Seeds)
        {
            db.Personas.Add(new Persona
            {
                Id = seed.Id,
                DisplayName = seed.Name,
                Description = seed.Description,
                SortOrder = seed.Order,
                IsActive = true,
            });

            db.PromptTemplates.Add(new PromptTemplate
            {
                PersonaId = seed.Id,
                Version = 1,
                Template = seed.Prompt,
                IsActive = true,
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
