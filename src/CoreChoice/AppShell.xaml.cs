using CoreChoice.Application;
using CoreChoice.Presentation;

namespace CoreChoice;

/// <summary>
/// Wires the personality-test flow (intro to <see cref="TestPage"/> to <see cref="ProfilePage"/>),
/// the ask-a-question flow (<see cref="DilemmaPage"/> to <see cref="PersonaPage"/> and back), the
/// analyses/coins store (<see cref="CoinsPage"/>), reached from <see cref="AnalysisPage"/>, and
/// appearance settings (<see cref="SettingsPage"/>), reached from <see cref="DilemmaPage"/>.
/// The root content is constructed here, not declared as a ShellContent DataTemplate in XAML,
/// so <see cref="TestIntroPage"/> is guaranteed to come from the composition root's DI container
/// (and so get its <see cref="TestIntroViewModel"/> by constructor injection) rather than from an
/// ambiguous Activator.CreateInstance path. The two pages pushed onto the stack afterwards go
/// through Shell's own route registration, which is documented to resolve via DI when the type is
/// registered in the service container.
/// </summary>
public partial class AppShell : Shell
{
    public AppShell(TestIntroPage introPage, IProfileRepository profiles)
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(TestPage), typeof(TestPage));
        Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
        Routing.RegisterRoute(nameof(DilemmaPage), typeof(DilemmaPage));
        Routing.RegisterRoute(nameof(PersonaPage), typeof(PersonaPage));
        Routing.RegisterRoute(nameof(AnalysisPage), typeof(AnalysisPage));
        Routing.RegisterRoute(nameof(CoinsPage), typeof(CoinsPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));

        Items.Add(new ShellContent
        {
            Route = nameof(TestIntroPage),
            Content = introPage,
        });

        // Routing on launch: a first-time person lands on TestIntroPage (already the root above);
        // a returning person who already has a scored profile is taken straight to the dilemma
        // screen instead, since walking them back through "who are you when you have to decide?"
        // a second time has nothing left to offer them. The test is still never a gate for anyone
        // else — TestIntroPage's own "Not now, ask a question first" link covers the unprofiled
        // path — this only shortens the *returning* path.
        //
        // Fire-and-forget from the constructor is deliberate and safe: LoadProfileAsync failing
        // for any reason (freshest install, a locked file, a transient error) simply leaves
        // TestIntroPage showing, which is exactly first-launch behaviour anyway. "//TestIntroPage/…"
        // rather than a bare "//DilemmaPage" is used because DilemmaPage is only a global route
        // (Routing.RegisterRoute above), not a ShellContent of its own — Shell resolves an
        // absolute route's unmatched trailing segments by pushing them onto the matched
        // ShellContent's stack, so this lands on DilemmaPage with TestIntroPage one "back" behind
        // it, the same stack shape every other screen in this app already pushes onto.
        _ = RouteReturningPersonToDilemmaAsync(profiles);
    }

    private async Task RouteReturningPersonToDilemmaAsync(IProfileRepository profiles)
    {
        try
        {
            var profile = await profiles.LoadProfileAsync();
            if (profile.IsPresent)
                await GoToAsync($"//{nameof(TestIntroPage)}/{nameof(DilemmaPage)}");
        }
        catch (Exception)
        {
            // No stored profile yet (or the database could not be reached): TestIntroPage, already
            // showing, is the correct screen for a first launch.
        }
    }
}
