---
title: "Trading System - Developer Onboarding"
tags: ["onboarding", "dev"]
aliases: ["Onboarding", "Developer Onboarding"]
status: current
audience: ["new-user", "developer"]
last-reviewed: "2026-04-21"
related:
  - "[[Getting Started with Trading System|GETTING_STARTED]]"
  - "[[Trading System - Architecture Overview|ARCHITECTURE_OVERVIEW]]"
  - "[[Contributing Guide|CONTRIBUTING]]"
  - "[[Configuration Reference|CONFIGURATION]]"
---

# Trading System - Developer Onboarding

Welcome to the Trading System! This guide will get you up and running in 30 minutes.

---

## Quick Start (5 minutes)

### 1. Clone Repository

```bash
git clone <repository-url>
cd trading-system
```

### 2. Install Prerequisites

**Required**:
- .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0
- Bun: `curl -fsSL https://bun.sh/install | bash`
- Git

**Optional** (for deployment):
- PowerShell 7+
- Docker Desktop
- Wrangler CLI: `npm install -g wrangler`

### 3. Build & Test

```bash
# Build .NET solution
dotnet build TradingSystem.sln

# Run tests
dotnet test TradingSystem.sln

# Build Worker
cd infra/cloudflare/worker
bun install
bun run build

# Build Dashboard
cd dashboard
bun install
bun run build
```

Expected output:
```
✓ TradingSystem.sln → 0 errors
✓ Tests → 138/220 passing (63%)
✓ Worker → 0 TypeScript errors
✓ Dashboard → dist/ created
```

---

## Project Structure (10 minutes)

```
trading-system/
├── .claude/                  # AI agent configuration
│   ├── rules/               # Auto-loaded rules (error prevention)
│   ├── skills/              # Reusable patterns (.NET, testing, etc.)
│   └── agents/              # Task-specific prompts
│
├── docs/                     # Documentation
│   ├── ARCHITECTURE_OVERVIEW.md
│   ├── DEPLOYMENT_GUIDE.md
│   └── ONBOARDING.md (this file)
│
├── src/                      # .NET source code
│   ├── SharedKernel/        # Common types, repositories, IBKR
│   ├── TradingSupervisorService/  # Monitoring service
│   └── OptionsExecutionService/   # Trading service
│
├── tests/                    # .NET test projects
│   ├── SharedKernel.Tests/
│   ├── TradingSupervisorService.Tests/
│   └── OptionsExecutionService.Tests/
│
├── infra/cloudflare/worker/  # Cloudflare Worker (bot + API)
│   ├── src/                 # TypeScript source
│   ├── test/                # Vitest tests
│   └── migrations/          # D1 migrations
│
├── dashboard/                # React dashboard
│   ├── src/                 # Components, pages, hooks
│   └── test/                # Vitest tests
│
├── knowledge/                # Living documentation
│   ├── errors-registry.md   # All errors discovered + fixes
│   ├── lessons-learned.md   # Patterns and best practices
│   └── skill-changelog.md   # Skill file version history
│
├── scripts/                  # Automation scripts
│   ├── deploy-windows-services.ps1
│   ├── pre-deployment-checklist.sh
│   └── sync-kb-to-rules.sh
│
└── strategies/               # Trading strategies (SDF format)
    ├── examples/            # Public examples
    └── private/             # GITIGNORED - your strategies
```

---

## Development Workflow (10 minutes)

### Running Locally

#### TradingSupervisorService

```bash
cd src/TradingSupervisorService
dotnet run

# Or with hot reload
dotnet watch run
```

Default config: `appsettings.json`
- TradingMode: paper
- Database: data/supervisor.db
- IBKR: 127.0.0.1:4002

#### OptionsExecutionService

```bash
cd src/OptionsExecutionService
dotnet run
```

#### Cloudflare Worker (local dev)

```bash
cd infra/cloudflare/worker
bun run dev

# Or with Wrangler
bunx wrangler dev
```

Accessible at: http://localhost:8787

#### Dashboard (local dev)

```bash
cd dashboard
bun run dev
```

Accessible at: http://localhost:5173

---

### Making Changes

#### 1. Create Feature Branch

```bash
git checkout -b feature/my-feature
```

#### 2. Check Knowledge Base

**Before coding**, check existing solutions:

```bash
# Search for related errors
grep -i "repository" knowledge/errors-registry.md

# Search for patterns
grep -i "testing" knowledge/lessons-learned.md

# Check relevant skills
cat .claude/skills/skill-dotnet.md
cat .claude/skills/skill-testing.md
```

#### 3. Write Code

Follow patterns in `.claude/skills/`:
- skill-dotnet.md: .NET patterns (repositories, workers, DI)
- skill-testing.md: xUnit patterns (async, mocking, assertions)
- skill-sqlite-dapper.md: Database patterns
- skill-ibkr-api.md: IBKR integration

#### 4. Write Tests

**Every feature needs tests**:

