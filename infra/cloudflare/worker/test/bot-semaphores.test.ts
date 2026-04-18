/**
 * Tests for bot semaphores (signal logic)
 * TEST-BOT-02-01 through TEST-BOT-02-08
 */

import { describe, it, expect } from 'vitest'
import {
  pnlSignal,
  pnlVsStopSignal,
  heartbeatSignal,
  ivtsSignal,
  deltaSignal,
  thetaSignal,
  spxVsWingSignal,
  daysRemainingSignal,
  processSignal
} from '../src/bot/semaphores'

describe('Bot Semaphores', () => {
  describe('pnlSignal', () => {
    it('TEST-BOT-02-01: returns green for positive PnL', () => {
      expect(pnlSignal(100)).toBe('🟢')
      expect(pnlSignal(1000)).toBe('🟢')
      expect(pnlSignal(0.01)).toBe('🟢')
      expect(pnlSignal(0)).toBe('🟢')
    })

    it('TEST-BOT-02-02: returns red for PnL < -200', () => {
      expect(pnlSignal(-500)).toBe('🔴')
      expect(pnlSignal(-200.01)).toBe('🔴')
      expect(pnlSignal(-1000)).toBe('🔴')
    })

    it('TEST-BOT-02-03: returns white for null PnL', () => {
      expect(pnlSignal(null)).toBe('⚪')
    })

    it('returns yellow for PnL between -200 and 0', () => {
      expect(pnlSignal(-199)).toBe('🟡')
      expect(pnlSignal(-100)).toBe('🟡')
      expect(pnlSignal(-0.01)).toBe('🟡')
      expect(pnlSignal(-200)).toBe('🟡')
    })
  })

  describe('heartbeatSignal', () => {
    it('TEST-BOT-02-04: returns green for age < 3 minutes', () => {
      expect(heartbeatSignal(0)).toBe('🟢')
      expect(heartbeatSignal(1)).toBe('🟢')
      expect(heartbeatSignal(2)).toBe('🟢')
      expect(heartbeatSignal(2.9)).toBe('🟢')
    })

    it('TEST-BOT-02-05: returns yellow for age 3-10 minutes', () => {
      expect(heartbeatSignal(3)).toBe('🟡')
      expect(heartbeatSignal(5)).toBe('🟡')
      expect(heartbeatSignal(8)).toBe('🟡')
      expect(heartbeatSignal(9.9)).toBe('🟡')
    })

    it('returns red for age >= 10 minutes', () => {
      expect(heartbeatSignal(10)).toBe('🔴')
      expect(heartbeatSignal(15)).toBe('🔴')
      expect(heartbeatSignal(60)).toBe('🔴')
    })

    it('returns white for null age', () => {
      expect(heartbeatSignal(null)).toBe('⚪')
    })
  })

  describe('spxVsWingSignal', () => {
    it('TEST-BOT-02-06: returns yellow for distance 34pt (< 50)', () => {
      expect(spxVsWingSignal(5234, 5200)).toBe('🟡')
    })

    it('TEST-BOT-02-07: returns green for distance 334pt (> 150)', () => {
      expect(spxVsWingSignal(5234, 4900)).toBe('🟢')
    })

    it('returns yellow for distance in 30-150 range', () => {
      expect(spxVsWingSignal(5234, 5100)).toBe('🟡') // 134pt
      expect(spxVsWingSignal(5234, 5084)).toBe('🟡') // 150pt
      expect(spxVsWingSignal(5234, 5184)).toBe('🟡') // 50pt (yellow with new threshold)
    })

    it('returns red for distance <= 30pt', () => {
      expect(spxVsWingSignal(5234, 5220)).toBe('🔴') // 14pt
      expect(spxVsWingSignal(5234, 5204)).toBe('🔴') // 30pt boundary
    })

    it('returns white for null inputs', () => {
      expect(spxVsWingSignal(null, 5200)).toBe('⚪')
      expect(spxVsWingSignal(5234, null)).toBe('⚪')
      expect(spxVsWingSignal(null, null)).toBe('⚪')
    })
  })

  describe('pnlVsStopSignal', () => {
    it('TEST-BOT-02-08: returns red for ratio >= 80% (82%)', () => {
      expect(pnlVsStopSignal(-4100, 5000)).toBe('🔴')
    })

    it('returns green for ratio < 50%', () => {
      expect(pnlVsStopSignal(-2000, 5000)).toBe('🟢') // 40%
      expect(pnlVsStopSignal(-1000, 5000)).toBe('🟢') // 20%
      expect(pnlVsStopSignal(0, 5000)).toBe('🟢') // 0%
    })

    it('returns yellow for ratio 50-80%', () => {
      expect(pnlVsStopSignal(-2500, 5000)).toBe('🟡') // 50%
      expect(pnlVsStopSignal(-3000, 5000)).toBe('🟡') // 60%
      expect(pnlVsStopSignal(-3999, 5000)).toBe('🟡') // 79.98%
    })

    it('returns white for null inputs', () => {
      expect(pnlVsStopSignal(null, 5000)).toBe('⚪')
      expect(pnlVsStopSignal(-4100, null)).toBe('⚪')
      expect(pnlVsStopSignal(null, null)).toBe('⚪')
    })
  })

  describe('ivtsSignal', () => {
    it('returns green for Active state and IVTS < 1.10', () => {
      expect(ivtsSignal(0.94, 'Active')).toBe('🟢')
      expect(ivtsSignal(1.09, 'Active')).toBe('🟢')
      expect(ivtsSignal(0.80, 'Active')).toBe('🟢')
    })

    it('returns yellow for Active state and IVTS 1.10-1.15', () => {
      expect(ivtsSignal(1.10, 'Active')).toBe('🟡')
      expect(ivtsSignal(1.12, 'Active')).toBe('🟡')
      expect(ivtsSignal(1.14, 'Active')).toBe('🟡')
    })

    it('returns red for Suspended state', () => {
      expect(ivtsSignal(0.94, 'Suspended')).toBe('🔴')
      expect(ivtsSignal(1.10, 'Suspended')).toBe('🔴')
    })

    it('returns red for IVTS > 1.15', () => {
      expect(ivtsSignal(1.16, 'Active')).toBe('🔴')
      expect(ivtsSignal(1.50, 'Active')).toBe('🔴')
    })

    it('returns white for null inputs', () => {
      expect(ivtsSignal(null, 'Active')).toBe('⚪')
      expect(ivtsSignal(0.94, null)).toBe('⚪')
      expect(ivtsSignal(null, null)).toBe('⚪')
    })
  })

  describe('deltaSignal', () => {
    it('returns green for ratio < 60%', () => {
      expect(deltaSignal(-300, 1000)).toBe('🟢') // 30%
      expect(deltaSignal(-500, 1000)).toBe('🟢') // 50%
      expect(deltaSignal(-100, 1000)).toBe('🟢') // 10%
    })

    it('returns yellow for ratio 60-85%', () => {
      expect(deltaSignal(-600, 1000)).toBe('🟡') // 60%
      expect(deltaSignal(-700, 1000)).toBe('🟡') // 70%
      expect(deltaSignal(-849, 1000)).toBe('🟡') // 84.9%
    })

    it('returns red for ratio >= 85%', () => {
      expect(deltaSignal(-850, 1000)).toBe('🔴') // 85%
      expect(deltaSignal(-950, 1000)).toBe('🔴') // 95%
      expect(deltaSignal(-1000, 1000)).toBe('🔴') // 100%
    })

    it('returns white for null inputs', () => {
      expect(deltaSignal(null, 1000)).toBe('⚪')
      expect(deltaSignal(-300, null)).toBe('⚪')
      expect(deltaSignal(null, null)).toBe('⚪')
    })
  })

  describe('thetaSignal', () => {
    it('returns green for ratio < 60%', () => {
      expect(thetaSignal(300, 1000)).toBe('🟢') // 30%
      expect(thetaSignal(500, 1000)).toBe('🟢') // 50%
      expect(thetaSignal(100, 1000)).toBe('🟢') // 10%
    })

    it('returns yellow for ratio 60-85%', () => {
      expect(thetaSignal(600, 1000)).toBe('🟡') // 60%
      expect(thetaSignal(700, 1000)).toBe('🟡') // 70%
      expect(thetaSignal(849, 1000)).toBe('🟡') // 84.9%
    })

    it('returns red for ratio >= 85%', () => {
      expect(thetaSignal(850, 1000)).toBe('🔴') // 85%
      expect(thetaSignal(950, 1000)).toBe('🔴') // 95%
      expect(thetaSignal(1000, 1000)).toBe('🔴') // 100%
    })

    it('returns white for null inputs', () => {
      expect(thetaSignal(null, 1000)).toBe('⚪')
      expect(thetaSignal(300, null)).toBe('⚪')
      expect(thetaSignal(null, null)).toBe('⚪')
    })
  })

  describe('daysRemainingSignal', () => {
    it('returns green for remaining > 30%', () => {
      expect(daysRemainingSignal(40, 100)).toBe('🟢') // 40% remaining
      expect(daysRemainingSignal(50, 100)).toBe('🟢') // 50% remaining
      expect(daysRemainingSignal(31, 100)).toBe('🟢') // 31% remaining
    })

    it('returns yellow for remaining 10-30%', () => {
      expect(daysRemainingSignal(30, 100)).toBe('🟡') // 30% remaining
      expect(daysRemainingSignal(20, 100)).toBe('🟡') // 20% remaining
      expect(daysRemainingSignal(10, 100)).toBe('🟡') // 10% remaining
    })

    it('returns red for remaining < 10%', () => {
      expect(daysRemainingSignal(9, 100)).toBe('🔴') // 9% remaining
      expect(daysRemainingSignal(5, 100)).toBe('🔴') // 5% remaining
      expect(daysRemainingSignal(0, 100)).toBe('🔴') // 0% remaining
    })

    it('returns white for null inputs', () => {
      expect(daysRemainingSignal(null, 100)).toBe('⚪')
      expect(daysRemainingSignal(40, null)).toBe('⚪')
      expect(daysRemainingSignal(null, null)).toBe('⚪')
    })
  })

  describe('processSignal', () => {
    it('returns green for running status', () => {
      expect(processSignal('running')).toBe('🟢')
    })

    it('returns red for stopped status', () => {
      expect(processSignal('stopped')).toBe('🔴')
    })

    it('returns yellow for any other status', () => {
      expect(processSignal('starting')).toBe('🟡')
      expect(processSignal('stopping')).toBe('🟡')
      expect(processSignal('unknown')).toBe('🟡')
    })

    it('returns white for null status', () => {
      expect(processSignal(null)).toBe('⚪')
    })
  })
})
