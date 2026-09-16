namespace CoreChoice.Presentation;

/// <summary>
/// Artboard 8 · "Analyses" — the coins/analyses store the answer screen's "Get more coins" links
/// to. Buying is fully implemented in <see cref="CoinsViewModel.BuyAsync"/> but disabled for now
/// (see that class's own doc): there is no backend endpoint yet to redeem a Play purchase, so
/// <see cref="CoinsViewModel.CanBuy"/> is false today regardless of the device. Each row's
/// <c>IsEnabled</c> binding already reflects that (blocking the gesture recognizer), and the
/// handlers below guard on it again defensively, since this page cannot be exercised on a device
/// from this machine.
/// </summary>
public partial class CoinsPage : ContentPage
{
    private readonly CoinsViewModel _viewModel;

    public CoinsPage(CoinsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private Task BuyAsync(string productId) =>
        _viewModel.CanBuy ? _viewModel.BuyAsync(productId) : Task.CompletedTask;

    private async void OnBuyTenTapped(object? sender, TappedEventArgs e) =>
        await BuyAsync(CoinsViewModel.TenAnalysesProductId);

    private async void OnBuyThirtyTapped(object? sender, TappedEventArgs e) =>
        await BuyAsync(CoinsViewModel.ThirtyAnalysesProductId);

    private async void OnBuyHundredTapped(object? sender, TappedEventArgs e) =>
        await BuyAsync(CoinsViewModel.HundredAnalysesProductId);
}
