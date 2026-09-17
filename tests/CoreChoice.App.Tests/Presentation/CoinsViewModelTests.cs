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

    // ===== CanRedeem =====

    [Theory]
    [InlineData("")]
    [InlineData("ABCD")]
    [InlineData("ABCDEF")]
    [InlineData("    ")]
    public void CanRedeem_WhenTheCodeIsNotFiveCharactersAfterTrimming_ShouldBeFalse(string code)
    {
        // Arrange
        var vm = new CoinsViewModel(new FakeBillingService(), StubLedger());

        // Act
        vm.PromoCode = code;

        // Assert
        vm.CanRedeem.Should().BeFalse();
    }

    [Fact]
    public void CanRedeem_WhenTheCodeIsExactlyFiveCharacters_ShouldBeTrue()
    {
        // Arrange
        var vm = new CoinsViewModel(new FakeBillingService(), StubLedger());

        // Act
        vm.PromoCode = "C779K";

        // Assert
        vm.CanRedeem.Should().BeTrue();
    }

    [Fact]
    public void CanRedeem_WhenTheCodeHasSurroundingWhitespaceButIsFiveCharactersTrimmed_ShouldBeTrue()
    {
        // Arrange
        var vm = new CoinsViewModel(new FakeBillingService(), StubLedger());

        // Act
        vm.PromoCode = "  C779K  ";

        // Assert
        vm.CanRedeem.Should().BeTrue();
    }

    // ===== RedeemPromoCodeAsync =====

    [Fact]
    public async Task RedeemPromoCodeAsync_WhenTheCodeIsNotFiveCharacters_ShouldNotCallTheLedgerAtAll()
    {
        // Arrange — the button itself is gated on CanRedeem, but the method guards the same way
        // regardless of caller, since the endpoint is rate-limited and every call counts.
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(5));
        var vm = new CoinsViewModel(new FakeBillingService(), ledger) { PromoCode = "AB" };

        // Act
        await vm.RedeemPromoCodeAsync();

        // Assert
        await ledger.DidNotReceive().RedeemPromoCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        vm.RedeemMessage.Should().BeNull();
    }

    [Fact]
    public async Task RedeemPromoCodeAsync_WhenRedeemed_ShouldUpdateTheBalanceClearTheFieldAndSayHowManyWereAdded()
    {
        // Arrange
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(25));
        ledger.RedeemPromoCodeAsync("C779K", Arg.Any<CancellationToken>())
            .Returns(PromoRedemptionResult.Redeemed(10, 35));
        var vm = new CoinsViewModel(new FakeBillingService(), ledger);
        await vm.LoadAsync();
        vm.PromoCode = "C779K";

        // Act
        await vm.RedeemPromoCodeAsync();

        // Assert
        vm.Balance.Should().Be(35);
        vm.PromoCode.Should().BeEmpty();
        vm.RedeemMessage.Should().Be("Added 10 analyses. Balance is now 35.");
    }

    [Fact]
    public async Task RedeemPromoCodeAsync_WhenExactlyOneAnalysisIsGranted_ShouldUseSingularWording()
    {
        // Arrange
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(0));
        ledger.RedeemPromoCodeAsync("SOLOO", Arg.Any<CancellationToken>())
            .Returns(PromoRedemptionResult.Redeemed(1, 1));
        var vm = new CoinsViewModel(new FakeBillingService(), ledger) { PromoCode = "SOLOO" };

        // Act
        await vm.RedeemPromoCodeAsync();

        // Assert
        vm.RedeemMessage.Should().Be("Added 1 analysis. Balance is now 1.");
    }

    [Fact]
    public async Task RedeemPromoCodeAsync_WhenAlreadyRedeemedByThisDevice_ShouldSayAsMuchAndNotChangeTheBalance()
    {
        // Arrange
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(12));
        ledger.RedeemPromoCodeAsync("LZQTS", Arg.Any<CancellationToken>())
            .Returns(PromoRedemptionResult.Failed(PromoRedemptionOutcome.AlreadyRedeemed));
        var vm = new CoinsViewModel(new FakeBillingService(), ledger);
        await vm.LoadAsync();
        vm.PromoCode = "LZQTS";

        // Act
        await vm.RedeemPromoCodeAsync();

        // Assert
        vm.RedeemMessage.Should().Be("That code has already been used on this device.");
        vm.Balance.Should().Be(12);
        vm.PromoCode.Should().Be("LZQTS", "a rejected code stays in the field so it can be corrected");
    }

    [Fact]
    public async Task RedeemPromoCodeAsync_WhenTheCodeIsUnknown_ShouldSaySo()
    {
        // Arrange
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(0));
        ledger.RedeemPromoCodeAsync("ZZZZZ", Arg.Any<CancellationToken>())
            .Returns(PromoRedemptionResult.Failed(PromoRedemptionOutcome.InvalidCode));
        var vm = new CoinsViewModel(new FakeBillingService(), ledger) { PromoCode = "ZZZZZ" };

        // Act
        await vm.RedeemPromoCodeAsync();

        // Assert
        vm.RedeemMessage.Should().Be("That code was not recognised.");
    }

    [Fact]
    public async Task PromoCode_WhenEditedAfterAFailedAttempt_ShouldClearTheOldMessage()
    {
        // Arrange — on the device, the answer to the PREVIOUS code stayed on screen while a new
        // one was being typed, which reads as if the new code had already been rejected.
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(0));
        ledger.RedeemPromoCodeAsync("ZZZZZ", Arg.Any<CancellationToken>())
            .Returns(PromoRedemptionResult.Failed(PromoRedemptionOutcome.InvalidCode));
        var vm = new CoinsViewModel(new FakeBillingService(), ledger) { PromoCode = "ZZZZZ" };
        await vm.RedeemPromoCodeAsync();
        vm.RedeemMessage.Should().NotBeNull();

        // Act
        vm.PromoCode = "ZZZZY";

        // Assert
        vm.RedeemMessage.Should().BeNull();
    }

    [Fact]
    public async Task PromoCode_WhenClearedByASuccessfulRedemption_ShouldKeepTheSuccessMessage()
    {
        // Arrange — the success path clears the field itself, and that self-inflicted change must
        // not wipe the sentence that just reported the success.
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(5));
        ledger.RedeemPromoCodeAsync("C779K", Arg.Any<CancellationToken>())
            .Returns(PromoRedemptionResult.Redeemed(coinsGranted: 5, balance: 10));
        var vm = new CoinsViewModel(new FakeBillingService(), ledger) { PromoCode = "C779K" };

        // Act
        await vm.RedeemPromoCodeAsync();

        // Assert
        vm.PromoCode.Should().BeEmpty();
        vm.RedeemMessage.Should().Be("Added 5 analyses. Balance is now 10.");
    }

    [Fact]
    public async Task RedeemPromoCodeAsync_WhenTheCodeIsRevoked_ShouldSaySo()
    {
        // Arrange
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(0));
        ledger.RedeemPromoCodeAsync("REVOK", Arg.Any<CancellationToken>())
            .Returns(PromoRedemptionResult.Failed(PromoRedemptionOutcome.RevokedCode));
        var vm = new CoinsViewModel(new FakeBillingService(), ledger) { PromoCode = "REVOK" };

        // Act
        await vm.RedeemPromoCodeAsync();

        // Assert
        vm.RedeemMessage.Should().Be("That code has been revoked and can no longer be used.");
    }

    [Fact]
    public async Task RedeemPromoCodeAsync_WhenTheCodeHasExpired_ShouldSaySo()
    {
        // Arrange
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(0));
        ledger.RedeemPromoCodeAsync("OLDCD", Arg.Any<CancellationToken>())
            .Returns(PromoRedemptionResult.Failed(PromoRedemptionOutcome.ExpiredCode));
        var vm = new CoinsViewModel(new FakeBillingService(), ledger) { PromoCode = "OLDCD" };

        // Act
        await vm.RedeemPromoCodeAsync();

        // Assert
        vm.RedeemMessage.Should().Be("That code has expired.");
    }

    [Fact]
    public async Task RedeemPromoCodeAsync_WhenTheServerReportsAMalformedCode_ShouldShowTheServersOwnMessage()
    {
        // Arrange
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(0));
        ledger.RedeemPromoCodeAsync("ABCDE", Arg.Any<CancellationToken>())
            .Returns(PromoRedemptionResult.Failed(
                PromoRedemptionOutcome.Malformed, "A code must be 5 characters."));
        var vm = new CoinsViewModel(new FakeBillingService(), ledger) { PromoCode = "ABCDE" };

        // Act
        await vm.RedeemPromoCodeAsync();

        // Assert
        vm.RedeemMessage.Should().Be("A code must be 5 characters.");
    }

    [Fact]
    public async Task RedeemPromoCodeAsync_WhenTheLedgerThrows_ShouldShowAGenericMessageAndNotChangeTheBalance()
    {
        // Arrange — a genuine transport failure (unreachable, timeout, rate-limited), not one of
        // the server's documented outcomes.
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(8));
        ledger.RedeemPromoCodeAsync("C779K", Arg.Any<CancellationToken>())
            .Returns<PromoRedemptionResult>(_ => throw new HttpRequestException("no signal"));
        var vm = new CoinsViewModel(new FakeBillingService(), ledger);
        await vm.LoadAsync();
        vm.PromoCode = "C779K";

        // Act
        await vm.RedeemPromoCodeAsync();

        // Assert
        vm.Balance.Should().Be(8);
        vm.RedeemMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task RedeemPromoCodeAsync_WhileInFlight_ShouldMakeCanRedeemFalse()
    {
        // Arrange — the endpoint is rate-limited (10/minute/IP), so the button must go inert for
        // the duration of the call rather than let a second tap queue up another request.
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(0));
        var gate = new TaskCompletionSource<PromoRedemptionResult>();
        ledger.RedeemPromoCodeAsync("C779K", Arg.Any<CancellationToken>()).Returns(gate.Task);
        var vm = new CoinsViewModel(new FakeBillingService(), ledger) { PromoCode = "C779K" };

        // Act
        var redeeming = vm.RedeemPromoCodeAsync();
        var canRedeemWhileInFlight = vm.CanRedeem;
        gate.SetResult(PromoRedemptionResult.Redeemed(10, 10));
        await redeeming;

        // Assert
        canRedeemWhileInFlight.Should().BeFalse();
        vm.IsRedeeming.Should().BeFalse("the flag must be cleared once the call completes");
    }

    [Fact]
    public async Task RedeemPromoCodeAsync_ShouldClearAnyMessageLeftOverFromAPreviousAttempt()
    {
        // Arrange
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(new CoinBalance(0));
        ledger.RedeemPromoCodeAsync("ZZZZZ", Arg.Any<CancellationToken>())
            .Returns(PromoRedemptionResult.Failed(PromoRedemptionOutcome.InvalidCode));
        var vm = new CoinsViewModel(new FakeBillingService(), ledger) { PromoCode = "ZZZZZ" };
        await vm.RedeemPromoCodeAsync();
        vm.RedeemMessage.Should().NotBeNull();

        // Act
        ledger.RedeemPromoCodeAsync("C779K", Arg.Any<CancellationToken>())
            .Returns(PromoRedemptionResult.Redeemed(10, 10));
        vm.PromoCode = "C779K";
        await vm.RedeemPromoCodeAsync();

        // Assert
        vm.RedeemMessage.Should().Be("Added 10 analyses. Balance is now 10.");
    }
}
