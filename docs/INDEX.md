---
title: Trading System Docs — Index
tags:
  - overview
aliases:
  - Wiki Home
  - Docs Index
status: current
last-reviewed: 2026-04-21
---

# Trading System Knowledge Base

Opened in Obsidian? Right-click the `docs/` folder → "Open as vault". All cross-document references use Obsidian wiki-link syntax (`[[Target]]`); the canonical names match the `title:` / `aliases:` fields in each note's frontmatter.

Repo root README: [Repository Overview](../README.md) — lives outside the `docs/` vault, so a relative Markdown link instead of a wiki-link (Obsidian vaults don't resolve `[[...]]` across their root folder).

---

## By audience

### Operator (day-to-day, running the system)

- [[DAILY_OPS]] — morning / midday / EOD rounds
- [[RUNBOOK]] — incident playbooks (1-9)
- [[SECRETS]] — secret rotation pipeline
- [[GO_LIVE]] — paper → live flip, with rollback
- [[RELEASE]] — tag-based release procedure
- [[DR]] — disaster recovery
- [[SLO]] — service-level objectives
- [[PAPER_VALIDATION]] — 14-day paper-run procedure
- [[OBSERVABILITY]] — logs, metrics, Sentry, uptime
- [[Windows Defender Unlock - Complete Guide|WINDOWS_DEFENDER]] — AVIRA / Defender setup

### Developer (building new features)

- [[Getting Started with Trading System|GETTING_STARTED]]
- [[Trading System - Developer Onboarding|ONBOARDING]] (30-minute tour)
- [[Contributing Guide|CONTRIBUTING]]
- [[Configuration Reference|CONFIGURATION]] + [[Configuration Checklist|CONFIGURATION-CHECKLIST]]
- [[Bot Setup Guide - Telegram & Discord Integration|BOT_SETUP_GUIDE]]
- [[Trading System Scripts|Scripts README]]

**Feature workflow** (Italian / English):
- [[Quick Start — Nuova Feature|QUICK-START-NUOVA-FEATURE]] — quick ref
- [[Workflow Nuove Feature — Guida Completa|WORKFLOW-NUOVE-FEATURE]] — full procedure
- [[FAQ — Workflow Nuove Feature|FAQ-WORKFLOW]] — Q&A
- [[Template Prompt per Brainstorming Nuove Feature|BRAINSTORMING-PROMPT-TEMPLATE]] (input)
- [[Brainstorming Output Template|brainstorming-output-template]] (AI output)

### Reference (architecture, contracts, formats)

- [[Trading System - Architecture Overview|ARCHITECTURE_OVERVIEW]] — quick overview
- [[Trading System Architecture|ARCHITECTURE]] — deep-dive
- [[MARKET_DATA_PIPELINE]] — ingestion path (Phase 7.1)
- [[Strategy File Format|STRATEGY_FORMAT]] — SDF schema
- [[Trading System - Deployment Guide|DEPLOYMENT_GUIDE]]
- [[Telegram Alert Integration|telegram-integration]] — .NET side
- [[Trading System Cloudflare Worker|Worker README]]
- [[Trading System Dashboard|Dashboard README]]
- [[LOAD_TESTING]]
- [[Trading System - End-to-End Test Plan|TEST_PLAN]]
- [[Troubleshooting Guide|TROUBLESHOOTING]]

---

## Knowledge base (append-only logs)

- [[errors-registry]] — documented errors with root causes and fixes
- [[lessons-learned]] — development lessons
- [[skill-changelog]] — changelog of internal skill files
- [[Guida Bash → PowerShell|bash-to-powershell]] — shell-portability reference

---

## Incident response (unhappy paths)

- [[RUNBOOK]] — playbooks 1-9 (Worker 5xx, dashboard stale, IBKR down, D1 quota, Sentry flood, …)
- [[DR]] — disaster recovery
- [[GO_LIVE]] § 4 — rollback (paper → live abort)
- [[Troubleshooting Guide|TROUBLESHOOTING]] — general troubleshooting
- [[Windows Defender Unlock - Complete Guide|WINDOWS_DEFENDER]] — antivirus-related build/test failures

---

## Release & governance

- [[RELEASE]] — release procedure
- [[BRANCH_PROTECTION]] — GitHub UI settings
- [[Phase 7 Completion Report|PHASE_7_COMPLETION_REPORT]] — what shipped in Phase 7

---

## Meta

- [[Documentation System|DOCUMENTATION_SYSTEM]] — how this doc estate is maintained
- [[GitHub Copilot Instructions - Trading System|Copilot Instructions]]
- `docs/archive/` — history preserved (open the folder directly; not part of the indexed wiki):
  - `docs/archive/phase-0-to-7-history/` — pre-Phase-7 session reports + superseded docs
  - `docs/archive/completed-features/` — specs for shipped features (e.g., wizard-strategies-and-bot)
  - `docs/archive/dev-sessions/` — brainstorm notes / plans

---

## Tag cheat-sheet

Domain: `ops`, `dev`, `architecture`, `security`, `testing`, `onboarding`, `reference`
Layer: `dashboard`, `worker`, `dotnet`, `infra`, `ibkr`
Activity: `deployment`, `observability`, `safety`, `release`, `incident-response`
Lifecycle: `knowledge-base`, `runbook`, `workflow`
