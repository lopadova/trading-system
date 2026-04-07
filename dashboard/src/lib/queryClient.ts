import { QueryClient } from '@tanstack/react-query'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 10_000, // 10s before refetch
      gcTime: 5 * 60_000, // 5min in cache
      retry: 2,
      refetchOnWindowFocus: true,
    },
  },
})
