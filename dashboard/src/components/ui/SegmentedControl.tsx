import { cn } from '../../utils/cn'
import { Lock } from 'lucide-react'

export interface SegmentOption<T extends string> {
  value: T
  label: string
  locked?: boolean
}

export interface SegmentedControlProps<T extends string> {
  value: T
  onChange: (value: T) => void
  options: SegmentOption<T>[]
  size?: 'sm' | 'md'
  className?: string
}

export function SegmentedControl<T extends string>({ value, onChange, options, size = 'md', className }: SegmentedControlProps<T>) {
  const pad = size === 'sm' ? 'px-2.5 py-0.5 text-[11px]' : 'px-3 py-1 text-[12px]'
  return (
    <div className={cn('inline-flex gap-0.5 p-[3px] bg-[var(--bg-1)] border border-border rounded-md', className)}>
      {options.map(opt => {
        const on = opt.value === value
        return (
          <button
            key={opt.value}
            type="button"
            aria-pressed={on}
            disabled={opt.locked}
            onClick={() => !opt.locked && onChange(opt.value)}
            className={cn(
              'inline-flex items-center gap-1 rounded font-medium transition-colors',
              pad,
              on ? 'bg-[var(--bg-3)] text-[var(--fg-1)] font-semibold' : 'text-[var(--fg-2)] hover:text-[var(--fg-1)]',
              opt.locked && 'opacity-60 cursor-not-allowed'
            )}
          >
            {opt.locked && <Lock size={10} />}
            {opt.label}
          </button>
        )
      })}
    </div>
  )
}
