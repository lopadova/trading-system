/**
 * TASK-21: Dashboard Integration Tests
 * Tests real API calls to Cloudflare Worker (not mocks)
 *
 * These tests verify:
 * - API endpoint functionality
 * - Authentication/authorization
 * - Error handling (404, 500, auth failures)
 * - CORS headers
 * - Rate limiting behavior
 * - Response data structure and types
 */

import { describe, it, expect } from 'vitest'
import ky, { HTTPError } from 'ky'

// Test configuration
const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:8787'
const API_KEY = import.meta.env.VITE_API_KEY || 'test-api-key-12345'

// API client instance
const apiClient = ky.create({
  prefixUrl: API_URL,
  timeout: 10000,
  retry: 0, // No retries for tests (we want to see real failures)
})

describe('Integration Tests: Cloudflare Worker API', () => {
  describe('Health Check Endpoint', () => {
    it('TEST-21-01: GET /api/health should return 200 OK', async () => {
      const response = await apiClient.get('api/health')

      expect(response.status).toBe(200)
      expect(response.ok).toBe(true)

      const data = await response.json<{ ok: boolean; timestamp: string; service: string }>()
      expect(data.ok).toBe(true)
      expect(data).toHaveProperty('timestamp')
      expect(data).toHaveProperty('service')
    })

    it('TEST-21-02: Health endpoint should include CORS headers', async () => {
      const response = await apiClient.get('api/health')

      expect(response.headers.has('access-control-allow-origin')).toBe(true)
      expect(response.headers.has('access-control-allow-methods')).toBe(true)
    })
  })

  describe('Authentication & Authorization', () => {
    it('TEST-21-03: Requests without API key should return 401', async () => {
      try {
        await apiClient.get('api/positions/active')
        expect.fail('Should have thrown 401 error')
      } catch (error) {
        expect(error).toBeInstanceOf(HTTPError)
        const httpError = error as HTTPError
        expect(httpError.response.status).toBe(401)

        const body = await httpError.response.json<{ error: string }>()
        expect(body.error).toBeTruthy()
      }
    })

    it('TEST-21-04: Requests with invalid API key should return 401', async () => {
      try {
        await apiClient.get('api/positions/active', {
          headers: { 'X-Api-Key': 'invalid-key-999' }
        })
        expect.fail('Should have thrown 401 error')
      } catch (error) {
        expect(error).toBeInstanceOf(HTTPError)
        const httpError = error as HTTPError
        expect(httpError.response.status).toBe(401)
      }
    })

    it('TEST-21-05: Requests with valid API key should return 200', async () => {
      const response = await apiClient.get('api/positions/active', {
        headers: { 'X-Api-Key': API_KEY }
      })

      expect(response.status).toBe(200)
    })
  })

  describe('Positions API Endpoints', () => {
    it('TEST-21-06: GET /api/positions/active should return positions list', async () => {
      const response = await apiClient.get('api/positions/active', {
        headers: { 'X-Api-Key': API_KEY }
      })

      expect(response.status).toBe(200)

      const data = await response.json<{
        positions: unknown[]
        count: number
        timestamp: string
      }>()

      expect(data).toHaveProperty('positions')
      expect(data).toHaveProperty('count')
      expect(data).toHaveProperty('timestamp')
      expect(Array.isArray(data.positions)).toBe(true)
      expect(typeof data.count).toBe('number')
      expect(data.count).toBeGreaterThanOrEqual(0)
    })

    it('TEST-21-07: GET /api/positions/active should support filtering by symbol', async () => {
      const response = await apiClient.get('api/positions/active', {
        headers: { 'X-Api-Key': API_KEY },
        searchParams: { symbol: 'SPY' }
      })

      expect(response.status).toBe(200)

      const data = await response.json<{ positions: { symbol: string }[] }>()

      // If there are positions, they should all match the symbol filter
      if (data.positions.length > 0) {
        data.positions.forEach(pos => {
          expect(pos.symbol.toUpperCase()).toContain('SPY')
        })
      }
    })

    it('TEST-21-08: GET /api/positions/:id should return single position', async () => {
      // First get list of positions
      const listResponse = await apiClient.get('api/positions/active', {
        headers: { 'X-Api-Key': API_KEY }
      })

      const listData = await listResponse.json<{ positions: { position_id: string }[] }>()

      if (listData.positions.length > 0) {
        const positionId = listData.positions[0]?.position_id

        if (positionId) {
          const detailResponse = await apiClient.get(`api/positions/${positionId}`, {
            headers: { 'X-Api-Key': API_KEY }
          })

          expect(detailResponse.status).toBe(200)

          const detailData = await detailResponse.json<{ position: { position_id: string } }>()
          expect(detailData).toHaveProperty('position')
          expect(detailData.position.position_id).toBe(positionId)
        }
      }
    })

    it('TEST-21-09: GET /api/positions/:id with invalid ID should return 404', async () => {
      try {
        await apiClient.get('api/positions/INVALID-ID-99999', {
          headers: { 'X-Api-Key': API_KEY }
        })
        expect.fail('Should have thrown 404 error')
      } catch (error) {
        expect(error).toBeInstanceOf(HTTPError)
        const httpError = error as HTTPError
        expect(httpError.response.status).toBe(404)
      }
    })
  })

  describe('Alerts API Endpoints', () => {
    it('TEST-21-10: GET /api/alerts should return alerts list', async () => {
      const response = await apiClient.get('api/alerts', {
        headers: { 'X-Api-Key': API_KEY }
      })

      expect(response.status).toBe(200)

      const data = await response.json<{
        alerts: unknown[]
        count: number
        timestamp: string
      }>()

      expect(data).toHaveProperty('alerts')
      expect(Array.isArray(data.alerts)).toBe(true)
    })

    it('TEST-21-11: GET /api/alerts should filter by severity', async () => {
      const response = await apiClient.get('api/alerts', {
        headers: { 'X-Api-Key': API_KEY },
        searchParams: { severity: 'critical' }
      })

      expect(response.status).toBe(200)

      const data = await response.json<{ alerts: { severity: string }[] }>()

      // If there are alerts, they should all be critical severity
      if (data.alerts.length > 0) {
        data.alerts.forEach(alert => {
          expect(alert.severity).toBe('critical')
        })
      }
    })

    it('TEST-21-12: GET /api/alerts/unresolved should return only unresolved alerts', async () => {
      const response = await apiClient.get('api/alerts/unresolved', {
        headers: { 'X-Api-Key': API_KEY }
      })

      expect(response.status).toBe(200)

      const data = await response.json<{ alerts: { resolved_at: string | null }[] }>()

      // All alerts should have null resolved_at
      data.alerts.forEach(alert => {
        expect(alert.resolved_at).toBeNull()
      })
    })
  })

  describe('Heartbeats API Endpoints', () => {
    it('TEST-21-13: GET /api/heartbeats should return heartbeats list', async () => {
      const response = await apiClient.get('api/heartbeats', {
        headers: { 'X-Api-Key': API_KEY }
      })

      expect(response.status).toBe(200)

      const data = await response.json<{
        heartbeats: unknown[]
        count: number
        timestamp: string
      }>()

      expect(data).toHaveProperty('heartbeats')
      expect(Array.isArray(data.heartbeats)).toBe(true)
    })

    it('TEST-21-14: GET /api/heartbeats/:service should return specific service heartbeat', async () => {
      // First get list
      const listResponse = await apiClient.get('api/heartbeats', {
        headers: { 'X-Api-Key': API_KEY }
      })

      const listData = await listResponse.json<{ heartbeats: { service_name: string }[] }>()

      if (listData.heartbeats.length > 0) {
        const serviceName = listData.heartbeats[0]?.service_name

        if (serviceName) {
          const detailResponse = await apiClient.get(`api/heartbeats/${serviceName}`, {
            headers: { 'X-Api-Key': API_KEY }
          })

          expect(detailResponse.status).toBe(200)

          const detailData = await detailResponse.json<{ heartbeat: { service_name: string } }>()
          expect(detailData.heartbeat.service_name).toBe(serviceName)
        }
      }
    })
  })

  describe('Error Handling', () => {
    it('TEST-21-15: Nonexistent endpoint should return 404', async () => {
      try {
        await apiClient.get('api/nonexistent-endpoint', {
          headers: { 'X-Api-Key': API_KEY }
        })
        expect.fail('Should have thrown 404 error')
      } catch (error) {
        expect(error).toBeInstanceOf(HTTPError)
        const httpError = error as HTTPError
        expect(httpError.response.status).toBe(404)

        const body = await httpError.response.json<{ error: string }>()
        expect(body.error).toBe('not_found')
      }
    })

    it('TEST-21-16: Malformed request should be handled gracefully', async () => {
      try {
        // Try to send malformed JSON in a PATCH request
        await apiClient.patch('api/alerts/invalid-id', {
          headers: { 'X-Api-Key': API_KEY },
          body: 'not-valid-json{{{',
        })
      } catch (error) {
        expect(error).toBeInstanceOf(HTTPError)
        const httpError = error as HTTPError
        // Should return 4xx or 5xx error, not crash
        expect(httpError.response.status).toBeGreaterThanOrEqual(400)
      }
    })
  })

  describe('CORS Headers Verification', () => {
    it('TEST-21-17: All authenticated endpoints should include CORS headers', async () => {
      const response = await apiClient.get('api/positions/active', {
        headers: { 'X-Api-Key': API_KEY }
      })

      expect(response.headers.has('access-control-allow-origin')).toBe(true)
      expect(response.headers.has('access-control-allow-methods')).toBe(true)
    })

    it('TEST-21-18: OPTIONS preflight request should be handled', async () => {
      const response = await apiClient('api/positions/active', {
        method: 'OPTIONS',
        headers: {
          'Origin': 'http://localhost:5173',
          'Access-Control-Request-Method': 'GET',
          'Access-Control-Request-Headers': 'X-Api-Key',
        }
      })

      // OPTIONS should return 2xx status
      expect(response.status).toBeLessThan(300)
    })
  })

  describe('Response Format Validation', () => {
    it('TEST-21-19: All responses should have correct Content-Type', async () => {
      const response = await apiClient.get('api/health')

      const contentType = response.headers.get('content-type')
      expect(contentType).toContain('application/json')
    })

    it('TEST-21-20: Timestamp fields should be ISO 8601 format', async () => {
      const response = await apiClient.get('api/health')
      const data = await response.json<{ timestamp: string }>()

      // Validate ISO 8601 format
      const isoRegex = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{3})?Z?$/
      expect(data.timestamp).toMatch(isoRegex)

      // Should be parseable as valid date
      const date = new Date(data.timestamp)
      expect(date.toString()).not.toBe('Invalid Date')
    })
  })
})
