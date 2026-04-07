/**
 * Unit tests for bot functions
 * TEST-BOT-01-XX: Function-level tests
 */

import { describe, test, expect } from 'vitest'
import { isWhitelisted } from '../src/bot/auth'
import { parseCommand, parseCallbackData } from '../src/bot/dispatcher'
import { t, detectLanguage, messages } from '../src/bot/i18n'

describe('Bot Auth Functions', () => {
  // TEST-BOT-01-03: isWhitelisted with valid user
  test('TEST-BOT-01-03: isWhitelisted returns true for whitelisted user', () => {
    const result = isWhitelisted('123456789', '123456789')
    expect(result).toBe(true)
  })

  // TEST-BOT-01-04: isWhitelisted with invalid user
  test('TEST-BOT-01-04: isWhitelisted returns false for non-whitelisted user', () => {
    const result = isWhitelisted('999', '123456789')
    expect(result).toBe(false)
  })

  test('isWhitelisted handles multiple users', () => {
    const result = isWhitelisted('456', '123, 456, 789')
    expect(result).toBe(true)
  })

  test('isWhitelisted handles whitespace in whitelist', () => {
    const result = isWhitelisted('789', '  123  ,  789  ,  456  ')
    expect(result).toBe(true)
  })

  test('isWhitelisted returns false for empty whitelist', () => {
    const result = isWhitelisted('123', '')
    expect(result).toBe(false)
  })
})

describe('Bot Command Parsing', () => {
  // TEST-BOT-01-05: parseCommand for /portfolio
  test('TEST-BOT-01-05: parseCommand parses /portfolio correctly', () => {
    const result = parseCommand('/portfolio')
    expect(result).toEqual({ type: 'query', query: 'portfolio' })
  })

  // TEST-BOT-01-06: parseCommand for /start
  test('TEST-BOT-01-06: parseCommand parses /start as menu', () => {
    const result = parseCommand('/start')
    expect(result).toEqual({ type: 'menu' })
  })

  test('parseCommand handles /menu', () => {
    const result = parseCommand('/menu')
    expect(result).toEqual({ type: 'menu' })
  })

  test('parseCommand handles /help', () => {
    const result = parseCommand('/help')
    expect(result).toEqual({ type: 'menu' })
  })

  test('parseCommand handles /status', () => {
    const result = parseCommand('/status')
    expect(result).toEqual({ type: 'query', query: 'status' })
  })

  test('parseCommand handles /campaigns', () => {
    const result = parseCommand('/campaigns')
    expect(result).toEqual({ type: 'query', query: 'campaigns' })
  })

  test('parseCommand handles /market', () => {
    const result = parseCommand('/market')
    expect(result).toEqual({ type: 'query', query: 'market' })
  })

  test('parseCommand handles /strategies', () => {
    const result = parseCommand('/strategies')
    expect(result).toEqual({ type: 'query', query: 'strategies' })
  })

  test('parseCommand handles /alerts', () => {
    const result = parseCommand('/alerts')
    expect(result).toEqual({ type: 'query', query: 'alerts' })
  })

  test('parseCommand handles /risk', () => {
    const result = parseCommand('/risk')
    expect(result).toEqual({ type: 'query', query: 'risk' })
  })

  test('parseCommand handles /snapshot', () => {
    const result = parseCommand('/snapshot')
    expect(result).toEqual({ type: 'query', query: 'snapshot' })
  })

  test('parseCommand handles unknown command', () => {
    const result = parseCommand('/unknown')
    expect(result).toEqual({ type: 'unknown', raw: '/unknown' })
  })

  test('parseCommand is case-insensitive', () => {
    const result = parseCommand('/PORTFOLIO')
    expect(result).toEqual({ type: 'query', query: 'portfolio' })
  })

  test('parseCommand trims whitespace', () => {
    const result = parseCommand('  /portfolio  ')
    expect(result).toEqual({ type: 'query', query: 'portfolio' })
  })

  // TEST-BOT-01-07: parseCallbackData for detail
  test('TEST-BOT-01-07: parseCallbackData parses detail:campaign:abc-123', () => {
    const result = parseCallbackData('detail:campaign:abc-123')
    expect(result).toEqual({ type: 'detail', entity: 'campaign', id: 'abc-123' })
  })

  test('parseCallbackData parses query:portfolio', () => {
    const result = parseCallbackData('query:portfolio')
    expect(result).toEqual({ type: 'query', query: 'portfolio' })
  })

  test('parseCallbackData parses menu', () => {
    const result = parseCallbackData('menu')
    expect(result).toEqual({ type: 'menu' })
  })

  test('parseCallbackData parses refresh', () => {
    const result = parseCallbackData('refresh:portfolio')
    expect(result).toEqual({ type: 'refresh', last: 'portfolio' })
  })

  test('parseCallbackData handles unknown data', () => {
    const result = parseCallbackData('invalid:data')
    expect(result).toEqual({ type: 'unknown', raw: 'invalid:data' })
  })
})

describe('Bot i18n Functions', () => {
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

  test('t handles missing key by returning key', () => {
    const result = t('nonexistent_key', 'en')
    expect(result).toBe('nonexistent_key')
  })

  test('detectLanguage returns it for Italian codes', () => {
    expect(detectLanguage('it')).toBe('it')
    expect(detectLanguage('it-IT')).toBe('it')
    expect(detectLanguage('IT')).toBe('it')
  })

  test('detectLanguage returns en for English codes', () => {
    expect(detectLanguage('en')).toBe('en')
    expect(detectLanguage('en-US')).toBe('en')
    expect(detectLanguage('en-GB')).toBe('en')
  })

  test('detectLanguage returns en for unknown codes', () => {
    expect(detectLanguage('fr')).toBe('en')
    expect(detectLanguage('de')).toBe('en')
  })

  test('detectLanguage returns en for undefined', () => {
    expect(detectLanguage(undefined)).toBe('en')
  })

  test('All i18n keys exist in both IT and EN', () => {
    const itKeys = Object.keys(messages.it)
    const enKeys = Object.keys(messages.en)

    expect(itKeys.sort()).toEqual(enKeys.sort())
  })
})
