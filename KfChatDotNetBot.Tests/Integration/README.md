# Integration Tests

This directory contains integration tests that invoke real bot commands through the test mode infrastructure and verify behavior against an isolated SQLite database.

## Overview

Unlike unit tests that test individual components in isolation, integration tests verify the full command pipeline:

1. Command parsing and regex matching
2. User permission checks
3. Gambler balance verification
4. Database operations (reads/writes)
5. Transaction recording
6. Balance updates

## Test Infrastructure

### TestBotFixture

The `TestBotFixture` class (`TestBotFixture.cs`) provides:

- **Isolated Database**: Each test run uses a unique SQLite database file in the temp directory
- **Test Mode Bot**: A `ChatBot` instance running in test mode (no WebSocket connections)
- **Test User**: A pre-configured user with `TrueAndHonest` rights and 1 trillion balance
- **Helper Methods**: Database queries, user creation, balance manipulation

### Key Fixture Methods

| Method | Description |
|--------|-------------|
| `SendCommand(string)` | Send a command as the test user |
| `GetBalance()` | Get the test user's current balance |
| `ResetForNewTest()` | Clear wagers, transactions, moms, juicers and reset balance |
| `CreateAdditionalUser()` | Create another user for multi-user tests |
| `SetTestUserRight()` | Change the test user's permission level |
| `GetAllWagers()` | Retrieve all wager records |
| `CalculateRtp()` | Calculate RTP from wager database |

## Running Integration Tests

```bash
# Run all integration tests
dotnet test --filter "Category=Integration"

# Run specific test class
dotnet test --filter "FullyQualifiedName~WhoisTests"

# Run single test
dotnet test --filter "FullyQualifiedName~WhoisTests.Whois_ExactMatch"

# Run with verbose output
dotnet test --filter "Category=Integration" --verbosity normal
```

## Test Categories

### Wager Pipeline Tests (`IntegrationRtpTests.cs`)

Tests that verify the full wager command pipeline works correctly.

**NOTE**: RTP (Return-to-Player) verification is handled by the `Games/` tests which simulate game math in isolation with 100K+ iterations. These integration tests focus on pipeline validation only.

#### Wager Recording
| Test | Description |
|------|-------------|
| `Dice_WagerCommand_RecordsInDatabase` | Dice wagers recorded in DB |
| `Limbo_WagerCommand_RecordsInDatabase` | Limbo wagers recorded in DB |
| `MultipleGames_MixedWagers_AllRecordedCorrectly` | Different game types recorded separately |
| `Wager_RecordsCorrectAmount` | Wager amount stored correctly |

#### Balance Updates
| Test | Description |
|------|-------------|
| `BalanceUpdates_AfterWagers_ReflectsWagerEffects` | Balance changes match wager effects |
| `Balance_DecreasesOnLoss_IncreasesOnWin` | Win/loss affects balance correctly |

#### Insufficient Balance
| Test | Description |
|------|-------------|
| `InsufficientBalance_WagerRejected_NoWagerRecorded` | Insufficient funds properly rejected |
| `ExactBalance_WagerAllowed` | Exact balance can be wagered |
| `ZeroBalance_WagerRejected` | Zero balance rejects wager |

#### Command Parsing
| Test | Description |
|------|-------------|
| `Dice_DecimalWager_Accepted` | Decimal wager amounts work |
| `Dice_WithTarget_RecordsWager` | Target parameter parsed correctly |
| `Limbo_DifferentMultipliers_AllRecord` | Various multipliers work |

#### Pipeline Stress
| Test | Description |
|------|-------------|
| `HighVolume_WagersProcessed_NoneDropped` | 200 rapid wagers all recorded |
| `Sequential_WagersUpdateBalance_Correctly` | Sequential balance updates correct |

### Mom Command Tests (`MomCommandTests.cs`)

Tests for the `!mom` command which tracks a counter with cooldown.

| Test | Description |
|------|-------------|
| `Mom_FirstInvocation_IncrementsCounter` | Counter increases on first call |
| `Mom_MultipleInvocations_CounterIncrementsCorrectly` | Multiple calls increment correctly |
| `Mom_LoserUser_CannotAddToDatabase` | Loser users can invoke but don't add to DB |
| `Mom_GuestUser_CanAddToDatabase` | Guest and above can add to DB |
| `Mom_CooldownActive_ReturnsTimeRemaining` | Active cooldown prevents new entry |
| `Mom_CooldownExpired_AllowsNewMom` | Expired cooldown allows new entry |
| `Mom_TracksUserWhoInvoked` | Correct user recorded in database |

