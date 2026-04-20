import { usePositionFilterStore } from '../../stores/positionFilterStore'
import { Card, CardContent } from '../ui/Card'
import { X } from 'lucide-react'

export function PositionFilters() {
  const { symbol, strategy, status, type, setSymbol, setStrategy, setStatus, setType, clearFilters } =
    usePositionFilterStore()

  const hasActiveFilters = Boolean(symbol || strategy || status || type)

  return (
    <Card>
      <CardContent className="py-4">
        <div className="flex flex-wrap items-center gap-4">
          <div className="flex-1 min-w-[200px]">
            <label htmlFor="symbol-filter" className="block text-xs text-muted mb-1">
              Symbol
            </label>
            <input
              id="symbol-filter"
              type="text"
              placeholder="Filter by symbol..."
              value={symbol || ''}
              onChange={(e) => setSymbol(e.target.value)}
              className="w-full px-3 py-2 bg-background border border-border rounded-lg text-sm
                       focus:outline-none focus:ring-2 focus:ring-accent/50 transition-all"
            />
          </div>

          <div className="flex-1 min-w-[200px]">
            <label htmlFor="strategy-filter" className="block text-xs text-muted mb-1">
              Strategy
            </label>
            <input
              id="strategy-filter"
              type="text"
              placeholder="Filter by strategy..."
              value={strategy || ''}
              onChange={(e) => setStrategy(e.target.value)}
              className="w-full px-3 py-2 bg-background border border-border rounded-lg text-sm
                       focus:outline-none focus:ring-2 focus:ring-accent/50 transition-all"
            />
          </div>

          <div className="min-w-[150px]">
            <label htmlFor="status-filter" className="block text-xs text-muted mb-1">
              Status
            </label>
            <select
              id="status-filter"
              value={status || ''}
              onChange={(e) => setStatus(e.target.value ? (e.target.value as 'open' | 'closed' | 'pending') : undefined)}
              className="w-full px-3 py-2 bg-background border border-border rounded-lg text-sm
                       focus:outline-none focus:ring-2 focus:ring-accent/50 transition-all"
            >
              <option value="">All Statuses</option>
              <option value="open">Open</option>
              <option value="closed">Closed</option>
              <option value="pending">Pending</option>
            </select>
          </div>

          <div className="min-w-[150px]">
            <label htmlFor="type-filter" className="block text-xs text-muted mb-1">
              Type
            </label>
            <select
              id="type-filter"
              value={type || ''}
              onChange={(e) => setType(e.target.value ? (e.target.value as 'option' | 'stock' | 'future') : undefined)}
              className="w-full px-3 py-2 bg-background border border-border rounded-lg text-sm
                       focus:outline-none focus:ring-2 focus:ring-accent/50 transition-all"
            >
              <option value="">All Types</option>
              <option value="option">Options</option>
              <option value="stock">Stocks</option>
              <option value="future">Futures</option>
            </select>
          </div>

          {hasActiveFilters && (
            <div className="flex items-end">
              <button
                onClick={clearFilters}
                className="px-4 py-2 bg-muted/10 hover:bg-muted/20 border border-border rounded-lg
                         text-sm font-medium transition-colors flex items-center gap-2"
              >
                <X className="h-4 w-4" />
                Clear Filters
              </button>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  )
}
