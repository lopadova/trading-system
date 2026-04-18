# GitHub Copilot Instructions - Trading System

> **Repository**: Trading System  
> **Owner**: Lorenzo Padovani (Padosoft)  
> **Tech Stack**: .NET 10, React 19, TypeScript, Bun, Cloudflare Workers

---

## Project Overview

Automated options trading system with:
- **Windows Services** (.NET 10): TradingSupervisorService, OptionsExecutionService
- **Cloudflare Worker** (Hono + TypeScript): Bot API, Strategy conversion, D1 database
- **React Dashboard** (Vite + React 19): Strategy wizard, monitoring UI
- **IBKR Integration**: Interactive Brokers TWS/Gateway connection

---

## Coding Standards (NON-NEGOTIABLE)

### C# / .NET

```csharp
// ✅ CORRECT: Early return, negative-first
public async Task<Result> ProcessAsync(Order order)
{
    if (order == null)
        return Result.Failure("Order is null");
    
    if (!order.IsValid)
        return Result.Failure("Invalid order");
    
    // Happy path last
    return await ExecuteAsync(order);
}

// ❌ WRONG: Nested else, positive-first
public async Task<Result> ProcessAsync(Order order)
{
    if (order != null)
    {
        if (order.IsValid)
        {
            return await ExecuteAsync(order);
        }
        else
        {
            return Result.Failure("Invalid order");
        }
    }
    return Result.Failure("Order is null");
}
```

**Rules**:
- Early return ALWAYS
- Negative-first conditionals (error cases first)
- No nested `else` (max 2 levels of `if`)
- Typed signatures everywhere (no `object`, `dynamic`, `var` for non-obvious types)
- Try/catch with logging on all IO (DB, file, HTTP, pipe)
- Verbose inline comments on every non-trivial logic block
- Dapper with explicit SQL (NO ORM, NO generated queries)
- Record immutable DTOs with `init` properties

### TypeScript / React

```typescript
// ✅ CORRECT: Explicit types, React Query for data
const Dashboard: React.FC = () => {
  const { data, isLoading } = usePositions()
  
  if (isLoading) return <Spinner />
  if (!data) return <Error />
  
  return <PositionsList positions={data} />
}

// ❌ WRONG: `any`, useEffect for data fetching
const Dashboard = () => {
  const [data, setData] = useState<any>(null)
  
  useEffect(() => {
    fetch('/api/positions')
      .then(r => r.json())
      .then(setData)
  }, [])
  
  return <div>{data?.map(...)}</div>
}
```

**Rules**:
- `strict: true` in tsconfig
- NO `any` (explicit types everywhere)
- React Query for ALL remote data (no useEffect for fetching)
- Zustand ONLY for UI state (theme, sidebar, filters)
- Functional components with hooks (NO class components)

### SQL

```sql
-- ✅ CORRECT: WAL mode, indices, LIMIT
PRAGMA journal_mode=WAL;

CREATE INDEX IF NOT EXISTS idx_campaigns_state 
  ON campaigns(state);

SELECT * FROM campaigns 
WHERE state = 'active'
ORDER BY created_at DESC
LIMIT 10;

-- ❌ WRONG: No WAL, no indices, no LIMIT on pageable queries
```

**Rules**:
- WAL mode ALWAYS for SQLite
- Indices on ALL WHERE/ORDER BY columns
- `INSERT OR IGNORE` on deduplication keys
- LIMIT on all pageable reads

---

## Testing Requirements

### Test Commands (CRITICAL)

| Component | Command | Notes |
|-----------|---------|-------|
| Dashboard | `npm test` | ⚠️ **REQUIRED** - Bun vitest doesn't support DOM |
| Worker | `bun test` | Runs unit + integration |
| .NET Services | `dotnet test` | Full suite |

**NEVER suggest `bun test` for dashboard** - it will fail with 144 false errors due to missing DOM support.

### Test Structure

**C# (xUnit)**:
```csharp
public class CampaignRepositoryTests
{
    [Fact]
    public async Task SaveCampaignAsync_ValidCampaign_ReturnsSuccess()
    {
        // Arrange
        var repo = new CampaignRepository(db);
        var campaign = CreateTestCampaign();
        
        // Act
        var result = await repo.SaveCampaignAsync(campaign);
        
        // Assert
        result.Should().BeTrue();
    }
}
```

**TypeScript (Vitest)**:
```typescript
describe('PositionsList', () => {
  it('renders positions correctly', () => {
    const positions = [createMockPosition()]
    render(<PositionsList positions={positions} />)
    
    expect(screen.getByText(/SPY/)).toBeInTheDocument()
  })
})
```

---

## Documentation Sync Rules

### BEFORE committing ANY .md file:

1. **Search for related content**:
   ```bash
   grep -r "keyword" --include="*.md" README.md docs/ dashboard/ infra/
   ```

2. **Update ALL locations** with same information

3. **Verify consistency**:
   - Test commands identical
   - Version numbers match
   - No conflicting info

### Documentation Hierarchy

```
README.md (root)                 ← SOURCE OF TRUTH
  ↓
docs/DEPLOYMENT_GUIDE.md         ← Deployment
docs/ARCHITECTURE.md             ← Architecture
docs/GETTING_STARTED.md          ← Quick start
  ↓
Component READMEs                ← Component-specific
  dashboard/README.md
  infra/cloudflare/worker/README.md
  scripts/README.md
```

**When updating docs**:
- Root README changes → Sync to component READMEs
- Component README changes → Check if root needs update
- NEVER contradict root README

---

## Safety Rules

### CRITICAL

