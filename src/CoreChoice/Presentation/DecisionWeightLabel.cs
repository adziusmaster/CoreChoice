namespace CoreChoice.Presentation;

/// <summary>
/// Short, plain-language stand-in for the numeric decision weight (1-5), shared by every screen
/// that names it: <see cref="DilemmaViewModel"/>'s slider label and <see cref="AnswerDetailViewModel"/>'s
/// past-decision detail both need the exact same words for the exact same number, so this is the
/// one place either can drift from. Deliberately not <see cref="Domain.DecisionWeight.Description"/>
/// — that full sentence is written for the advisor prompt, not a short label next to a slider or a
/// past answer.
/// </summary>
public static class DecisionWeightLabel
{
    public static string For(int weight) => weight switch
    {
        <= 1 => "Barely anything",
        2 => "Not much",
        3 => "A fair amount",
        4 => "Quite a lot",
        _ => "A great deal",
    };
}
