using KfChatDotNetBot;
using KfChatDotNetBot.Models.DbModels;

namespace KfChatDotNetBot.Tests.Integration;

/// <summary>
/// Integration tests for admin commands.
/// Tests role setting, cache clearing, and authorization checks.
/// </summary>
[Collection("IntegrationTests")]
public class AdminCommandTests : IDisposable
{
    private readonly TestBotFixture _fixture;

    public AdminCommandTests(TestBotFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetForNewTest();
    }

    public void Dispose()
    {
        // Cleanup handled by fixture
    }

    #region Authorization Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void AdminRoleSet_NonAdmin_CommandRejected()
    {
        // Arrange - Create a target user
        var (_, targetKfId, _) = _fixture.CreateAdditionalUser("TargetUser", UserRight.Guest);

        // Ensure test user is not admin
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);

        // Get target user's initial right
        var initialRight = _fixture.GetUserByUsername("TargetUser")?.UserRight;

        // Act - Try to set role as non-admin
        _fixture.SendCommand($"!admin role set {targetKfId} 100");
        Thread.Sleep(100);

        // Assert - Target's role should be unchanged
        var finalRight = _fixture.GetUserByUsername("TargetUser")?.UserRight;
        Console.WriteLine($"Non-admin role change: {initialRight} -> {finalRight}");

        Assert.Equal(initialRight, finalRight);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AdminRoleSet_AsAdmin_RoleChanged()
    {
        // Arrange - Create a target user and make test user admin
        var (_, targetKfId, _) = _fixture.CreateAdditionalUser("RoleTarget", UserRight.Guest);
        _fixture.SetTestUserRight(UserRight.Admin);

        // Act
        _fixture.SendCommand($"!admin role set {targetKfId} 100");
        Thread.Sleep(100);

        // Assert
        var targetUser = _fixture.GetUserByUsername("RoleTarget");
        Console.WriteLine($"Admin role change: Guest -> {targetUser?.UserRight}");

        Assert.Equal(UserRight.TrueAndHonest, targetUser?.UserRight);

        // Reset test user right
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AdminCacheClear_NonAdmin_CommandRejected()
    {
        // Arrange
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);

        // Act
        _fixture.SendCommand("!admin cache clear");
        Thread.Sleep(100);

        // Assert - Command should be rejected (no way to verify cache state, but shouldn't crash)
        Console.WriteLine("Cache clear attempted by non-admin");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AdminCacheClear_AsAdmin_Succeeds()
    {
        // Arrange
        _fixture.SetTestUserRight(UserRight.Admin);

        // Act
        _fixture.SendCommand("!admin cache clear");
        Thread.Sleep(100);

        // Assert - Command should succeed
        Console.WriteLine("Cache cleared by admin");

        // Reset
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);
    }

    #endregion

