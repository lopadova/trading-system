# Documentation Rules
> Last updated: 2026-04-22

**THESE RULES ARE LOADED AUTOMATICALLY IN EVERY SESSION**

---

## Obsidian-Compatible Docs Wiki (post PR #13)

The `docs/` folder is an Obsidian vault. Every file created or edited
MUST comply with the rules below. For the HOW-TO (templates, examples,
checklists), invoke `skill-obsidian-docs` — this section is WHAT.

### RULE 0: Location — NEVER put new docs at the repo root

The repo root contains ONLY:
- `README.md` (project landing page)
- `CLAUDE.md` (project rules, consumed by Claude)
- `CHANGELOG-SCRIPTS.md` (scripts/ change log)

Everything else goes under `docs/`:

| Doc type | Location |
|----------|----------|
| Operational runbook / procedure / SLO | `docs/ops/` |
| Architecture / reference / onboarding | `docs/` |
| Paper-run daily logs | `docs/ops/paper-run/` |
| Obsolete / historical docs | `docs/archive/` (via `git mv`, never delete) |

**Never** create session reports / status reports / final reports at
the repo root. Phase 7.x accumulated 17+ such files before the PR #13
cleanup; don't regress. If a session summary is genuinely useful,
put it under `docs/archive/dev-sessions/<topic>/` per RULE 7 — otherwise
skip it (conversation context + `git log` are the primary history).

### RULE 1: Frontmatter is mandatory on all `.md` files except the exclusions below

Exceptions (NO frontmatter): `.claude/`, `docs/archive/**`, `CLAUDE.md`,
GitHub Issue Templates, files inside `node_modules/`.

Any other `.md` file (including `README.md` at repo root in its
minimal form AND component READMEs under `dashboard/`, `scripts/`,
`tests/`, `infra/cloudflare/worker/` etc.) MUST start with:

```yaml
---
title: "Human-Readable Title"
tags: ["ops", "deployment"]       # 1-3 from the closed vocabulary (RULE 2)
aliases: ["Short Name"]           # 0-2 wiki-link alternatives
status: current                   # current | reference | draft | superseded
audience: ["operator"]            # 1-2: operator | developer | reviewer | ai-agent | new-user
phase: "phase-7.7"                # omit for perennial docs
last-reviewed: "YYYY-MM-DD"       # update to TODAY on substantive edit
related:                          # 0-6 BIDIRECTIONAL wiki-links
  - "[[RelatedDoc]]"
---
```

### RULE 2: Closed tag vocabulary — no free-form tags

Pick 1-3 total from:

- **Domain** (1 required): `ops` · `dev` · `architecture` · `security` · `testing` · `onboarding` · `reference`
- **Layer** (0-1): `dashboard` · `worker` · `dotnet` · `infra` · `ibkr`
- **Activity** (0-1): `deployment` · `observability` · `safety` · `release` · `incident-response`
- **Lifecycle** (0-1): `knowledge-base` · `runbook` · `workflow`

Adding a new tag requires updating this rule AND retagging existing
docs that would match. Do NOT sneak in one-off tags.

### RULE 3: Wiki-links for cross-doc refs, relative links for code

Cross-doc references inside `docs/**` use wiki-link syntax:

```markdown
See [[GO_LIVE]] for the paper → live flip.
See [[SECRETS|secret rotation]] for the 90-day cadence.
```

`[[Title]]` resolves against `title:` AND `aliases:` in other docs.

For links to **code / scripts / config** (non-doc files), use
standard relative Markdown links with the full relative path:

```markdown
See `scripts/backup-d1.sh` or
[`SemaphoreGate.cs`](../../src/OptionsExecutionService/Services/SemaphoreGate.cs).
```

### RULE 4: `related:` must be bidirectional

If `A.md` frontmatter has `related: [[B]]`, then `B.md` MUST have
`related: [[A]]`. One-way links create orphan pages in Obsidian graph
view. Max 6 entries per list — prune the genuinely tangential.

