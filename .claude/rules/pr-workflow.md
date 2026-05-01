# PR Workflow Rules
> Last updated: 2026-05-01

**THESE RULES ARE LOADED AUTOMATICALLY IN EVERY SESSION**

---

## RULE 1: Copilot Review is MANDATORY Before Merge

**ALWAYS** request a Copilot review BEFORE merging any PR:

```bash
# Step 1: Create PR
gh pr create --base main --head feature-branch --title "..." --body "..."

# Step 2: MANDATORY - Request Copilot review
gh copilot -p "Review PR #XX for: 1) Code quality issues, 2) Missing DI registrations, 3) Test coverage gaps, 4) Documentation accuracy. Focus on runtime bugs and integration issues."

# Step 3: Read Copilot review output and address ALL HIGH priority issues

# Step 4: ONLY merge after Copilot review is complete and issues are fixed
gh pr merge XX --squash --auto
```

**Why this rule exists:**
- PR #19 was merged without Copilot review
- Copilot found a CRITICAL bug: `IOrderOutboxRepository` not registered in DI
- Service would crash at runtime due to unresolved dependency
- Integration tests were NOT actually end-to-end (missing coverage)
- Documentation overstated what was actually implemented

**When to skip:**
- **NEVER**. Even for "trivial" PRs like docs-only, Copilot can catch inconsistencies.

**If you forget:**
Request a post-merge review immediately and create a hotfix PR for any HIGH issues found.

---

## RULE 2: CI Checks Must Pass Before Merge

**ALL** CI checks must be green (SUCCESS) before merging:
- Build and Test .NET Services: SUCCESS
- Dashboard Build & Test: SUCCESS  
- Cloudflare Worker Build & Test: SUCCESS
- Security Checks: SUCCESS
- .NET Test Results: SUCCESS

SKIPPED checks are acceptable (e.g., deployment on non-main branches).

---

## RULE 3: PR Review Checklist

Before clicking merge, verify:

- [ ] Copilot review requested and completed
- [ ] All HIGH priority issues from Copilot are fixed
- [ ] All CI checks passed (green)
- [ ] DI registrations added for all new interfaces/services
- [ ] No captive dependencies (singletons inject IServiceScopeFactory, not scoped services directly)
- [ ] Integration tests actually test the full flow (not just repository calls)
- [ ] Documentation matches actual implementation
- [ ] No performance claims without benchmarks
- [ ] Test assertions verify DB state, not just "no rows"

**If ANY check fails: FIX before merging**

---

## RULE 4: Post-Merge Copilot Review for Emergencies

If you merged without Copilot review (emergency only):

1. Request review immediately after merge
2. Create hotfix PR for any HIGH issues within 24h
3. Update this rule with the lesson learned
4. Add the mistake to `knowledge/errors-registry.md`

---

*Related: `.claude/rules/code-quality.md` (zero-tolerance policy), `knowledge/errors-registry.md` (ERR-XXX tracking)*
