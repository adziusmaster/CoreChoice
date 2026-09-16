using CoreChoice.Presentation;

namespace CoreChoice;

/// <summary>
/// Wires the personality-test flow (intro to <see cref="TestPage"/> to <see cref="ProfilePage"/>)
/// and the ask-a-question flow (<see cref="DilemmaPage"/> to <see cref="PersonaPage"/> and back).
/// The root content is constructed here, not declared as a ShellContent DataTemplate in XAML,
/// so <see cref="TestIntroPage"/> is guaranteed to come from the composition root's DI container
/// (and so get its <see cref="TestIntroViewModel"/> by constructor injection) rather than from an
/// ambiguous Activator.CreateInstance path. The two pages pushed onto the stack afterwards go
/// through Shell's own route registration, which is documented to resolve via DI when the type is
/// registered in the service container.
/// </summary>
public partial class AppShell : Shell
{
    public AppShell(TestIntroPage introPage)
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(TestPage), typeof(TestPage));
        Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
        Routing.RegisterRoute(nameof(DilemmaPage), typeof(DilemmaPage));
        Routing.RegisterRoute(nameof(PersonaPage), typeof(PersonaPage));

        Items.Add(new ShellContent
        {
            Route = nameof(TestIntroPage),
            Content = introPage,
        });
    }
}
