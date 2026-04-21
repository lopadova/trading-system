import { forwardRef, type HTMLAttributes } from 'react'
import { cn } from '../../utils/cn'

type CardPadding = 0 | 16 | 20 | 24

export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  padding?: CardPadding
}

const paddingClass: Record<CardPadding, string> = {
  0: 'p-0',
  16: 'p-4',
  20: 'p-5',
  24: 'p-6',
}

export const Card = forwardRef<HTMLDivElement, CardProps>(
  ({ padding = 20, className, children, ...rest }, ref) => (
    <div
      ref={ref}
      className={cn(
        'bg-surface border border-border rounded-card',
        paddingClass[padding],
        className
      )}
      {...rest}
    >
      {children}
    </div>
  )
)
Card.displayName = 'Card'

export const CardContent = forwardRef<HTMLDivElement, HTMLAttributes<HTMLDivElement>>(
  ({ className, children, ...rest }, ref) => (
    <div ref={ref} className={cn('p-5', className)} {...rest}>
      {children}
    </div>
  )
)
CardContent.displayName = 'CardContent'

// Backwards-compat legacy subcomponents (used by AlertsPage/CampaignsPage until Phase 2 restyle)
export const CardHeader = forwardRef<HTMLDivElement, HTMLAttributes<HTMLDivElement>>(
  ({ className, children, ...rest }, ref) => (
    <div ref={ref} className={cn('px-6 py-4 border-b border-border', className)} {...rest}>
      {children}
    </div>
  )
)
CardHeader.displayName = 'CardHeader'

export const CardTitle = forwardRef<HTMLHeadingElement, HTMLAttributes<HTMLHeadingElement>>(
  ({ className, children, ...rest }, ref) => (
    <h3 ref={ref} className={cn('text-lg font-semibold', className)} {...rest}>
      {children}
    </h3>
  )
)
CardTitle.displayName = 'CardTitle'
