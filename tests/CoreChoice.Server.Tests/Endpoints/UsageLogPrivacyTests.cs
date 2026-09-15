using System.Net;
using System.Net.Http.Json;
using CoreChoice.Ai;
using CoreChoice.Application;
using CoreChoice.Domain;
using CoreChoice.Server.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CoreChoice.Server.Tests.Endpoints;

/// <summary>
/// The data-policy guard. The dilemma and the personality profile travel in the request, are used
/// to build the prompt, and are never written to disk or to a log sink. This is the test that says
/// so.
/// </summary>
public class UsageLogPrivacyTests
{
    /// <summary>
    /// THIS LIST IS THE ENTIRETY OF WHAT CORECHOICE RECORDS ABOUT A DECISION REQUEST.
    ///
    /// Adding an entry here means adding a column to a table whose rows describe what a person was
    /// privately agonising over. That is a product decision, not a refactor: justify it in the PR,
    /// or find somewhere else to put the datum. Do not "fix" a failure here by pasting in the new
    /// column name.
    /// </summary>
    private static readonly string[] RecordedColumns =
    [
        "Id", "DeviceHash", "PersonaId", "PromptVersion", "Weight",
        "Personalized", "PromptTokens", "OutputTokens", "TotalTokens", "Success", "At",
    ];

    private const string MarkerA = "QUOKKAALPHA-leave-my-husband";
    private const string MarkerB = "QUOKKABRAVO-stay-another-year";
    private const string MarkerC = "QUOKKACHARLIE-he-does-not-know-yet";

    // Distinctive trait values that collide with nothing else on the row: the token counts are
    // 1009/2003/3012, the weight is 4, the id is 1. Compared per-column as numbers, never as
    // substrings of the serialized row, so "17" cannot hide inside "1700".
    private static readonly int[] TraitValues = [83, 17, 41, 96, 62];

    [Fact]
    public async Task UsageLog_Always_ShouldRecordNothingBeyondTheDeclaredColumns()
    {
        // Arrange
        using var factory = new CoreChoiceAppFactory(Substitute.For<IGeminiClient>());
        using var client = factory.CreateClient();

        // Act
        var columns = await ReadColumnNamesAsync(factory);

        // Assert
        columns.Should().BeEquivalentTo(RecordedColumns,
            "the usage log records cost and outcome, never the dilemma or the profile — a new " +
            "column here is a new fact recorded about a private decision");
    }

    [Fact]
    public async Task Generate_OnSuccess_ShouldPersistNoTraceOfTheDilemmaOrTheProfile()
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        gemini.AnalyseAsync(default!, default!, default, default).ReturnsForAnyArgs(
            new DecisionResult(
                new DecisionAnalysis("Option A", 70, ["because"],
                    new OptionAssessment("A", ["up"], ["down"]),
                    new OptionAssessment("B", ["up"], ["down"]), "note", true),
                new TokenUsage(1009, 2003, 3012)));

        using var factory = new CoreChoiceAppFactory(gemini);
        using var client = factory.CreateClient();
        var device = Guid.NewGuid();
        (await client.PostAsJsonAsync("/api/coins/ensure", new { deviceId = device }))
            .EnsureSuccessStatusCode();

        // Act
        var response = await client.PostAsJsonAsync("/api/decisions", new
        {
            deviceId = device,
            optionA = MarkerA,
            optionB = MarkerB,
            context = MarkerC,
            persona = "pure-logic",
            weight = 4,
            profile = new
            {
                openness = TraitValues[0],
                conscientiousness = TraitValues[1],
                extraversion = TraitValues[2],
                agreeableness = TraitValues[3],
                neuroticism = TraitValues[4],
            },
        });
        response.EnsureSuccessStatusCode();

        // Assert
        var row = await ReadSingleRowAsync(factory);
        AssertRowCarriesNoMarkersOrTraits(row);

        factory.Logs.Messages.Should().NotContain(m => HasAnyMarker(m),
            "a successful request must never carry the dilemma into the log sink");
    }

    [Fact]
    public async Task Generate_WhenTheModelReturnsGarbage_ShouldNotLeakTheDilemmaToTheRowOrTheLogs()
    {
        // Arrange — the 502 branch is the one that logs on purpose (persona + prompt version), so
        // it is the most plausible place for someone to one day add "and here's what they asked".
        var gemini = Substitute.For<IGeminiClient>();
        gemini.AnalyseAsync(default!, default!, default, default)
            .ThrowsAsyncForAnyArgs(new MalformedAdvisorResponseException("nope"));

        using var factory = new CoreChoiceAppFactory(gemini);
        using var client = factory.CreateClient();
        var device = Guid.NewGuid();
        (await client.PostAsJsonAsync("/api/coins/ensure", new { deviceId = device }))
            .EnsureSuccessStatusCode();

        // Act
        var response = await client.PostAsJsonAsync("/api/decisions", new
        {
            deviceId = device,
            optionA = MarkerA,
            optionB = MarkerB,
            context = MarkerC,
            persona = "pure-logic",
            weight = 4,
            profile = (object?)null,
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        var row = await ReadSingleRowAsync(factory);
        AssertRowCarriesNoMarkersOrTraits(row);

        factory.Logs.Messages.Should().NotContain(m => HasAnyMarker(m),
            "the 502 warning log carries the persona and prompt version on purpose, and must never " +
            "grow to carry the dilemma with them — that would ship it straight to the container logs, " +
            "a hole the column allowlist cannot see");
    }

    private static bool HasAnyMarker(string message) =>
        message.Contains(MarkerA[..8], StringComparison.Ordinal)
        || message.Contains(MarkerB[..8], StringComparison.Ordinal)
        || message.Contains(MarkerC[..8], StringComparison.Ordinal);

    private static void AssertRowCarriesNoMarkersOrTraits(IReadOnlyDictionary<string, object?> row)
    {
        row.Keys.Should().BeEquivalentTo(RecordedColumns,
            "a request must not add a column that the schema test did not already declare");

        foreach (var (column, value) in row)
        {
            if (value is string text)
            {
                foreach (var marker in new[] { MarkerA, MarkerB, MarkerC })
                {
                    // The first eight characters, so a truncated or prefixed copy is caught too.
                    text.Should().NotContain(marker[..8],
                        $"column {column} must hold no part of what the person typed");
                }
            }

            if (value is long or int)
            {
                var number = Convert.ToInt64(value);
                TraitValues.Should().NotContain((int)number,
                    $"column {column} holds {number}, which is one of the person's trait scores");
            }
        }
    }

    private static async Task<IReadOnlyList<string>> ReadColumnNamesAsync(CoreChoiceAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ServerDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """SELECT name FROM pragma_table_info('UsageLogs')""";
        await using var reader = await command.ExecuteReaderAsync();

        var names = new List<string>();
        while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        return names;
    }

    private static async Task<IReadOnlyDictionary<string, object?>> ReadSingleRowAsync(
        CoreChoiceAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ServerDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """SELECT * FROM "UsageLogs" """;
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        rows.Should().ContainSingle("one request writes exactly one usage row");
        return rows[0];
    }
}
