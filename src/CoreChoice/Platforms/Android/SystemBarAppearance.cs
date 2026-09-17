using Android.App;
using Android.Graphics.Drawables;
using AndroidX.Core.View;
using Microsoft.Maui.Platform;

namespace CoreChoice.Platforms.Android;

/// <summary>
/// Keeps the system status/navigation bars following whichever of the six palettes
/// <see cref="CoreChoice.Services.ThemeService"/> currently has applied, rather than the black
/// band edge-to-edge enforcement (targetSdk 35+) otherwise leaves behind. The bars are made fully
/// transparent once, at startup, in <see cref="MainActivity.OnCreate"/>; what changes at runtime,
/// every time a palette or mode is applied, is only the window's own background colour (so the
/// area behind the transparent bars shows the app's current <c>Bg</c> token rather than whatever
/// colour happened to be there before) and the bar icons' light/dark appearance (so they stay
/// legible against it) — both handled here, from <see cref="Apply"/>.
/// </summary>
internal static class SystemBarAppearance
{
    /// <summary>Set once, from <see cref="MainActivity.OnCreate"/>, before
    /// <see cref="CoreChoice.Services.ThemeService.Apply"/> is first called.</summary>
    public static Activity? Activity { get; set; }

    /// <summary>
    /// <paramref name="background"/> is the current palette's <c>Bg</c> token, painted directly as
    /// the window's background so it shows through the transparent bars. Icons are set to render
    /// dark when <paramref name="lightBackground"/> is true (a light-mode palette) and light
    /// otherwise — the reverse of that flag's own naming, since "appearance light" means "icons
    /// suited to a light background", i.e. dark icons.
    /// </summary>
    public static void Apply(Microsoft.Maui.Graphics.Color background, bool lightBackground)
    {
        if (Activity?.Window is not { } window)
            return;

        window.SetBackgroundDrawable(new ColorDrawable(background.ToPlatform()));

        var controller = new WindowInsetsControllerCompat(window, window.DecorView);
        controller.AppearanceLightStatusBars = lightBackground;
        controller.AppearanceLightNavigationBars = lightBackground;
    }
}
