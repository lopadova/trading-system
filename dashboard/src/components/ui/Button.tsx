import { cn } from '../../utils/cn'

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost'
  size?: 'sm' | 'md' | 'lg'
}

export function Button({
  variant = 'primary',
  size = 'md',
  className,
  children,
  ...props
}: ButtonProps) {
  return (
    <button
      {...props}
      className={cn(
        'inline-flex items-center justify-center font-medium rounded-lg',
        'transition-colors focus:outline-none focus:ring-2 focus:ring-offset-2',
        'disabled:opacity-50 disabled:cursor-not-allowed',
        // Variants
        variant === 'primary' &&
          'bg-primary text-white hover:bg-primary/90 focus:ring-primary',
        variant === 'secondary' &&
          'bg-muted text-foreground hover:bg-muted/80 focus:ring-muted',
        variant === 'danger' && 'bg-danger text-white hover:bg-danger/90 focus:ring-danger',
        variant === 'ghost' && 'bg-transparent text-foreground hover:bg-muted/10 focus:ring-muted',
        // Sizes
        size === 'sm' && 'text-xs px-3 py-1.5',
        size === 'md' && 'text-sm px-4 py-2',
        size === 'lg' && 'text-base px-6 py-3',
        className
      )}
    >
      {children}
    </button>
  )
}
