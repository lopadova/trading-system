import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'

export interface SystemHeartbeat {
  serviceName: string
  lastSeenAt: string
  cpuPercent: number
  ramPercent: number
  diskFreeGb: number
  tradingMode: string
  ibkrConnected: boolean
  version: string
}

export function useSystemStatus() {
  return useQuery({
    queryKey: ['system', 'status'],
    queryFn: async () => {
      const response = await api.get('system/heartbeat').json<SystemHeartbeat[]>()
      return response
    },
    refetchInterval: 5000, // Refresh every 5s
  })
}

export function useSystemMetrics() {
  return useQuery({
    queryKey: ['system', 'metrics'],
    queryFn: async () => {
      // Mock data for now - will be replaced with real API
      return {
        cpu: Math.random() * 40 + 20,
        ram: Math.random() * 30 + 40,
        disk: Math.random() * 20 + 70,
        network: Math.random() * 50 + 10,
      }
    },
    refetchInterval: 2000,
  })
}
