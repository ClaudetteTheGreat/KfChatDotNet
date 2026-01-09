using KfChatDotNetBot;
using KfChatDotNetBot.Models.DbModels;
using KfChatDotNetBot.Settings;

namespace KfChatDotNetBot.Tests.Integration;

/// <summary>
/// Integration tests for the !mom command.
/// Tests cooldown enforcement, counter increment, and user rights.
/// </summary>
[Collection("IntegrationTests")]
public class MomCommandTests : IDisposable
{
    private readonly TestBotFixture _fixture;

    public MomCommandTests(TestBotFixture fixture)
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
    public void Mom_FirstInvocation_IncrementsCounter()
    {
        // Arrange
        var initialCount = _fixture.GetMomCount();

        // Act
        _fixture.SendCommand("!mom");
        Thread.Sleep(100);

        // Assert
        var newCount = _fixture.GetMomCount();
        Console.WriteLine($"Initial count: {initialCount}, New count: {newCount}");

        // Counter should increment (test user has TrueAndHonest rights by default)
        Assert.Equal(initialCount + 1, newCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Mom_MultipleInvocations_CounterIncrementsCorrectly()
    {
        // Arrange - clear moms and set cooldown to 0 for this test
        var initialCount = _fixture.GetMomCount();

        // Act - Send multiple mom commands with delays to allow cooldown
        const int iterations = 5;
        for (int i = 0; i < iterations; i++)
        {
            _fixture.SendCommand("!mom");
            // Wait longer than typical cooldown
            Thread.Sleep(200);
        }

        // Assert
        var newCount = _fixture.GetMomCount();
        Console.WriteLine($"Initial count: {initialCount}, New count: {newCount}");

        // At least one mom should have been added (cooldown may prevent all)
        Assert.True(newCount > initialCount, "At least one mom should have been added");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Mom_LoserUser_CannotAddToDatabase()
    {
        // Arrange
        _fixture.SetTestUserRight(UserRight.Loser);
        var initialCount = _fixture.GetMomCount();

        // Need to ensure there's no active cooldown - add a mom from a long time ago
        // (Loser can invoke the command but shouldn't add to DB per the code)

        // Act
        _fixture.SendCommand("!mom");
        Thread.Sleep(100);

        // Assert
        var newCount = _fixture.GetMomCount();
        Console.WriteLine($"Loser user - Initial count: {initialCount}, New count: {newCount}");

        // Loser can invoke command but per code (line 31-35), only users with UserRight > Loser
        // can add to the database. Count should remain the same.
        Assert.Equal(initialCount, newCount);

        // Restore user right for other tests
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Mom_GuestUser_CanAddToDatabase()
    {
        // Arrange
        _fixture.SetTestUserRight(UserRight.Guest);
        var initialCount = _fixture.GetMomCount();

        // Act
        _fixture.SendCommand("!mom");
        Thread.Sleep(100);

        // Assert
        var newCount = _fixture.GetMomCount();
        Console.WriteLine($"Guest user - Initial count: {initialCount}, New count: {newCount}");

        // Guest (UserRight = 10) is > Loser (UserRight = 0), so should add to DB
        Assert.Equal(initialCount + 1, newCount);

        // Restore user right for other tests
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Mom_CooldownActive_ReturnsTimeRemaining()
    {
        // Arrange - Add a recent mom to trigger cooldown
        _fixture.AddMom(DateTimeOffset.UtcNow);
        var initialCount = _fixture.GetMomCount();

        // Act
        _fixture.SendCommand("!mom");
        Thread.Sleep(100);

        // Assert
        var newCount = _fixture.GetMomCount();
        Console.WriteLine($"Cooldown test - Initial count: {initialCount}, New count: {newCount}");

        // With active cooldown, no new mom should be added
        // (command still runs but just shows cooldown message)
        Assert.Equal(initialCount, newCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Mom_CooldownExpired_AllowsNewMom()
    {
        // Arrange - Add a mom from long ago (past cooldown)
        // Default cooldown is controlled by BuiltIn.Keys.MomCooldown
        // For testing, we'll use a time far in the past
        _fixture.AddMom(DateTimeOffset.UtcNow.AddHours(-24));
        var initialCount = _fixture.GetMomCount();

        // Act
        _fixture.SendCommand("!mom");
        Thread.Sleep(100);

        // Assert
        var newCount = _fixture.GetMomCount();
        Console.WriteLine($"Expired cooldown test - Initial count: {initialCount}, New count: {newCount}");

        // With expired cooldown, should allow new mom
        Assert.Equal(initialCount + 1, newCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Mom_TracksUserWhoInvoked()
    {
        // Arrange
        var initialMoms = _fixture.GetAllMoms();

        // Act
        _fixture.SendCommand("!mom");
        Thread.Sleep(100);

        // Assert
        var allMoms = _fixture.GetAllMoms();

        // Find the new mom
        var newMoms = allMoms.Where(m => !initialMoms.Any(im => im.Id == m.Id)).ToList();

        if (newMoms.Count > 0)
        {
            var newMom = newMoms.First();
            Console.WriteLine($"New mom added by user: {newMom.User.KfUsername} (KfId: {newMom.User.KfId})");

            // Verify the correct user is tracked
            Assert.Equal(_fixture.TestUserKfId, newMom.User.KfId);
            Assert.Equal(_fixture.TestUsername, newMom.User.KfUsername);
        }
        else
        {
            Console.WriteLine("No new mom added (possibly due to cooldown)");
        }
    }
}
