# Errors Registry — Trading System Build
*Auto-aggiornato dagli agenti. Leggi questo file all'inizio di ogni task.*

---

## ERR-TEMPLATE — Come aggiungere un errore
```
## ERR-NNN — [titolo descrittivo]
**Scoperto da**: T-XX | **Data**: YYYY-MM-DD
**Sintomo**: [errore visibile]
**Root cause**: [perché]
**Fix**: [codice corretto]
**Skill aggiornato**: [nome file]
**Impatto sui task futuri**: [T-XX, ...]
```

---

## ERR-001 — .NET SDK not installed in environment

**Scoperto da**: T-00
**Data**: 2026-04-05
**Sintomo**: `dotnet --version` returns "No .NET SDKs were found", `dotnet new` and `dotnet build` fail
**Root cause**: .NET 8 SDK is not installed on the Windows system. The dotnet runtime exists at `/c/Program Files/dotnet/dotnet` but no SDKs are available (`dotnet --list-sdks` returns empty)
**Fix**: Install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0 before attempting to build. Alternatively, all project files can be created manually (as done in T-00) and will compile once SDK is installed.
**Workaround applied in T-00**: Created all .csproj, .sln, and .cs files manually with correct structure. Files are ready to build once SDK is available.
**Skill aggiornato**: N/A (environmental issue, not code pattern)
**Impatto sui task futuri**: T-01 through T-27 will need .NET 8 SDK installed to compile and test code. User must install SDK before proceeding with implementation tasks.

---
