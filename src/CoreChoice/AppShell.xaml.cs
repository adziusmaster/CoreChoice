using CoreChoice.Application;
using CoreChoice.Presentation;

namespace CoreChoice;

/// <summary>
/// The bottom tab bar: <b>Ask</b> (<see cref="DilemmaPage"/>, the primary/default tab),
/// <b>Profile</b> (<see cref="TestIntroPage"/>, which auto-navigates on to
/// <see cref="ProfilePage"/> once it is safe to — see that page's own doc comment),
/// <b>Analyses</b> (<see cref="CoinsPage"/>) and <b>Settings</b> (<see cref="SettingsPage"/>).
/// The four tab roots are constructed here from the four injected instances, not declared as
/// ShellContent DataTemplates in XAML, so each is guaranteed to come from the composition root's
/// DI container (and so get its view model by constructor injection) rather than an ambiguous
/// Activator.CreateInstance path — the same reasoning this class used for its single root page
/// before tabs existed. <see cref="TestPage"/>, <see cref="ProfilePage"/>, <see cref="PersonaPage"/>
/// and <see cref="AnalysisPage"/> stay ordinary routes, pushed onto whichever tab's own stack sent
/// the person there, never tabs of their own.
/// </summary>
public partial class AppShell : Shell
{
    public AppShell(
        TestIntroPage introPage,
        DilemmaPage dilemmaPage,
        CoinsPage coinsPage,
        SettingsPage settingsPage,
        ICoinLedgerClient coinLedger)
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(TestPage), typeof(TestPage));
        Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
        Routing.RegisterRoute(nameof(PersonaPage), typeof(PersonaPage));
        Routing.RegisterRoute(nameof(AnalysisPage), typeof(AnalysisPage));

        var tabs = new TabBar();
        tabs.Items.Add(new ShellContent { Title = "Ask", Route = nameof(DilemmaPage), Content = dilemmaPage });
        tabs.Items.Add(new ShellContent { Title = "Profile", Route = nameof(TestIntroPage), Content = introPage });
        tabs.Items.Add(new ShellContent { Title = "Analyses", Route = nameof(CoinsPage), Content = coinsPage });
        tabs.Items.Add(new ShellContent { Title = "Settings", Route = nameof(SettingsPage), Content = settingsPage });
        Items.Add(tabs);

        // First contact: seeds the free coins. Fire-and-forget from the constructor, the same
        // shape this class previously used for its returning-person routing (now superseded by
        // Ask simply being the default tab): never blocks first render, and a dead network or an
        // unreachable backend simply leaves the balance wherever it already was — invisible and
        // harmless, never a crash or a hang. Safe to call on every launch because the server is
        // idempotent (see ICoinLedgerClient.EnsureSeededAsync's own doc comment).
        _ = SeedFirstContactCoinsAsync(coinLedger);
    }

    private static async Task SeedFirstContactCoinsAsync(ICoinLedgerClient coinLedger)
    {
        try
        {
            await coinLedger.EnsureSeededAsync();
        }
        catch (Exception)
        {
            // No signal, a dead server, a cold start with nothing reachable yet — none of it may
            // surface to the person. The balance simply stays whatever it last was.
        }
    }

    /// <summary>
    /// The hardware back button: pops the current tab's own pushed-page stack when there is
    /// something on it, otherwise returns to the primary Ask tab from any other tab's root, and
    /// only exits the app from the Ask tab's own root — standard Android behaviour, and the one
    /// case Shell's own default handling (which this defers to below) already gets right. The
    /// actual decision is <see cref="BackNavigation.Decide"/>, a MAUI-free pure function so it can
    /// be unit tested; this override is only the thin adapter reading Shell's live state.
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        var canPop = Navigation.NavigationStack.Count > 0;
        var isOnPrimaryTab = CurrentItem?.CurrentItem?.CurrentItem?.Route == nameof(DilemmaPage);

        if (BackNavigation.Decide(canPop, isOnPrimaryTab) == BackNavigationAction.GoToPrimaryTab)
        {
            _ = GoToAsync($"//{nameof(DilemmaPage)}");
            return true;
        }

        return base.OnBackButtonPressed();
    }
}
