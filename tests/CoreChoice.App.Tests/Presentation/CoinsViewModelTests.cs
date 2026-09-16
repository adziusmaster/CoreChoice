using CoreChoice.Application;
using CoreChoice.Presentation;
using FluentAssertions;
using NSubstitute;

namespace CoreChoice.App.Tests.Presentation;

public class CoinsViewModelTests
{
    private static ICoinLedgerClient StubLedger(int balance = 0) =>
        LedgerReturningBalance(balance);

    private static ICoinLedgerClient LedgerReturningBalance(int balance)
    {
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(balance));
        return ledger;
    }

    // ===== LoadAsync: prices =====

    [Fact]
    public async Task LoadAsync_WhenTheStoreReturnsPacks_ShouldExposeThemAndSetPricesAreFromStoreTrue()
    {
        // Arrange
        var storePacks = new AnalysisPack[]
        {
            new("corechoice.analyses.10", 10, "€2.99"),
            new("corechoice.analyses.30", 30, "€5.99"),
            new("corechoice.analyses.100", 100, "€14.99"),
        };
        var billing = new FakeBillingService { StorePacks = storePacks };
        var vm = new CoinsViewModel(billing, StubLedger());

        // Act
        await vm.LoadAsync();

        // Assert
        vm.Packs.Should().BeEquivalentTo(storePacks, o => o.WithStrictOrdering());
        vm.PricesAreFromStore.Should().BeTrue();
        vm.ShowsPlaceholderPrices.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WhenTheBillingPortThrows_ShouldFallBackToFallbackPacksAndSetPricesAreFromStoreFalse()
    {
        // Arrange
        var billing = new FakeBillingService { GetPacksException = new InvalidOperationException("offline") };
        var vm = new CoinsViewModel(billing, StubLedger());

        // Act
        await vm.LoadAsync();

        // Assert
        vm.Packs.Should().BeEquivalentTo(billing.FallbackPacks, o => o.WithStrictOrdering());
        vm.PricesAreFromStore.Should().BeFalse();
        vm.ShowsPlaceholderPrices.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_ShouldAlsoExposeTheBalanceFromTheLedger()
    {
        // Arrange
        var billing = new FakeBillingService();
        var vm = new CoinsViewModel(billing, LedgerReturningBalance(7));

        // Act
        await vm.LoadAsync();

        // Assert
        vm.Balance.Should().Be(7);
    }

    [Fact]
    public async Task LoadAsync_WhenTheLedgerThrows_ShouldStillLoadPacksAndLeaveBalanceAtZero()
    {
        // Arrange — the two calls are independent: a dead ledger says nothing about whether
        // Play billing itself works.
        var billing = new FakeBillingService();
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>())
            .Returns<CoinBalance>(_ => throw new HttpRequestException("no signal"));
        var vm = new CoinsViewModel(billing, ledger);

        // Act
        await vm.LoadAsync();

        // Assert
        vm.Balance.Should().Be(0);
        vm.Packs.Should().NotBeEmpty();
    }

    // ===== CanBuy =====

    [Fact]
    public async Task CanBuy_WhenBillingIsNotSupported_ShouldBeFalse()
    {
        // Arrange
        var billing = new FakeBillingService { IsSupported = false };
        var vm = new CoinsViewModel(billing, StubLedger());
        await vm.LoadAsync();

        // Assert
        vm.CanBuy.Should().BeFalse();
    }

    [Fact]
    public async Task CanBuy_WhenBillingIsSupported_ShouldStillBeFalseBecausePurchasingIsNotLiveYet()
    {
        // Arrange — there is no POST /api/billing/redeem on the server yet, so buying stays
        // disabled even on a device that fully supports Play billing.
        var billing = new FakeBillingService { IsSupported = true };
        var vm = new CoinsViewModel(billing, StubLedger());
        await vm.LoadAsync();

        // Assert
        vm.CanBuy.Should().BeFalse();
    }

    // ===== BuyAsync: cancellation =====

    [Fact]
    public async Task BuyAsync_WhenThePersonCancelsTheBillingFlow_ShouldLeaveBalanceUntouchedAndShowNoMessage()
    {
        // Arrange
        var billing = new FakeBillingService { BuyResult = null };
        var ledger = LedgerReturningBalance(5);
        var vm = new CoinsViewModel(billing, ledger);
        await vm.LoadAsync();

        // Act
        await vm.BuyAsync("corechoice.analyses.10");

        // Assert
        vm.Balance.Should().Be(5);
        vm.Message.Should().BeNull();
        billing.ConsumedTokens.Should().BeEmpty();
        await ledger.DidNotReceive().RedeemPurchaseAsync(Arg.Any<PurchaseTicket>(), Arg.Any<CancellationToken>());
    }

    // ===== BuyAsync: the purchase order =====

    [Fact]
    public async Task BuyAsync_WhenRedemptionFails_ShouldNeverConsumeThePurchase()
    {
        // Arrange — the important one: a purchase must never be consumed unless the backend has
        // actually granted the coins it paid for. Consuming first would mean a person pays and
        // gets nothing if the network drops in between, and the purchase would be gone for good,
        // since a consumed product cannot be re-redeemed.
        var ticket = new PurchaseTicket("corechoice.analyses.10", "token-123");
        var billing = new FakeBillingService { BuyResult = ticket };
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(3));
        ledger.RedeemPurchaseAsync(Arg.Any<PurchaseTicket>(), Arg.Any<CancellationToken>())
            .Returns<GrantResult>(_ => throw new HttpRequestException("no route (endpoint not built yet)"));
        var vm = new CoinsViewModel(billing, ledger);
        await vm.LoadAsync();

        // Act
        await vm.BuyAsync("corechoice.analyses.10");

        // Assert
        billing.ConsumedTokens.Should().BeEmpty("the ticket must stay un-consumed when redemption fails");
        vm.Balance.Should().Be(3, "no grant happened, so the balance must not change");
        vm.Message.Should().NotBeNull();
    }

    [Fact]
    public async Task BuyAsync_WhenRedemptionRefusesTheGrant_ShouldShowTheReasonAndNeverConsume()
    {
        // Arrange — a redemption call that completes but reports Granted=false (e.g. the token was
        // already redeemed) is just as much "not a grant" as a thrown exception.
        var ticket = new PurchaseTicket("corechoice.analyses.10", "token-456");
        var billing = new FakeBillingService { BuyResult = ticket };
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(2));
        ledger.RedeemPurchaseAsync(Arg.Any<PurchaseTicket>(), Arg.Any<CancellationToken>())
            .Returns(new GrantResult(false, 2, "That purchase was already redeemed."));
        var vm = new CoinsViewModel(billing, ledger);
        await vm.LoadAsync();

        // Act
        await vm.BuyAsync("corechoice.analyses.10");

        // Assert
        billing.ConsumedTokens.Should().BeEmpty();
        vm.Message.Should().Be("That purchase was already redeemed.");
        vm.Balance.Should().Be(2);
    }

    [Fact]
    public async Task BuyAsync_WhenTheBillingFlowFailsToStart_ShouldShowAMessageAndNeverAttemptRedemption()
    {
        // Arrange
        var billing = new FakeBillingService { BuyException = new InvalidOperationException("no foreground activity") };
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(1));
        var vm = new CoinsViewModel(billing, ledger);
        await vm.LoadAsync();

        // Act
        await vm.BuyAsync("corechoice.analyses.10");

        // Assert
        vm.Message.Should().NotBeNull();
        billing.ConsumedTokens.Should().BeEmpty();
        await ledger.DidNotReceive().RedeemPurchaseAsync(Arg.Any<PurchaseTicket>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuyAsync_WhenRedemptionSucceeds_ShouldConsumeThePurchaseAndUpdateTheBalance()
    {
        // Arrange — the full, correct order: buy, redeem, THEN consume.
        var ticket = new PurchaseTicket("corechoice.analyses.30", "token-789");
        var billing = new FakeBillingService { BuyResult = ticket };
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(0));
        ledger.RedeemPurchaseAsync(Arg.Any<PurchaseTicket>(), Arg.Any<CancellationToken>())
            .Returns(new GrantResult(true, 30, null));
        var vm = new CoinsViewModel(billing, ledger);
        await vm.LoadAsync();

        // Act
        await vm.BuyAsync("corechoice.analyses.30");

        // Assert
        billing.ConsumedTokens.Should().ContainSingle().Which.Should().Be("token-789");
        vm.Balance.Should().Be(30);
        vm.Message.Should().BeNull();
        await ledger.Received(1).RedeemPurchaseAsync(
            Arg.Is<PurchaseTicket>(t => t == ticket), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuyAsync_WhenTheCallerTokenIsCancelledRightAfterRedemptionSucceeds_ShouldStillConsumeThePurchase()
    {
        // Arrange — the exact race BuyAsync's CancellationToken.None guards against: the coins are
        // already granted, and then the caller's own token is cancelled a moment before the final
        // ConsumeAsync call. Consuming must still happen, unconditionally, or the person ends up
        // holding granted coins on an un-consumed purchase that then blocks a future repurchase
        // with ITEM_ALREADY_OWNED. FakeBillingService.ConsumeAsync throws if handed an already-
        // cancelled token, so this test fails outright if CoinsViewModel ever passes `ct` here
        // instead of CancellationToken.None.
        var ticket = new PurchaseTicket("corechoice.analyses.10", "token-race");
        var billing = new FakeBillingService { BuyResult = ticket };
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(0));
        using var cts = new CancellationTokenSource();
        ledger.RedeemPurchaseAsync(Arg.Any<PurchaseTicket>(), Arg.Any<CancellationToken>())
            .Returns<GrantResult>(_ =>
            {
                cts.Cancel();
                return new GrantResult(true, 10, null);
            });
        var vm = new CoinsViewModel(billing, ledger);
        await vm.LoadAsync();

        // Act
        await vm.BuyAsync("corechoice.analyses.10", cts.Token);

        // Assert
        billing.ConsumedTokens.Should().ContainSingle().Which.Should().Be("token-race");
        vm.Balance.Should().Be(10);
    }

    [Fact]
    public async Task BuyAsync_ShouldClearAnyMessageLeftOverFromAPreviousAttempt()
    {
        // Arrange
        var billing = new FakeBillingService { BuyException = new InvalidOperationException("first failure") };
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(0));
        var vm = new CoinsViewModel(billing, ledger);
        await vm.LoadAsync();
        await vm.BuyAsync("corechoice.analyses.10");
        vm.Message.Should().NotBeNull();

        // Act — the person cancels a second attempt outright.
        billing.BuyException = null;
        billing.BuyResult = null;
        await vm.BuyAsync("corechoice.analyses.10");

        // Assert
        vm.Message.Should().BeNull();
    }
}
