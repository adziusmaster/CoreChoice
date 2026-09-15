namespace CoreChoice.Application;

/// <summary>The balance could not cover the request. Carries both numbers so the UI can be specific.</summary>
public sealed class InsufficientCoinsException(int required, int available)
    : Exception($"This costs {required} coin(s); the balance is {available}.")
{
    public int Required { get; } = required;
    public int Available { get; } = available;
}

/// <summary>The advisor could not be reached or timed out. Retryable; the coin is refunded.</summary>
public sealed class DecisionUnavailableException(string reason, Exception? inner = null)
    : Exception(reason, inner);

/// <summary>
/// The model returned something that does not match the response schema. Treated as a distinct
/// failure from unavailability because it is the shape a successful prompt injection would take,
/// and it should be logged with the prompt version rather than retried blindly.
/// </summary>
public sealed class MalformedAdvisorResponseException(string detail)
    : Exception($"The advisor returned an unusable response: {detail}");
