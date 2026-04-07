# Trading System Integration Tests

This directory contains comprehensive integration tests for the Trading System backend services.

## Test Structure

```
tests/
├── SharedKernel.Tests/           # Shared kernel unit tests
├── TradingSupervisorService.Tests/
│   ├── ProgramIntegrationTests.cs          # Service startup & DI tests (TEST-22-01 to TEST-22-10)
│   ├── Migrations/
│   │   └── MigrationIntegrationTests.cs    # Database migration tests (TEST-22-11 to TEST-22-15)
│   ├── Workers/
│   │   └── WorkerLifecycleIntegrationTests.cs  # Worker lifecycle tests (TEST-22-21 to TEST-22-25)
│   └── Repositories/
│       └── RepositoryIntegrationTests.cs   # Repository integration tests (TEST-22-26 to TEST-22-30)
└── OptionsExecutionService.Tests/
    ├── ProgramIntegrationTests.cs          # Service startup & DI tests (TEST-09-01 to TEST-09-05)
    ├── Migrations/
    │   └── MigrationIntegrationTests.cs    # Database migration tests (TEST-22-16 to TEST-22-20)
    └── Repositories/
        └── RepositoryIntegrationTests.cs   # Repository integration tests (TEST-22-31 to TEST-22-35)
```

## Test Categories

### 1. Service Startup Tests (ProgramIntegrationTests)
Tests that verify the entire service host can be created and all dependencies are registered correctly in the DI container.

**TradingSupervisorService:**
- TEST-22-01: All required services registered in DI
- TEST-22-02: Configuration validation at startup
- TEST-22-03: IBKR client singleton registration
- TEST-22-04: Repository services registration
- TEST-22-05: Metrics collector availability
- TEST-22-06: HttpClientFactory registration
- TEST-22-07: TelegramAlerter service
- TEST-22-08: Database connection factory
- TEST-22-09: Positions repository separate database
- TEST-22-10: All hosted services (workers) registered

**OptionsExecutionService:**
- TEST-09-01: All required services registered in DI
- TEST-09-02: IBKR configuration validates for paper trading
- TEST-09-03: Order safety configuration validates
- TEST-09-04: Singleton services return same instance
- TEST-09-05: Scoped services return different instances across scopes

### 2. Database Migration Tests (MigrationIntegrationTests)
Tests that verify all database migrations apply successfully and create the correct schema.

**TradingSupervisorService:**
- TEST-22-11: All supervisor migrations apply successfully
- TEST-22-12: Migration 001 creates heartbeats table
- TEST-22-13: Migration 001 creates outbox table
- TEST-22-14: Migration 001 creates alerts table
- TEST-22-15: Migration 002 creates ivts_snapshots table

**OptionsExecutionService:**
- TEST-22-16: All options migrations apply successfully
- TEST-22-17: Migration 001 creates campaigns table
- TEST-22-18: Migration 001 creates positions table
- TEST-22-19: Migration 002 adds greeks columns
- TEST-22-20: Migration 003 creates order_tracking table

### 3. Worker Lifecycle Tests (WorkerLifecycleIntegrationTests)
Tests that verify workers start, execute their cycles, and stop gracefully.

**TradingSupervisorService:**
- TEST-22-21: HeartbeatWorker starts and executes cycle
- TEST-22-22: OutboxSyncWorker starts and stops gracefully
- TEST-22-23: TelegramWorker handles cancellation correctly
- TEST-22-24: LogReaderWorker starts with valid configuration
- TEST-22-25: Multiple workers can run concurrently

### 4. Repository Integration Tests (RepositoryIntegrationTests)
Tests that verify repositories persist and retrieve data correctly with real SQLite database.

**TradingSupervisorService:**
- TEST-22-26: HeartbeatRepository inserts and retrieves metrics
- TEST-22-27: OutboxRepository enqueues and dequeues events
- TEST-22-28: AlertRepository creates and retrieves alerts
- TEST-22-29: LogReaderStateRepository persists and loads state
- TEST-22-30: IvtsRepository stores and queries snapshots

**OptionsExecutionService:**
- TEST-22-31: CampaignRepository creates and retrieves campaigns
- TEST-22-32: CampaignRepository lists active campaigns
- TEST-22-33: CampaignRepository updates campaign status
- TEST-22-34: OrderTrackingRepository creates and tracks orders
- TEST-22-35: OrderTrackingRepository updates order status

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Project
```bash
dotnet test tests/TradingSupervisorService.Tests
dotnet test tests/OptionsExecutionService.Tests
```

### Run Tests by Category (Trait Filter)
```bash
# Run all T-22 integration tests
dotnet test --filter "TaskId=T-22"

# Run specific test by ID
dotnet test --filter "TestId=TEST-22-01"
```

