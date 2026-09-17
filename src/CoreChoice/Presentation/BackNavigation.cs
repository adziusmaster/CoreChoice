namespace CoreChoice.Presentation;

/// <summary>What the hardware back button should do next.</summary>
public enum BackNavigationAction
{
    /// <summary>Pop the current tab's own pushed-page stack — there is something on top of it.</summary>
    PopStack,

    /// <summary>Nothing to pop, and this is not the primary tab: switch to the primary (Ask) tab
    /// rather than exit, so a bottom-tab app is never left with no way out.</summary>
    GoToPrimaryTab,

    /// <summary>Nothing to pop, and this already is the primary tab's own root: let the platform
    /// handle it — standard Android behaviour is to exit.</summary>
    ExitApp,
}

/// <summary>
/// The pure decision behind <see cref="CoreChoice.AppShell"/>'s hardware-back-button override.
/// Kept free of any Microsoft.Maui.* type so it can be unit tested directly; AppShell is only the
/// thin adapter that reads Shell's current navigation state (can the current tab's stack pop, is
/// the current tab the primary one) and calls <see cref="Decide"/>.
/// </summary>
public static class BackNavigation
{
    public static BackNavigationAction Decide(bool canPopCurrentStack, bool isOnPrimaryTab)
    {
        if (canPopCurrentStack)
            return BackNavigationAction.PopStack;

        return isOnPrimaryTab ? BackNavigationAction.ExitApp : BackNavigationAction.GoToPrimaryTab;
    }
}
