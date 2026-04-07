/**
 * Unit tests for bot whitelist functionality
 * TEST-11-XX: Whitelist management tests
 */

import { describe, test, expect, beforeEach } from 'vitest'
import { parseCommand } from '../src/bot/dispatcher'
import { isWhitelisted } from '../src/bot/auth'

describe('Whitelist Command Parsing', () => {
  // TEST-11-01: Parse /whitelist list command
  test('TEST-11-01: parseCommand parses /whitelist list', () => {
    const result = parseCommand('/whitelist list')
    expect(result).toEqual({ type: 'whitelist', action: 'list' })
  })

  // TEST-11-02: Parse /whitelist (implicit list)
  test('TEST-11-02: parseCommand parses /whitelist as list', () => {
    const result = parseCommand('/whitelist')
    expect(result).toEqual({ type: 'whitelist', action: 'list' })
  })

  // TEST-11-03: Parse /whitelist add <user_id>
  test('TEST-11-03: parseCommand parses /whitelist add 123456789', () => {
    const result = parseCommand('/whitelist add 123456789')
    expect(result).toEqual({ type: 'whitelist', action: 'add', userId: '123456789' })
  })

  // TEST-11-04: Parse /whitelist remove <user_id>
  test('TEST-11-04: parseCommand parses /whitelist remove 123456789', () => {
    const result = parseCommand('/whitelist remove 123456789')
    expect(result).toEqual({ type: 'whitelist', action: 'remove', userId: '123456789' })
  })

  // TEST-11-05: Invalid whitelist command (missing user_id for add)
  test('TEST-11-05: parseCommand returns unknown for /whitelist add without user_id', () => {
    const result = parseCommand('/whitelist add')
    expect(result).toEqual({ type: 'unknown', raw: '/whitelist add' })
  })

  // TEST-11-06: Invalid whitelist command (missing user_id for remove)
  test('TEST-11-06: parseCommand returns unknown for /whitelist remove without user_id', () => {
    const result = parseCommand('/whitelist remove')
    expect(result).toEqual({ type: 'unknown', raw: '/whitelist remove' })
  })

  test('parseCommand handles whitelist with extra spaces', () => {
    const result = parseCommand('/whitelist   add   987654321')
    expect(result).toEqual({ type: 'whitelist', action: 'add', userId: '987654321' })
  })

  test('parseCommand is case-insensitive for whitelist commands', () => {
    const result = parseCommand('/WHITELIST ADD 123')
    expect(result).toEqual({ type: 'whitelist', action: 'add', userId: '123' })
  })
})

describe('Legacy Whitelist Functions', () => {
  // TEST-11-07: isWhitelisted with single user
  test('TEST-11-07: isWhitelisted returns true for single whitelisted user', () => {
    const result = isWhitelisted('123456789', '123456789')
    expect(result).toBe(true)
  })

  // TEST-11-08: isWhitelisted with multiple users
  test('TEST-11-08: isWhitelisted returns true for user in comma-separated list', () => {
    const result = isWhitelisted('456789', '123456,456789,789012')
    expect(result).toBe(true)
  })

  // TEST-11-09: isWhitelisted returns false for non-whitelisted user
  test('TEST-11-09: isWhitelisted returns false for non-whitelisted user', () => {
    const result = isWhitelisted('999999', '123456,456789')
    expect(result).toBe(false)
  })

  // TEST-11-10: isWhitelisted handles whitespace
  test('TEST-11-10: isWhitelisted trims whitespace in whitelist', () => {
    const result = isWhitelisted('789', ' 123 , 456 , 789 ')
    expect(result).toBe(true)
  })

  test('isWhitelisted returns false for empty whitelist', () => {
    const result = isWhitelisted('123', '')
    expect(result).toBe(false)
  })

  test('isWhitelisted returns false for whitespace-only whitelist', () => {
    const result = isWhitelisted('123', '   ')
    expect(result).toBe(false)
  })
})

describe('Whitelist i18n Keys', () => {
  // TEST-11-11: All whitelist i18n keys exist
  test('TEST-11-11: All whitelist message keys exist in both languages', async () => {
    const { messages } = await import('../src/bot/i18n')

    const requiredKeys = [
      'whitelist_add_success',
      'whitelist_add_already_exists',
      'whitelist_add_missing_userid',
      'whitelist_remove_success',
      'whitelist_remove_not_found',
      'whitelist_remove_missing_userid',
      'whitelist_list_title',
      'whitelist_list_empty'
    ]

    for (const key of requiredKeys) {
      expect(messages.it[key]).toBeDefined()
      expect(messages.en[key]).toBeDefined()
      expect(messages.it[key]).not.toBe('')
      expect(messages.en[key]).not.toBe('')
    }
  })

  // TEST-11-12: Whitelist messages support parameter substitution
  test('TEST-11-12: Whitelist messages support userId parameter', async () => {
    const { t } = await import('../src/bot/i18n')

    const addSuccess = t('whitelist_add_success', 'en', { userId: '123456' })
    expect(addSuccess).toContain('123456')

    const removeSuccess = t('whitelist_remove_success', 'it', { userId: '789012' })
    expect(removeSuccess).toContain('789012')
  })
})
