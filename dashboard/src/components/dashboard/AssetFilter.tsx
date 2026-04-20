// AssetFilter — 4 segmented chips that drive the global asset bucket filter
// (All / Systematic / Options / Other). Selected chip is highlighted with a
// color dot; All uses a neutral ring instead of a solid dot.

import { cn } from '../../utils/cn'
import { useAssetFilterStore, type AssetBucket } from '../../stores/assetFilterStore'

interface AssetOption {
  id: AssetBucket
  label: string
  color: string
}

const ASSETS: AssetOption[] = [
  { id: 'all', label: 'All assets', color: '#e6edf3' },
  { id: 'systematic', label: 'Systematic', color: '#2f81f7' },
  { id: 'options', label: 'Options', color: '#a371f7' },
  { id: 'other', label: 'Other', color: '#3fb950' },
]

export function AssetFilter() {
  const asset = useAssetFilterStore(s => s.asset)
  const setAsset = useAssetFilterStore(s => s.setAsset)

  return (
    <div className="inline-flex gap-1 p-1 bg-surface border border-border rounded-lg">
      {ASSETS.map(a => {
        const on = a.id === asset
        const isAll = a.id === 'all'
        return (
          <button
            key={a.id}
            type="button"
            aria-pressed={on}
            onClick={() => setAsset(a.id)}
            className={cn(
              'px-3.5 py-1.5 rounded-md text-[12.5px] font-medium flex items-center gap-1.5 transition-colors',
              on
                ? 'bg-[var(--bg-3)] text-[var(--fg-1)]'
                : 'text-muted hover:text-[var(--fg-1)]',
            )}
          >
            <span
              className={cn(
                'w-1.5 h-1.5 rounded-full',
                isAll ? 'border border-muted' : '',
              )}
              style={isAll ? undefined : { background: a.color }}
            />
            {a.label}
          </button>
        )
      })}
    </div>
  )
}
