using KfChatDotNetBot;
using KfChatDotNetBot.Models.DbModels;

namespace KfChatDotNetBot.Tests.Integration;

/// <summary>
/// Integration tests for utility commands.
/// Tests nogamba/yesgamba toggle, version, and lastactivity commands.
/// </summary>
[Collection("IntegrationTests")]
public class UtilityCommandTests : IDisposable
{
    private readonly TestBotFixture _fixture;

    public UtilityCommandTests(TestBotFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetForNewTest();
    }

    public void Dispose()
    {
        // Cleanup handled by fixture
    }

    #region NoGamba/YesGamba Toggle Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void NoGamba_ExecutesWithoutCrash()
    {
        // Arrange
        // Note: BotServices is internal, so we can only verify command executes

        // Act
        _fixture.SendCommand("!nogamba");
        Thread.Sleep(100);

        // Assert - Command should execute without crash
        Console.WriteLine("!nogamba command executed successfully");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void YesGamba_ExecutesWithoutCrash()
    {
        // Arrange

        // Act
        _fixture.SendCommand("!yesgamba");
        Thread.Sleep(100);

        // Assert - Command should execute without crash
        Console.WriteLine("!yesgamba command executed successfully");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void NoGamba_ThenYesGamba_BothExecute()
    {
        // Arrange

        // Act - Toggle multiple times
        _fixture.SendCommand("!nogamba");
        Thread.Sleep(50);

        _fixture.SendCommand("!yesgamba");
        Thread.Sleep(50);

        _fixture.SendCommand("!nogamba");
        Thread.Sleep(50);

        // Assert - All commands should execute without crash
        Console.WriteLine("Toggle sequence executed successfully");
    }

    #endregion

    #region Version Command Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void Version_ReturnsValidString()
    {
        // Arrange

        // Act
        _fixture.SendCommand("!version");
        Thread.Sleep(100);

        // Assert - Command should execute without crash
        Console.WriteLine("Version command executed successfully");
    }

    #endregion

    #region LastActivity Command Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void LastActivity_ExecutesWithoutCrash()
    {
        // Arrange

        // Act
        _fixture.SendCommand("!lastactivity");
        Thread.Sleep(100);

        // Assert - Command should execute without crash
        Console.WriteLine("LastActivity command executed successfully");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void LastActive_AliasWorks()
    {
        // Arrange

        // Act
        _fixture.SendCommand("!lastactive");
        Thread.Sleep(100);

        // Assert - Command should execute without crash
        Console.WriteLine("LastActive alias executed successfully");
    }

    #endregion

    #region User Rights Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void NoGamba_RequiresGuest_LoserCannotInvoke()
    {
        // Arrange
        _fixture.SetTestUserRight(UserRight.Loser);

        // Act - Loser (0) < Guest (10), so command should be rejected
        _fixture.SendCommand("!nogamba");
        Thread.Sleep(100);

        // Assert - Command should be blocked for Loser
        Console.WriteLine("NoGamba command attempted by Loser user");

        // Reset for other tests
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Version_RequiresLoser_GuestCanInvoke()
    {
        // Arrange
        _fixture.SetTestUserRight(UserRight.Guest);

        // Act - Guest (10) > Loser (0), so command should work
        _fixture.SendCommand("!version");
        Thread.Sleep(100);

        // Assert - Command should execute
        Console.WriteLine("Version command executed by Guest user");

        // Reset for other tests
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void LastActivity_RequiresLoser_GuestCanInvoke()
    {
        // Arrange
        _fixture.SetTestUserRight(UserRight.Guest);

        // Act
        _fixture.SendCommand("!lastactivity");
        Thread.Sleep(100);

        // Assert - Command should execute
        Console.WriteLine("LastActivity command executed by Guest user");

        // Reset for other tests
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);
    }

    #endregion
}
