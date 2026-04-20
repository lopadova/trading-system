import { Spinner } from './Spinner'
export interface FilterOverlayProps {
  visible: boolean
  label?: string
}
export function FilterOverlay({ visible, label = 'Loading…' }: FilterOverlayProps) {
  if (!visible) return null
  return (
    <div
      className="absolute inset-0 flex items-center justify-center gap-2.5 z-10"
      style={{ background: 'rgba(13,17,23,0.72)', backdropFilter: 'blur(2px)', animation: 'fadeIn 140ms ease-out' }}
    >
      <Spinner size={16} color="var(--blue)" />
      <span className="text-[12.5px] text-muted font-medium">{label}</span>
    </div>
  )
}
