using CoreChoice.Application;
using CoreChoice.Domain;
using FluentAssertions;

namespace CoreChoice.App.Tests.Application;

public class PortContractTests
{
    [Fact]
    public void AnalysisPack_ShouldCarryAStorePriceString()
    {
        // Arrange — the display price is a string, not a decimal, because Play returns a
        // formatted, localised, tax-inclusive label and the app must show exactly that.
        var pack = new AnalysisPack("corechoice.analyses.30", 30, "€5,99");

        // Act
        var price = pack.DisplayPrice;

        // Assert
        price.Should().Be("€5,99");
        pack.Analyses.Should().Be(30);
    }

    [Fact]
    public void GrantResult_WhenRefused_ShouldStillCarryTheCurrentBalance()
    {
        // Arrange — a refused grant must never look like an error to the UI; the person
        // still needs to be told what they have.
        var outcome = new GrantResult(Granted: false, Balance: 10, Reason: "already-granted");

        // Act & Assert
        outcome.Granted.Should().BeFalse();
        outcome.Balance.Should().Be(10);
        outcome.Reason.Should().Be("already-granted");
    }

    [Fact]
    public void StoredAnswers_ShouldExposeResponsesByItemNumber()
    {
        // Arrange — answers are keyed by IPIP item number, which is a storage contract:
        // renumbering the bank silently re-keys every saved profile.
        var answers = new StoredAnswers(new Dictionary<int, int> { [1] = 4, [17] = 2 }, DateTimeOffset.UtcNow);

        // Act
        var seventeen = answers.Responses[17];

        // Assert
        seventeen.Should().Be(2);
        answers.Responses.Should().HaveCount(2);
    }
}
