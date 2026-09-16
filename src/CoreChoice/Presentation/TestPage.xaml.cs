using System.ComponentModel;
using CoreChoice.Domain;

namespace CoreChoice.Presentation;

/// <summary>
/// The fifty-item test, three statements at a time ("The 50 items" in the design). Every tap
/// persists immediately through <see cref="TestViewModel.AnswerAsync"/>; this page never batches
/// answers, so nothing is lost if the person is interrupted mid-page.
/// </summary>
public partial class TestPage : ContentPage
{
    private readonly TestViewModel _viewModel;

    public TestPage(TestViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
        UpdateProgress();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TestViewModel.AnsweredCount))
            UpdateProgress();
    }

    /// <summary>
    /// Sizes the two-column progress track by star-weighting its columns with the answered and
    /// remaining counts, so the accent segment always covers exactly AnsweredCount/TotalCount of
    /// the full width regardless of device size — the XAML equivalent of the artboard's
    /// percentage width, without hard-coding a pixel value that would only be right at 390px.
    /// </summary>
    private void UpdateProgress()
    {
        var answered = _viewModel.AnsweredCount;
        var total = _viewModel.TotalCount;

        FillColumn.Width = new GridLength(answered, GridUnitType.Star);
        RestColumn.Width = new GridLength(Math.Max(total - answered, 0.0001), GridUnitType.Star);
    }

    private async void OnLikertClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: TestItem item } button) return;
        if (!int.TryParse((string?)button.CommandParameter, out var response)) return;

        await _viewModel.AnswerAsync(item.Number, response);
    }

    private async void OnAdvanceClicked(object? sender, EventArgs e)
    {
        var complete = await _viewModel.AdvanceAsync();

        // Never reach ProfilePage speculatively: only once every one of the fifty items carries
        // an answer does AdvanceAsync return true, which is ProfileViewModel.LoadAsync's
        // documented precondition (it throws IncompleteProfileException otherwise).
        if (complete)
            await Shell.Current.GoToAsync(nameof(ProfilePage));
    }

    private async void OnPauseTapped(object? sender, EventArgs e)
    {
        // Nothing to save here: every answer already persisted the moment it was tapped, so
        // leaving is simply a navigation, not a checkpoint.
        await Shell.Current.GoToAsync("..");
    }
}
