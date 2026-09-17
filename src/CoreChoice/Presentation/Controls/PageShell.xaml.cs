namespace CoreChoice.Presentation.Controls;

/// <summary>
/// The shared page shell. Every one of the eight top-level pages used to repeat the same
/// ScrollView + VerticalStackLayout + Padding shell, each faithfully matching its own artboard —
/// and the artboards disagreed with each other on the one number that matters most for a
/// consistent first impression: how far the heading sits from the top of the screen. This control
/// makes that a single decision (see <see cref="TopPadding"/>'s default) instead of eight.
///
/// A ContentView rather than a ControlTemplate on ContentPage: several pages (<see
/// cref="TestPage"/>'s progress header, most pages' pinned footer button) need a region that sits
/// outside the scrollable area, and a single ContentPresenter — which is all TemplatedPage's
/// ControlTemplate gives a page's Content — cannot be split into a fixed header, a scrolling
/// middle and a fixed footer without page-specific code. Three named slots on a plain ContentView
/// do that directly, stay visible in every page's own XAML (a reader sees the three regions right
/// there, not in a separate template resource), and still make the padding/scroll/background a
/// single implementation to change.
/// </summary>
public partial class PageShell : ContentView
{
    public static readonly BindableProperty HeaderProperty =
        BindableProperty.Create(nameof(Header), typeof(View), typeof(PageShell), propertyChanged: OnHeaderChanged);

    public static readonly BindableProperty MainContentProperty =
        BindableProperty.Create(nameof(MainContent), typeof(View), typeof(PageShell), propertyChanged: OnMainContentChanged);

    public static readonly BindableProperty FooterProperty =
        BindableProperty.Create(nameof(Footer), typeof(View), typeof(PageShell), propertyChanged: OnFooterChanged);

    /// <summary>Top padding in device-independent pixels. Defaults to 60 — the value four of the
    /// eight artboards already used and the modal value across all eight. Only override this with
    /// an explicit, commented reason on the page that sets it; an uncommented override is exactly
    /// the silent-number problem this control exists to remove.</summary>
    public static readonly BindableProperty TopPaddingProperty =
        BindableProperty.Create(nameof(TopPadding), typeof(double), typeof(PageShell), 60d, propertyChanged: OnTopPaddingChanged);

    public PageShell()
    {
        InitializeComponent();
        UpdatePadding();
    }

    public View? Header
    {
        get => (View?)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public View? MainContent
    {
        get => (View?)GetValue(MainContentProperty);
        set => SetValue(MainContentProperty, value);
    }

    public View? Footer
    {
        get => (View?)GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public double TopPadding
    {
        get => (double)GetValue(TopPaddingProperty);
        set => SetValue(TopPaddingProperty, value);
    }

    private static void OnHeaderChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((PageShell)bindable).HeaderHost.Content = (View?)newValue;

    private static void OnMainContentChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((PageShell)bindable).MainHost.Content = (View?)newValue;

    private static void OnFooterChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((PageShell)bindable).FooterHost.Content = (View?)newValue;

    private static void OnTopPaddingChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((PageShell)bindable).UpdatePadding();

    private void UpdatePadding() => RootGrid.Padding = new Thickness(24, TopPadding, 24, 24);
}