```csharp
// Repository test example
[Fact(DisplayName = "TEST-XX-YY: SaveAsync then GetAsync returns entity")]
[Trait("TaskId", "T-XX")]
[Trait("TestId", "TEST-XX-YY")]
public async Task TEST_XX_YY_SaveAsync_ThenGetAsync_ReturnsEntity()
{
    // Arrange
    var entity = CreateTestEntity();
    
    // Act
    await _repo.SaveAsync(entity, CancellationToken.None);
    var retrieved = await _repo.GetAsync(entity.Id, CancellationToken.None);
    
    // Assert
    Assert.NotNull(retrieved);
    Assert.Equal(entity.Id, retrieved.Id);
}
```

#### 5. Run Tests

```bash
# Run all tests
dotnet test TradingSystem.sln

# Run specific test project
dotnet test tests/TradingSupervisorService.Tests

# Run specific test
dotnet test --filter "TestId=TEST-22-26"
```

#### 6. Update Knowledge Base

**If you discovered an error**:

```bash
# Add to errors-registry.md
echo "## ERR-XXX — [Description]
**Scoperto da**: T-YY
**Data**: $(date +%Y-%m-%d)
**Sintomo**: [What you saw]
**Root cause**: [Why it happened]
**Fix**: [How you solved it]
**Skill aggiornato**: [Which skill file]
**Impatto sui task futuri**: [Lessons for future work]
" >> knowledge/errors-registry.md
```

**If you learned something**:

```bash
# Add to lessons-learned.md
echo "- **LESSON-XXX**: [Category] — [Title]
  - Context: [Where/when]
  - Discovery: [What you learned]
  - Application: [How to use it]
  - Reference: ERR-XXX (if applicable)
" >> knowledge/lessons-learned.md
```

#### 7. Commit

```bash
git add .
git commit -m "feat: description

- Detail 1
- Detail 2

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

#### 8. Push & PR

```bash
git push origin feature/my-feature

# Create PR on GitHub
# CI will run tests automatically
```

---

## Common Tasks

### Add New Worker

```csharp
// 1. Create worker class
public sealed class MyWorker : BackgroundService
{
    private readonly ILogger<MyWorker> _logger;
    private readonly IConfiguration _config;
    
    public MyWorker(
        ILogger<MyWorker> logger,
        IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }
    
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        int intervalSeconds = _config.GetValue<int>("Monitoring:MyWorkerIntervalSeconds", 60);
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(intervalSeconds));
        
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                // Your logic here
                _logger.LogInformation("MyWorker tick");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MyWorker error");
            }
        }
    }
}

// 2. Register in Program.cs
builder.Services.AddHostedService<MyWorker>();

// 3. Add config to appsettings.json
{
  "Monitoring": {
    "MyWorkerIntervalSeconds": 60
  }
}

// 4. Write tests
public class MyWorkerTests { ... }
```

### Add New Repository

```csharp
// 1. Define interface (domain-driven names)
public interface IMyRepository
{
    Task SaveAsync(MyEntity entity, CancellationToken ct);
    Task<MyEntity?> GetAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<MyEntity>> GetByStateAsync(MyState state, CancellationToken ct);
}

