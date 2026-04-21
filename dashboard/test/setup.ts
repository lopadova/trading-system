/**
 * Vitest setup file
 * Runs before all tests to configure test environment
 */

import { afterEach, expect } from 'vitest'
import { cleanup } from '@testing-library/react'
import * as matchers from '@testing-library/jest-dom/matchers'

// Extend vitest matchers with jest-dom
expect.extend(matchers)

// Install an in-memory localStorage/sessionStorage shim.
// Node 25 + happy-dom 20 interact poorly: native Node `localStorage`
// (behind --localstorage-file) collides with the happy-dom one and the
// result is a global that lacks `clear()`/`removeItem`. Replace both
// with a deterministic in-memory implementation for tests.
function createMemoryStorage(): Storage {
  let store: Record<string, string> = {}
  return {
    get length(): number { return Object.keys(store).length },
    clear(): void { store = {} },
    getItem(key: string): string | null { return Object.prototype.hasOwnProperty.call(store, key) ? store[key] : null },
    key(index: number): string | null { return Object.keys(store)[index] ?? null },
    removeItem(key: string): void { delete store[key] },
    setItem(key: string, value: string): void { store[key] = String(value) },
  }
}

Object.defineProperty(globalThis, 'localStorage', { value: createMemoryStorage(), configurable: true, writable: true })
Object.defineProperty(globalThis, 'sessionStorage', { value: createMemoryStorage(), configurable: true, writable: true })

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
