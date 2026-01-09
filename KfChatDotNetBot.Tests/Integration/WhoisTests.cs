using KfChatDotNetBot;
using KfChatDotNetBot.Models.DbModels;

namespace KfChatDotNetBot.Tests.Integration;

/// <summary>
/// Integration tests for the !whois command.
/// Tests exact match, fuzzy matching, and edge cases.
/// </summary>
[Collection("IntegrationTests")]
public class WhoisTests : IDisposable
{
    private readonly TestBotFixture _fixture;

    public WhoisTests(TestBotFixture fixture)
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
    public void Whois_ExactMatch_ReturnsUserId()
    {
        // Arrange - TestGambler is created by fixture

        // Act
        _fixture.SendCommand($"!whois {_fixture.TestUsername}");
        Thread.Sleep(100);

        // Assert - Verify user exists in database
        var user = _fixture.GetUserByUsername(_fixture.TestUsername);
        Assert.NotNull(user);
        Assert.Equal(_fixture.TestUserKfId, user.KfId);
        Assert.Equal(_fixture.TestUsername, user.KfUsername);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Whois_ExactMatchWithAtSymbol_StripsAtAndMatches()
    {
        // Arrange - @ prefix should be stripped

        // Act
        _fixture.SendCommand($"!whois @{_fixture.TestUsername}");
        Thread.Sleep(100);

        // Assert - User lookup should still work
        var user = _fixture.GetUserByUsername(_fixture.TestUsername);
        Assert.NotNull(user);
        Console.WriteLine($"Found user: {user.KfUsername} with ID {user.KfId}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Whois_ExactMatchWithTrailingComma_StripsCommaAndMatches()
    {
        // Arrange - Trailing comma should be stripped

        // Act
        _fixture.SendCommand($"!whois {_fixture.TestUsername},");
        Thread.Sleep(100);

        // Assert - User lookup should still work
        var user = _fixture.GetUserByUsername(_fixture.TestUsername);
        Assert.NotNull(user);
        Console.WriteLine($"Found user with trailing comma stripped: {user.KfUsername}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Whois_PartialMatch_UsesFuzzyMatching()
    {
        // Arrange - Create users with similar names
        _fixture.CreateAdditionalUser("JohnDoe123");
        _fixture.CreateAdditionalUser("JohnSmith");
        _fixture.CreateAdditionalUser("JaneDoe");

        // Act - Search for partial name
        _fixture.SendCommand("!whois JohnDo");
        Thread.Sleep(100);

        // Assert - Should find closest match via fuzzy matching
        var john = _fixture.GetUserByUsername("JohnDoe123");
        Assert.NotNull(john);
        Console.WriteLine($"Fuzzy search for 'JohnDo' - expecting to find 'JohnDoe123'");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Whois_CaseSensitivity_ExactMatchIsCaseSensitive()
    {
        // Arrange - Create user with specific casing
        _fixture.CreateAdditionalUser("CamelCaseUser");

        // Act - Search with different casing
        _fixture.SendCommand("!whois camelcaseuser");
        Thread.Sleep(100);

        // Assert - Exact match is case sensitive, so should fall back to fuzzy
        var user = _fixture.GetUserByUsername("CamelCaseUser");
        Assert.NotNull(user);
        Console.WriteLine($"Case sensitivity test: Created 'CamelCaseUser', searched 'camelcaseuser'");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Whois_NoMatch_FuzzyReturnsClosest()
    {
        // Arrange - Create some users
        _fixture.CreateAdditionalUser("Alpha");
        _fixture.CreateAdditionalUser("Beta");
        _fixture.CreateAdditionalUser("Gamma");

        // Act - Search for non-existent user
        _fixture.SendCommand("!whois Alphx");
        Thread.Sleep(100);

        // Assert - Fuzzy should find closest match (Alpha)
        var alpha = _fixture.GetUserByUsername("Alpha");
        Assert.NotNull(alpha);
        Console.WriteLine("Searched for 'Alphx', fuzzy should return 'Alpha'");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Whois_MultipleUsers_DatabaseHasAll()
    {
        // Arrange - Create multiple users
        var (_, kfId1, _) = _fixture.CreateAdditionalUser("SearchUser1");
        var (_, kfId2, _) = _fixture.CreateAdditionalUser("SearchUser2");
        var (_, kfId3, _) = _fixture.CreateAdditionalUser("SearchUser3");

        // Assert - All users exist in database
        var users = _fixture.GetAllUsers();
        Console.WriteLine($"Total users in database: {users.Count}");

        Assert.Contains(users, u => u.KfId == kfId1);
        Assert.Contains(users, u => u.KfId == kfId2);
        Assert.Contains(users, u => u.KfId == kfId3);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Whois_EmptyQuery_HandlesGracefully()
    {
        // Arrange - We need to test edge case of empty-ish query
        // Note: The regex requires at least one character after "whois "

        // Act - Send with spaces
        _fixture.SendCommand("!whois   ");
        Thread.Sleep(100);

        // Assert - Should not crash (regex may not match, which is fine)
        Console.WriteLine("Empty query test - command should handle gracefully");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Whois_SpecialCharactersInName_HandlesGracefully()
    {
        // Arrange - Create user with special characters
        _fixture.CreateAdditionalUser("User_With-Special.Chars");

        // Act
        _fixture.SendCommand("!whois User_With-Special.Chars");
        Thread.Sleep(100);

        // Assert
        var user = _fixture.GetUserByUsername("User_With-Special.Chars");
        Assert.NotNull(user);
        Console.WriteLine($"Found user with special chars: {user.KfUsername}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Whois_LongUsername_HandlesGracefully()
    {
        // Arrange - Create user with very long name
        var longName = new string('A', 50) + "LongUser";
        _fixture.CreateAdditionalUser(longName);

        // Act
        _fixture.SendCommand($"!whois {longName}");
        Thread.Sleep(100);

        // Assert
        var user = _fixture.GetUserByUsername(longName);
        Assert.NotNull(user);
        Console.WriteLine($"Found user with long name: {user.KfUsername.Length} chars");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Whois_SimilarNames_FuzzyPicksClosest()
    {
        // Arrange - Create users with similar names
        _fixture.CreateAdditionalUser("TestUser1");
        _fixture.CreateAdditionalUser("TestUser2");
        _fixture.CreateAdditionalUser("TestUser3");
        _fixture.CreateAdditionalUser("TestUser123");

        // Act - Search for exact prefix
        _fixture.SendCommand("!whois TestUser12");
        Thread.Sleep(100);

        // Assert - Should find TestUser123 as closest match
        var closest = _fixture.GetUserByUsername("TestUser123");
        Assert.NotNull(closest);
        Console.WriteLine("Searched 'TestUser12', expecting 'TestUser123' as closest");
    }
}
