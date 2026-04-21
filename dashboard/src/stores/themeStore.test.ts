import { describe, it, expect, beforeEach } from 'vitest'
import { useThemeStore } from './themeStore'

describe('themeStore', () => {
  beforeEach(() => {
    localStorage.clear()
    document.documentElement.removeAttribute('data-theme')
    useThemeStore.setState({ theme: 'dark' })
  })
  it('defaults to dark', () => {
    expect(useThemeStore.getState().theme).toBe('dark')
  })
  it('toggles to light and persists', () => {
    useThemeStore.getState().toggle()
    expect(useThemeStore.getState().theme).toBe('light')
    expect(localStorage.getItem('ts-theme')).toBe('light')
    expect(document.documentElement.getAttribute('data-theme')).toBe('light')
  })
  it('setTheme applies value', () => {
    useThemeStore.getState().setTheme('light')
    expect(useThemeStore.getState().theme).toBe('light')
  })
})
