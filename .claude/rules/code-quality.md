# Code Quality Rules
> Last updated: 2026-04-21

**THESE RULES ARE LOADED AUTOMATICALLY IN EVERY SESSION**

---

## Zero-Tolerance Lint & Warning Policy

### RULE 1: Dashboard — 0 errors, 0 warnings

**ALWAYS** before committing any dashboard code:

```bash
cd dashboard
npm run lint            # MUST report 0 errors AND 0 warnings
npm run typecheck       # MUST report 0 errors
npm test                # MUST pass 100%
npm run build           # MUST succeed
```

**If any of these fail or emit warnings: FIX the root cause. Do NOT:**

- ❌ Downgrade a rule from `error` → `warn` "just to unblock CI"
- ❌ Add blanket `/* eslint-disable */` at file level without a comment explaining WHY the disable is legitimate (vitest module augmentation is the only pre-approved case — see `dashboard/src/vitest.d.ts` for the pattern)
- ❌ Suppress with `// eslint-disable-next-line <rule>` without a comment on the line above explaining why
- ❌ Add `TreatWarningsAsErrors=false` in any new project file

**The correct responses to a lint violation:**

| Violation | Correct fix |
|-----------|-------------|
| `no-unused-vars` on import | Remove the import |
| `no-unused-vars` on param | Prefix with `_` (rule respects `^_`) |
| `no-explicit-any` on API body | Define an interface in `dashboard/src/types/` |
| `no-explicit-any` on event handler | Use the proper `React.ChangeEvent<…>` etc. |
| `no-explicit-any` on test mock | Use `vi.mocked(hook).mockReturnValue(x as unknown as ReturnType<typeof hook>)` |
| `prefer-const` | Change `let` → `const` |
| `no-useless-escape` | Remove the redundant backslash |
| `no-empty-object-type` | Use `Record<string, unknown>`, a proper interface, or `unknown` |
| React 19 `static-components` | Refactor OR file-scope disable with rationale (see `AlertCard.tsx` comment) |
| React 19 `set-state-in-effect` | Use functional updater with equality guard OR block-scope disable |

### RULE 2: Worker — 0 errors on build + typecheck

**ALWAYS** before committing any worker code:

```bash
cd infra/cloudflare/worker
bun test                # MUST pass 100%
bunx tsc --noEmit       # MUST report 0 errors
bun run build           # MUST succeed
```

Integration tests (`test/integration/**/*-spec.ts`) are allowed to be blocked by the known vitest-pool-workers Windows-path bug locally; they MUST pass on CI (Linux).

### RULE 3: .NET — 0 errors, 0 warnings

**ALWAYS** before committing any .NET code:

```bash
dotnet build TradingSystem.sln -c Release    # MUST report: Avvisi: 0  Errori: 0
dotnet test TradingSystem.sln --no-build     # MUST pass 100%
```

**`TreatWarningsAsErrors=true` is enabled in all production project files.** Do NOT add project-level `NoWarn` exceptions without an explicit agreement. Exception list in existing projects (see csproj): `CA1416` (platform-specific APIs, accepted for Windows Service code), `CA2024` (async naming, narrow case).

### RULE 4: CI mirrors local — if your local is clean, CI must be clean

If you cannot reproduce a CI failure locally:

1. Check that your local environment matches CI:
   - .NET: simulate the "no TWS SDK" state with `dotnet build -p:UseRealIbkrDll=false`
   - Dashboard/Worker: run `npm ci` (not `npm install`) after deleting `node_modules/` to catch lockfile drift
2. Check CI logs for the exact command + environment
3. If CI has a rule local doesn't enforce, **add the enforcement locally** (via eslint config, Directory.Build.props, or a pre-commit hook) — don't just hope

### RULE 5: Fix the broken-window before committing

Before `git commit`:

1. Files you **touched**: lint + typecheck + build MUST be clean for those files
2. Files you **didn't touch**: if they have pre-existing violations, do NOT mask them with a rule downgrade. Either (a) leave them alone and they remain warnings/errors, or (b) fix them in a dedicated cleanup commit in the same PR, or (c) if they would delay your feature, open a follow-up cleanup issue/PR and leave a TODO with link

### RULE 6: Before opening a PR

**Checklist** (all required):

- [ ] `cd dashboard && npm run lint` → 0 errors, 0 warnings
- [ ] `cd dashboard && npm run typecheck` → 0 errors
- [ ] `cd dashboard && npm test && npm run build` → all green
- [ ] `cd infra/cloudflare/worker && bun test && bunx tsc --noEmit && bun run build` → all green
- [ ] `dotnet build TradingSystem.sln -c Release -p:UseRealIbkrDll=false` → 0 errors, 0 warnings (CI-simulating)
- [ ] `dotnet test -p:UseRealIbkrDll=false` → all pass
- [ ] `git status --short` → no untracked-but-should-be-tracked files (especially `package-lock.json` after dep changes)

If ANY check is red: FIX before pushing. CI existing to catch mistakes is fine; CI existing to be a second line of defense behind a broken local environment is waste.

---

## Why This Policy Exists

Historical: PR #2 had to downgrade 5 ESLint rules from `error` → `warn` to unblock CI when the React 19 compiler lint introduced `static-components` and `set-state-in-effect` rules that caught pre-existing patterns. This was a band-aid, not a fix. PR #4 cleaned all 26 warnings and re-promoted the rules. See `knowledge/lessons-learned.md` → LESSON-184.

The cost of merging with warnings is exponential: once 5 accumulate, the team stops reading lint output, and the 6th is silently welcome. Zero tolerance is easier to enforce than "a few is fine."

---

*Related: `knowledge/errors-registry.md` (tracked critical errors), `knowledge/lessons-learned.md` LESSON-184 (the 26→0 cleanup rationale).*
