# Wrapper script for bun test on Windows
# Bun vitest doesn't support DOM environments, so we use npm (Node vitest) instead

Write-Host "📦 Running tests with Node vitest (Bun doesn't support DOM environments)..." -ForegroundColor Cyan
npm test
