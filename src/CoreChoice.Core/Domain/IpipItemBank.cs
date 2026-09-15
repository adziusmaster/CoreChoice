namespace CoreChoice.Domain;

/// <summary>
/// The IPIP Big-Five Factor Markers, 50 items (Goldberg, 1992). Public domain: the International
/// Personality Item Pool places its items in the public domain explicitly, which is why this
/// instrument was chosen over the BFI-2 or the NEO, both of which carry licensing conditions.
///
/// Each item is prefixed "I…" in the UI ("I am the life of the party"). The stem is omitted here so
/// the presentation layer controls phrasing without the scoring layer caring.
/// </summary>
public static class IpipItemBank
{
    public const int ItemCount = 50;

    /// <summary>Lowest and highest valid Likert response.</summary>
    public const int MinResponse = 1;
    public const int MaxResponse = 5;

    private static readonly IpipItem[] All =
    [
        new(1,  "Am the life of the party.",                              Trait.Extraversion,       false),
        new(2,  "Feel little concern for others.",                        Trait.Agreeableness,      true),
        new(3,  "Am always prepared.",                                    Trait.Conscientiousness,  false),
        new(4,  "Get stressed out easily.",                               Trait.Neuroticism,        false),
        new(5,  "Have a rich vocabulary.",                                Trait.Openness,           false),
        new(6,  "Don't talk a lot.",                                      Trait.Extraversion,       true),
        new(7,  "Am interested in people.",                               Trait.Agreeableness,      false),
        new(8,  "Leave my belongings around.",                            Trait.Conscientiousness,  true),
        new(9,  "Am relaxed most of the time.",                           Trait.Neuroticism,        true),
        new(10, "Have difficulty understanding abstract ideas.",          Trait.Openness,           true),
        new(11, "Feel comfortable around people.",                        Trait.Extraversion,       false),
        new(12, "Insult people.",                                         Trait.Agreeableness,      true),
        new(13, "Pay attention to details.",                              Trait.Conscientiousness,  false),
        new(14, "Worry about things.",                                    Trait.Neuroticism,        false),
        new(15, "Have a vivid imagination.",                              Trait.Openness,           false),
        new(16, "Keep in the background.",                                Trait.Extraversion,       true),
        new(17, "Sympathize with others' feelings.",                      Trait.Agreeableness,      false),
        new(18, "Make a mess of things.",                                 Trait.Conscientiousness,  true),
        new(19, "Seldom feel blue.",                                      Trait.Neuroticism,        true),
        new(20, "Am not interested in abstract ideas.",                   Trait.Openness,           true),
        new(21, "Start conversations.",                                   Trait.Extraversion,       false),
        new(22, "Am not interested in other people's problems.",          Trait.Agreeableness,      true),
        new(23, "Get chores done right away.",                            Trait.Conscientiousness,  false),
        new(24, "Am easily disturbed.",                                   Trait.Neuroticism,        false),
        new(25, "Have excellent ideas.",                                  Trait.Openness,           false),
        new(26, "Have little to say.",                                    Trait.Extraversion,       true),
        new(27, "Have a soft heart.",                                     Trait.Agreeableness,      false),
        new(28, "Often forget to put things back in their proper place.", Trait.Conscientiousness,  true),
        new(29, "Get upset easily.",                                      Trait.Neuroticism,        true),
        new(30, "Do not have a good imagination.",                        Trait.Openness,           true),
        new(31, "Talk to a lot of different people at parties.",          Trait.Extraversion,       false),
        new(32, "Am not really interested in others.",                    Trait.Agreeableness,      true),
        new(33, "Like order.",                                            Trait.Conscientiousness,  false),
        new(34, "Change my mood a lot.",                                  Trait.Neuroticism,        true),
        new(35, "Am quick to understand things.",                         Trait.Openness,           false),
        new(36, "Don't like to draw attention to myself.",                Trait.Extraversion,       true),
        new(37, "Take time out for others.",                              Trait.Agreeableness,      false),
        new(38, "Shirk my duties.",                                       Trait.Conscientiousness,  true),
        new(39, "Have frequent mood swings.",                             Trait.Neuroticism,        false),
        new(40, "Use difficult words.",                                   Trait.Openness,           true),
        new(41, "Don't mind being the center of attention.",              Trait.Extraversion,       false),
        new(42, "Feel others' emotions.",                                 Trait.Agreeableness,      false),
        new(43, "Follow a schedule.",                                     Trait.Conscientiousness,  true),
        new(44, "Get irritated easily.",                                  Trait.Neuroticism,        true),
        new(45, "Spend time reflecting on things.",                       Trait.Openness,           true),
        new(46, "Am quiet around strangers.",                             Trait.Extraversion,       true),
        new(47, "Make people feel at ease.",                              Trait.Agreeableness,      true),
        new(48, "Am exacting in my work.",                                Trait.Conscientiousness,  false),
        new(49, "Often feel blue.",                                       Trait.Neuroticism,        false),
        new(50, "Am full of ideas.",                                      Trait.Openness,           false),
    ];

    public static IReadOnlyList<IpipItem> Items => All;

    public static IpipItem ByNumber(int number) =>
        All.FirstOrDefault(i => i.Number == number)
        ?? throw new ArgumentException($"No IPIP item numbered {number}.", nameof(number));
}
