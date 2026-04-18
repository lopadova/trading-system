import { defineWorkersConfig } from '@cloudflare/vitest-pool-workers/config'

/**
 * Vitest config for integration tests
 * Run with: bun run test:integration
 *
 * These tests require:
 * - @cloudflare/vitest-pool-workers package
 * - Wrangler D1 local environment
 * - cloudflare:test imports for env and SELF
 */
export default defineWorkersConfig({
  test: {
    // Only run integration tests (renamed to avoid auto-discovery)
    include: [
      'test/integration/**/*-spec.ts',
    ],
    poolOptions: {
      workers: {
        wrangler: { configPath: './wrangler.toml' },
      },
    },
  },
})
