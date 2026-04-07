# Development Setup Guide

Guida completa per configurare l'ambiente di sviluppo per il Trading System.

---

## 📋 Table of Contents

- [Prerequisites](#prerequisites)
- [IDE Setup](#ide-setup)
  - [Visual Studio Community (Recommended)](#visual-studio-community-recommended)
  - [Visual Studio Code (Alternative)](#visual-studio-code-alternative)
- [Clone Repository](#clone-repository)
- [Build & Test](#build--test)
- [AVIRA / Antivirus Setup](#avira--antivirus-setup)
- [Strong-Name Signing](#strong-name-signing)
- [Development Workflow](#development-workflow)
- [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required

- **Windows 10/11** (Windows Server 2019+ per produzione)
- **.NET 10 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Git** - [Download](https://git-scm.com/download/win)

### Verify Installation

```powershell
# Verifica .NET SDK
dotnet --version
# Expected: 10.0.x

# Verifica Git
git --version
# Expected: 2.x.x
```

---

## IDE Setup

### Visual Studio Community (Recommended)

**Vantaggi**:
- ✅ IDE completo per .NET (gratis per uso personale)
- ✅ Include **sn.exe** e Windows SDK tools (per firma assembly)
- ✅ Debugging avanzato, profiling, test explorer
- ✅ NuGet package manager GUI
- ✅ Risolve problema AVIRA (con firma digitale)

**Download & Install**:

1. **Download**: [Visual Studio Community 2022](https://visualstudio.microsoft.com/vs/community/)

2. **Durante installazione**, seleziona questi workload:
   - ✅ **.NET desktop development**
   - ✅ **ASP.NET and web development** (per dashboard React)
   - ⚠️ Deseleziona tutto il resto (risparmia spazio)

3. **Componenti individuali** (opzionale ma utile):
   - ✅ `.NET Framework 4.8 SDK`
   - ✅ `Windows 10 SDK` (include sn.exe)

4. **Installazione**: ~10GB, richiede ~20 minuti

**Apri Progetto**:
```powershell
# Apri solution in Visual Studio
start TradingSystem.sln

# Oppure da VS: File → Open → Project/Solution → TradingSystem.sln
```

---

### Visual Studio Code (Alternative)

**Vantaggi**:
- ✅ Leggero (~200MB)
- ✅ Veloce
- ✅ Ottimo per editing + CLI workflow
- ✅ Cross-platform

**Download & Install**:

1. **Download**: [Visual Studio Code](https://code.visualstudio.com/)

2. **Extension necessarie**:
   ```powershell
   # Installa extension C# e .NET
   code --install-extension ms-dotnettools.csharp
   code --install-extension ms-dotnettools.csdevkit
   
   # Extension opzionali utili
   code --install-extension eamodio.gitlens          # Git visualization
   code --install-extension dbaeumer.vscode-eslint   # JavaScript linting
   code --install-extension esbenp.prettier-vscode   # Code formatting
   ```

3. **Apri Progetto**:
   ```powershell
   cd "C:\path\to\trading-system"
   code .
   ```

**Note**: VS Code non include `sn.exe` - se serve firma digitale, installa Windows SDK separatamente.

---

## Clone Repository

```bash
# Clone repository
git clone https://github.com/your-username/trading-system.git
cd trading-system

# Verifica struttura
ls
# Expected: src/, tests/, scripts/, docs/, etc.
```

---

## Build & Test

### First Build

```powershell
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Expected output:
# ✅ Build succeeded.
#     0 Warning(s)
#     0 Error(s)
```

### Run Tests

```powershell
# Run all tests
dotnet test

# Expected (senza AVIRA blocking):
# ✅ Total tests: 278
#    Passed: 276
#    Failed: 2 (LogReaderWorker - known issue)
#    Pass Rate: 99.3%
```

**Note**: Se AVIRA blocca OptionsExecutionService.Tests, vedi [AVIRA Setup](#avira--antivirus-setup).

---

## AVIRA / Antivirus Setup

### Problema

AVIRA Security (o altri antivirus) possono bloccare `OptionsExecutionService.Tests.dll` con errore:
```
Un criterio di controllo dell'applicazione ha bloccato il file. (0x800711C7)
```

Questo blocca **49 test** (21% della suite).

### Soluzione 1: Aggiungi Esclusione in AVIRA

1. **Apri AVIRA** (icona system tray)
2. **Menu** → **Settings (⚙️)** → **General** → **Exceptions**
3. **Add Exception** → **Folder**
4. Seleziona: `C:\path\to\trading-system`
5. **Save**
6. **Rebuild**:
   ```powershell
   dotnet clean
   dotnet build
   dotnet test
   ```

### Soluzione 2: Strong-Name Signing (Permanente)

Firma digitale delle DLL risolve il problema con AVIRA (vedi [Strong-Name Signing](#strong-name-signing)).

### Soluzione 3: Disabilita AVIRA Temporaneamente

1. Click icona AVIRA → **Real-Time Protection**
2. **Disable for 10 minutes**
3. **SUBITO** esegui: `dotnet test`
4. AVIRA si riabilita automaticamente

### Soluzione 4: GitHub Actions (Test su Linux)

I test su Linux (GitHub Actions) non hanno AVIRA → 100% pass rate.

Vedi workflow: `.github/workflows/test-on-tag.yml`

```bash
# Crea tag per triggerare CI/CD
git tag test-$(date +%Y%m%d)
git push origin --tags

# Vedi risultati su: https://github.com/your-repo/actions
```

---

## Strong-Name Signing

### Quando Serve

- ✅ Risolve blocco AVIRA / antivirus
- ✅ Assembly firmate = riconosciute come "sicure"
- ✅ Professionale per distribuzione

### Prerequisiti

**Visual Studio Community** include `sn.exe` automaticamente in:
```
C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\VSIX\sn.exe
```

**Oppure** installa **Windows SDK** standalone:
- Download: [Windows SDK](https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/)
- Durante install: seleziona solo `.NET Framework Tools`

### Setup Automatico

```powershell
# Esegui script setup (trova sn.exe automaticamente)
.\scripts\setup-strong-name-signing.ps1

# Output atteso:
# ✅ Strong-name key generated: TradingSystem.snk
# ✅ Directory.Build.props created
# ✅ Build completed - all assemblies are now SIGNED!
# ✅ Tests running successfully with signed assemblies! 🎉
```

### Cosa Fa lo Script

1. ✅ Cerca `sn.exe` nel sistema
2. ✅ Genera `TradingSystem.snk` (chiave privata - **NON committare su git**)
3. ✅ Crea `Directory.Build.props` con configurazione firma
4. ✅ Rebuild con firma digitale
5. ✅ Verifica assemblies firmate
6. ✅ Esegue test

### Verifica Firma

```powershell
# Verifica assembly firmata
sn -v src\SharedKernel\bin\Debug\net10.0\SharedKernel.dll

# Expected:
# Microsoft (R) .NET Framework Strong Name Utility  Version 4.0.30319.0
# Copyright (c) Microsoft Corporation.  All rights reserved.
#
# Assembly 'SharedKernel.dll' is valid
```

### Security Note

⚠️ **IMPORTANTE**: `TradingSystem.snk` è la chiave **PRIVATA**
- ❌ **NON committare su git** (già in `.gitignore`)
- ❌ **NON condividere** pubblicamente
- ✅ Backup sicuro (password manager, encrypted storage)

---

## Development Workflow

### Daily Workflow

```powershell
# 1. Pull latest changes
git pull origin main

# 2. Create feature branch
git checkout -b feature/your-feature-name

# 3. Make changes (usa VS o VS Code)

# 4. Build
dotnet build

# 5. Run tests
dotnet test

# 6. Commit (se test passano)
git add .
git commit -m "feat: your feature description"

# 7. Push
git push origin feature/your-feature-name

# 8. Create PR su GitHub
```

### Before Committing

```powershell
# Verifica build + test
dotnet build && dotnet test

# Verifica no files sensibili
git status

# Verifica .gitignore corretto
cat .gitignore | grep -E "snk|secrets|appsettings.Production"
```

---

## Troubleshooting

### Build Errors

#### Error: "Cannot find project or solution"

```powershell
# Verifica sei nella directory corretta
ls TradingSystem.sln

# Se non c'è, vai nella root
cd C:\path\to\trading-system
```

#### Error: "SDK not found"

```powershell
# Verifica .NET SDK installato
dotnet --version

# Se manca, scarica da:
# https://dotnet.microsoft.com/download/dotnet/10.0
```

#### Warning: "TreatWarningsAsErrors"

Il progetto ha `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` - i warning sono errori!

Risolvi tutti i warning prima di committare.

---

### Test Failures

#### OptionsExecutionService.Tests: 0x800711C7

**Causa**: AVIRA blocca DLL

**Soluzione**: Vedi [AVIRA Setup](#avira--antivirus-setup)

#### LogReaderWorker tests fail

**Causa**: Worker timing issue (known issue, low priority)

**Soluzione**: Ignora o esegui su GitHub Actions (passano su Linux)

#### TradingSupervisorService tests timeout

**Causa**: Worker BackgroundService richiede tempo

**Soluzione**: Aumenta timeout in test, oppure esegui singolarmente:
```powershell
dotnet test --filter "FullyQualifiedName~WorkerName"
```

---

### AVIRA Issues

#### "File bloccato anche dopo esclusione"

**Soluzione**:
1. Clean + Rebuild (AVIRA potrebbe aver "marchiato" DLL vecchia)
   ```powershell
   dotnet clean
   dotnet build
   ```

2. Controlla quarantena AVIRA e ripristina file

3. Usa firma digitale (soluzione permanente)

---

### Git Issues

#### "Permission denied" su push

**Causa**: Credenziali git non configurate

**Soluzione**:
```bash
# Configure git credentials
git config --global user.name "Your Name"
git config --global user.email "your.email@example.com"

# Setup credential helper
git config --global credential.helper wincred
```

---

## Additional Resources

### Documentation

- **Architecture**: [docs/ARCHITECTURE_OVERVIEW.md](docs/ARCHITECTURE_OVERVIEW.md)
- **Deployment**: [DEPLOYMENT.md](DEPLOYMENT.md)
- **Configuration**: [docs/CONFIGURATION.md](docs/CONFIGURATION.md)
- **Troubleshooting**: [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)
- **AVIRA Setup**: [WINDOWS_DEFENDER_UNLOCK.md](WINDOWS_DEFENDER_UNLOCK.md)

### Knowledge Base

- **Errors Registry**: [knowledge/errors-registry.md](knowledge/errors-registry.md) - 11+ documented errors
- **Lessons Learned**: [knowledge/lessons-learned.md](knowledge/lessons-learned.md) - 156+ lessons
- **Skills**: [.claude/skills/](.claude/skills/) - Coding patterns

### CI/CD

- **GitHub Actions**: [.github/workflows/](.github/workflows/)
  - `test-on-tag.yml` - Test suite su Linux (no AVIRA!)
  - Trigger: `git tag test-YYYYMMDD && git push origin --tags`

---

## Quick Reference

### Essential Commands

```powershell
# Build
dotnet build

# Test
dotnet test

# Test specifico
dotnet test --filter "FullyQualifiedName~TestName"

# Clean
dotnet clean

# Restore
dotnet restore

# Watch (auto-rebuild on changes)
dotnet watch build
```

### Project Structure

```
trading-system/
├── src/
│   ├── SharedKernel/           # Core domain + utilities
│   ├── OptionsExecutionService/ # Trading execution
│   └── TradingSupervisorService/ # Monitoring + alerts
├── tests/
│   ├── SharedKernel.Tests/
│   ├── OptionsExecutionService.Tests/
│   └── TradingSupervisorService.Tests/
├── dashboard/                   # React dashboard
├── scripts/                     # Automation scripts
├── docs/                        # Documentation
├── knowledge/                   # Knowledge base
└── TradingSystem.sln           # Solution file
```

### Test Coverage

```
✅ SharedKernel: 58/58 (100%)
⚠️ OptionsExecutionService: 49/49 (blocked by AVIRA without signing)
⚠️ TradingSupervisorService: 169/171 (98.8%)

TOTAL: 276/278 (99.3%) with signing
       227/229 (99.1%) without AVIRA fix
```

---

## Support

- **Issues**: [GitHub Issues](https://github.com/your-repo/issues)
- **Knowledge Base**: [knowledge/](knowledge/)
- **Documentation**: [docs/](docs/)

---

**Happy Coding!** 🚀
