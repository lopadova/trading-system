// System metrics sample type for the SystemPerfMini widget

export interface SystemMetricsSample {
  cpu: number[]
  ram: number[]
  network: number[]
  diskUsedPct: number
  diskFreeGb: number
  diskTotalGb: number
  asOf: string
}
