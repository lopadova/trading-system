/**
 * Simple unit test runner for bot functions
 * Runs tests in Node.js (not Cloudflare Workers runtime)
 */

import { isWhitelisted } from '../../infra/cloudflare/worker/src/bot/auth.ts'
import { parseCommand, parseCallbackData } from '../../infra/cloudflare/worker/src/bot/dispatcher.ts'
import { t, detectLanguage, messages } from '../../infra/cloudflare/worker/src/bot/i18n.ts'

let testsPassed = 0
let testsFailed = 0
const failedTests = []

function test(name, fn) {
  try {
    fn()
    console.log(`✅ ${name}`)
    testsPassed++
  } catch (error) {
    console.log(`❌ ${name}`)
    console.log(`   Error: ${error.message}`)
    testsFailed++
    failedTests.push(name)
  }
}

function expect(value) {
  return {
    toBe(expected) {
      if (value !== expected) {
        throw new Error(`Expected ${JSON.stringify(expected)}, got ${JSON.stringify(value)}`)
      }
    },
    toEqual(expected) {
      const valueStr = JSON.stringify(value)
      const expectedStr = JSON.stringify(expected)
      if (valueStr !== expectedStr) {
        throw new Error(`Expected ${expectedStr}, got ${valueStr}`)
      }
    },
    toContain(substring) {
      if (!value.includes(substring)) {
        throw new Error(`Expected "${value}" to contain "${substring}"`)
      }
    }
  }
}

console.log('=== Bot Unit Tests ===\n')

// Auth tests
console.log('Bot Auth Functions:')
test('TEST-BOT-01-03: isWhitelisted returns true for whitelisted user', () => {
  expect(isWhitelisted('123456789', '123456789')).toBe(true)
})

test('TEST-BOT-01-04: isWhitelisted returns false for non-whitelisted user', () => {
  expect(isWhitelisted('999', '123456789')).toBe(false)
})

test('isWhitelisted handles multiple users', () => {
  expect(isWhitelisted('456', '123, 456, 789')).toBe(true)
})

test('isWhitelisted handles whitespace', () => {
  expect(isWhitelisted('789', '  123  ,  789  ,  456  ')).toBe(true)
})

test('isWhitelisted returns false for empty whitelist', () => {
  expect(isWhitelisted('123', '')).toBe(false)
})

console.log('')

// Command parsing tests
console.log('Bot Command Parsing:')
test('TEST-BOT-01-05: parseCommand parses /portfolio correctly', () => {
  expect(parseCommand('/portfolio')).toEqual({ type: 'query', query: 'portfolio' })
})

test('TEST-BOT-01-06: parseCommand parses /start as menu', () => {
  expect(parseCommand('/start')).toEqual({ type: 'menu' })
})

test('parseCommand handles /menu', () => {
  expect(parseCommand('/menu')).toEqual({ type: 'menu' })
})

test('parseCommand handles /status', () => {
  expect(parseCommand('/status')).toEqual({ type: 'query', query: 'status' })
})

test('parseCommand handles unknown command', () => {
  expect(parseCommand('/unknown')).toEqual({ type: 'unknown', raw: '/unknown' })
})

test('TEST-BOT-01-07: parseCallbackData parses detail:campaign:abc-123', () => {
  expect(parseCallbackData('detail:campaign:abc-123')).toEqual({
    type: 'detail',
    entity: 'campaign',
    id: 'abc-123'
  })
})

test('parseCallbackData parses query:portfolio', () => {
  expect(parseCallbackData('query:portfolio')).toEqual({ type: 'query', query: 'portfolio' })
})

console.log('')

// i18n tests
console.log('Bot i18n Functions:')
test('t returns English translation by default', () => {
  const result = t('menu_title')
  expect(result).toContain('Trading System')
  expect(result).toContain('Main Menu')
})

test('t returns Italian translation when specified', () => {
  const result = t('menu_title', 'it')
  expect(result).toContain('Trading System')
  expect(result).toContain('Menu Principale')
})

test('t replaces parameters', () => {
  const result = t('error_generic', 'en', { message: 'Test error' })
  expect(result).toContain('Test error')
})

test('detectLanguage returns it for Italian codes', () => {
  expect(detectLanguage('it')).toBe('it')
  expect(detectLanguage('it-IT')).toBe('it')
})

test('detectLanguage returns en for English codes', () => {
  expect(detectLanguage('en')).toBe('en')
  expect(detectLanguage('en-US')).toBe('en')
})

test('detectLanguage returns en for undefined', () => {
  expect(detectLanguage(undefined)).toBe('en')
})

test('All i18n keys exist in both IT and EN', () => {
  const itKeys = Object.keys(messages.it).sort()
  const enKeys = Object.keys(messages.en).sort()
  expect(itKeys).toEqual(enKeys)
})

// Summary
console.log('')
console.log('========================================')
console.log(`Test Summary:`)
console.log(`  Passed: ${testsPassed}`)
console.log(`  Failed: ${testsFailed}`)
console.log('========================================')

if (testsFailed > 0) {
  console.log('')
  console.log('Failed tests:')
  failedTests.forEach(test => console.log(`  - ${test}`))
  console.log('')
  process.exit(1)
}

console.log('')
console.log('✅ All tests passed!')
process.exit(0)
