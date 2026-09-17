using CoreChoice.Domain;

namespace CoreChoice.Presentation.Controls;

/// <summary>
/// The one rendering of an advisor's answer — see the header comment in <c>AnswerView.xaml</c> for
/// why this exists as bindable properties on a shared control rather than a second copy of the
/// same XAML. <see cref="AnalysisPage"/> and <see cref="AnswerDetailPage"/> are its two consumers.
/// </summary>
public partial class AnswerView : ContentView
{
    public static readonly BindableProperty AnalysisProperty =
        BindableProperty.Create(nameof(Analysis), typeof(DecisionAnalysis), typeof(AnswerView));

    public static readonly BindableProperty VerdictProperty =
        BindableProperty.Create(nameof(Verdict), typeof(string), typeof(AnswerView), string.Empty);

    public static readonly BindableProperty IsVerdictStrongProperty =
        BindableProperty.Create(nameof(IsVerdictStrong), typeof(bool), typeof(AnswerView), false);

    /// <summary>Whether the "Option A/B — strengths and risks" cards are shown. Defaults to true —
    /// <see cref="AnswerDetailPage"/> always shows them, since a past decision reopened for its
    /// full detail has no reason to hide half of it; <see cref="AnalysisPage"/> is the only
    /// consumer that ever sets this to false, driven by its own "Both sides" toggle.</summary>
    public static readonly BindableProperty ShowBothSidesProperty =
        BindableProperty.Create(nameof(ShowBothSides), typeof(bool), typeof(AnswerView), true);

    public AnswerView() => InitializeComponent();

    public DecisionAnalysis? Analysis
    {
        get => (DecisionAnalysis?)GetValue(AnalysisProperty);
        set => SetValue(AnalysisProperty, value);
    }

    public string Verdict
    {
        get => (string)GetValue(VerdictProperty);
        set => SetValue(VerdictProperty, value);
    }

    public bool IsVerdictStrong
    {
        get => (bool)GetValue(IsVerdictStrongProperty);
        set => SetValue(IsVerdictStrongProperty, value);
    }

    public bool ShowBothSides
    {
        get => (bool)GetValue(ShowBothSidesProperty);
        set => SetValue(ShowBothSidesProperty, value);
    }
}