### Juice Stats Tests (`JuiceStatsTests.cs`)

Tests for the `!juice stats` command which aggregates juicer data.

| Test | Description |
|------|-------------|
| `JuiceStats_EmptyDatabase_HandlesGracefully` | Empty table doesn't crash |
| `JuiceStats_WithData_ReturnsCorrectAggregation` | Sum calculated correctly |
| `JuiceStats_MultipleUsers_AggregatesCorrectly` | Per-user aggregation works |
| `JuiceStats_TopLimit_DefaultsToThree` | Default top 3 returned |
| `JuiceStats_CustomTopLimit_RespectsLimit` | Custom limit parameter works |
| `JuiceStats_TopLimitExceedsMax_RejectsRequest` | Values > 10 rejected |
| `JuiceStats_UserWithMultipleJuices_SumsCorrectly` | Multiple entries per user summed |

### Whois Tests (`WhoisTests.cs`)

Tests for the `!whois` command which looks up users by name.

| Test | Description |
|------|-------------|
| `Whois_ExactMatch_ReturnsUserId` | Exact username match returns ID |
| `Whois_ExactMatchWithAtSymbol_StripsAtAndMatches` | @ prefix stripped |
| `Whois_ExactMatchWithTrailingComma_StripsCommaAndMatches` | Trailing comma stripped |
| `Whois_PartialMatch_UsesFuzzyMatching` | Partial names use fuzzy search |
| `Whois_CaseSensitivity_ExactMatchIsCaseSensitive` | Case sensitivity behavior |
| `Whois_NoMatch_FuzzyReturnsClosest` | Non-existent name finds closest |
| `Whois_SimilarNames_FuzzyPicksClosest` | Best fuzzy match selected |

### Kasino User Command Tests (`KasinoUserCommandTests.cs`)

Tests for non-wager kasino commands.

#### Balance Commands
| Test | Description |
|------|-------------|
| `Balance_ReturnsCurrentBalance` | Balance reported correctly |
| `Bal_ShorthandWorks` | `!bal` alias works |

#### Juice Transfer
| Test | Description |
|------|-------------|
| `Juice_TransferToOtherUser_UpdatesBothBalances` | Sender decreases, recipient increases |
| `Juice_InsufficientBalance_TransferRejected` | Can't send more than balance |
| `Juice_NonExistentUser_TransferRejected` | Invalid user ID rejected |
| `Juice_DecimalAmount_TransferWorks` | Decimal amounts work |
| `Juice_CreatesTransactionRecords` | Two transactions created |

#### Pocketwatch
| Test | Description |
|------|-------------|
| `Pocketwatch_ValidUser_ReturnsBalance` | Other user's balance shown |
| `Pocketwatch_NonExistentUser_HandlesGracefully` | Invalid ID handled |
| `Pocketwatch_UserWithoutGambler_HandlesGracefully` | User without gambler handled |

#### Daily Dollar
| Test | Description |
|------|-------------|
| `Daily_FirstClaim_IncreasesBalance` | New accounts can't claim |
| `Daily_AlreadyClaimed_ShowsCooldown` | Cooldown prevents double claim |
| `JuiceMe_AliasWorks` | `!juiceme` alias works |

#### Rakeback/Lossback
| Test | Description |
|------|-------------|
| `Rakeback_NoWagersSinceLastClaim_ReturnsNoWagers` | No wagers message |
| `Rapeback_AliasWorks` | `!rapeback` alias works |
| `Lossback_NoLosses_ReturnsNoLosses` | No losses message |

#### Abandon
| Test | Description |
|------|-------------|
| `Abandon_WithoutConfirm_ShowsWarning` | Warning shown without confirm |
| `Abandon_WithConfirm_MarksAbandoned` | Account marked as abandoned |

### Utility Command Tests (`UtilityCommandTests.cs`)

Tests for utility commands.

| Test | Description |
|------|-------------|
| `NoGamba_ExecutesWithoutCrash` | `!nogamba` command works |
| `YesGamba_ExecutesWithoutCrash` | `!yesgamba` command works |
| `NoGamba_ThenYesGamba_BothExecute` | Toggle sequence works |
| `Version_ReturnsValidString` | `!version` executes |
| `LastActivity_ExecutesWithoutCrash` | `!lastactivity` executes |
| `LastActive_AliasWorks` | `!lastactive` alias works |

### Admin Command Tests (`AdminCommandTests.cs`)

Tests for admin commands with authorization checks.

