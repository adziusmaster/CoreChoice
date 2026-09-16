namespace CoreChoice.Presentation;

/// <summary>
/// The "while it thinks" screen — artboard 6. Everything it shows comes from
/// <see cref="AnalysisViewModel"/>'s own local, offline state (<c>WaitingLine</c> and
/// <c>ReadingSummary</c>), so it renders correctly the instant a request is sent, before any
/// network reply exists.
///
/// The three dots breathe in a slow, staggered loop purely as an "it is still working" cue — never
/// a percentage or a countdown, which would promise a specific time this screen cannot keep. The
/// loop is cancelled on <see cref="OnUnloaded"/> so it cannot keep animating (and holding a
/// reference to this view) after the page has been navigated away from.
/// </summary>
public partial class LoadingView : ContentView
{
    private CancellationTokenSource? _breatheCts;

    public LoadingView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _breatheCts = new CancellationTokenSource();
        _ = BreatheAsync(Dot1, TimeSpan.Zero, _breatheCts.Token);
        _ = BreatheAsync(Dot2, TimeSpan.FromMilliseconds(450), _breatheCts.Token);
        _ = BreatheAsync(Dot3, TimeSpan.FromMilliseconds(900), _breatheCts.Token);
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        _breatheCts?.Cancel();
        _breatheCts?.Dispose();
        _breatheCts = null;
    }

    /// <summary>Fades one dot between two opacities on a loop, offset by <paramref name="delay"/>
    /// so the three dots read as one wave rather than blinking in unison — the artboard's own
    /// staggered <c>animation-delay</c>. Never throws on cancellation: this is fire-and-forget
    /// decoration, not a task anything awaits.</summary>
    private static async Task BreatheAsync(VisualElement dot, TimeSpan delay, CancellationToken ct)
    {
        try
        {
            dot.Opacity = 0.2;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);

            while (!ct.IsCancellationRequested)
            {
                await dot.FadeToAsync(0.85, 1300, Easing.SinInOut);
                if (ct.IsCancellationRequested) break;
                await dot.FadeToAsync(0.2, 1300, Easing.SinInOut);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on navigating away — nothing to clean up beyond stopping the loop.
        }
    }
}
