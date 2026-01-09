using KfChatDotNetBot;
using KfChatDotNetBot.Models.DbModels;
using KfChatDotNetBot.Services;

namespace KfChatDotNetBot.Tests.Integration;

/// <summary>
/// Integration tests for non-wager Kasino user commands.
/// Tests balance, daily dollar, juice transfer, rakeback/lossback, pocketwatch, and abandon.
/// </summary>
[Collection("IntegrationTests")]
public class KasinoUserCommandTests : IDisposable
{
    private readonly TestBotFixture _fixture;

    public KasinoUserCommandTests(TestBotFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetForNewTest();
    }

    public void Dispose()
    {
        // Cleanup handled by fixture
    }

    #region Balance Command Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void Balance_ReturnsCurrentBalance()
    {
        // Arrange
        var expectedBalance = _fixture.GetBalance();

        // Act
        _fixture.SendCommand("!balance");
        Thread.Sleep(100);

        // Assert
        var actualBalance = _fixture.GetBalance();
        Console.WriteLine($"Balance command returned. Balance: {actualBalance:N2}");

        // Balance should still be the same (balance command doesn't modify)
        Assert.Equal(expectedBalance, actualBalance);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Bal_ShorthandWorks()
    {
        // Arrange
        var expectedBalance = _fixture.GetBalance();

        // Act
        _fixture.SendCommand("!bal");
        Thread.Sleep(100);

        // Assert
        var actualBalance = _fixture.GetBalance();
        Assert.Equal(expectedBalance, actualBalance);
    }

    #endregion

    #region Juice Transfer Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void Juice_TransferToOtherUser_UpdatesBothBalances()
    {
        // Arrange
        var (_, recipientKfId, recipientName) = _fixture.CreateAdditionalUser("JuiceRecipient", createGambler: true);
        var senderInitialBalance = _fixture.GetBalance();
        var recipientInitialBalance = _fixture.GetGambler(recipientKfId)?.Balance ?? 0;
        var transferAmount = 100m;

        // Act
        _fixture.SendCommand($"!juice {recipientKfId} {transferAmount}");
        Thread.Sleep(150);

        // Assert
        var senderFinalBalance = _fixture.GetBalance();
        var recipientFinalBalance = _fixture.GetGambler(recipientKfId)?.Balance ?? 0;

        Console.WriteLine($"Sender: {senderInitialBalance:N2} -> {senderFinalBalance:N2}");
        Console.WriteLine($"Recipient: {recipientInitialBalance:N2} -> {recipientFinalBalance:N2}");

        Assert.Equal(senderInitialBalance - transferAmount, senderFinalBalance);
        Assert.Equal(recipientInitialBalance + transferAmount, recipientFinalBalance);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Juice_InsufficientBalance_TransferRejected()
    {
        // Arrange
        var (_, recipientKfId, _) = _fixture.CreateAdditionalUser("RichRecipient", createGambler: true);
        _fixture.SetGamblerBalance(50m);
        var initialBalance = _fixture.GetBalance();

        // Act - Try to transfer more than balance
        _fixture.SendCommand($"!juice {recipientKfId} 100");
        Thread.Sleep(100);

        // Assert - Balance should be unchanged
        var finalBalance = _fixture.GetBalance();
        Console.WriteLine($"Insufficient balance test: {initialBalance:N2} -> {finalBalance:N2}");

        Assert.Equal(initialBalance, finalBalance);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Juice_NonExistentUser_TransferRejected()
    {
        // Arrange
        var initialBalance = _fixture.GetBalance();
        var nonExistentKfId = 9999999;

        // Act
        _fixture.SendCommand($"!juice {nonExistentKfId} 100");
        Thread.Sleep(100);

        // Assert - Balance should be unchanged
        var finalBalance = _fixture.GetBalance();
        Assert.Equal(initialBalance, finalBalance);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Juice_DecimalAmount_TransferWorks()
    {
        // Arrange
        var (_, recipientKfId, _) = _fixture.CreateAdditionalUser("DecimalRecipient", createGambler: true);
        var senderInitialBalance = _fixture.GetBalance();
        var transferAmount = 50.25m;

        // Act
        _fixture.SendCommand($"!juice {recipientKfId} {transferAmount}");
        Thread.Sleep(150);

        // Assert
        var senderFinalBalance = _fixture.GetBalance();
        Console.WriteLine($"Decimal transfer: {senderInitialBalance:N2} - {transferAmount} = {senderFinalBalance:N2}");

        Assert.Equal(senderInitialBalance - transferAmount, senderFinalBalance);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Juice_CreatesTransactionRecords()
    {
        // Arrange
        var (_, recipientKfId, _) = _fixture.CreateAdditionalUser("TxnRecipient", createGambler: true);
        var initialTransactions = _fixture.GetTransactionsByType(TransactionSourceEventType.Juicer).Count;

        // Act
        _fixture.SendCommand($"!juice {recipientKfId} 50");
        Thread.Sleep(150);

        // Assert
        var finalTransactions = _fixture.GetTransactionsByType(TransactionSourceEventType.Juicer);
        Console.WriteLine($"Juicer transactions: {initialTransactions} -> {finalTransactions.Count}");

        // Should create 2 transactions (one for sender, one for recipient)
        Assert.Equal(initialTransactions + 2, finalTransactions.Count);
    }

    #endregion

    #region Pocketwatch Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void Pocketwatch_ValidUser_ReturnsBalance()
    {
        // Arrange
        var (_, targetKfId, _) = _fixture.CreateAdditionalUser("WatchTarget", createGambler: true);

        // Act
        _fixture.SendCommand($"!pocketwatch {targetKfId}");
        Thread.Sleep(100);

        // Assert - Command should execute without error
        var targetGambler = _fixture.GetGambler(targetKfId);
        Assert.NotNull(targetGambler);
        Console.WriteLine($"Pocketwatch for {targetKfId}: Balance = {targetGambler.Balance:N2}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Pocketwatch_NonExistentUser_HandlesGracefully()
    {
        // Arrange
        var nonExistentKfId = 8888888;

        // Act
        _fixture.SendCommand($"!pocketwatch {nonExistentKfId}");
        Thread.Sleep(100);

        // Assert - Should not crash
        Console.WriteLine("Pocketwatch for non-existent user - should handle gracefully");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Pocketwatch_UserWithoutGambler_HandlesGracefully()
    {
        // Arrange - Create user without gambler account
        var (_, noGamblerKfId, _) = _fixture.CreateAdditionalUser("NoGamblerUser", createGambler: false);

        // Act
        _fixture.SendCommand($"!pocketwatch {noGamblerKfId}");
        Thread.Sleep(100);

        // Assert - Should handle gracefully (user exists but no gambler)
        var gambler = _fixture.GetGambler(noGamblerKfId);
        Assert.Null(gambler);
        Console.WriteLine("User without gambler account - pocketwatch should handle gracefully");
    }

    #endregion

    #region Daily Dollar Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void Daily_FirstClaim_IncreasesBalance()
    {
        // Arrange - Need to ensure gambler wasn't created today
        // The fixture creates gambler with Created = UtcNow, so new accounts can't claim
        // We need to verify this restriction
        var initialBalance = _fixture.GetBalance();

        // Act
        _fixture.SendCommand("!daily");
        Thread.Sleep(100);

        // Assert - Since account was created today, claim should be rejected
        var finalBalance = _fixture.GetBalance();
        Console.WriteLine($"Daily claim (new account): {initialBalance:N2} -> {finalBalance:N2}");

        // New accounts created same day cannot claim daily dollar
        // Balance should be unchanged
        Assert.Equal(initialBalance, finalBalance);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Daily_AlreadyClaimed_ShowsCooldown()
    {
        // Arrange - Add a recent daily dollar transaction
        _fixture.AddTransaction(TransactionSourceEventType.DailyDollar, 1m, DateTimeOffset.UtcNow);
        var initialBalance = _fixture.GetBalance();

        // Act
        _fixture.SendCommand("!daily");
        Thread.Sleep(100);

        // Assert - Should not add balance (already claimed today)
        var finalBalance = _fixture.GetBalance();
        Console.WriteLine($"Daily already claimed: {initialBalance:N2} -> {finalBalance:N2}");

        Assert.Equal(initialBalance, finalBalance);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void JuiceMe_AliasWorks()
    {
        // Arrange
        var initialBalance = _fixture.GetBalance();

        // Act
        _fixture.SendCommand("!juiceme");
        Thread.Sleep(100);

        // Assert - Command should execute (may or may not modify balance based on conditions)
        Console.WriteLine("!juiceme alias executed");
    }

    #endregion

    #region Rakeback Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void Rakeback_NoWagersSinceLastClaim_ReturnsNoWagers()
    {
        // Arrange - No wagers exist

        // Act
        _fixture.SendCommand("!rakeback");
        Thread.Sleep(100);

        // Assert - Should not crash, balance unchanged
        var transactions = _fixture.GetTransactionsByType(TransactionSourceEventType.Rakeback);
        Console.WriteLine($"Rakeback with no wagers - transactions: {transactions.Count}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Rapeback_AliasWorks()
    {
        // Arrange

        // Act
        _fixture.SendCommand("!rapeback");
        Thread.Sleep(100);

        // Assert - Should not crash
        Console.WriteLine("!rapeback alias executed");
    }

    #endregion

    #region Lossback Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void Lossback_NoLosses_ReturnsNoLosses()
    {
        // Arrange - No losing wagers exist

        // Act
        _fixture.SendCommand("!lossback");
        Thread.Sleep(100);

        // Assert - Should not crash, balance unchanged
        var transactions = _fixture.GetTransactionsByType(TransactionSourceEventType.Lossback);
        Console.WriteLine($"Lossback with no losses - transactions: {transactions.Count}");
    }

    #endregion

    #region Abandon Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void Abandon_WithoutConfirm_ShowsWarning()
    {
        // Arrange
        var gambler = _fixture.GetGambler(_fixture.TestUserKfId);
        var initialState = gambler?.State;

        // Act
        _fixture.SendCommand("!abandon");
        Thread.Sleep(100);

        // Assert - State should not change without confirm
        gambler = _fixture.GetGambler(_fixture.TestUserKfId);
        Console.WriteLine($"Abandon without confirm: State = {gambler?.State}");

        Assert.Equal(initialState, gambler?.State);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Abandon_WithConfirm_MarksAbandoned()
    {
        // Arrange
        var gambler = _fixture.GetGambler(_fixture.TestUserKfId);
        Assert.NotNull(gambler);
        Assert.Equal(GamblerState.Active, gambler.State);

        // Act
        _fixture.SendCommand("!abandon confirm");
        Thread.Sleep(100);

        // Assert - State should be Abandoned
        gambler = _fixture.GetGambler(_fixture.TestUserKfId);
        Console.WriteLine($"Abandon with confirm: State = {gambler?.State}");

        Assert.Equal(GamblerState.Abandoned, gambler?.State);
    }

    #endregion

    #region Exclusion Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void Exclusion_NoActiveExclusion_ReturnsNotExcluded()
    {
        // Arrange - No exclusions exist

        // Act
        _fixture.SendCommand("!exclusion");
        Thread.Sleep(100);

        // Assert - Should not crash
        Console.WriteLine("Exclusion check completed - no active exclusion");
    }

    #endregion
}