- **NEVER** send live orders to IBKR without explicit confirmation
- **NEVER** commit secrets (API keys, tokens) to git
- **NEVER** modify files in `docs/` or `.claude/` without reading current content first
- **ALWAYS** verify `TradingMode = "paper"` before deployment
- `strategies/private/` ALWAYS in `.gitignore`

### Git Workflow

```bash
# ✅ CORRECT: Create commit with conventional commits
git add .
git commit -m "feat: Add position monitoring worker

- Implements PositionMonitorWorker
- Updates every 5 minutes
- Stores snapshots in SQLite

Co-Authored-By: GitHub Copilot <noreply@github.com>"

# ❌ WRONG: Vague message
git commit -m "updates"
```

---

## File Structure

```
trading-system/
├── src/
│   ├── SharedKernel/              # Shared domain types
│   ├── TradingSupervisorService/  # Monitor, alerting
│   └── OptionsExecutionService/   # Order execution
├── dashboard/                     # React UI
│   ├── src/
│   │   ├── components/
│   │   ├── stores/               # Zustand
│   │   └── utils/
├── infra/cloudflare/worker/      # Hono API worker
│   ├── src/
│   │   ├── routes/
│   │   └── bot/
├── tests/                         # Integration tests
├── docs/                          # Documentation
├── knowledge/                     # AI knowledge base
│   ├── errors-registry.md
│   ├── lessons-learned.md
│   └── skill-changelog.md
├── .claude/                       # Claude Code config
│   ├── skills/
│   ├── rules/
│   └── agents/
└── scripts/                       # Build/deploy scripts
```

---

## Common Patterns

### Dependency Injection (.NET)

```csharp
public class CampaignService
{
    private readonly ICampaignRepository _repo;
    private readonly ILogger<CampaignService> _logger;
    
    public CampaignService(
        ICampaignRepository repo,
        ILogger<CampaignService> logger)
    {
        _repo = repo;
        _logger = logger;
    }
}
```

### React Query Hooks (Dashboard)

```typescript
export function usePositions(symbol?: string) {
  return useQuery({
    queryKey: ['positions', symbol],
    queryFn: () => apiClient.get(`/api/positions`, { 
      searchParams: symbol ? { symbol } : {} 
    }).json<Position[]>(),
    refetchInterval: 30_000, // 30s
  })
}
```

### Dapper Queries (.NET)

```csharp
public async Task<List<Campaign>> GetActiveCampaignsAsync()
{
    const string sql = @"
        SELECT campaign_id, strategy_id, state, opened_at
        FROM campaigns
        WHERE state = 'active'
        ORDER BY opened_at DESC
        LIMIT 100";
    
    return (await _db.QueryAsync<Campaign>(sql)).ToList();
}
```

---

## Error Patterns to Avoid

### ERR-004: Repository API Evolution

**Wrong**:
```csharp
// ❌ Using generic CRUD names
await repo.InsertAsync(campaign);
await repo.GetByIdAsync(id);
```

**Correct**:
```csharp
// ✅ Domain-specific names
await repo.SaveCampaignAsync(campaign);      // Upsert pattern
await repo.GetCampaignAsync(id);             // Domain-specific
```

### ERR-015: Culture-Specific Formatting

**Wrong**:
```csharp
// ❌ CurrentCulture (produces "0,85" in Italian Windows)
Message = $"Delta: {delta:F2}"
```

**Correct**:
```csharp
// ✅ InvariantCulture (always "0.85")
Message = string.Format(CultureInfo.InvariantCulture,
    "Delta: {0:F2}", delta)
```

### ERR-016: Windows Defender Blocking Tests

**Context**: Windows Defender Application Control blocks unsigned DLLs

**Solution**:
```powershell
# Use unlock script before testing
.\scripts\unlock-and-test-all.ps1
```

---

## Knowledge System

### Before Implementation

**Read these files**:
1. `.claude/rules/error-prevention.md` - Known critical errors
2. `knowledge/errors-registry.md` - All errors with root causes
3. `knowledge/lessons-learned.md` - 128+ lessons from past work

### During Implementation

**Update knowledge**:
```bash
# New critical error discovered
echo "## ERR-XXX: [Description]
Root cause: ...
Fix: ...
" >> knowledge/errors-registry.md

# New lesson learned
echo "- LESSON-XXX: [Category] — Description
  Discovery: ...
  Impact: ...
" >> knowledge/lessons-learned.md
```

---

## Quick Reference

### Port Numbers

| Service | Port | Purpose |
|---------|------|---------|
| IBKR TWS (Paper) | 4002 | Trading API |
| IBKR TWS (Live) | 4001 | Trading API |
| Dashboard Dev | 5173 | Vite dev server |
| Worker Local | 8787 | Cloudflare local |

### Environment Variables

```bash
# .NET Services
TradingMode=paper                    # CRITICAL
IBKR__Port=4002
Sqlite__SupervisorDbPath=/data/supervisor.db

# Cloudflare Worker
TELEGRAM_BOT_TOKEN=secret
DISCORD_PUBLIC_KEY=secret
CLAUDE_API_KEY=secret

# Dashboard
VITE_API_URL=https://trading-bot.padosoft.workers.dev
```

---

## Resources

- **Main Docs**: `docs/GETTING_STARTED.md`
- **Deployment**: `docs/DEPLOYMENT_GUIDE.md`
- **Architecture**: `docs/ARCHITECTURE.md`
- **Knowledge Base**: `knowledge/`
- **Error Registry**: `knowledge/errors-registry.md`

---

**Last Updated**: 2026-04-18  
**Author**: Lorenzo Padovani (Padosoft)  
**AI Assistant**: GitHub Copilot
