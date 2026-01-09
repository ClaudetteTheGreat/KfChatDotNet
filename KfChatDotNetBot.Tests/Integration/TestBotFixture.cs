using KfChatDotNetBot;
using KfChatDotNetBot.Models.DbModels;
using KfChatDotNetBot.Services;
using KfChatDotNetBot.Settings;
using Microsoft.EntityFrameworkCore;

namespace KfChatDotNetBot.Tests.Integration;

/// <summary>
/// Fixture that sets up a test bot instance with an isolated SQLite database.
/// Use this for integration tests that need to invoke real commands and measure results.
/// </summary>
public class TestBotFixture : IDisposable
{
    public ChatBot Bot { get; private set; }
    public string DatabasePath { get; private set; }
    public int TestUserId { get; private set; }
    public string TestUsername { get; private set; }
    public int TestUserKfId { get; private set; }

    private readonly CancellationTokenSource _cts = new();

    public TestBotFixture()
    {
        // Create a unique database file for this test run
        DatabasePath = Path.Combine(Path.GetTempPath(), $"kfbot_test_{Guid.NewGuid():N}.sqlite");
        var connectionString = $"Data Source={DatabasePath}";

        // Configure the database to use our test file
        ApplicationDbContext.TestConnectionString = connectionString;

        // Initialize the database
        InitializeDatabase();

        // Create the test bot in test mode
        Bot = new ChatBot(testMode: true, cancellationToken: _cts.Token);

        TestUsername = "TestGambler";
        TestUserKfId = 999999;
    }

    private void InitializeDatabase()
    {
        using var db = new ApplicationDbContext();

        // Create the schema
        db.Database.Migrate();

        // Sync built-in settings (this sets up Money.Enabled etc.)
        BuiltIn.SyncSettingsWithDb().Wait();

        // Enable the casino
        SettingsProvider.SetValueAsBooleanAsync(BuiltIn.Keys.MoneyEnabled, true).Wait();

        // Create a test user with high balance
        var testUser = new UserDbModel
        {
            KfId = 999999,
            KfUsername = "TestGambler",
            UserRight = UserRight.TrueAndHonest,
            Ignored = false
        };
        db.Users.Add(testUser);
        db.SaveChanges();

        TestUserId = testUser.Id;

        // Create gambler account with massive balance
        var gambler = new GamblerDbModel
        {
            User = testUser,
            Balance = 1_000_000_000_000m, // 1 trillion
            TotalWagered = 0,
            RandomSeed = Guid.NewGuid().ToString(),
            NextVipLevelWagerRequirement = Money.VipLevels[0].BaseWagerRequirement,
            State = GamblerState.Active,
            Created = DateTimeOffset.UtcNow
        };
        db.Gamblers.Add(gambler);
        db.SaveChanges();
    }

    /// <summary>
    /// Send a command as the test user and wait for it to complete
    /// </summary>
    public void SendCommand(string command)
    {
        Bot.ProcessTestMessage(command, TestUserKfId, TestUsername);
        // Give async command handlers time to complete
        Thread.Sleep(50);
    }

    /// <summary>
    /// Send multiple commands rapidly
    /// </summary>
    public void SendCommands(IEnumerable<string> commands)
    {
        foreach (var command in commands)
        {
            Bot.ProcessTestMessage(command, TestUserKfId, TestUsername);
        }
        // Wait for all commands to process
        Thread.Sleep(100);
    }

    /// <summary>
    /// Get all wagers from the test database
    /// </summary>
    public List<WagerDbModel> GetAllWagers()
    {
        using var db = new ApplicationDbContext();
        return db.Wagers.Include(w => w.Gambler).ToList();
    }

    /// <summary>
    /// Get wagers for a specific game type
    /// </summary>
    public List<WagerDbModel> GetWagersByGame(WagerGame game)
    {
        using var db = new ApplicationDbContext();
        return db.Wagers
            .Include(w => w.Gambler)
            .Where(w => w.Game == game)
            .ToList();
    }

