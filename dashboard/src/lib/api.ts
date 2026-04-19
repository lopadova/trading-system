import ky from 'ky'

// API client configuration
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:8787/api'

export const api = ky.create({
  prefixUrl: API_BASE_URL,
  timeout: 10000,
  retry: {
    limit: 2,
    methods: ['get'],
    statusCodes: [408, 413, 429, 500, 502, 503, 504],
  },
  hooks: {
    beforeRequest: [
      (request) => {
        // Add any auth headers here if needed
        const apiKey = import.meta.env.VITE_API_KEY
        if (apiKey) {
          request.headers.set('X-API-Key', apiKey)
        }
      },
    ],
  },
})
