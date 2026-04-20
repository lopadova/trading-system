import type { HTMLAttributes } from 'react'
import { cn } from '../../utils/cn'

export type BadgeTone = 'green' | 'red' | 'yellow' | 'blue' | 'purple' | 'muted'
export type BadgeSize = 'sm' | 'md'

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  tone?: BadgeTone
  size?: BadgeSize
  pulse?: boolean
}

const toneStyles: Record<BadgeTone, string> = {
  green: 'bg-[var(--tint-green)] text-[var(--green)] border-[color:var(--green)]/25',
  red: 'bg-[var(--tint-red)] text-[var(--red)] border-[color:var(--red)]/25',
  yellow: 'bg-[var(--tint-yellow)] text-[var(--yellow)] border-[color:var(--yellow)]/25',
  blue: 'bg-[var(--tint-blue)] text-[var(--blue)] border-[color:var(--blue)]/25',
  purple: 'bg-[var(--tint-purple)] text-[var(--purple)] border-[color:var(--purple)]/25',
  muted: 'bg-[var(--tint-muted)] text-[var(--fg-2)] border-[color:var(--fg-2)]/25',
}

const dotColor: Record<BadgeTone, string> = {
  green: 'bg-[var(--green)]',
  red: 'bg-[var(--red)]',
  yellow: 'bg-[var(--yellow)]',
  blue: 'bg-[var(--blue)]',
  purple: 'bg-[var(--purple)]',
  muted: 'bg-[var(--fg-2)]',
}

export function Badge({ tone = 'muted', size = 'md', pulse, className, children, ...rest }: BadgeProps) {
  const padding = size === 'sm' ? 'px-[7px] py-[1px] text-[10.5px]' : 'px-2.5 py-0.5 text-[11px]'
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-pill font-semibold tracking-wide whitespace-nowrap border',
        toneStyles[tone],
        padding,
        className
      )}
      {...rest}
    >
      {pulse && <span className={cn('w-1.5 h-1.5 rounded-full pulse-dot', dotColor[tone])} />}
      {children}
    </span>
  )
}
