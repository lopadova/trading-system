# Contributing Guide

> How to extend and improve the Trading System
> Last updated: 2026-04-05

---

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Development Setup](#development-setup)
- [Project Structure](#project-structure)
- [Coding Standards](#coding-standards)
- [Adding New Features](#adding-new-features)
- [Testing Requirements](#testing-requirements)
- [Pull Request Process](#pull-request-process)
- [Self-Improvement System](#self-improvement-system)

---

## Code of Conduct

This is a professional trading system. Contributions must prioritize:

1. **Safety**: Never compromise safety features
2. **Reliability**: Thoroughly test all changes
3. **Maintainability**: Follow coding standards strictly
4. **Documentation**: Document all new features

---

## Development Setup

### Prerequisites

- Windows 10/11 or Windows Server 2019+
- .NET 10 SDK
- Git
- Visual Studio 2022 or VS Code with C# extension
- SQLite command-line tool (for database inspection)
- Bun 1.x (for dashboard development)

### Initial Setup

```powershell
# Clone repository
git clone <repository-url> trading-system
cd trading-system

# Restore dependencies
dotnet restore

# Build all projects
dotnet build

# Run tests
dotnet test

# Build dashboard (if modifying frontend)
cd dashboard
bun install
bun run dev
```

### Recommended Tools

- **DB Browser for SQLite**: Inspect databases visually
- **Postman**: Test Cloudflare Worker API
- **Windows Terminal**: Better PowerShell experience
- **Git Bash**: Unix-like shell on Windows

---

## Project Structure

```
trading-system/
├── .claude/                    # AI agent instructions (READ-ONLY)
│   ├── agents/                 # Task-specific prompts
│   └── skills/                 # Reusable skill files
├── docs/                       # Documentation
│   ├── INDEX.md                # Wiki entry point
│   ├── ARCHITECTURE.md / ARCHITECTURE_OVERVIEW.md
│   ├── GETTING_STARTED.md
│   ├── ONBOARDING.md
│   ├── CONFIGURATION.md / CONFIGURATION-CHECKLIST.md
│   ├── STRATEGY_FORMAT.md
│   ├── TROUBLESHOOTING.md
│   ├── CONTRIBUTING.md         # This file
│   ├── telegram-integration.md
│   ├── BOT_SETUP_GUIDE.md
│   ├── ops/                    # Phase 7 operational docs (runbook, DR, observability...)
│   └── archive/                # Superseded docs (history preserved)
├── knowledge/                  # Self-improvement system
│   ├── errors-registry.md      # Known errors and fixes
│   ├── lessons-learned.md      # Patterns and discoveries
│   ├── skill-changelog.md      # Skill file version history
│   └── task-corrections.md     # Spec corrections
├── src/
│   ├── SharedKernel/           # Domain types, shared utilities
│   │   ├── Domain/
│   │   ├── Data/
│   │   ├── Ibkr/
│   │   ├── Strategy/
│   │   ├── Options/
│   │   ├── MarketData/
│   │   └── Configuration/
│   ├── TradingSupervisorService/
│   │   ├── Workers/
│   │   ├── Repositories/
│   │   ├── Services/
│   │   ├── Migrations/
│   │   └── Ibkr/
│   └── OptionsExecutionService/
│       ├── Workers/
│       ├── Repositories/
│       ├── Campaign/
│       ├── Orders/
│       ├── Migrations/
│       └── Ibkr/
├── tests/                      # Unit and integration tests
├── dashboard/                  # React frontend
├── infra/
│   ├── cloudflare/worker/      # Cloudflare Worker
│   └── windows/                # Windows Service install scripts
├── strategies/
│   ├── examples/               # Example strategies (committed)
│   └── private/                # User strategies (git-ignored)
└── logs/                       # Runtime logs (git-ignored)
```

---

## Coding Standards

**ALL code MUST follow these standards. Pull requests violating these will be rejected.**

### C# / .NET Standards

#### 1. Early Return Pattern

**ALWAYS** use early returns. **NEVER** use `else` after `return`.

**✅ CORRECT**:
```csharp
public async Task<Order?> GetOrderAsync(string orderId, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(orderId))
    {
        throw new ArgumentException("Order ID cannot be empty");
    }

    var command = new CommandDefinition(sql, new { orderId }, cancellationToken: ct);
    var result = await _connection.QueryFirstOrDefaultAsync<Order>(command);

    if (result == null)
    {
        _logger.LogWarning("Order {OrderId} not found", orderId);
        return null;
    }

    return result;
}
```

**❌ WRONG**:
```csharp
public async Task<Order?> GetOrderAsync(string orderId, CancellationToken ct)
{
    if (!string.IsNullOrWhiteSpace(orderId))
    {
        var command = new CommandDefinition(sql, new { orderId }, cancellationToken: ct);
        var result = await _connection.QueryFirstOrDefaultAsync<Order>(command);
        
        if (result != null)
        {
            return result;
        }
        else
        {
            _logger.LogWarning("Order {OrderId} not found", orderId);
            return null;
        }
    }
    else
    {
        throw new ArgumentException("Order ID cannot be empty");
    }
}
```

---

#### 2. Negative-First Conditionals

Check error/invalid cases FIRST, then proceed with happy path.

**✅ CORRECT**:
```csharp
public ValidationResult Validate(Strategy strategy)
{
    if (strategy == null)
    {
        return new ValidationResult(false, new[] { "Strategy is null" });
    }

    if (string.IsNullOrWhiteSpace(strategy.StrategyName))
    {
        return new ValidationResult(false, new[] { "Strategy name is required" });
    }

    // Happy path continues...
    return new ValidationResult(true, Array.Empty<string>());
}
```

---

#### 3. No Nested Else

Maximum 2 levels of `if` nesting. No `else` blocks.

**✅ CORRECT**:
```csharp
if (!isValid)
{
    return ValidationResult.Invalid("Invalid input");
}

if (!hasPermission)
{
    return ValidationResult.Unauthorized();
}

// Proceed with operation
```

**❌ WRONG**:
```csharp
if (isValid)
{
    if (hasPermission)
    {
        // Proceed
    }
    else
    {
        return ValidationResult.Unauthorized();
    }
}
else
{
    return ValidationResult.Invalid("Invalid input");
}
```

---

#### 4. Typed Signatures

**NO** `object`, `dynamic`, or unclear `var`.

**✅ CORRECT**:
```csharp
public async Task<List<Campaign>> GetActiveCampaignsAsync(CancellationToken ct)
{
    CommandDefinition command = new(sql, cancellationToken: ct);
    IEnumerable<Campaign> result = await _connection.QueryAsync<Campaign>(command);
    return result.ToList();
}
```

**❌ WRONG**:
```csharp
public async Task<object> GetActiveCampaignsAsync(CancellationToken ct)  // NO
{
    var command = new(sql, cancellationToken: ct);  // Unclear type
    var result = await _connection.QueryAsync<Campaign>(command);
    return result.ToList();
}
```

---

#### 5. Try/Catch with Logging

Wrap ALL I/O operations (DB, file, HTTP, IBKR) in try/catch with structured logging.

**✅ CORRECT**:
```csharp
public async Task SaveHeartbeatAsync(string serviceName, DateTime timestamp, CancellationToken ct)
{
    try
    {
        CommandDefinition command = new(sql, new { serviceName, timestamp }, cancellationToken: ct);
        await _connection.ExecuteAsync(command);
        _logger.LogInformation("Heartbeat saved for {ServiceName}", serviceName);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to save heartbeat for {ServiceName}", serviceName);
        throw;  // Rethrow after logging
    }
}
```

---

#### 6. Immutable Records for DTOs

Use `record` with `init` properties for data transfer objects.

**✅ CORRECT**:
```csharp
public record Campaign(
    string CampaignId,
    string StrategyName,
    CampaignState State,
    DateTime CreatedAt
);

public record CampaignUpdate(
    string CampaignId,
    CampaignState NewState
);
```

**❌ WRONG**:
```csharp
public class Campaign
{
    public string CampaignId { get; set; }  // Mutable
    public string StrategyName { get; set; }
    public CampaignState State { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

#### 7. Verbose Inline Comments

Every non-trivial logic block MUST have a comment explaining WHY.

**✅ CORRECT**:
```csharp
// Calculate IV Rank: (current IV - 52-week low) / (52-week high - 52-week low)
// Result is 0-100 percentile of IV range over past year
decimal ivRange = ivHigh - ivLow;
if (ivRange == 0)
{
    // Avoid division by zero when IV has been constant
    return 50.0m;  // Assume middle of range
}
decimal ivRank = (currentIv - ivLow) / ivRange * 100.0m;
```

---

### TypeScript / React Standards

#### 1. Strict Mode

```json
// tsconfig.json
{
  "compilerOptions": {
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "exactOptionalPropertyTypes": true
  }
}
```

---

#### 2. No `any`

**NEVER** use `any`. Use explicit types or `unknown` if truly dynamic.

**✅ CORRECT**:
```typescript
interface ApiResponse {
  status: string;
  data: Campaign[];
}

const response: ApiResponse = await api.get('/campaigns');
```

**❌ WRONG**:
```typescript
const response: any = await api.get('/campaigns');
```

---

#### 3. React Query for Data Fetching

**NO** `useEffect` for data fetching. Use `@tanstack/react-query`.

**✅ CORRECT**:
```typescript
import { useQuery } from '@tanstack/react-query';

function CampaignsPage() {
  const { data, isLoading, error } = useQuery({
    queryKey: ['campaigns'],
    queryFn: async () => {
      const response = await api.get<Campaign[]>('/api/campaigns');
      return response.data;
    },
    refetchInterval: 30000  // Poll every 30s
  });

  if (isLoading) return <Spinner />;
  if (error) return <ErrorMessage error={error} />;

  return <CampaignsList campaigns={data ?? []} />;
}
```

---

### SQL Standards

#### 1. Dapper with CommandDefinition

**ALWAYS** use `CommandDefinition` for `CancellationToken` support.

**✅ CORRECT**:
```csharp
const string sql = "SELECT * FROM campaigns WHERE state = @state";
CommandDefinition command = new(sql, new { state = "Active" }, cancellationToken: ct);
List<Campaign> campaigns = (await _connection.QueryAsync<Campaign>(command)).ToList();
```

**❌ WRONG**:
```csharp
List<Campaign> campaigns = (await _connection.QueryAsync<Campaign>(sql, new { state = "Active" })).ToList();
```

---

#### 2. Explicit SQL, No ORMs

Write SQL explicitly. No Entity Framework, no query builders.

**✅ CORRECT**:
```csharp
const string sql = @"
    INSERT INTO campaigns (campaign_id, strategy_name, state, created_at)
    VALUES (@campaignId, @strategyName, @state, @createdAt)";
```

---

#### 3. Indexes on WHERE/ORDER BY Columns

Every `WHERE` or `ORDER BY` column MUST have an index.

```sql
CREATE INDEX idx_campaigns_state ON campaigns(state);
CREATE INDEX idx_campaigns_created ON campaigns(created_at DESC);
```

---

## Adding New Features

### 1. Create a Feature Branch

```powershell
git checkout -b feature/add-calendar-spread-support
```

### 2. Implement Feature

Follow coding standards above.

### 3. Add Tests

**REQUIRED**: All new features MUST have tests.

```csharp
[Fact]
public async Task CalendarSpreadValidator_ValidSpread_ReturnsValid()
{
    // Arrange
    var spread = new CalendarSpread(/* ... */);
    var validator = new CalendarSpreadValidator();

    // Act
    ValidationResult result = await validator.ValidateAsync(spread);

    // Assert
    Assert.True(result.IsValid);
    Assert.Empty(result.Errors);
}
```

### 4. Update Documentation

- Add to `CONFIGURATION.md` if new config options
- Add to `STRATEGY_FORMAT.md` if new strategy type
- Add to `ARCHITECTURE.md` if new component

### 5. Update Self-Improvement Files

See [Self-Improvement System](#self-improvement-system) below.

---

## Testing Requirements

### Unit Tests

**ALL** new classes MUST have unit tests.

**Coverage Requirements**:
- Repositories: 90%+ coverage
- Validators: 100% coverage
- Domain logic: 90%+ coverage

**Test Structure**:
```csharp
public class CampaignRepositoryTests : IAsyncDisposable
{
    private readonly InMemoryConnectionFactory _factory;
    private readonly CampaignRepository _repository;

    public CampaignRepositoryTests()
    {
        _factory = new InMemoryConnectionFactory();
        _repository = new CampaignRepository(_factory, NullLogger<CampaignRepository>.Instance);
    }

    [Fact]
    public async Task SaveCampaignAsync_ValidCampaign_SavesSuccessfully()
    {
        // Arrange
        Campaign campaign = new(/* ... */);

        // Act
        await _repository.SaveCampaignAsync(campaign, CancellationToken.None);

        // Assert
        Campaign? retrieved = await _repository.GetCampaignAsync(campaign.CampaignId, CancellationToken.None);
        Assert.NotNull(retrieved);
        Assert.Equal(campaign.StrategyName, retrieved.StrategyName);
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
    }
}
```

---

### Integration Tests

**ALL** critical paths MUST have integration tests.

Example: Full campaign lifecycle test

```csharp
[Fact]
public async Task CampaignMonitorWorker_EntryRulesMet_CreatesCampaign()
{
    // Arrange
    // - Load test strategy
    // - Mock IBKR client
    // - Mock market data service

    // Act
    // - Run worker for 1 cycle
    // - Trigger entry conditions

    // Assert
    // - Campaign created in database
    // - State is Active
    // - Position created
}
```

---

## Pull Request Process

### Before Submitting

1. **Run all tests**: `dotnet test`
2. **Build in Release mode**: `dotnet build -c Release`
3. **Check coding standards**: Review your diff
4. **Update documentation**: If applicable
5. **Update knowledge base**: If errors discovered

### PR Title Format

```
[T-XX] Feature: Add calendar spread support
[FIX] Bug: Greeks monitor null reference
[DOCS] Update configuration guide
[TEST] Add integration tests for campaign lifecycle
```

### PR Description Template

```markdown
## Summary
Brief description of changes.

## Type of Change
- [ ] New feature
- [ ] Bug fix
- [ ] Documentation
- [ ] Refactoring
- [ ] Test coverage

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing completed

## Checklist
- [ ] Follows coding standards
- [ ] No TODO comments (or tracked as issues)
- [ ] Documentation updated
- [ ] Self-improvement files updated (if applicable)
- [ ] No secrets in commit

## Breaking Changes
List any breaking changes and migration steps.
```

---

## Self-Improvement System

**CRITICAL**: This system ensures errors are never repeated.

### When to Update Knowledge Base

Update `knowledge/` files when:

- You discover a bug and fix it
- You find a better pattern/practice
- A specification is ambiguous
- A library version incompatibility is found
- A test fails for unexpected reason

### Files to Update

#### 1. errors-registry.md

Add entry for every error discovered.

```markdown
## ERR-XXX — [Short title]

**Discovered by**: T-XX or Developer Name
**Date**: YYYY-MM-DD
**Symptom**: [What error message or behavior was seen]
**Root cause**: [Why it happened]
**Fix**: [Code showing correct implementation]
**Skill updated**: [If a skill file was updated]
**Impact on future tasks**: [What should be avoided]
```

---

#### 2. lessons-learned.md

Add at least 1 entry per major feature.

```markdown
## LL-XXX — Feature: [Title]

**Task**: T-XX or Feature Name
**Category**: pattern | performance | compatibility | tooling | testing
**Discovered**: [What was learned]
**Application**: [How to use this knowledge]
**Relevant for tasks**: [What future work benefits from this]
```

---

#### 3. skill-changelog.md

Update when modifying a skill file.

```markdown
## YYYY-MM-DD — T-XX

**Skill**: skill-dotnet.md
**Section**: Repository Pattern
**Type**: fix | addition | version update
**Problem resolved**: [What error/gap was fixed]
**Impact**: [Who should reread this skill]
```

---

#### 4. task-corrections.md

If you find an error or ambiguity in a specification.

```markdown
## CORR-XXX — TASK-YY: [Description]

**Sezione**: [Section of spec that's wrong]
**Problema**: [What is wrong or unclear]
**Correzione**: [Proposed fix]
**Priorità**: CRITICAL | HIGH | LOW
```

---

### Updating Skill Files

Skill files in `.claude/skills/` can be updated.

**When to update**:
- You discover a better pattern
- Library version changes
- Error pattern is found

**How to update**:
1. Add warning block at top of relevant section:
   ```markdown
   > ⚠️ UPDATED by T-XX — YYYY-MM-DD
   > Reason: [Brief explanation]
   > Fix: [What changed]
   ```

2. Update content below with correct pattern

3. Update version footer:
   ```markdown
   ---
   *Skill version: 1.1 — Last modified: T-XX — Date: YYYY-MM-DD*
   ```

4. Update `knowledge/skill-changelog.md`

---

## Common Contribution Scenarios

### Adding a New Strategy Type

1. **Define domain type**: `src/SharedKernel/Domain/StrategyType.cs`
2. **Update validator**: `src/SharedKernel/Strategy/StrategyValidator.cs`
3. **Add example**: `strategies/examples/example-new-type.json`
4. **Document**: `docs/STRATEGY_FORMAT.md`
5. **Test**: Unit tests for validation logic
6. **Update lessons learned**: Document new patterns

---

### Adding a New Worker

1. **Create worker class**: Extends `BackgroundService`
2. **Register in DI**: `Program.cs`
3. **Add configuration**: `appsettings.json`
4. **Test**: Integration test for worker lifecycle
5. **Document**: Configuration guide

---

### Adding a New Database Table

1. **Create migration**: `src/.../Migrations/XXX_AddNewTable.cs`
2. **Add to migrations class**: `XXXMigrations.All` property
3. **Create repository**: Implement I/O operations
4. **Test**: Unit tests with in-memory database
5. **Document**: Update the relevant migration file in `infra/cloudflare/worker/migrations/` (source of truth for schema). For SQLite (`supervisor.db`, `options.db`) document in the relevant `src/*/Migrations/` C# files.

---

## Code Review Checklist

Reviewers should verify:

- [ ] Follows ALL coding standards
- [ ] No `else` after `return`
- [ ] No nested `else` blocks
- [ ] All I/O wrapped in try/catch
- [ ] Dapper uses `CommandDefinition`
- [ ] TypeScript strict mode enabled
- [ ] No `any` types in TypeScript
- [ ] Tests added for new features
- [ ] Documentation updated
- [ ] Knowledge base updated (if applicable)
- [ ] No secrets committed
- [ ] Strategy files in `private/` not committed
- [ ] No TODO comments without tracking issue

---

## Questions?

Contact: lorenzo.padovani@padosoft.com

---

*Last updated: 2026-04-05 | Trading System v1.0*
