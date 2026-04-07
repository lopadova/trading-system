/**
 * Vitest setup file
 * Runs before all tests to configure test environment
 */

import { afterEach } from 'vitest'
import { cleanup } from '@testing-library/react'

// Cleanup after each test (unmount React components)
afterEach(() => {
  cleanup()
})

// Mock environment variables for tests
if (!import.meta.env.VITE_API_URL) {
  import.meta.env.VITE_API_URL = 'http://localhost:8787'
}

if (!import.meta.env.VITE_API_KEY) {
  import.meta.env.VITE_API_KEY = 'test-api-key-12345'
}
