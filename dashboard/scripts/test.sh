#!/usr/bin/env bash
# Wrapper script for bun test
# Bun vitest doesn't support DOM environments, so we use npm (Node vitest) instead

echo "📦 Running tests with Node vitest (Bun doesn't support DOM environments)..."
npm test
