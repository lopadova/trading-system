import { forwardRef, type ButtonHTMLAttributes } from 'react'
import type { LucideIcon } from 'lucide-react'
import { cn } from '../../utils/cn'

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger'
export type ButtonSize = 'sm' | 'md' | 'lg'

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  size?: ButtonSize
  icon?: LucideIcon
  loading?: boolean
}

const variantClass: Record<ButtonVariant, string> = {
  primary: 'bg-[var(--blue)] text-white border-transparent hover:brightness-110',
  secondary: 'bg-[var(--bg-3)] text-[var(--fg-1)] border-[var(--border-1)] hover:brightness-110',
  ghost: 'bg-transparent text-[var(--fg-1)] border-transparent hover:bg-[var(--bg-3)]',
  danger: 'bg-[var(--red)] text-white border-transparent hover:brightness-110',
}

const sizeClass: Record<ButtonSize, string> = {
  sm: 'px-2.5 py-1 text-[12px] gap-1.5',
  md: 'px-3.5 py-1.5 text-[13px] gap-1.5',
  lg: 'px-4.5 py-2.5 text-[14px] gap-2',
}

const iconSize: Record<ButtonSize, number> = { sm: 12, md: 14, lg: 16 }

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  ({ variant = 'primary', size = 'md', icon: Icon, loading, disabled, className, children, ...rest }, ref) => {
    const isDisabled = disabled || loading
    return (
      <button
        ref={ref}
        disabled={isDisabled}
        className={cn(
          'inline-flex items-center justify-center rounded-md border font-medium whitespace-nowrap transition-[filter,background-color,border-color] duration-150',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--border-focus)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--bg-1)]',
          'disabled:opacity-50 disabled:cursor-not-allowed',
          variantClass[variant],
          sizeClass[size],
          className
        )}
        {...rest}
      >
        {loading ? (
          <svg data-spinner width={iconSize[size]} height={iconSize[size]} viewBox="0 0 24 24" fill="none" className="animate-spin">
            <circle cx="12" cy="12" r="9" stroke="currentColor" strokeOpacity=".2" strokeWidth="3" />
            <path d="M21 12a9 9 0 0 0-9-9" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
          </svg>
        ) : Icon ? (
          <Icon size={iconSize[size]} />
        ) : null}
        {children}
      </button>
    )
  }
)
Button.displayName = 'Button'
