---
name: obsidian-docs
description: Authoring, editing, and archiving docs under the docs/ Obsidian vault — frontmatter, tag vocabulary, wiki-links, INDEX.md maintenance, archive decisions
trigger: Automatic when creating or substantially editing any .md file under docs/**, including docs/INDEX.md
---

# Obsidian Docs Authoring Skill

## Purpose

Keep the `docs/` folder behaving as a coherent Obsidian wiki:
- Frontmatter is uniform and honest (tags from a closed vocabulary, dates current).
- Wiki-links `[[Title]]` resolve bidirectionally.
- `docs/INDEX.md` stays the single entry point for Obsidian users.
- Archiving happens via `git mv`, never delete.
- The `.claude/rules/documentation.md` rules get applied mechanically.

Invoke this skill BEFORE you write or restructure any doc.

---

## 1. Decision tree — where does this doc go?

```
You're about to create / edit a markdown doc. Ask in order:

1. Is this an operational procedure, runbook, SLO, incident playbook?
   → docs/ops/<NAME>.md

2. Is this a paper-run daily log?
   → docs/ops/paper-run/YYYY-MM-DD.md (copy from TEMPLATE.md)

3. Is this architecture, reference, onboarding, workflow, or config?
   → docs/<NAME>.md

4. Is this a DR drill report?
   → docs/ops/dr-drills/YYYY-QX.md

5. Is this a session/status/final report for work just done?
   → Usually the answer is "don't write it at all".
     The PR description + git log + knowledge/lessons-learned.md
     already cover it. If it's genuinely useful:
   → docs/archive/dev-sessions/YYYY-MM-DD-<topic>/

6. Is this a README for a subfolder (dashboard/, scripts/, tests/)?
   → KEEP IT there, but add frontmatter per section 3.

7. Is this content already covered by an existing doc?
   → EDIT THAT DOC — do not fork. Use a new H2 section if needed.

NEVER create a new *.md at the repo root. Root is
strictly: README.md, CLAUDE.md, CHANGELOG-SCRIPTS.md.
```

## 2. Frontmatter template

Paste this at the top of EVERY new doc under `docs/**`:

```yaml
---
title: ""
tags: []
aliases: []
status: current
audience: []
phase: ""
last-reviewed: ""
related: []
---
```

Then fill each field using the decision tree below.

### 2.1 `title:` (required)

Human-readable. What appears in Obsidian's graph view and the page
header. DO NOT paste the filename.

| Good | Bad |
|------|-----|
| `"Go-Live Procedure"` | `"GO_LIVE"` |
| `"Secret Rotation and Recovery"` | `"SECRETS"` |
| `"Semaphore Gate Architecture"` | `"semaphore-gate"` |

### 2.2 `tags:` (1-3 from the closed vocabulary — RULE 2)

Closed vocabulary:

- **Domain** (pick exactly 1): `ops` · `dev` · `architecture` · `security` · `testing` · `onboarding` · `reference`
- **Layer** (pick 0-1): `dashboard` · `worker` · `dotnet` · `infra` · `ibkr`
- **Activity** (pick 0-1): `deployment` · `observability` · `safety` · `release` · `incident-response`
- **Lifecycle** (pick 0-1): `knowledge-base` · `runbook` · `workflow`

Common combinations:

| Doc type | Suggested tags |
|----------|----------------|
| Runbook playbook | `["ops", "runbook", "incident-response"]` |
| SLO definition | `["ops", "observability"]` |
| Release procedure | `["ops", "release", "deployment"]` |
| Architecture doc | `["architecture", "reference"]` |
| Onboarding guide | `["onboarding", "dev"]` |
| Worker API reference | `["reference", "worker"]` |
| Testing strategy | `["testing", "dev"]` |
| Secret rotation | `["ops", "security"]` |

If none of the combinations fits: pick the closest, don't invent a
new tag. If a genuinely new tag is needed, extend the vocabulary in
`.claude/rules/documentation.md` AND retag existing docs that match
(same branch / same PR).

### 2.3 `aliases:` (0-2 short names Obsidian uses for link resolution)

If readers might type a shorter form in a wiki-link, list it here.
Example for `docs/ops/GO_LIVE.md`:

```yaml
aliases: ["Go-Live", "Production Flip"]
```

Now `[[Go-Live]]` and `[[Production Flip]]` both resolve to this doc.

### 2.4 `status:` (one of four)

| Value | Meaning | Obsidian usage |
|-------|---------|----------------|
| `current` | Actively maintained, safe to follow | Default — most docs |
| `reference` | Stable, rarely updated (architecture, spec) | Same — just signals low edit cadence |
| `draft` | In-progress, not yet authoritative | Show a disclaimer at the top of the body |
| `superseded` | Kept for audit but replaced by another doc | Move to `docs/archive/` instead (rule 7) |

`superseded` is a transitional state — if you tag a doc this way,
either migrate its readers to the successor doc and archive, or
merge its delta into the successor and archive.

### 2.5 `audience:` (1-2 — who reads this)

| Value | Meaning |
|-------|---------|
| `operator` | Lorenzo running the system day-to-day |
| `developer` | Anyone editing code |
| `reviewer` | Code reviewer reading a PR |
| `ai-agent` | Claude / subagent consuming the doc |
| `new-user` | First-time repo visitor |

Example: `GO_LIVE.md` is `["operator"]`. `ARCHITECTURE.md` is
`["developer", "reviewer"]`. `ONBOARDING.md` is `["new-user", "developer"]`.

### 2.6 `phase:`

Which Phase (or feature) introduced or last substantially updated
this doc. Omit for perennial docs.

Values seen so far: `phase-7.1`, `phase-7.2`, ... `phase-7.7`,
`phase-8+` (future), or feature-specific like `wizard-v1`.

When a new phase substantially rewrites a doc, UPDATE this field.

### 2.7 `last-reviewed:`

ISO date of the last substantive read-through. Update:
- On every content edit (not typos/formatting)
- After a clean read-through that confirms currency
- As part of quarterly doc-review sweeps

The LLM / operator can trust `last-reviewed: "2026-04-22"` more than
a doc that hasn't been reviewed in 6 months.

### 2.8 `related:` — BIDIRECTIONAL

Max 6 wiki-links to the genuinely related docs. Rules:

1. Every entry MUST be a wiki-link `"[[Title]]"`.
2. If `A.related` contains `[[B]]`, then `B.related` MUST contain `[[A]]`.
3. Don't list everything you mention — list what a reader genuinely
   needs to know after reading this doc.
4. If `related:` would be empty, that's fine — some docs are islands.

## 3. Component READMEs (dashboard/, scripts/, tests/, etc.)

Frontmatter is MANDATORY on these too. Use:

```yaml
---
title: "Dashboard — React 19 + Vite"
tags: ["dev", "dashboard"]
aliases: ["Dashboard README"]
status: current
audience: ["developer"]
last-reviewed: "YYYY-MM-DD"
related:
  - "[[Architecture Overview]]"
  - "[[Getting Started]]"
---
```

## 4. Wiki-link syntax cheat sheet

```markdown
[[Title]]                    → link with Title as the visible text
[[Title|custom text]]        → link with custom visible text
[[Title#Section]]            → link to a specific H2/H3 inside Title
[[Title#Section|custom]]     → same with custom text
```

What to wiki-link:
- Cross-doc refs INSIDE `docs/**` → always wiki-links
- Refs TO code/scripts/config → relative Markdown link:
  ```markdown
  See `scripts/backup-d1.sh` or
  [`SemaphoreGate.cs`](../../src/OptionsExecutionService/Services/SemaphoreGate.cs)
  ```
- Refs TO external URLs → standard Markdown link

## 5. INDEX.md update checklist

Every time you CREATE a new doc under `docs/**`:

```
- [ ] Pick the right audience section in docs/INDEX.md:
    - Operator: DAILY_OPS, RUNBOOK, SECRETS, GO_LIVE, RELEASE, ...
    - Developer: GETTING_STARTED, CONTRIBUTING, ...
    - Reference: ARCHITECTURE, STRATEGY_FORMAT, ...
    - Knowledge base: errors-registry, lessons-learned
    - Incident response: RUNBOOK playbooks, DR, GO_LIVE § 4
    - Meta: DOCUMENTATION_SYSTEM, archive
- [ ] Add one-line entry: `- [[Title]] — <one-line purpose>`
- [ ] Keep alphabetical inside the section (where it makes sense)
```

Every time you ARCHIVE a doc:

```
- [ ] Remove the INDEX.md entry
- [ ] `git grep '[[<Title>]]' docs/` — find all wiki-link referrers
- [ ] For each referrer: update or remove the broken link
```

## 6. Archive decision matrix

```
You're about to delete a doc because it feels outdated. STOP.

Ask:
1. Is there NEW content in it that nowhere else captures?
   YES → merge the delta into the successor doc FIRST, then archive.
   NO → proceed.

2. Does ANY other doc reference it by wiki-link or path?
   YES → update the referrers FIRST, then archive.
   NO → proceed.

3. Is it genuinely obsolete (superseded, wrong, or stale)?
   Archive path:
   - Feature shipped → docs/archive/completed-features/<feature>/
   - Brainstorm / plan → docs/archive/dev-sessions/<topic>/
   - Phase history / status report → docs/archive/phase-N-to-M-history/

4. Use `git mv` — never `rm`. Git history is the audit trail.
```

## 7. Verification commands (run before committing)

```bash
# 1. All docs have frontmatter (except exceptions)
find docs -name "*.md" -not -path "docs/archive/*" \
  -exec sh -c 'head -1 "$1" | grep -q "^---$" || echo "MISSING: $1"' _ {} \;

# 2. No broken wiki-links (quick heuristic — spot-check rendered)
grep -rnoE '\[\[[^\]]+\]\]' docs/ | grep -v 'docs/archive/' \
  | head -20
# Visually verify each Title resolves against an existing title: or alias:.

# 3. Root stays clean (should print exactly 3 files)
ls *.md 2>/dev/null | wc -l      # expect 3
ls *.md                           # expect README.md CLAUDE.md CHANGELOG-SCRIPTS.md

# 4. No sneak tags (outside the closed vocabulary)
grep -rhE '^tags:' docs/ | sort -u
# Visually verify every tag is in the vocabulary table.
```

## 8. Common mistakes & fixes

| Mistake | Fix |
|---------|-----|
| Created `SESSION_REPORT_X.md` at repo root | `git mv` to `docs/archive/dev-sessions/YYYY-MM-DD-<topic>/`, or just delete (it's session scrap) |
| Added frontmatter with `tags: ["cool-new-tag"]` | Use the closed vocabulary. If you really need a new tag, extend `.claude/rules/documentation.md` in the same PR and retag existing docs |
| Linked with `[Go Live](./GO_LIVE.md)` inside docs/ | Convert to `[[GO_LIVE]]` or `[[Go-Live Procedure]]` |
| `related: ["[[A]]"]` in X.md but not `[[X]]` in A.md | Add the back-link to A.md |
| Edited a doc but didn't touch `last-reviewed:` | Bump to today's date |
| Archived a doc but left its INDEX.md entry | Remove from INDEX and fix wiki-links |
| New doc but forgot INDEX.md entry | Add it, one-line purpose, correct audience section |

## 9. When in doubt

1. Grep for similar docs: `ls docs/ops/ | head` — pick the closest existing sibling as a formatting template.
2. Re-read `.claude/rules/documentation.md` — the rules it encodes ARE the spec.
3. If the rule book doesn't cover your case, add the case to THIS skill (same branch / same PR) so the next time it's documented.

---

## Self-check before returning

After writing or editing, verify:

- [ ] Frontmatter present with all required fields
- [ ] Tags from the closed vocabulary (1-3 total)
- [ ] Wiki-links used for cross-doc refs
- [ ] `related:` is bidirectional (update the other end if needed)
- [ ] INDEX.md updated (new entry OR removed entry)
- [ ] `last-reviewed:` = today's date if content changed
- [ ] No new `.md` at the repo root
- [ ] Archive decisions used `git mv` not `rm`

If any box is unchecked, fix before committing.

---

*Related rules: `.claude/rules/documentation.md` (the WHAT).*
*Historical: PR #13 (2026-04-22) established this structure.*