#### Authorization
| Test | Description |
|------|-------------|
| `AdminRoleSet_NonAdmin_CommandRejected` | Non-admin can't set roles |
| `AdminRoleSet_AsAdmin_RoleChanged` | Admin can set roles |
| `AdminCacheClear_NonAdmin_CommandRejected` | Non-admin can't clear cache |
| `AdminCacheClear_AsAdmin_Succeeds` | Admin can clear cache |

#### Role Setting
| Test | Description |
|------|-------------|
| `AdminRoleSet_NonExistentUser_HandlesGracefully` | Invalid user handled |
| `AdminRoleSet_AllRoleLevels_SetCorrectly` | All role values work |
| `AdminRightSet_AlternativePattern_Works` | `!admin right set` works |

#### Edge Cases
| Test | Description |
|------|-------------|
| `AdminRoleSet_InvalidRoleValue_HandlesGracefully` | Invalid enum value handled |
| `AdminRoleSet_NegativeRoleValue_HandlesGracefully` | Negative values rejected by regex |
| `AdminRoleSet_SelfDemotion_Works` | Admin can demote self |
| `AdminRoleSet_SelfPromotion_NotPossible` | Non-admin can't promote self |

## Writing New Integration Tests

### Basic Test Structure

```csharp
[Collection("IntegrationTests")]
public class MyCommandTests : IDisposable
{
    private readonly TestBotFixture _fixture;

    public MyCommandTests(TestBotFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetForNewTest(); // Clean state for each test
    }

    public void Dispose()
    {
        // Cleanup handled by fixture
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void MyCommand_Scenario_ExpectedBehavior()
    {
        // Arrange
        var initialState = _fixture.GetBalance();

        // Act
        _fixture.SendCommand("!mycommand args");
        Thread.Sleep(100); // Allow async processing

        // Assert
        var finalState = _fixture.GetBalance();
        Assert.NotEqual(initialState, finalState);
    }
}
```

### Testing Multi-User Scenarios

```csharp
[Fact]
[Trait("Category", "Integration")]
public void Transfer_BetweenUsers_UpdatesBoth()
{
    // Create additional user with gambler account
    var (_, recipientKfId, _) = _fixture.CreateAdditionalUser("Recipient", createGambler: true);

    var senderBefore = _fixture.GetBalance();
    var recipientBefore = _fixture.GetGambler(recipientKfId)?.Balance ?? 0;

    _fixture.SendCommand($"!juice {recipientKfId} 100");
    Thread.Sleep(150);

    var senderAfter = _fixture.GetBalance();
    var recipientAfter = _fixture.GetGambler(recipientKfId)?.Balance ?? 0;

    Assert.Equal(senderBefore - 100, senderAfter);
    Assert.Equal(recipientBefore + 100, recipientAfter);
}
```

### Testing Permission Levels

```csharp
[Fact]
[Trait("Category", "Integration")]
public void AdminCommand_NonAdmin_Rejected()
{
    _fixture.SetTestUserRight(UserRight.TrueAndHonest); // Not admin
    var before = GetSomeState();

    _fixture.SendCommand("!admin something");
    Thread.Sleep(100);

    var after = GetSomeState();
    Assert.Equal(before, after); // State unchanged

    _fixture.SetTestUserRight(UserRight.TrueAndHonest); // Reset
}
```

### Testing Cooldowns

```csharp
[Fact]
[Trait("Category", "Integration")]
public void Command_WithCooldown_RejectsSecondCall()
{
    // First call should work
    _fixture.SendCommand("!cooldowncmd");
    Thread.Sleep(100);
    var countAfterFirst = GetCount();

    // Second call should be blocked by cooldown
    _fixture.SendCommand("!cooldowncmd");
    Thread.Sleep(100);
    var countAfterSecond = GetCount();

    Assert.Equal(countAfterFirst, countAfterSecond);
}
```

## CI/CD Integration

Integration tests run in the GitHub Actions workflow under the `integration-tests` job:

```yaml
- name: Run Integration Tests
  run: |
    dotnet test KfChatDotNetBot.Tests/KfChatDotNetBot.Tests.csproj \
      --no-build \
      --configuration Release \
      --filter "Category=Integration" \
      --verbosity normal
```

## Notes

- Integration tests use real database operations with an isolated SQLite file
- Each test class shares a fixture instance but calls `ResetForNewTest()` to ensure clean state
- Test mode bot skips WebSocket connections and rate limiting
- Commands execute synchronously but handlers may be async - use `Thread.Sleep()` to allow completion
- Console output is captured and displayed in test results for debugging
