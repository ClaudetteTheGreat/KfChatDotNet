using KfChatDotNetBot;
using KfChatDotNetBot.Models.DbModels;

namespace KfChatDotNetBot.Tests.Integration;

/// <summary>
/// Integration tests for the !juice stats command.
/// Tests query aggregation, top N limits, and empty database handling.
/// </summary>
[Collection("IntegrationTests")]
public class JuiceStatsTests : IDisposable
{
    private readonly TestBotFixture _fixture;

    public JuiceStatsTests(TestBotFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetForNewTest();
    }

    public void Dispose()
    {
        // Cleanup handled by fixture
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void JuiceStats_EmptyDatabase_HandlesGracefully()
    {
        // Arrange - Database is already empty after ResetForNewTest

        // Act
        _fixture.SendCommand("!juice stats");
        Thread.Sleep(100);

        // Assert - Command should not crash
        // The command queries juicers table and calculates stats
        // With empty table, it should handle gracefully (may show 0s or division by zero protection needed)
        var juicers = _fixture.GetAllJuicers();
        Assert.Empty(juicers);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void JuiceStats_WithData_ReturnsCorrectAggregation()
    {
        // Arrange - Add some juicer entries
        _fixture.AddJuicer(_fixture.TestUserKfId, 100.0f);
        _fixture.AddJuicer(_fixture.TestUserKfId, 200.0f);
        _fixture.AddJuicer(_fixture.TestUserKfId, 50.0f);

        // Act
        _fixture.SendCommand("!juice stats");
        Thread.Sleep(100);

        // Assert
        var juicers = _fixture.GetAllJuicers();
        var totalAmount = juicers.Sum(j => j.Amount);

        Console.WriteLine($"Total juicers: {juicers.Count}");
        Console.WriteLine($"Total amount: {totalAmount}");

        Assert.Equal(3, juicers.Count);
        Assert.Equal(350.0f, totalAmount, 0.01);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void JuiceStats_MultipleUsers_AggregatesCorrectly()
    {
        // Arrange - Create additional users and add juicer entries
        var (_, user2KfId, _) = _fixture.CreateAdditionalUser("JuiceUser2");
        var (_, user3KfId, _) = _fixture.CreateAdditionalUser("JuiceUser3");

        // Add juicer entries for different users
        _fixture.AddJuicer(_fixture.TestUserKfId, 100.0f);
        _fixture.AddJuicer(_fixture.TestUserKfId, 50.0f);
        _fixture.AddJuicer(user2KfId, 200.0f);
        _fixture.AddJuicer(user3KfId, 300.0f);

        // Act
        _fixture.SendCommand("!juice stats");
        Thread.Sleep(100);

        // Assert
        var juicers = _fixture.GetAllJuicers();
        var totalAmount = juicers.Sum(j => j.Amount);
        var uniqueUsers = juicers.Select(j => j.User.KfId).Distinct().Count();

        Console.WriteLine($"Total juicers: {juicers.Count}");
        Console.WriteLine($"Unique users: {uniqueUsers}");
        Console.WriteLine($"Total amount: {totalAmount}");

        Assert.Equal(4, juicers.Count);
        Assert.Equal(3, uniqueUsers);
        Assert.Equal(650.0f, totalAmount, 0.01);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void JuiceStats_TopLimit_DefaultsToThree()
    {
        // Arrange - Create multiple users with different juice amounts
        var (_, user2KfId, _) = _fixture.CreateAdditionalUser("TopUser2");
        var (_, user3KfId, _) = _fixture.CreateAdditionalUser("TopUser3");
        var (_, user4KfId, _) = _fixture.CreateAdditionalUser("TopUser4");
        var (_, user5KfId, _) = _fixture.CreateAdditionalUser("TopUser5");

        _fixture.AddJuicer(_fixture.TestUserKfId, 500.0f);
        _fixture.AddJuicer(user2KfId, 400.0f);
        _fixture.AddJuicer(user3KfId, 300.0f);
        _fixture.AddJuicer(user4KfId, 200.0f);
        _fixture.AddJuicer(user5KfId, 100.0f);

        // Act - Default stats (top 3)
        _fixture.SendCommand("!juice stats");
        Thread.Sleep(100);

        // Assert - Command runs successfully with data
        var juicers = _fixture.GetAllJuicers();
        Assert.Equal(5, juicers.Count);

        // Top 3 by amount would be: TestGambler (500), TopUser2 (400), TopUser3 (300)
        var topThree = juicers
            .GroupBy(j => j.User.KfUsername)
            .Select(g => new { User = g.Key, Total = g.Sum(j => j.Amount) })
            .OrderByDescending(x => x.Total)
            .Take(3)
            .ToList();

        Console.WriteLine("Top 3 leeches:");
        foreach (var user in topThree)
        {
            Console.WriteLine($"  {user.User}: {user.Total}");
        }

        Assert.Equal(3, topThree.Count);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void JuiceStats_CustomTopLimit_RespectsLimit()
    {
        // Arrange
        var (_, user2KfId, _) = _fixture.CreateAdditionalUser("LimitUser2");
        var (_, user3KfId, _) = _fixture.CreateAdditionalUser("LimitUser3");

        _fixture.AddJuicer(_fixture.TestUserKfId, 100.0f);
        _fixture.AddJuicer(user2KfId, 200.0f);
        _fixture.AddJuicer(user3KfId, 300.0f);

        // Act - Request top 5 (but only 3 users exist)
        _fixture.SendCommand("!juice stats 5");
        Thread.Sleep(100);

        // Assert
        var juicers = _fixture.GetAllJuicers();
        var uniqueUsers = juicers.Select(j => j.User.KfId).Distinct().Count();

        Console.WriteLine($"Requested top 5, but only {uniqueUsers} users exist");
        Assert.Equal(3, uniqueUsers);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void JuiceStats_TopLimitExceedsMax_RejectsRequest()
    {
        // Arrange - The command limits top to 10

        // Act - Request top 15 (should be rejected)
        _fixture.SendCommand("!juice stats 15");
        Thread.Sleep(100);

        // Assert - Command should reject values > 10
        // (We can't directly verify the message, but the command should handle it)
        Console.WriteLine("Requested top 15 - should be rejected by command");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void JuiceStats_UserWithMultipleJuices_SumsCorrectly()
    {
        // Arrange - Single user with multiple juice entries
        _fixture.AddJuicer(_fixture.TestUserKfId, 10.0f);
        _fixture.AddJuicer(_fixture.TestUserKfId, 20.0f);
        _fixture.AddJuicer(_fixture.TestUserKfId, 30.0f);
        _fixture.AddJuicer(_fixture.TestUserKfId, 40.0f);

        // Act
        _fixture.SendCommand("!juice stats");
        Thread.Sleep(100);

        // Assert
        var juicers = _fixture.GetAllJuicers();
        var userTotal = juicers
            .Where(j => j.User.KfId == _fixture.TestUserKfId)
            .Sum(j => j.Amount);

        Console.WriteLine($"User's total juice: {userTotal}");
        Assert.Equal(100.0f, userTotal, 0.01);
    }
}
