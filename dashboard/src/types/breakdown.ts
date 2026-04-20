// Positions breakdown segment types (exposure slices by strategy/asset)

export interface ExposureSegment {
  label: string
  value: number
  color: string
}

export interface PositionsBreakdownData {
  byStrategy: ExposureSegment[]
  byAsset: ExposureSegment[]
}
