using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace CoreChoice;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation
        | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Set before base.OnCreate(): that call is what constructs App and runs ThemeService's
        // first Apply(), which reaches into CoreChoice.Platforms.Android.SystemBarAppearance to
        // colour the system bars — it must already have an Activity to act on by then.
        CoreChoice.Platforms.Android.SystemBarAppearance.Activity = this;

        base.OnCreate(savedInstanceState);

        // targetSdk 35+ enforces edge-to-edge: the app draws under the system bars whether it asks
        // to or not, and the removed windowOptOutEdgeToEdgeEnforcement manifest flag is a temporary
        // escape hatch this app deliberately does not use (see AndroidManifest.xml's own note on
        // enableOnBackInvokedCallback for the parallel reasoning on the back button). Both bars are
        // made fully transparent — SystemBarAppearance then keeps the window's own background in
        // step with whichever palette is active, so the app's real background colour shows through
        // rather than a black band — and the automatic contrast scrim Android draws behind a
        // transparent bar is switched off, since without it "transparent" still renders as a
        // translucent black strip, which is the exact symptom being fixed.
        WindowCompat.SetDecorFitsSystemWindows(Window!, false);
        Window!.SetStatusBarColor(global::Android.Graphics.Color.Transparent);
        Window.SetNavigationBarColor(global::Android.Graphics.Color.Transparent);
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            Window.NavigationBarContrastEnforced = false;
            Window.StatusBarContrastEnforced = false;
        }

        // Pads the root content view — which holds both the page content and Shell's native
        // bottom tab bar — up by exactly the gesture/navigation bar's height, so the tab bar sits
        // above it instead of underneath it. Left deliberately to the bottom inset only: every
        // page already reserves generous, hand-tuned top padding for the status bar from before
        // edge-to-edge was enforced, and adding a second, dynamic top inset on top of that would
        // double-pad every screen rather than fix anything reported.
        var content = Window!.DecorView!.FindViewById(global::Android.Resource.Id.Content)!;
        ViewCompat.SetOnApplyWindowInsetsListener(content, new SystemBarsBottomInsetListener());

        // Shell's bottom tab bar is native chrome (a Material BottomNavigationView) with no XAML
        // surface — see TabBarLabelCentering's own doc for why its labels need a one-time,
        // measured nudge rather than a style fix, and why that is unrelated to the inset padding
        // above (it corrects where the label sits *inside* the bar, not the bar's height).
        CoreChoice.Platforms.Android.TabBarLabelCentering.Apply(content);
    }

    /// <summary>
    /// The first time <see cref="CoreChoice.Services.ThemeService.Apply"/> switches the app into
    /// dark mode (Settings default to a light-resolving choice, so this is the cold-start case for
    /// anyone who picks Dark, or the device's own dark mode for ThemeMode.System), setting
    /// <c>Application.UserAppTheme</c> flips this activity's own night-mode resources. Because
    /// MainActivity declares <see cref="ConfigChanges.UiMode"/> above, Android does not recreate
    /// the activity for that — instead AppCompatActivity's own onConfigurationChanged handling
    /// (invoked from inside <c>base.OnConfigurationChanged</c> below) re-resolves the theme against
    /// the new night qualifier and, as part of that, resets the window's background drawable back
    /// to the theme's own default windowBackground — Android's stock Material dark surface,
    /// <c>#121212</c> — even though <see cref="SystemBarAppearance.Apply"/> had already painted the
    /// real palette colour moments earlier in the same call stack. This is exactly the documented
    /// AppCompatDelegate caveat: an activity that opts out of recreation for uiMode changes must
    /// manually redo anything theme-dependent itself. Re-painting here, once that settles, is what
    /// makes the system navigation band show the palette's actual Bg in dark mode instead of that
    /// default.
    /// </summary>
    public override void OnConfigurationChanged(global::Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        CoreChoice.Platforms.Android.SystemBarAppearance.Reapply();
    }

    private sealed class SystemBarsBottomInsetListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(global::Android.Views.View? v, WindowInsetsCompat? insets)
        {
            if (v is null || insets is null)
                return insets!;

            // BOTH ends, not just the bottom. The app draws edge to edge, so without the top
            // inset the page's own padding is measured from the screen edge and the first heading
            // sits hard against the status bar. The bottom inset keeps the tab bar clear of the
            // navigation bar; consuming the result below stops Material's BottomNavigationView
            // applying that same bottom inset a second time to its own height.
            var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars());
            v.SetPadding(v.PaddingLeft, bars?.Top ?? 0, v.PaddingRight, bars?.Bottom ?? 0);

            // Consumed, not the original insets: this view already turned the bottom system-bar
            // inset into padding above, and Shell's native bottom tab bar (a Material
            // BottomNavigationView) applies that same bottom inset to itself automatically the
            // moment it sees one during dispatch — that's what inflated it to roughly natural
            // height plus a second full nav-bar inset. Returning WindowInsetsCompat.Consumed stops
            // the inset from reaching any descendant a second time, so the tab bar renders at its
            // own natural height instead of double-padded.
            return WindowInsetsCompat.Consumed;
        }
    }
}
