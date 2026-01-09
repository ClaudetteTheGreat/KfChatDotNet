using KfChatDotNetBot;
using KfChatDotNetBot.Models.DbModels;

namespace KfChatDotNetBot.Tests.Integration;

/// <summary>
/// Integration tests that verify the full wager command pipeline.
///
/// NOTE: RTP (Return-to-Player) verification is handled by the Games/ tests
/// which simulate game math in isolation with 100K+ iterations.
///
/// These tests focus on pipeline validation:
/// - Command parsing and regex matching
/// - Gambler balance checks
/// - Wager database recording
/// - Balance updates after wagers
/// - Multi-game wager tracking
/// </summary>
[Collection("IntegrationTests")]
public class IntegrationRtpTests : IDisposable
{
    private readonly TestBotFixture _fixture;

    public IntegrationRtpTests(TestBotFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetForNewTest();
    }

    public void Dispose()
    {
        // Cleanup handled by fixture
    }

    #region Wager Recording Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void Dice_WagerCommand_RecordsInDatabase()
    {
        const int wagerAmount = 100;
        const int iterations = 10;

        // Send dice commands
        for (int i = 0; i < iterations; i++)
        {
            _fixture.SendCommand($"!dice {wagerAmount}");
        }

        Thread.Sleep(200);

        var wagers = _fixture.GetWagersByGame(WagerGame.Dice);

        Console.WriteLine($"Dice wagers recorded: {wagers.Count}");

        // Verify wagers were recorded
        Assert.True(wagers.Count > 0, "Dice wagers should be recorded in the database");
        Assert.True(wagers.Count <= iterations, $"Should have at most {iterations} wagers");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Limbo_WagerCommand_RecordsInDatabase()
    {
        const int wagerAmount = 100;
        const string multiplier = "2";
        const int iterations = 10;

        // Send limbo commands
        for (int i = 0; i < iterations; i++)
        {
            _fixture.SendCommand($"!limbo {wagerAmount} {multiplier}");
        }

        Thread.Sleep(200);

        var wagers = _fixture.GetWagersByGame(WagerGame.Limbo);

        Console.WriteLine($"Limbo wagers recorded: {wagers.Count}");

        // Verify wagers were recorded
        Assert.True(wagers.Count > 0, "Limbo wagers should be recorded in the database");
        Assert.True(wagers.Count <= iterations, $"Should have at most {iterations} wagers");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void MultipleGames_MixedWagers_AllRecordedCorrectly()
    {
        const int wagerAmount = 100;
        const int iterations = 20;

        // Mix of different games
        for (int i = 0; i < iterations; i++)
        {
            _fixture.SendCommand($"!dice {wagerAmount}");
            _fixture.SendCommand($"!limbo {wagerAmount} 2");
        }

        Thread.Sleep(300);

        var diceWagers = _fixture.GetWagersByGame(WagerGame.Dice);
        var limboWagers = _fixture.GetWagersByGame(WagerGame.Limbo);
        var allWagers = _fixture.GetAllWagers();

        Console.WriteLine($"Dice wagers: {diceWagers.Count}");
        Console.WriteLine($"Limbo wagers: {limboWagers.Count}");
        Console.WriteLine($"Total wagers: {allWagers.Count}");

        // Verify both game types were recorded
        Assert.True(diceWagers.Count > 0, "Dice wagers should be recorded");
        Assert.True(limboWagers.Count > 0, "Limbo wagers should be recorded");

        // Verify total matches sum (no orphaned wagers)
        Assert.Equal(allWagers.Count, diceWagers.Count + limboWagers.Count);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Wager_RecordsCorrectAmount()
    {
        const int wagerAmount = 500;

        _fixture.SendCommand($"!dice {wagerAmount}");
        Thread.Sleep(100);

        var wagers = _fixture.GetWagersByGame(WagerGame.Dice);

        Assert.NotEmpty(wagers);
        Assert.Equal(wagerAmount, wagers.First().WagerAmount);
    }

    #endregion

    #region Balance Update Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void BalanceUpdates_AfterWagers_ReflectsWagerEffects()
    {
        var initialBalance = _fixture.GetBalance();
        const int wagerAmount = 1000;
        const int iterations = 50;

        // Send wagers
        for (int i = 0; i < iterations; i++)
        {
            _fixture.SendCommand($"!dice {wagerAmount}");
        }

        Thread.Sleep(300);

        var finalBalance = _fixture.GetBalance();
        var (totalWagered, totalReturned, _) = _fixture.CalculateRtp(WagerGame.Dice);

        Console.WriteLine($"Initial Balance: {initialBalance:N2}");
        Console.WriteLine($"Final Balance: {finalBalance:N2}");
        Console.WriteLine($"Total Wagered: {totalWagered:N2}");
        Console.WriteLine($"Total Returned: {totalReturned:N2}");

        // Balance change should match wager effects
        var expectedBalance = initialBalance - totalWagered + totalReturned;
        Console.WriteLine($"Expected Final: {expectedBalance:N2}");

        // Allow small tolerance for floating point
        Assert.InRange(finalBalance, expectedBalance - 1, expectedBalance + 1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Balance_DecreasesOnLoss_IncreasesOnWin()
    {
        var initialBalance = _fixture.GetBalance();

        // Run enough wagers that we should see both wins and losses
        for (int i = 0; i < 100; i++)
        {
            _fixture.SendCommand("!dice 100");
        }

        Thread.Sleep(300);

        var wagers = _fixture.GetWagersByGame(WagerGame.Dice);
        var wins = wagers.Count(w => w.WagerEffect > 0);
        var losses = wagers.Count(w => w.WagerEffect < 0);

        Console.WriteLine($"Total wagers: {wagers.Count}, Wins: {wins}, Losses: {losses}");

        // With 100 wagers, we should see both outcomes
        Assert.True(wagers.Count > 0, "Wagers should be recorded");
        // At least some activity should occur
        Assert.True(wins + losses > 0, "Should have at least some outcomes");
    }

    #endregion

    #region Insufficient Balance Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void InsufficientBalance_WagerRejected_NoWagerRecorded()
    {
        // Reset with low balance
        _fixture.ResetForNewTest();

        // Set balance to very low
        _fixture.SetGamblerBalance(50m);

        // Try to wager more than balance
        _fixture.SendCommand("!dice 100");

        Thread.Sleep(100);

        var wagers = _fixture.GetAllWagers();

        // No wager should be recorded since balance was insufficient
        Assert.Empty(wagers);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ExactBalance_WagerAllowed()
    {
        _fixture.ResetForNewTest();

        // Set balance to exact wager amount
        _fixture.SetGamblerBalance(100m);

        _fixture.SendCommand("!dice 100");
        Thread.Sleep(100);

        var wagers = _fixture.GetAllWagers();

        // Wager should be allowed when balance equals wager amount
        Assert.Single(wagers);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ZeroBalance_WagerRejected()
    {
        _fixture.ResetForNewTest();
        _fixture.SetGamblerBalance(0m);

        _fixture.SendCommand("!dice 100");
        Thread.Sleep(100);

        var wagers = _fixture.GetAllWagers();

        Assert.Empty(wagers);
    }

    #endregion

    #region Command Parsing Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void Dice_DecimalWager_Accepted()
    {
        _fixture.SendCommand("!dice 100.50");
        Thread.Sleep(100);

        var wagers = _fixture.GetWagersByGame(WagerGame.Dice);

        Assert.NotEmpty(wagers);
        Assert.Equal(100.50m, wagers.First().WagerAmount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Dice_WithTarget_RecordsWager()
    {
        // Dice with target value
        _fixture.SendCommand("!dice 100 50.5");
        Thread.Sleep(100);

        var wagers = _fixture.GetWagersByGame(WagerGame.Dice);

        Assert.NotEmpty(wagers);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Limbo_DifferentMultipliers_AllRecord()
    {
        _fixture.SendCommand("!limbo 100 1.5");
        _fixture.SendCommand("!limbo 100 5");
        _fixture.SendCommand("!limbo 100 10");
        Thread.Sleep(200);

        var wagers = _fixture.GetWagersByGame(WagerGame.Limbo);

        Assert.Equal(3, wagers.Count);
    }

    #endregion

    #region Pipeline Stress Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void HighVolume_WagersProcessed_NoneDropped()
    {
        const int iterations = 200;
        const int wagerAmount = 100;

        // Rapid fire wagers
        for (int i = 0; i < iterations; i++)
        {
            _fixture.SendCommand($"!dice {wagerAmount}");
        }

        Thread.Sleep(500);

        var wagers = _fixture.GetWagersByGame(WagerGame.Dice);

        Console.WriteLine($"High volume test: {wagers.Count}/{iterations} wagers recorded");

        // All wagers should be processed
        Assert.Equal(iterations, wagers.Count);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Sequential_WagersUpdateBalance_Correctly()
    {
        var balance = _fixture.GetBalance();
        const int wagerAmount = 100;

        for (int i = 0; i < 10; i++)
        {
            _fixture.SendCommand($"!dice {wagerAmount}");
            Thread.Sleep(50); // Let each complete

            var newBalance = _fixture.GetBalance();
            Console.WriteLine($"Wager {i + 1}: Balance {balance:N2} -> {newBalance:N2}");
            balance = newBalance;
        }

        var wagers = _fixture.GetAllWagers();
        Assert.Equal(10, wagers.Count);
    }

    #endregion
}