// 2. Implement with Dapper
public sealed class MyRepository : IMyRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<MyRepository> _logger;
    
    public MyRepository(IDbConnectionFactory db, ILogger<MyRepository> logger)
    {
        _db = db;
        _logger = logger;
    }
    
    public async Task SaveAsync(MyEntity entity, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO my_entities (id, name, state, created_at)
            VALUES (@Id, @Name, @State, @CreatedAt)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                state = excluded.state,
                updated_at = @UpdatedAt
            """;
        
        try
        {
            await using var conn = await _db.OpenAsync(ct);
            await conn.ExecuteAsync(sql, new
            {
                entity.Id,
                entity.Name,
                State = entity.State.ToString(),
                CreatedAt = DateTime.UtcNow.ToString("O"),
                UpdatedAt = DateTime.UtcNow.ToString("O")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveAsync failed for {EntityId}", entity.Id);
            throw;
        }
    }
}

// 3. Register in DI
builder.Services.AddSingleton<IMyRepository, MyRepository>();

// 4. Write integration tests
public sealed class MyRepositoryTests : IAsyncLifetime { ... }
```

### Add New Migration

```bash
# .NET (SQLite)
cd src/TradingSupervisorService/Migrations
# Create new file: NNNN_description.cs

public sealed class Migration_NNNN_Description : IMigration
{
    public int Version => NNNN;
    public string Description => "Add my_table";
    
    public async Task UpAsync(SqliteConnection conn)
    {
        const string sql = """
            CREATE TABLE my_table (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            """;
        await conn.ExecuteAsync(sql);
    }
}

# Add to Migrations.All
public static IReadOnlyList<IMigration> All => new IMigration[]
{
    // ...existing...
    new Migration_NNNN_Description(),
};

# Cloudflare (D1)
cd infra/cloudflare/worker/migrations
# Create: NNNN_description.sql

CREATE TABLE my_table (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    created_at TEXT NOT NULL
);

# Apply locally
bunx wrangler d1 migrations apply trading-db --local

# Apply remote (production)
bunx wrangler d1 migrations apply trading-db --remote
```

---

## Debugging

### Attach Debugger (.NET)

**VS Code**:
```json
// .vscode/launch.json
{
  "name": ".NET Core Launch (console)",
  "type": "coreclr",
  "request": "launch",
  "program": "${workspaceFolder}/src/TradingSupervisorService/bin/Debug/net10.0/TradingSupervisorService.dll",
  "cwd": "${workspaceFolder}/src/TradingSupervisorService",
  "stopAtEntry": false
}
```

**Visual Studio**: F5

### View Logs

```bash
# Console output (dev)
dotnet run  # Logs to stdout

# Windows Event Viewer (production)
Get-EventLog -LogName Application -Source TradingSupervisorService -Newest 10

# Cloudflare Worker logs
bunx wrangler tail
```

### Inspect Database

```bash
# SQLite CLI
sqlite3 data/supervisor.db

# Queries
.tables
.schema heartbeats
SELECT * FROM heartbeats;
PRAGMA journal_mode;  -- Should be 'wal'
```

---

## Testing Strategy

### Test Levels

1. **Unit Tests**: Business logic, pure functions
2. **Integration Tests**: Repositories + SQLite, workers + mocks
3. **E2E Tests**: Full workflows (bot, wizard)

### Coverage Targets

- New code: 80%+ coverage
- Critical paths: 100% (order execution, safety checks)
- Repositories: 100% (all CRUD operations)

### Running Tests

```bash
# All tests
dotnet test TradingSystem.sln
bun test  # Worker + Dashboard

# Watch mode
dotnet watch test --project tests/TradingSupervisorService.Tests
bun test --watch

# Coverage report
dotnet test TradingSystem.sln --collect:"XPlat Code Coverage"
bun test --coverage
```

---

## CI/CD

### GitHub Actions Workflows

1. **`.github/workflows/dotnet-build-test.yml`**
   - Triggers: Push/PR to main/develop
   - Runs: Build + test .NET solution
   - Artifacts: Published services (on main only)

2. **`.github/workflows/cloudflare-deploy.yml`**
   - Triggers: Push to main (Worker/Dashboard changes)
   - Runs: Build + test + deploy to Cloudflare
   - Secrets: `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`

### Pull Request Process

1. Create PR from feature branch to `main`
2. CI runs automatically (build + tests)
3. Review checklist:
   - [ ] All tests pass
   - [ ] Code follows patterns in `.claude/skills/`
   - [ ] Knowledge base updated (if error discovered)
   - [ ] No hardcoded secrets
4. Merge to `main` (squash & merge)
5. Automatic deployment (Worker + Dashboard)
6. Manual deployment (Windows Services)

---

## Resources

### Documentation

- [Architecture Overview](ARCHITECTURE_OVERVIEW.md)
- [Deployment Guide](DEPLOYMENT_GUIDE.md)
- [CLAUDE.md](../CLAUDE.md) - AI agent instructions

### Knowledge Base

- [errors-registry.md](../knowledge/errors-registry.md) - All known errors + fixes
- [lessons-learned.md](../knowledge/lessons-learned.md) - Best practices
- [skill-changelog.md](../knowledge/skill-changelog.md) - Skill version history

### Skills (Coding Patterns)

- [skill-dotnet.md](../.claude/skills/skill-dotnet.md)
- [skill-testing.md](../.claude/skills/skill-testing.md)
- [skill-sqlite-dapper.md](../.claude/skills/skill-sqlite-dapper.md)
- [skill-ibkr-api.md](../.claude/skills/skill-ibkr-api.md)

### External Documentation

- .NET: https://learn.microsoft.com/dotnet
- xUnit: https://xunit.net
- Cloudflare Workers: https://developers.cloudflare.com/workers
- React: https://react.dev

---

## Getting Help

### Ask Questions

1. Check knowledge base first (errors-registry.md, lessons-learned.md)
2. Search `.claude/skills/` for patterns
3. Ask team in Slack/Teams
4. Create GitHub issue

### Report Bugs

```bash
# Include:
# 1. Steps to reproduce
# 2. Expected behavior
# 3. Actual behavior
# 4. Logs/screenshots
# 5. Environment (OS, .NET version, etc.)
```

### Suggest Improvements

```bash
# Create issue with:
# - Problem statement
# - Proposed solution
# - Benefits
# - Breaking changes (if any)
```

---

## Next Steps

Now that you're set up:

1. ✅ Read [ARCHITECTURE_OVERVIEW.md](ARCHITECTURE_OVERVIEW.md)
2. ✅ Review [errors-registry.md](../knowledge/errors-registry.md) (top 10)
3. ✅ Read relevant skills (skill-dotnet.md, skill-testing.md)
4. ✅ Pick a good first issue (GitHub label: `good-first-issue`)
5. ✅ Write your first PR!

---

**Welcome aboard! 🚀**

Questions? Contact: trading-system-team@padosoft.com