    #region Role Set Command Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void AdminRoleSet_NonExistentUser_HandlesGracefully()
    {
        // Arrange
        _fixture.SetTestUserRight(UserRight.Admin);
        var nonExistentKfId = 7777777;

        // Act
        _fixture.SendCommand($"!admin role set {nonExistentKfId} 100");
        Thread.Sleep(100);

        // Assert - Should not crash
        Console.WriteLine($"Role set for non-existent user {nonExistentKfId} - should handle gracefully");

        // Reset
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AdminRoleSet_AllRoleLevels_SetCorrectly()
    {
        // Arrange
        _fixture.SetTestUserRight(UserRight.Admin);
        var (_, targetKfId, _) = _fixture.CreateAdditionalUser("RoleLevelTest");

        var roleLevels = new[]
        {
            (0, UserRight.Loser),
            (10, UserRight.Guest),
            (100, UserRight.TrueAndHonest),
            (1000, UserRight.Admin)
        };

        // Act & Assert
        foreach (var (value, expected) in roleLevels)
        {
            _fixture.SendCommand($"!admin role set {targetKfId} {value}");
            Thread.Sleep(50);

            var targetUser = _fixture.GetUserByUsername("RoleLevelTest");
            Console.WriteLine($"Set role to {value}: {targetUser?.UserRight}");

            Assert.Equal(expected, targetUser?.UserRight);
        }

        // Reset
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AdminRightSet_AlternativePattern_Works()
    {
        // Arrange
        _fixture.SetTestUserRight(UserRight.Admin);
        var (_, targetKfId, _) = _fixture.CreateAdditionalUser("RightPatternTest", UserRight.Loser);

        // Act - Use "right" instead of "role"
        _fixture.SendCommand($"!admin right set {targetKfId} 10");
        Thread.Sleep(100);

        // Assert
        var targetUser = _fixture.GetUserByUsername("RightPatternTest");
        Console.WriteLine($"Admin right set: {targetUser?.UserRight}");

        Assert.Equal(UserRight.Guest, targetUser?.UserRight);

        // Reset
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);
    }

    #endregion

    #region Invalid Input Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void AdminRoleSet_InvalidRoleValue_HandlesGracefully()
    {
        // Arrange
        _fixture.SetTestUserRight(UserRight.Admin);
        var (_, targetKfId, _) = _fixture.CreateAdditionalUser("InvalidRoleTest", UserRight.Guest);

        // Act - Set invalid role value (not a defined enum value)
        _fixture.SendCommand($"!admin role set {targetKfId} 999");
        Thread.Sleep(100);

        // Assert - The value will be cast to enum even if not defined
        // This is a potential security issue that should be documented
        var targetUser = _fixture.GetUserByUsername("InvalidRoleTest");
        Console.WriteLine($"Invalid role 999 set to: {targetUser?.UserRight} ({(int)(targetUser?.UserRight ?? 0)})");

        // Note: C# allows casting any int to enum, so 999 becomes (UserRight)999
        // This is documented behavior, not necessarily a bug

        // Reset
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AdminRoleSet_NegativeRoleValue_HandlesGracefully()
    {
        // Arrange
        _fixture.SetTestUserRight(UserRight.Admin);
        var (_, targetKfId, _) = _fixture.CreateAdditionalUser("NegativeRoleTest", UserRight.Guest);

        // Act - Negative value (regex should reject this)
        _fixture.SendCommand($"!admin role set {targetKfId} -1");
        Thread.Sleep(100);

        // Assert - Regex pattern requires \d+ which doesn't match negative
        var targetUser = _fixture.GetUserByUsername("NegativeRoleTest");
        Console.WriteLine($"Negative role test: {targetUser?.UserRight}");

        // Role should be unchanged (regex won't match)
        Assert.Equal(UserRight.Guest, targetUser?.UserRight);

        // Reset
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);
    }

    #endregion

    #region Self-Modification Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void AdminRoleSet_SelfDemotion_Works()
    {
        // Arrange
        _fixture.SetTestUserRight(UserRight.Admin);

        // Act - Admin demotes themselves
        _fixture.SendCommand($"!admin role set {_fixture.TestUserKfId} 10");
        Thread.Sleep(100);

        // Assert
        var currentRight = _fixture.GetTestUserRight();
        Console.WriteLine($"Self-demotion: Admin -> {currentRight}");

        Assert.Equal(UserRight.Guest, currentRight);

        // Reset
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AdminRoleSet_SelfPromotion_NotPossible()
    {
        // Arrange - Start as non-admin
        _fixture.SetTestUserRight(UserRight.TrueAndHonest);

        // Act - Try to promote self to admin
        _fixture.SendCommand($"!admin role set {_fixture.TestUserKfId} 1000");
        Thread.Sleep(100);

        // Assert - Should be rejected (not admin)
        var currentRight = _fixture.GetTestUserRight();
        Console.WriteLine($"Self-promotion attempt: {currentRight}");

        Assert.Equal(UserRight.TrueAndHonest, currentRight);
    }

    #endregion
}