### Run Tests with Detailed Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Generate Code Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Test Design Principles

### 1. Use In-Memory Databases
All integration tests use `InMemoryConnectionFactory` from `SharedKernel.Tests` to create isolated in-memory SQLite databases. This ensures:
- Fast test execution
- No side effects between tests
- No cleanup required
- Reproducible results

### 2. Test Isolation
Each test class implements `IAsyncLifetime` to set up and tear down its own database instance. Tests within a class share the same database but use transactions or separate records to avoid conflicts.

### 3. Real Dependencies, Minimal Mocks
Integration tests use real implementations wherever possible:
- Real SQLite databases (in-memory)
- Real repositories
- Real migrations
- Real workers

Mocks are only used for:
- External HTTP calls
- IBKR API connections
- Machine metrics collection (hardware-dependent)

### 4. Test Naming Convention
Tests follow the pattern: `TEST_XX_YY_DescriptiveTestName`
- `XX` = Task number (e.g., 22 for T-22)
- `YY` = Sequential test number within the task
- Tests are tagged with `[Trait("TaskId", "T-XX")]` and `[Trait("TestId", "TEST-XX-YY")]`

### 5. Arrange-Act-Assert Pattern
All tests follow the AAA pattern:
```csharp
// Arrange: Set up test data and dependencies
var entity = new Entity { ... };

// Act: Execute the operation being tested
await repository.InsertAsync(entity, ct);
var result = await repository.GetByIdAsync(entity.Id, ct);

// Assert: Verify expected outcomes
Assert.NotNull(result);
Assert.Equal(expectedValue, result.Property);
```

## Troubleshooting

### "Database is locked" Errors
In-memory databases use WAL mode by default, but WAL is not supported in-memory. The `InMemoryConnectionFactory` automatically uses DELETE journal mode instead.

### "Table does not exist" Errors
Ensure migrations are run in `InitializeAsync()` before running tests:
```csharp
public async Task InitializeAsync()
{
    _factory = new InMemoryConnectionFactory();
    MigrationRunner runner = new(_factory, NullLogger<MigrationRunner>.Instance);
    await runner.RunAsync(SupervisorMigrations.All, CancellationToken.None);
}
```

### Worker Tests Hang
Worker tests run background services with short intervals. Make sure to:
1. Use a `CancellationTokenSource` with a reasonable timeout
2. Cancel the token after the test completes
3. Handle `OperationCanceledException` (this is expected behavior)

### Tests Fail Intermittently
Worker lifecycle tests may have timing issues. If a test is flaky:
1. Increase the delay before cancellation
2. Add explicit synchronization (e.g., wait for specific database state)
3. Verify that the worker interval is shorter than the test timeout

## CI/CD Integration

These tests are designed to run in CI/CD pipelines without any external dependencies:
- No real IBKR connection required
- No real Telegram API required
- No file system dependencies (except for temp directories)
- No network calls (all mocked)

Example GitHub Actions workflow:
```yaml
- name: Run Integration Tests
  run: dotnet test --filter "TaskId=T-22" --logger "trx;LogFileName=test-results.trx"
```

## Related Documentation

- [Coding Standards](../CLAUDE.md) - Project coding standards and patterns
- [Skill: Testing](../.claude/skills/skill-testing.md) - Testing patterns and best practices
- [Skill: .NET](../.claude/skills/skill-dotnet.md) - .NET patterns and conventions

## Adding New Integration Tests

When adding new integration tests:

1. **Choose the right test category:**
   - Service startup → ProgramIntegrationTests
   - Database schema → MigrationIntegrationTests
   - Worker behavior → WorkerLifecycleIntegrationTests
   - Data persistence → RepositoryIntegrationTests

2. **Follow the naming convention:**
   - File: `{Feature}IntegrationTests.cs`
   - Class: `{Feature}IntegrationTests`
   - Test: `TEST_XX_YY_{DescriptiveName}`

3. **Use IAsyncLifetime:**
   ```csharp
   public sealed class MyIntegrationTests : IAsyncLifetime
   {
       private InMemoryConnectionFactory _factory = default!;

       public async Task InitializeAsync() { /* setup */ }
       public async Task DisposeAsync() { /* cleanup */ }
   }
   ```

4. **Tag with traits:**
   ```csharp
   [Fact(DisplayName = "TEST-XX-YY: Description")]
   [Trait("TaskId", "T-XX")]
   [Trait("TestId", "TEST-XX-YY")]
   public async Task TEST_XX_YY_Description() { ... }
   ```

5. **Document in this README** (add test to appropriate category above)

---

*Last updated: 2026-04-05 (Task T-22)*
