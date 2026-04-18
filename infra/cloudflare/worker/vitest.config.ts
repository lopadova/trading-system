import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    // Exclude integration tests that require cloudflare:test
    // Run those separately with: bun run test:integration
    exclude: [
      '**/node_modules/**',
      '**/dist/**',
      '**/test/integration/**',  // Integration tests (require cloudflare:test package)
    ],
  },
})
