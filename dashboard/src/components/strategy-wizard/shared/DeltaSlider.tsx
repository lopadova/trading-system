/**
 * DeltaSlider — Custom Slider for Option Delta Selection
 *
 * Features:
 * - Range: 0.01 to 0.99 (step 0.001)
 * - Color-coded track: green (≤0.20) → yellow (0.20-0.50) → red (>0.50)
 * - Visual markers at key delta values (0.05, 0.16, 0.30, 0.50, 0.68, 0.85)
 * - Synchronized numeric input
 * - Automatic clamping to valid range
 */

import { useState, useEffect, useRef } from 'react'
import '../../../styles/wizard.css'

// ============================================================================
// TYPES
// ============================================================================

export interface DeltaSliderProps {
  value: number
  onChange: (value: number) => void
  min?: number
  max?: number
  step?: number
  disabled?: boolean
  className?: string
}

// ============================================================================
// CONSTANTS
// ============================================================================

const MARKERS = [
  { value: 0.05, label: 'Deep OTM' },
  { value: 0.16, label: 'OTM' },
  { value: 0.30, label: 'Neutral' },
  { value: 0.50, label: 'ATM' },
  { value: 0.68, label: 'ITM' },
  { value: 0.85, label: 'Deep ITM' },
] as const

const DEFAULT_MIN = 0.01
const DEFAULT_MAX = 0.99
const DEFAULT_STEP = 0.001

// ============================================================================
// HELPERS
// ============================================================================

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value))
}

function getTrackColor(delta: number): string {
  if (delta <= 0.20) {
    return 'var(--wz-success)' // Green for OTM
  }
  if (delta <= 0.50) {
    return 'var(--wz-warning)' // Yellow for neutral/ATM range
  }
  return 'var(--wz-error)' // Red for ITM
}

// ============================================================================
// COMPONENT
// ============================================================================

export function DeltaSlider({
  value,
  onChange,
  min = DEFAULT_MIN,
  max = DEFAULT_MAX,
  step = DEFAULT_STEP,
  disabled = false,
  className = '',
}: DeltaSliderProps) {
  const [localValue, setLocalValue] = useState(clamp(value, min, max))
  const [inputValue, setInputValue] = useState(value.toFixed(3))
  const trackRef = useRef<HTMLDivElement>(null)

  // Sync local value with prop
  useEffect(() => {
    const clamped = clamp(value, min, max)
    setLocalValue(clamped)
    setInputValue(clamped.toFixed(3))
  }, [value, min, max])

  const handleSliderChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newValue = parseFloat(e.target.value)
    const clamped = clamp(newValue, min, max)
    setLocalValue(clamped)
    setInputValue(clamped.toFixed(3))
    onChange(clamped)
  }

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setInputValue(e.target.value)
  }

  const handleInputBlur = () => {
    const parsed = parseFloat(inputValue)
    if (!isNaN(parsed)) {
      const clamped = clamp(parsed, min, max)
      setLocalValue(clamped)
      setInputValue(clamped.toFixed(3))
      onChange(clamped)
    } else {
      // Reset to current value if invalid
      setInputValue(localValue.toFixed(3))
    }
  }

  const handleInputKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      handleInputBlur()
    }
  }

  // Calculate percentage for gradient
  const percentage = ((localValue - min) / (max - min)) * 100
  const trackColor = getTrackColor(localValue)

  return (
    <div className={`delta-slider space-y-4 ${className}`}>
      {/* Slider Track with Markers */}
      <div className="relative pt-2 pb-8">
        {/* Custom Track Background */}
        <div
          ref={trackRef}
          className="relative h-2 bg-[var(--wz-surface)] rounded-full overflow-hidden"
        >
          {/* Filled Track (gradient) */}
          <div
            className="absolute h-full rounded-full transition-all duration-150"
            style={{
              width: `${percentage}%`,
              background: `linear-gradient(to right,
                var(--wz-success) 0%,
                var(--wz-success) ${(0.20 / (max - min)) * 100}%,
                var(--wz-warning) ${(0.20 / (max - min)) * 100}%,
                var(--wz-warning) ${(0.50 / (max - min)) * 100}%,
                var(--wz-error) ${(0.50 / (max - min)) * 100}%
              )`,
            }}
          />
        </div>

        {/* Range Input (overlaid) */}
        <input
          type="range"
          min={min}
          max={max}
          step={step}
          value={localValue}
          onChange={handleSliderChange}
          disabled={disabled}
          className="absolute top-2 left-0 w-full h-2 appearance-none bg-transparent cursor-pointer
                     [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-5 [&::-webkit-slider-thumb]:h-5
                     [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-[var(--wz-amber)]
                     [&::-webkit-slider-thumb]:border-2 [&::-webkit-slider-thumb]:border-[var(--wz-bg)]
                     [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:cursor-grab
                     [&::-webkit-slider-thumb]:hover:scale-110 [&::-webkit-slider-thumb]:transition-transform
                     [&::-moz-range-thumb]:appearance-none [&::-moz-range-thumb]:w-5 [&::-moz-range-thumb]:h-5
                     [&::-moz-range-thumb]:rounded-full [&::-moz-range-thumb]:bg-[var(--wz-amber)]
                     [&::-moz-range-thumb]:border-2 [&::-moz-range-thumb]:border-[var(--wz-bg)]
                     [&::-moz-range-thumb]:shadow-lg [&::-moz-range-thumb]:cursor-grab
                     [&::-moz-range-thumb]:hover:scale-110 [&::-moz-range-thumb]:transition-transform
                     disabled:opacity-50 disabled:cursor-not-allowed"
          aria-label="Delta value"
        />

        {/* Markers */}
        <div className="absolute top-10 left-0 w-full">
          {MARKERS.map((marker) => {
            const markerPercent = ((marker.value - min) / (max - min)) * 100
            return (
              <div
                key={marker.value}
                className="absolute"
                style={{ left: `${markerPercent}%` }}
              >
                {/* Marker Tick */}
                <div className="relative -translate-x-1/2">
                  <div className="w-0.5 h-2 bg-[var(--wz-border)] mx-auto" />
                  <div className="text-xs text-[var(--wz-muted)] mt-1 whitespace-nowrap text-center">
                    {marker.label}
                  </div>
                  <div className="text-xs font-mono text-[var(--wz-muted)] opacity-60 text-center">
                    {marker.value.toFixed(2)}
                  </div>
                </div>
              </div>
            )
          })}
        </div>
      </div>

      {/* Value Display + Input */}
      <div className="flex items-center gap-4">
        <div className="flex-1">
          <div className="text-sm text-[var(--wz-muted)] mb-1">Current Delta</div>
          <div className="text-2xl font-mono font-bold" style={{ color: trackColor }}>
            {localValue.toFixed(3)}
          </div>
        </div>

        {/* Numeric Input */}
        <div className="w-32">
          <label htmlFor="delta-input" className="text-xs text-[var(--wz-muted)] block mb-1">
            Manual Input
          </label>
          <input
            id="delta-input"
            type="number"
            min={min}
            max={max}
            step={step}
            value={inputValue}
            onChange={handleInputChange}
            onBlur={handleInputBlur}
            onKeyDown={handleInputKeyDown}
            disabled={disabled}
            className="
              w-full px-3 py-2 rounded-lg
              bg-[var(--wz-surface)] border border-[var(--wz-border)]
              text-[var(--wz-text)] font-mono text-sm
              focus:outline-none focus:border-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]
              transition-colors
              disabled:opacity-50 disabled:cursor-not-allowed
            "
          />
        </div>
      </div>
    </div>
  )
}
