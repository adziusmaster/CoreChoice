using CommunityToolkit.Mvvm.ComponentModel;
using CoreChoice.Application;
using CoreChoice.Domain;
using CoreChoice.Services;

namespace CoreChoice.Presentation;

/// <summary>
/// Backs the waiting screen and the answer itself — the payoff of the whole app. No MAUI type
/// appears anywhere in this file, same discipline as <see cref="DilemmaViewModel"/> and
/// <see cref="PersonaViewModel"/>: <c>CoreChoice.App.Tests</c> links this in by source, and a
/// converter or helper that touched <c>Microsoft.Maui.*</c> here would silently drop out of test
/// coverage entirely.
///
/// Every failure path is written to say one thing explicitly: the coin was not spent. The backend
/// refunds on every failure after the spend, but a person who cannot see that happened assumes the
/// worst, and that assumption costs more trust than the error itself — so <see cref="AskAsync"/>
/// never lets a bare exception reach the screen. Nothing escapes it uncaught, with one deliberate
/// exception: a caller-cancelled request (the person navigated away) propagates as the
/// <see cref="OperationCanceledException"/> it is, rather than being dressed up as a user-facing
/// error. That one exception is carved out as its own <c>catch (OperationCanceledException) when
/// (ct.IsCancellationRequested)</c> arm — mirroring <c>CoreChoiceApiClient.SendAsync</c>'s own
/// cancellation arm — that rethrows, ahead of a second, unconditional <c>catch (Exception)</c>.
/// Splitting them this way (rather than one <c>catch (Exception) when (!ct.IsCancellationRequested)</c>
/// filter doing both jobs) matters: with the single filter, a genuine bug thrown while <c>ct</c>
/// happened to already be cancelled fell through uncaught and crashed the screen instead of
/// showing the reassurance above.
/// </summary>
public sealed partial class AnalysisViewModel(IDecisionClient client) : ObservableObject
{
    [ObservableProperty]
    private bool isWorking;

