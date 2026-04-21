// PositionsFilterBar — search input + status chips + type select + view toggle.
// This bar is rendered inside a padding-less Card so the filter row looks
// flush with the table/grid below it.
import { Search, RefreshCw, List, LayoutGrid } from 'lucide-react'
import { Button } from '../ui/Button'
import { SegmentedControl } from '../ui/SegmentedControl'
import { cn } from '../../utils/cn'

export type PositionStatus = 'All' | 'Open' | 'Closed' | 'Pending'
export type ViewMode = 'table' | 'cards'

const STATUSES: PositionStatus[] = ['All', 'Open', 'Closed', 'Pending']
const TYPES = [
  'All',
  'Iron Condor',
  'Credit Spread',
  'Put Spread',
  'Call Spread',
  'Covered Call',
  'Long Call',
  'Short Strangle',
]

interface Props {
  status: PositionStatus
  setStatus: (status: PositionStatus) => void
  typeFilter: string
  setTypeFilter: (type: string) => void
  query: string
  setQuery: (query: string) => void
  view: ViewMode
  setView: (view: ViewMode) => void
  onRefresh: () => void
  isFetching: boolean
}

export function PositionsFilterBar({
  status,
  setStatus,
  typeFilter,
  setTypeFilter,
  query,
  setQuery,
  view,
  setView,
  onRefresh,
  isFetching,
}: Props) {
  return (
    <div className="px-4 py-3 flex gap-2.5 items-center flex-wrap border-b border-border">
      {/* Search input with inset Search icon — clamped width so the rest of the
          bar has breathing room on narrow viewports */}
      <div className="relative flex-1 min-w-[220px] max-w-[320px]">
        <Search size={13} className="absolute left-2.5 top-2.5 text-muted" />
        <input
          value={query}
          onChange={e => setQuery(e.target.value)}
          placeholder="Search symbol…"
          className="w-full py-[7px] pl-[30px] pr-2.5 bg-[var(--bg-1)] border border-border rounded-md text-[12.5px] text-[var(--fg-1)] focus-visible:outline-none focus-visible:border-[var(--border-focus)]"
        />
      </div>

      <SegmentedControl<PositionStatus>
        value={status}
        onChange={setStatus}
        options={STATUSES.map(s => ({ value: s, label: s }))}
        size="sm"
      />

      <select
        value={typeFilter}
        onChange={e => setTypeFilter(e.target.value)}
        className="px-2.5 py-1.5 bg-[var(--bg-1)] border border-border rounded-md text-[12.5px] text-[var(--fg-1)] focus-visible:outline-none focus-visible:border-[var(--border-focus)]"
      >
        {TYPES.map(t => (
          <option key={t} value={t}>
            {t === 'All' ? 'All types' : t}
          </option>
        ))}
      </select>

      <div className="ml-auto flex gap-2 items-center">
        <Button
          variant="secondary"
          size="sm"
          icon={RefreshCw}
          loading={isFetching}
          onClick={onRefresh}
        >
          Refresh
        </Button>

        {/* View toggle — custom icon-only segmented control so it stays compact */}
        <div className="flex gap-0.5 p-[3px] bg-[var(--bg-1)] border border-border rounded-md">
          <button
            type="button"
            aria-label="Table view"
            aria-pressed={view === 'table'}
            onClick={() => setView('table')}
            className={cn(
              'px-2 py-1 rounded flex items-center',
              view === 'table'
                ? 'bg-[var(--blue)] text-white'
                : 'text-muted'
            )}
          >
            <List size={14} />
          </button>
          <button
            type="button"
            aria-label="Card view"
            aria-pressed={view === 'cards'}
            onClick={() => setView('cards')}
            className={cn(
              'px-2 py-1 rounded flex items-center',
              view === 'cards'
                ? 'bg-[var(--blue)] text-white'
                : 'text-muted'
            )}
          >
            <LayoutGrid size={14} />
          </button>
        </div>
      </div>
    </div>
  )
}