    /// <summary>
    /// Calculate RTP from wagers in the database
    /// </summary>
    public (decimal totalWagered, decimal totalReturned, double rtpPercent) CalculateRtp(WagerGame? game = null)
    {
        using var db = new ApplicationDbContext();
        var query = db.Wagers.AsQueryable();

        if (game != null)
            query = query.Where(w => w.Game == game);

        var wagers = query.ToList();

        if (wagers.Count == 0)
            return (0, 0, 0);

        var totalWagered = wagers.Sum(w => w.WagerAmount);
        var totalReturned = wagers.Sum(w => w.WagerEffect + w.WagerAmount);

        var rtpPercent = totalWagered == 0 ? 0 : (double)totalReturned / (double)totalWagered * 100;

        return (totalWagered, totalReturned, rtpPercent);
    }

    /// <summary>
    /// Get the test user's current balance
    /// </summary>
    public decimal GetBalance()
    {
        using var db = new ApplicationDbContext();
        var gambler = db.Gamblers.FirstOrDefault(g => g.User.KfId == TestUserKfId);
        return gambler?.Balance ?? 0;
    }

    /// <summary>
    /// Reset the test user's balance and clear wagers
    /// </summary>
    public void ResetForNewTest()
    {
        using var db = new ApplicationDbContext();

        // Clear all wagers
        db.Wagers.RemoveRange(db.Wagers);

        // Clear all transactions
        db.Transactions.RemoveRange(db.Transactions);

        // Clear all moms
        db.Moms.RemoveRange(db.Moms);

        // Clear all juicers
        db.Juicers.RemoveRange(db.Juicers);

        // Reset gambler balance
        var gambler = db.Gamblers.Include(g => g.User).FirstOrDefault(g => g.User.KfId == TestUserKfId);
        if (gambler != null)
        {
            gambler.Balance = 1_000_000_000_000m;
            gambler.TotalWagered = 0;
        }

        db.SaveChanges();
    }

    /// <summary>
    /// Create an additional test user for multi-user tests
    /// </summary>
    public (int userId, int kfId, string username) CreateAdditionalUser(string username, UserRight userRight = UserRight.TrueAndHonest, bool createGambler = false)
    {
        using var db = new ApplicationDbContext();

        var kfId = new Random().Next(100000, 999999);
        var user = new UserDbModel
        {
            KfId = kfId,
            KfUsername = username,
            UserRight = userRight,
            Ignored = false
        };
        db.Users.Add(user);
        db.SaveChanges();

        if (createGambler)
        {
            var gambler = new GamblerDbModel
            {
                User = user,
                Balance = 10_000m, // 10K starting balance for additional users
                TotalWagered = 0,
                RandomSeed = Guid.NewGuid().ToString(),
                NextVipLevelWagerRequirement = Money.VipLevels[0].BaseWagerRequirement,
                State = GamblerState.Active,
                Created = DateTimeOffset.UtcNow
            };
            db.Gamblers.Add(gambler);
            db.SaveChanges();
        }

        return (user.Id, kfId, username);
    }

    /// <summary>
    /// Set the user right for the test user
    /// </summary>
    public void SetTestUserRight(UserRight userRight)
    {
        using var db = new ApplicationDbContext();
        var user = db.Users.FirstOrDefault(u => u.KfId == TestUserKfId);
        if (user != null)
        {
            user.UserRight = userRight;
            db.SaveChanges();
        }
    }

    /// <summary>
    /// Get the user right for the test user
    /// </summary>
    public UserRight GetTestUserRight()
    {
        using var db = new ApplicationDbContext();
        var user = db.Users.FirstOrDefault(u => u.KfId == TestUserKfId);
        return user?.UserRight ?? UserRight.Guest;
    }

    /// <summary>
    /// Get all moms from the database
    /// </summary>
    public List<MomDbModel> GetAllMoms()
    {
        using var db = new ApplicationDbContext();
        return db.Moms.Include(m => m.User).ToList();
    }

    /// <summary>
    /// Get mom count from the database
    /// </summary>
    public int GetMomCount()
    {
        using var db = new ApplicationDbContext();
        return db.Moms.Count();
    }

