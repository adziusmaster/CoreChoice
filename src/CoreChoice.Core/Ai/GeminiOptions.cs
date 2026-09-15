namespace CoreChoice.Ai;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>Google AI Studio key. Supplied as Gemini__ApiKey at runtime; never committed.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gemini-flash-lite-latest";

    /// <summary>Attempts before giving up on a 429/503. Gemini sheds load routinely.</summary>
    public int MaxAttempts { get; set; } = 3;
}
