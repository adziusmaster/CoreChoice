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
    }

    private sealed class SystemBarsBottomInsetListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(global::Android.Views.View? v, WindowInsetsCompat? insets)
        {
            if (v is null || insets is null)
                return insets!;

            var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars());
            v.SetPadding(v.PaddingLeft, v.PaddingTop, v.PaddingRight, bars?.Bottom ?? 0);
            return insets;
        }
    }
}
