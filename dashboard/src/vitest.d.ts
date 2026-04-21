/// <reference types="vitest" />
/// <reference types="@testing-library/jest-dom" />

import type { TestingLibraryMatchers } from '@testing-library/jest-dom/matchers'

declare module 'vitest' {
  // Extend vitest's Assertion and AsymmetricMatchersContaining interfaces with
  // jest-dom matchers. These declarations must be interfaces (module augmentation
  // merging) and intentionally declare no new members — they inherit everything
  // from TestingLibraryMatchers.
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type
  interface Assertion<T = unknown> extends TestingLibraryMatchers<T, void> {}
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type
  interface AsymmetricMatchersContaining extends TestingLibraryMatchers {}
}
