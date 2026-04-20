import type { HTMLAttributes } from 'react'
import { cn } from '../../utils/cn'
export interface SkeletonProps extends HTMLAttributes<HTMLDivElement> {
  w?: string | number
  h?: number
  radius?: number
}
export function Skeleton({ w = '100%', h = 14, radius = 4, className, style, ...rest }: SkeletonProps) {
  return (
    <div
      className={cn('shimmer bg-[linear-gradient(90deg,#21262d_0%,#2d333b_50%,#21262d_100%)] bg-[length:200%_100%]', className)}
      style={{ width: w, height: h, borderRadius: radius, animation: 'shimmer 1.4s infinite', ...style }}
      {...rest}
    />
  )
}
