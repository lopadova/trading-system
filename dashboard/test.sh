#!/usr/bin/env bash
# Wrapper for bun test
# Usage: bun test (calls this script which uses npm)

set -e

echo ""
echo "🔧 Dashboard tests require Node vitest (Bun doesn't support DOM environments)"
echo "   Running: npm test"
echo ""

npm test "$@"
