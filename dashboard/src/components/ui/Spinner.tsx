import type { SVGAttributes } from 'react'
export interface SpinnerProps extends SVGAttributes<SVGSVGElement> {
  size?: number
  color?: string
}
export function Spinner({ size = 14, color = 'currentColor', ...rest }: SpinnerProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" className="animate-spin shrink-0" {...rest}>
      <circle cx="12" cy="12" r="9" stroke={color} strokeOpacity=".2" strokeWidth="3" />
      <path d="M21 12a9 9 0 0 0-9-9" stroke={color} strokeWidth="3" strokeLinecap="round" />
    </svg>
  )
}
