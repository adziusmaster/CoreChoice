namespace CoreChoice.Presentation.Controls;

/// <summary>
/// Reusable in-app confirmation overlay — the first consumer is <see cref="ProfilePage"/>'s
/// retake confirmation, but any page can add one instance of this to its own root Grid (as a
/// sibling placed after its <see cref="PageShell"/>, so it paints on top) and drive it through
/// <see cref="ShowAsync"/>.
/// </summary>
public partial class ConfirmationDialog : ContentView
{
    private TaskCompletionSource<bool>? _pending;

    public ConfirmationDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the dialog and waits for a decision. Returns <c>true</c> only if
    /// <paramref name="destructiveText"/>'s button was tapped; the scrim, and
    /// <paramref name="safeText"/>'s button, both resolve to <c>false</c>.
    /// </summary>
    public Task<bool> ShowAsync(string title, string message, string safeText, string destructiveText)
    {
        // A dialog already open resolves to "not confirmed" before a second one starts, rather
        // than leaving the first caller's Task pending forever.
        _pending?.TrySetResult(false);

        TitleLabel.Text = title;
        MessageLabel.Text = message;
        SafeButton.Text = safeText;
        DestructiveButton.Text = destructiveText;

        var tcs = new TaskCompletionSource<bool>();
        _pending = tcs;
        IsVisible = true;
        return tcs.Task;
    }

    private void OnSafeClicked(object? sender, EventArgs e) => Resolve(false);

    private void OnDestructiveClicked(object? sender, EventArgs e) => Resolve(true);

    private void OnScrimTapped(object? sender, EventArgs e) => Resolve(false);

    private void Resolve(bool confirmed)
    {
        IsVisible = false;
        var tcs = _pending;
        _pending = null;
        tcs?.TrySetResult(confirmed);
    }
}
