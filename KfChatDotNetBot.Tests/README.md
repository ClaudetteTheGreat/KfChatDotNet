# KfChatDotNet Bot Tests

Comprehensive test suite for the KfChatDotNet bot, including RTP analysis, edge case testing, security validation, and integration tests.

## Test Categories

| Category | Description | Filter |
|----------|-------------|--------|
| **RTP** | Return-to-Player analysis for casino games | `FullyQualifiedName~RtpTests` |
| **Integration** | Full command pipeline tests with database | `Category=Integration` |
| **EdgeCase** | Boundary conditions and error handling | `Category=EdgeCase` |
| **Security** | Input validation and overflow protection | `Category=Security` |
| **Concurrency** | Race condition documentation | `Category=Concurrency` |

## Quick Start

```bash
# Run all tests
dotnet test KfChatDotNetBot.Tests/KfChatDotNetBot.Tests.csproj

# Run specific category
dotnet test --filter "Category=Integration"

# Run with verbose output
dotnet test --filter "Category=Integration" --verbosity normal
```

## Directory Structure

```
KfChatDotNetBot.Tests/
├── Games/                    # RTP analysis tests for casino games
│   ├── DiceRtpTests.cs
│   ├── LimboRtpTests.cs
│   ├── KenoRtpTests.cs
│   ├── WheelRtpTests.cs
│   ├── PlinkoRtpTests.cs
│   ├── LambchopRtpTests.cs
│   ├── BlackjackRtpTests.cs
│   ├── SlotsRtpTests.cs
│   ├── PlanesRtpTests.cs
│   └── GuessWhatNumberRtpTests.cs
│
├── Integration/              # Full pipeline integration tests
│   ├── TestBotFixture.cs     # Test infrastructure
│   ├── IntegrationRtpTests.cs
│   ├── MomCommandTests.cs
│   ├── JuiceStatsTests.cs
│   ├── WhoisTests.cs
│   ├── KasinoUserCommandTests.cs
│   ├── UtilityCommandTests.cs
│   ├── AdminCommandTests.cs
│   └── README.md             # Integration test documentation
│
├── EdgeCases/                # Boundary condition tests
│   ├── DivisionByZeroTests.cs
│   ├── MoneyCalculationTests.cs
│   ├── BlackjackStateTests.cs
│   └── BoundaryConditionTests.cs
│
├── Security/                 # Input validation tests
│   ├── InputValidationTests.cs
│   ├── NumericOverflowTests.cs
│   └── CommandInjectionTests.cs
│
├── Concurrency/              # Race condition tests
│   ├── SentMessagesTests.cs
│   ├── SettingsCacheTests.cs
│   └── RateLimitConcurrencyTests.cs
│
└── Helpers/                  # Test utilities
    └── TestHelpers.cs
```

## Test Types

### RTP Tests (Games/)

Simulate casino game logic thousands of times to verify expected return percentages. These tests isolate the game math for accurate RTP measurement.

**Documentation**: [RTP_TESTING_GUIDE.md](RTP_TESTING_GUIDE.md)

```bash
# Run all RTP tests
dotnet test --filter "FullyQualifiedName~RtpTests"

# Run specific game
dotnet test --filter "FullyQualifiedName~DiceRtpTests"
```

Example output:
```
Dice RTP: 97.34% over 100,000 iterations
Expected: ~97% (1.5% house edge)
```

**Note**: These are separate from Integration tests. RTP tests verify game math in isolation (100K iterations), while Integration tests verify the wager pipeline works correctly.

### Integration Tests (Integration/)

Invoke real bot commands through test mode and verify database operations.

**Documentation**: [Integration/README.md](Integration/README.md)

```bash
# Run all integration tests
dotnet test --filter "Category=Integration"

# Run specific command tests
dotnet test --filter "FullyQualifiedName~WhoisTests"
```

Features:
- Isolated SQLite database per test run
- Test mode bot (no WebSocket connections)
- Full command pipeline verification
- Multi-user scenario support

### Edge Case Tests (EdgeCases/)

Test boundary conditions and error handling:

- Division by zero guards
- Decimal precision
- Array bounds
- NaN/Infinity handling
- Min/max value behavior

```bash
dotnet test --filter "Category=EdgeCase"
```

### Security Tests (Security/)

Validate input handling and prevent vulnerabilities:

- Invalid numeric parsing
- Overflow protection
- Injection prevention
- Enum validation

```bash
dotnet test --filter "Category=Security"
```

### Concurrency Tests (Concurrency/)

Document race conditions in the codebase:

- List modification during iteration
- Cache read/write races
- Rate limit bucket races

```bash
dotnet test --filter "Category=Concurrency"
```

## CI/CD Integration

Tests run automatically in GitHub Actions:

| Job | Tests | Purpose |
|-----|-------|---------|
| `build` | - | Compile only |
| `rtp-analysis` | RTP | Casino game fairness |
| `edge-case-tests` | EdgeCase | Error handling |
| `security-tests` | Security | Input validation |
| `concurrency-tests` | Concurrency | Race conditions |
| `integration-tests` | Integration | Full pipeline |

## Writing New Tests

### RTP Test

```csharp
[Fact]
public void NewGame_RTP_ShouldBeReasonable()
{
    decimal totalWagered = 0;
    decimal totalReturned = 0;

    for (int i = 0; i < 100_000; i++)
    {
        totalWagered += 100m;
        totalReturned += SimulateGame();
    }

    var rtp = (double)totalReturned / (double)totalWagered * 100;
    Assert.InRange(rtp, 90.0, 110.0);
}
```

### Integration Test

```csharp
[Fact]
[Trait("Category", "Integration")]
public void Command_Scenario_ExpectedResult()
{
    _fixture.SendCommand("!command args");
    Thread.Sleep(100);

    var result = _fixture.GetSomeState();
    Assert.Equal(expected, result);
}
```

### Edge Case Test

```csharp
[Fact]
[Trait("Category", "EdgeCase")]
public void Method_EdgeInput_HandlesGracefully()
{
    Assert.Throws<DivideByZeroException>(() => Calculate(0));
}
```

### Security Test

```csharp
[Theory]
[Trait("Category", "Security")]
[InlineData("")]
[InlineData("-999999999999999999")]
[InlineData("1e308")]
public void Parser_InvalidInput_RejectsGracefully(string input)
{
    var success = decimal.TryParse(input, out _);
    // Assert expected behavior
}
```

## Dependencies

- **xUnit**: Test framework
- **Moq**: Mocking (for unit tests)
- **FluentAssertions**: Assertion helpers
- **RandN**: Deterministic RNG for reproducible tests

## Troubleshooting

### Tests Timeout

Integration tests may timeout on first run due to database initialization:

```bash
# Increase timeout
dotnet test --filter "Category=Integration" -- xunit.methodDisplayOptions=all
```

### RTP Outside Range

RTP variance is normal with limited iterations:

1. Increase iteration count
2. Widen assertion range
3. Run multiple times to verify

### Database Conflicts

Integration tests use isolated databases but may conflict if run in parallel:

```bash
# Run sequentially
dotnet test --filter "Category=Integration" -- xunit.parallelizeTestCollections=false
```