    /// <summary>
    /// Add a mom entry for testing
    /// </summary>
    public void AddMom(DateTimeOffset time)
    {
        using var db = new ApplicationDbContext();
        var user = db.Users.First(u => u.KfId == TestUserKfId);
        db.Moms.Add(new MomDbModel { User = user, Time = time });
        db.SaveChanges();
    }

    /// <summary>
    /// Get all juicers from the database
    /// </summary>
    public List<JuicerDbModel> GetAllJuicers()
    {
        using var db = new ApplicationDbContext();
        return db.Juicers.Include(j => j.User).ToList();
    }

    /// <summary>
    /// Add a juicer entry for testing
    /// </summary>
    public void AddJuicer(int userKfId, float amount)
    {
        using var db = new ApplicationDbContext();
        var user = db.Users.First(u => u.KfId == userKfId);
        db.Juicers.Add(new JuicerDbModel
        {
            User = user,
            Amount = amount,
            JuicedAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();
    }

    /// <summary>
    /// Get all users from the database
    /// </summary>
    public List<UserDbModel> GetAllUsers()
    {
        using var db = new ApplicationDbContext();
        return db.Users.ToList();
    }

    /// <summary>
    /// Get a user by username
    /// </summary>
    public UserDbModel? GetUserByUsername(string username)
    {
        using var db = new ApplicationDbContext();
        return db.Users.FirstOrDefault(u => u.KfUsername == username);
    }

    /// <summary>
    /// Get all transactions from the database
    /// </summary>
    public List<TransactionDbModel> GetAllTransactions()
    {
        using var db = new ApplicationDbContext();
        return db.Transactions.Include(t => t.Gambler).ThenInclude(g => g.User).ToList();
    }

    /// <summary>
    /// Get transactions of a specific type
    /// </summary>
    public List<TransactionDbModel> GetTransactionsByType(TransactionSourceEventType eventType)
    {
        using var db = new ApplicationDbContext();
        return db.Transactions
            .Include(t => t.Gambler).ThenInclude(g => g.User)
            .Where(t => t.EventSource == eventType)
            .ToList();
    }

    /// <summary>
    /// Get the gambler entity for a specific user
    /// </summary>
    public GamblerDbModel? GetGambler(int userKfId)
    {
        using var db = new ApplicationDbContext();
        return db.Gamblers.Include(g => g.User).FirstOrDefault(g => g.User.KfId == userKfId);
    }

    /// <summary>
    /// Set the test user's gambler balance
    /// </summary>
    public void SetGamblerBalance(decimal balance)
    {
        using var db = new ApplicationDbContext();
        var gambler = db.Gamblers.Include(g => g.User).FirstOrDefault(g => g.User.KfId == TestUserKfId);
        if (gambler != null)
        {
            gambler.Balance = balance;
            db.SaveChanges();
        }
    }

    /// <summary>
    /// Set the test user's total wagered
    /// </summary>
    public void SetTotalWagered(decimal totalWagered)
    {
        using var db = new ApplicationDbContext();
        var gambler = db.Gamblers.Include(g => g.User).FirstOrDefault(g => g.User.KfId == TestUserKfId);
        if (gambler != null)
        {
            gambler.TotalWagered = totalWagered;
            db.SaveChanges();
        }
    }

    /// <summary>
    /// Add a transaction for testing daily dollar / rakeback / lossback cooldowns
    /// </summary>
    public void AddTransaction(TransactionSourceEventType eventType, decimal effect, DateTimeOffset time)
    {
        using var db = new ApplicationDbContext();
        var gambler = db.Gamblers.Include(g => g.User).First(g => g.User.KfId == TestUserKfId);
        db.Transactions.Add(new TransactionDbModel
        {
            Gambler = gambler,
            EventSource = eventType,
            Time = time,
            TimeUnixEpochSeconds = time.ToUnixTimeSeconds(),
            Effect = effect,
            NewBalance = gambler.Balance + effect
        });
        db.SaveChanges();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();

        // Clean up the test connection string
        ApplicationDbContext.TestConnectionString = null;

        // Delete the test database file
        if (File.Exists(DatabasePath))
        {
            try
            {
                File.Delete(DatabasePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}

/// <summary>
/// Collection definition for sharing a single bot fixture across multiple test classes
/// </summary>
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<TestBotFixture>
{
}