    /// <summary>The local, offline line shown the instant a request is sent — see
    /// <see cref="WaitingLineComposer"/>. Never blank: it is set before the call starts, from the
    /// request's own profile, so it never waits on a reply that has not arrived.</summary>
    [ObservableProperty]
    private string waitingLine = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Verdict))]
    [NotifyPropertyChangedFor(nameof(IsVerdictStrong))]
    [NotifyPropertyChangedFor(nameof(HasAnalysis))]
    private DecisionAnalysis? analysis;

    /// <summary>The balance left after paying for the analysis — arrives on the very response
    /// that spent the coin, so it is never a separate, potentially-stale round trip.</summary>
    [ObservableProperty]
    private int balance;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    /// <summary>Whether the failure just shown is worth trying again for. False only for
    /// <see cref="InsufficientCoinsException"/> — retrying without more coins fails the same way
    /// every time.</summary>
    [ObservableProperty]
    private bool canRetry;

    /// <summary>True only for <see cref="InsufficientCoinsException"/>. The page uses this to
    /// offer a route to buying more coins instead of a retry button that cannot succeed.</summary>
    [ObservableProperty]
    private bool isOutOfCoins;

    /// <summary>The advisor persona's display name, for the header. Set by the page from the
    /// navigation parameters handed forward from the persona picker — this view model has no
    /// dependency on <c>IPersonaCatalog</c> of its own, since the name was already resolved before
    /// this screen was ever reached.</summary>
    [ObservableProperty]
    private string personaDisplayName = string.Empty;

    /// <summary>
    /// The loading screen's small "reading" footer — every trait's coarse band, in the same
    /// "Openness low" style <see cref="ProfileViewModel.BuildSummary"/> already uses, not the raw
    /// 0-100 score the artboard's own mockup shows. This app never puts a bare number in front of
    /// someone (see <see cref="VerdictLabel"/>'s doc for why), so the footer follows that rule
    /// rather than the artboard's literal placeholder figures. Empty when there is no profile —
    /// <see cref="HasReadingSummary"/> is what the page checks before showing this section at all.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReadingSummary))]
    private string readingSummary = string.Empty;

    private DecisionRequest? _lastRequest;

    public bool HasReadingSummary => !string.IsNullOrEmpty(ReadingSummary);

    public bool HasAnalysis => Analysis is not null;

    public bool HasError => ErrorMessage is not null;

    /// <summary>The four-word verdict, never the raw confidence number — see
    /// <see cref="VerdictLabel"/>'s own doc for why the number itself never reaches a screen.
    /// Empty until an analysis exists.</summary>
    public string Verdict => Analysis is null ? string.Empty : VerdictLabel.For(Analysis.Confidence);

    /// <summary>Whether the verdict badge should carry accent emphasis.</summary>
    public bool IsVerdictStrong => Analysis is not null && VerdictLabel.IsStrong(Analysis.Confidence);

    /// <summary>Whether the "Both sides" detail — each option's strengths and risks — is
    /// currently expanded on the answer screen. Local, page-only state: never reset by anything
    /// except a fresh <see cref="AskAsync"/>.</summary>
    [ObservableProperty]
    private bool areBothSidesShown;

    public void ToggleBothSides() => AreBothSidesShown = !AreBothSidesShown;

    /// <summary>
    /// Sends one request and resolves into exactly one of: a rendered analysis, or a message that
    /// explicitly says the coin is safe. <see cref="WaitingLine"/> is computed from
    /// <paramref name="request"/>'s own profile before the call starts, and <see cref="IsWorking"/>
    /// is guaranteed true for the call's duration and false afterward — including when the call
    /// throws — because both are set in a <c>finally</c> block.
    /// </summary>
    public async Task AskAsync(DecisionRequest request, CancellationToken ct = default)
    {
        _lastRequest = request;
        WaitingLine = WaitingLineComposer.Compose(request.Profile);
        ReadingSummary = request.Profile.IsPresent ? BuildReadingSummary(request.Profile) : string.Empty;
        ErrorMessage = null;
        CanRetry = false;
        IsOutOfCoins = false;
        Analysis = null;
        AreBothSidesShown = false;
        IsWorking = true;
        try
        {
            var result = await client.AnalyseAsync(request, ct);
            Analysis = result.Analysis;
            Balance = result.Balance;
        }
        catch (InsufficientCoinsException)
        {
            ErrorMessage = "You are out of analyses.";
            CanRetry = false;
            IsOutOfCoins = true;
        }
        catch (DecisionUnavailableException)
        {
            ErrorMessage = "The advisor could not be reached. Your coin was not spent.";
            CanRetry = true;
        }
        catch (MalformedAdvisorResponseException)
        {
            ErrorMessage = "That came back unusable. Your coin was not spent.";
            CanRetry = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Genuine caller cancellation (the person navigated away): propagate it as the
            // cancellation it is, rather than showing an error the person did not cause.
            throw;
        }
        catch (Exception)
        {
            // The catch-all this codebase needs: three too-narrow-catch bugs have already shipped
            // in the HTTP client here, and a bare exception reaching this screen would show a
            // stack-trace-shaped message where a reassurance belongs. Split from the cancellation
            // arm above on purpose: a single `when (!ct.IsCancellationRequested)` filter used to
            // gate this whole catch, which meant a genuine bug thrown while ct happened to already
            // be cancelled fell through both arms and crashed the screen instead of showing the
            // reassurance. This arm now catches everything that is not the caller's own
            // cancellation, unconditionally.
            ErrorMessage = "Something went wrong. Your coin was not spent.";
            CanRetry = true;
        }
        finally
        {
            IsWorking = false;
        }
    }

    /// <summary>Re-sends the last request verbatim — the "try again" affordance for every
    /// retryable failure. Does nothing if nothing has been asked yet.</summary>
    public Task RetryAsync(CancellationToken ct = default) =>
        _lastRequest is null ? Task.CompletedTask : AskAsync(_lastRequest, ct);

    /// <summary>Same "Openness low" style <see cref="ProfileViewModel.BuildSummary"/> uses for the
    /// free result screen — the coarse three-level band, never the raw score.</summary>
    private static string BuildReadingSummary(OceanProfile profile) => string.Join(" · ",
    [
        $"Openness {profile.Openness.Band}",
        $"Conscientiousness {profile.Conscientiousness.Band}",
        $"Extraversion {profile.Extraversion.Band}",
        $"Agreeableness {profile.Agreeableness.Band}",
        $"Neuroticism {profile.Neuroticism.Band}",
    ]);
}
