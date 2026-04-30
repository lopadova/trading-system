# PR Workflow Rules
> Last updated: 2026-05-01

**THESE RULES ARE LOADED AUTOMATICALLY IN EVERY SESSION**

---

## RULE 1: Incremental PRs > Giant PRs

**ALWAYS** create PRs at logical checkpoints, NOT at the end of all tasks.

**Good PR size:**
- 3-8 related tasks forming a cohesive feature
- Single responsibility (e.g., "Outbox pattern" or "State machine extension")
- Reviewable in < 30 minutes

**Bad PR size:**
- All 26 tasks in one PR
- Multiple unrelated features mixed
- Requires > 1 hour to review

**Rationale:**
- Easier to review → faster feedback → fewer merge conflicts
- Early detection of architectural issues
- Continuous integration → main branch always deployable

---

## RULE 2: Automated Review Loop Until Green

**EVERY PR** must go through this loop before merge:

```
1. Open PR with clear summary (Summary, Changes, Test Coverage, Next Steps)
   ↓
2. Trigger Copilot/AI code review
   ↓
3. Read review comments
   ↓
4. Fix ALL comments found
   ↓
5. Run full test suite locally
   ↓ (if PASS)
6. Commit fixes with clear message
   ↓
7. Push to branch
   ↓
8. Trigger review again
   ↓
9. Check results:
   ├─ NO comments + CI PASS → MERGE PR → Continue with next PR
   └─ Comments found OR CI FAIL → Go back to step 3
```

**NEVER:**
- Skip review comments ("I'll fix later")
- Merge with failing CI
- Merge with unaddressed review feedback
- Push without running local tests first

**Checking review status:**
```bash
# View PR comments
gh pr view <PR-number> --comments

# Check CI status
gh pr checks <PR-number>

# View full PR details
gh pr view <PR-number>
```

---

## RULE 3: PR Description Template

**ALWAYS** include in PR description:

```markdown
## Summary
[2-3 bullet points: what changed and why]

## Changes
[Detailed breakdown by category]

### 1. [Category Name]
**Created:**
- File1: purpose
- File2: purpose

**Modified:**
- File3: what changed

**Why:** [Rationale for this change]

## Test Coverage
**All X tests PASS:**
- Component1: N tests
- Component2: M tests

**New Tests:**
- TestFile1: K tests (purpose)

## Database Schema (if applicable)
[Schema changes with SQL snippets]

## Next Steps
[What's coming in future PRs]

## Checklist
- [ ] All tests pass
- [ ] Build succeeds with 0 warnings
- [ ] Migrations tested
- [ ] TDD workflow followed (RED → GREEN)
- [ ] Documentation updated
```

---

## RULE 4: Fix Workflow

**When review finds issues:**

1. **Read ALL comments** before starting fixes
2. **Group related fixes** (e.g., all naming issues together)
3. **Fix in order:**
   - CRITICAL bugs first (data loss, security)
   - Build/test failures second
   - Code quality third (naming, formatting)
   - Documentation last
4. **Commit per category** (not one giant "fix review comments" commit)
5. **Re-run tests** after EACH commit
6. **Push** only after ALL fixes done + tests pass

**Commit message format for fixes:**
```
fix: address review feedback - [category]

- Specific change 1
- Specific change 2
- Specific change 3

Reviewer: Copilot/Human
```

---

## RULE 5: CI Must Pass Before Merge

**NEVER merge with:**
- ❌ Red CI checks
- ❌ Unresolved review comments
- ❌ Failing tests (even if "flaky")
- ❌ Build warnings in changed files

**If CI fails:**
1. Read CI logs (use `gh pr checks <PR-number>`)
2. Reproduce locally (`dotnet test`, `npm test`, `bun test`)
3. Fix root cause (not symptoms)
4. Commit fix
5. Push
6. Wait for CI green
7. If still red → repeat from step 1

---

## RULE 6: Post-Merge Actions

**After merge:**

1. Delete feature branch (local + remote)
   ```bash
   git branch -d feat/branch-name
   git push origin --delete feat/branch-name
   ```

2. Pull main + verify
   ```bash
   git checkout main
   git pull origin main
   dotnet build && dotnet test
   ```

3. Create next PR branch immediately
   ```bash
   git checkout -b feat/next-feature
   ```

4. Update `.agent-state.json` if tracking tasks

---

## RULE 7: When Review Finds Critical Issues

**If review reveals architectural problems:**

1. **STOP** implementing more tasks on current branch
2. Discuss with team/user:
   - Is current approach correct?
   - Should we refactor before continuing?
   - Does this affect future tasks?
3. **Decision:**
   - If minor: Fix in current PR
   - If major: Close PR, refactor in new branch, re-submit
4. **Document decision** in commit message or PR comment

**Example critical issues:**
- Wrong database schema design
- Security vulnerability
- Performance bottleneck
- Breaking change to public API

---

*These rules ensure PRs are reviewable, mergeable, and maintainable.*