### RULE 5: Update `docs/INDEX.md` when adding or removing a doc

`docs/INDEX.md` is the Obsidian home page organized by audience. Every
NEW doc under `docs/**` MUST be added under the appropriate section
with a one-line purpose. When archiving a doc, REMOVE its INDEX
entry — a dangling link is worse than none.

### RULE 6: `last-reviewed` discipline

Update `last-reviewed:` to **today's date** whenever you:
- substantively edit the doc (not typos or formatting)
- re-read and confirm it's still accurate

Bumping only `last-reviewed:` after a clean read is valid — it tells
the next reader "this was verified current on that date".

### RULE 7: Archive, don't delete

When a doc becomes obsolete, `git mv` it to one of:

- `docs/archive/phase-N-to-M-history/` — historical session/status reports
- `docs/archive/completed-features/<feature>/` — specs for shipped features
- `docs/archive/dev-sessions/<topic>/` — superseded brainstorm / plan drafts

Then `git grep` for referrers and update them (update or remove the
dead link). Deletion is only appropriate when the content is fully
recreated elsewhere — even then, prefer the archive path for audit
reasons.

### RULE 8: Invoke `skill-obsidian-docs` for any doc work

WHEN to invoke: before creating OR substantively editing any `.md`
under `docs/**` (including INDEX.md). The skill contains:

- Frontmatter template with decision tree for each field
- Tag-vocabulary picker
- Wiki-link syntax cheat sheet
- INDEX.md update checklist
- Archive-vs-rewrite decision matrix
- Cross-link verification command

Do NOT reinvent the procedure — the skill encodes the consensus from
PR #13's docs restructure. If the skill is missing a case you need,
extend the skill file (same branch) rather than freestyling.

---

## Mandatory Documentation Sync

### RULE 1: Test Command Consistency

**ALWAYS** use these exact commands in ALL documentation:

| Component | Command | Notes |
|-----------|---------|-------|
| Dashboard | `npm test` | ⚠️ REQUIRED (NOT `bun test`) - Bun doesn't support DOM |
| Worker | `bun test` | Runs unit + integration tests |
| .NET Services | `dotnet test` | Full test suite |

**When editing test instructions**:
1. Search for test commands: `grep -r "test" --include="*.md" README.md docs/ dashboard/ infra/`
2. Update ALL locations
3. Always include the warning for dashboard: "REQUIRED: use npm, Bun doesn't support DOM"

### RULE 2: README Hierarchy

```
README.md (root)                 ← SOURCE OF TRUTH
  ↓
docs/DEPLOYMENT_GUIDE.md         ← Deployment procedures
docs/ARCHITECTURE.md             ← Architecture details
docs/GETTING_STARTED.md          ← Quick start
  ↓
Component READMEs                ← Component-specific
  dashboard/README.md
  infra/cloudflare/worker/README.md
  scripts/README.md
```

**When updating**:
- Root README → Sync to component READMEs
- Component README → Check if root needs update
- Never contradict root README

### RULE 3: Before Committing Docs

**Checklist** (ALL required):
- [ ] Searched related content: `grep -r "keyword" --include="*.md"`
- [ ] Updated all locations with same information
- [ ] Verified command syntax identical across files
- [ ] Checked version numbers consistent
- [ ] No conflicting information

### RULE 4: Critical Warnings

**ALWAYS preserve these warnings**:

1. **Dashboard testing**:
   ```markdown
   ⚠️ IMPORTANT: Dashboard tests MUST use `npm test` (NOT `bun test`)
   ```

2. **TradingMode**:
   ```markdown
   ⚠️ CRITICAL: Always verify TradingMode = "paper" before deployment
   ```

3. **Secrets**:
   ```markdown
   ⚠️ NEVER commit secrets (API keys, tokens) to git
   ```

### RULE 5: Invoke Documentation Skill

**WHEN**: Editing ANY .md file

**ACTION**: Use `Skill tool` → `skill-documentation-sync` to check consistency

---

*These rules prevent documentation drift and ensure all guides stay synchronized*
