using CoreChoice.Presentation;
using FluentAssertions;

namespace CoreChoice.App.Tests.Presentation;

public class BackNavigationTests
{
    [Fact]
    public void Decide_WhenTheCurrentTabsStackCanPop_ShouldPopRegardlessOfWhichTabItIs()
    {
        // Arrange & Act
        var onPrimary = BackNavigation.Decide(canPopCurrentStack: true, isOnPrimaryTab: true);
        var onSecondary = BackNavigation.Decide(canPopCurrentStack: true, isOnPrimaryTab: false);

        // Assert — a pushed page always wins over which tab it happens to be pushed onto.
        onPrimary.Should().Be(BackNavigationAction.PopStack);
        onSecondary.Should().Be(BackNavigationAction.PopStack);
    }

    [Fact]
    public void Decide_AtTheRootOfANonPrimaryTabWithNothingToPop_ShouldReturnToThePrimaryTab()
    {
        // Arrange
        // Act
        var action = BackNavigation.Decide(canPopCurrentStack: false, isOnPrimaryTab: false);

        // Assert — Profile, Analyses and Settings never leave the person with no way out.
        action.Should().Be(BackNavigationAction.GoToPrimaryTab);
    }

    [Fact]
    public void Decide_AtTheRootOfThePrimaryTabWithNothingToPop_ShouldExitTheApp()
    {
        // Arrange
        // Act
        var action = BackNavigation.Decide(canPopCurrentStack: false, isOnPrimaryTab: true);

        // Assert — standard Android behaviour: back at the app's true home exits.
        action.Should().Be(BackNavigationAction.ExitApp);
    }
}
