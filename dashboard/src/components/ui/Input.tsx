import { cn } from '../../utils/cn'

interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string | undefined
  description?: string | undefined
  error?: string | undefined
}

export function Input({ label, description, error, className, ...props }: InputProps) {
  return (
    <div className="space-y-1">
      {label && (
        <label htmlFor={props.id} className="block text-sm font-medium text-foreground">
          {label}
        </label>
      )}
      {description && <p className="text-xs text-muted">{description}</p>}
      <input
        {...props}
        className={cn(
          'w-full px-3 py-2 text-sm rounded-lg',
          'bg-background border border-border',
          'text-foreground placeholder:text-muted',
          'focus:outline-none focus:ring-2 focus:ring-primary focus:border-transparent',
          'disabled:opacity-50 disabled:cursor-not-allowed',
          error && 'border-danger focus:ring-danger',
          className
        )}
      />
      {error && <p className="text-xs text-danger">{error}</p>}
    </div>
  )
}
