import { cn } from '../../utils/cn'

interface SelectOption {
  value: string
  label: string
}

interface SelectProps extends Omit<React.SelectHTMLAttributes<HTMLSelectElement>, 'onChange'> {
  label?: string | undefined
  description?: string | undefined
  error?: string | undefined
  options: SelectOption[]
  onChange: (value: string) => void
}

export function Select({
  label,
  description,
  error,
  options,
  onChange,
  className,
  ...props
}: SelectProps) {
  return (
    <div className="space-y-1">
      {label && (
        <label htmlFor={props.id} className="block text-sm font-medium text-foreground">
          {label}
        </label>
      )}
      {description && <p className="text-xs text-muted">{description}</p>}
      <select
        {...props}
        onChange={(e) => onChange(e.target.value)}
        className={cn(
          'w-full px-3 py-2 text-sm rounded-lg',
          'bg-background border border-border',
          'text-foreground',
          'focus:outline-none focus:ring-2 focus:ring-primary focus:border-transparent',
          'disabled:opacity-50 disabled:cursor-not-allowed',
          error && 'border-danger focus:ring-danger',
          className
        )}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      {error && <p className="text-xs text-danger">{error}</p>}
    </div>
  )
}
